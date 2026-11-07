"""ZOD-137 isolated mask/RNG stage descriptions, not a managed or original AI runner."""
import argparse
import json
from pathlib import Path

from verify_ai_decision_contract import draws
from verify_ai_rank_admission_contract import audit as audit_rank_source
from verify_sword_knockback_contract import function_fingerprint, require

MAX = (1 << 64) - 1
EVIDENCE = 'docs/evidence/ai/composition-contract-v1.json'
ROOT = Path(__file__).resolve().parents[2]


def source_handoff(scored_origins, actor, baseline_destination, attack_destination):
    """Supplied reachable lower-layer cells; no scenario generation or geometry admission."""
    scored = [tuple(p) for p in scored_origins]
    require(scored and len(set(scored)) == len(scored), 'Nonempty unique scoring mask')
    require(all(len(p) == 2 and all(type(v) is int and v >= 0 for v in p)
                for p in scored + [tuple(actor)]), 'Normalized lower-layer cells')
    ordered = sorted(scored, key=lambda p: (p[1], p[0]))
    require(tuple(baseline_destination) in scored, 'Baseline must have a fresh score cell')
    require(tuple(attack_destination) in scored, 'Scenario-1 destination precedes actor insertion')
    return dict(scoredOrigins=[list(p) for p in ordered],
                attackOrigins=[list(p) for p in sorted(set(scored) | {tuple(actor)}, key=lambda p: (p[1], p[0]))],
                baselineIndex=ordered.index(tuple(baseline_destination)), additionalDraws=0)


def source_schedule(seed, count, movement_cells, attack_costs):
    """Closed source-stage schedule with SUPPLIED Attack costs, not freshly evaluated candidates.

    Each living move-only evaluation costs one draw. Seeding and insertion cost zero.
    This describes synchronous managed rollback, not native VSync resume or C rewind.
    """
    require(type(movement_cells) is int and movement_cells > 0, 'Movement scoring prefix')
    require(0 <= seed <= 0xffffffff and 0 <= count <= MAX, 'RNG domain')
    require(all(type(cost) is int and cost >= 4 for cost in attack_costs), 'Supplied ordinary Attack costs')
    word, used = seed, count
    entry = None
    for stage, costs in (('Movement', [1] * movement_cells), ('Attack', attack_costs)):
        for index, cost in enumerate(costs):
            if used > MAX - cost:
                return dict(accepted=False, failedStage=stage, failedIndex=index, outcome=None,
                            rngWord=seed, rngCount=count)
            word, _ = draws(word, cost)
            used += cost
        if stage == 'Movement':
            entry = dict(rngWord=word, rngCount=used)
    return dict(accepted=True, failedStage=None, failedIndex=None,
                outcome=dict(scanEntry=entry, baselineSeedDraws=0, insertionDraws=0), rngWord=word, rngCount=used)


def audit(reference):
    report = audit_rank_source(reference)
    evidence = json.loads((ROOT / EVIDENCE).read_text(encoding='utf-8'))
    require(evidence['referenceRevision'] == report['referenceRevision'], 'Composition reference revision')
    for entry in evidence['additionalSourceFunctions']:
        actual = function_fingerprint(reference / entry['path'], entry['symbol'])
        require(actual['normalizedLfSha256'] == entry['normalizedLfSha256'], 'Function mismatch: ' + entry['symbol'])
    return dict(**report, additionalFunctions=len(evidence['additionalSourceFunctions']))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    report = audit(args.reference)
    if args.output:
        with args.output.open('x', encoding='utf-8') as stream:
            json.dump(report, stream, indent=2)
            stream.write('\n')
    print(json.dumps(report))


if __name__ == '__main__':
    main()
