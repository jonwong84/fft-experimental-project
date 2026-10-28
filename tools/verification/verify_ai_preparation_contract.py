"""Pinned ZOD-160 source study and closed preparation scalars; no C/managed execution."""
import argparse
import json
from pathlib import Path

from verify_ai_refresh_contract import audit as audit_refresh
from verify_sword_knockback_contract import fingerprint, function_fingerprint, require

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'docs/evidence/ai/preparation-contract-v1.json'


def prepare(actor_ct, actor_speed, target_ct, target_speed, move, distance, cached_support):
    require(0 <= actor_ct < 100 and 0 <= target_ct <= 254, 'CT domain')
    require(1 <= actor_speed <= 255 and 1 <= target_speed <= 255, 'Speed domain')
    require(0 <= move <= 124 and 1 <= distance <= 30, 'Closed geometry')
    delay = lambda ct, speed: (max(0, 100-ct)+speed-1)//speed
    actor_delay = delay(actor_ct, actor_speed)
    target_delay = delay(target_ct, target_speed)
    return dict(actorDelay=actor_delay, targetDelay=target_delay, enemyReach=move+1, allyReach=move,
                actorSupportUnavailable=True, retreatSupport=False, singleEnemy=False,
                proximity=max(0,10-distance) if target_delay <= actor_delay else 0,
                randomUseThreshold=0, extraDraws=0)


def audit(reference):
    report = audit_refresh(reference)
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(evidence['referenceRevision'] == report['referenceRevision'], 'Preparation reference')
    for entry in evidence['additionalSourceFiles']:
        require(fingerprint(reference, entry['path']) == entry, 'Source mismatch: '+entry['path'])
    for entry in evidence['additionalSourceFunctions']:
        actual = function_fingerprint(reference / entry['path'], entry['symbol'])
        require(actual['normalizedLfSha256'] == entry['normalizedLfSha256'], 'Function mismatch: '+entry['symbol'])
    return dict(**report, preparationFunctions=len(evidence['additionalSourceFunctions']))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    result = audit(args.reference)
    if args.output:
        with args.output.open('x', encoding='utf-8') as stream:
            json.dump(result, stream, indent=2); stream.write('\n')
    print(json.dumps(result))
