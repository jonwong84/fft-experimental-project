"""Prepare the reviewed general-0 full-visible diagnostic plan under scratch."""
import argparse
import json
from pathlib import Path
import uuid

import compare_observed_banner as b
import compare_visual_checkpoints as v


def record(path, budget=65536):
    relative = path.relative_to(v.ROOT).as_posix()
    path = v.scratch_file(relative)
    v.require(path.stat().st_size <= budget, 'Input exceeds budget')
    data = path.read_bytes()
    v.require(len(data) <= budget, 'Input exceeds budget')
    return dict(path=relative, sha256=v.sha(data))


def run(managed_root, prepared_root, output):
    destination = v.scratch_file(output)
    v.require(not destination.exists(), 'Output already exists; choose a fresh directory')
    managed, prepared = v.scratch_file(managed_root), v.scratch_file(prepared_root)
    # This initial profile intentionally selects one reviewed original observation.
    source = v.ROOT / 'docs/evidence/visual/original-banner-state-v1.json'
    v.require(source.stat().st_size <= 65536, 'Observation metadata exceeds budget')
    observation_bytes = source.read_bytes(); observation = v.parse(observation_bytes)
    def observed(item):
        result = dict(path=item['localPath'], sha256=item['sha256'])
        v.verified_file(result, 32 * 1024 * 1024)
        return result
    plan = dict(format='fft.observed-banner-comparison', schemaVersion=1, profile='general-0-full-visible-v1',
        original=dict(png=observed(observation['restoreImages'][0]), state=observed(observation['state']),
                      profile=observed(observation['profile'])),
        candidate=dict(png=record(managed / 'tick-020.png', 32 * 1024 * 1024), receipt=record(managed / 'check.json'), tick=20),
        prepared=dict(definition=record(prepared / 'banner.json'), indices=record(prepared / 'indices.bin'), palette=record(prepared / 'palette-4.bin')),
        alignment=dict(originalRect=[12, 0, 256, 240], candidateRect=[0, 0, 256, 240],
            rationale='Measured raw original buffer has exactly 12 black columns on each side of a 256x240 content view; retain rows and native sampling. Checked before comparison; no alignment search.'),
        anchorRationale='Visually fully visible two-line artwork in the restored original scene versus managed tick 20 Holding. This is an artwork anchor, not an original tick/state equivalence claim.')
    destination.parent.mkdir(parents=True, exist_ok=True)
    staging = destination.parent / ('.observed-banner-plan-' + uuid.uuid4().hex); staging.mkdir()
    try:
        (staging / 'observation.json').write_bytes(observation_bytes)
        plan['original']['observation'] = record(staging / 'observation.json')
        b.load(plan)  # Verify all identities, context, full-visible receipt and alignment before publication.
        plan['original']['observation']['path'] = (destination / 'observation.json').relative_to(v.ROOT).as_posix()
        (staging / 'plan.json').write_text(json.dumps(plan, indent=2) + '\n', encoding='utf-8', newline='\n')
        staging.rename(destination)
    finally:
        if staging.exists():
            for name in ('plan.json', 'observation.json'): (staging / name).unlink(missing_ok=True)
            staging.rmdir()
    print('Prepared explicit observed banner plan; artwork anchor only, original timing unknown')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--managed-root', required=True); parser.add_argument('--prepared-root', required=True); parser.add_argument('--output', required=True)
    args = parser.parse_args()
    try: run(args.managed_root, args.prepared_root, args.output)
    except (ValueError, OSError, KeyError, TypeError, IndexError, RecursionError) as error:
        parser.exit(2, 'Invalid/unavailable observed banner plan: ' + str(error) + '\n')
