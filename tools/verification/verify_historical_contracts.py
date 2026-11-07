"""Audit recorded fingerprints in a Git snapshot, independently of current code.

This executes historical Python source-description auditors, never the game or C.
The exported snapshot contains tracked files only; the checkout is not changed.
"""
import argparse
import io
import json
from pathlib import Path
import stat
import subprocess
import sys
import tempfile
import zipfile


# Each manifest describes its own checkpoint, including then-current managed code.
# A later checkpoint legitimately contains changes to those managed baselines.
CHECKPOINTS = {
    'verify_sword_knockback_contract': 'dd6210acae25711444aae5dc3a8cb970da741768',
    'verify_ai_decision_contract': '7a284c3860b6c5d9acc4a3a9e2efba99e637bdf3',
    'verify_turn_facing_contract': '26ee4ad1cc9cc40d273c0fe3e45d5c0ed1875a05',
    'verify_flat_movement_contract': 'afa479975e5c69e51275064d3b664a453889bd05',
    'verify_voluntary_move_contract': '2d5a933684026432751132365693ba5c0960bd12',
    'verify_ai_candidate_contract': '10d8e613642a3a0827dcf789a8081f262a9f17ed',
    'verify_ai_score_contract': 'ec03c5cb816b0231e3f47e31372a327c360104d3',
    'verify_ai_comparison_contract': '46f55d93d72452911ba5a0afdd9a5a497c67cee4',
    'verify_ai_scan_contract': '667ed4688540069a924962ba59ad2f2ac723f320',
}
WORKER = '''
import importlib, json, sys
from pathlib import Path
root = Path(sys.argv[1])
sys.path.insert(0, str(root / 'tools' / 'verification'))
results = {}
for name in json.loads(sys.argv[2]):
    results[name] = importlib.import_module(name).audit(root, check_managed_baseline=True)
    if results[name].get('passed') is not True:
        raise ValueError('Historical audit did not pass: ' + name)
print(json.dumps(results))
'''


def extract_snapshot(data, destination):
    """Accept regular files/directories confined to the temporary export."""
    destination = destination.resolve()
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        for entry in archive.infolist():
            target = (destination / entry.filename).resolve()
            mode = entry.external_attr >> 16
            if not target.is_relative_to(destination) or stat.S_ISLNK(mode):
                raise ValueError('Unsafe snapshot path: ' + entry.filename)
        archive.extractall(destination)


def audit_snapshot(repository, revision, modules):
    repository = repository.resolve()
    commit = subprocess.check_output(
        ['git', '-C', str(repository), 'rev-parse', '--verify', '--end-of-options', revision + '^{commit}'],
        text=True, stderr=subprocess.PIPE).strip()
    data = subprocess.check_output(['git', '-C', str(repository), 'archive', '--format=zip', commit], stderr=subprocess.PIPE)
    scratch = repository / 'scratch'
    scratch.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='historical-contracts-', dir=scratch) as directory:
        snapshot = Path(directory)
        extract_snapshot(data, snapshot)
        worker = subprocess.run([sys.executable, '-I', '-c', WORKER, str(snapshot), json.dumps(modules)],
                                cwd=snapshot, text=True, capture_output=True, check=True)
        results = json.loads(worker.stdout)
    return dict(format='fft.historical-contract-audit', schemaVersion=1, revision=commit,
                scope='recorded-source-descriptions-and-managed-fingerprints',
                currentRuntimeExecuted=False, referenceCheckoutChecked=False,
                originalExecuted=False, audits=results, passed=True)


def audit_history(repository, checkpoints):
    return dict(format='fft.historical-contract-audits', schemaVersion=1,
                audits={name: audit_snapshot(repository, revision, (name,))
                        for name, revision in checkpoints.items()}, passed=True,
                currentRuntimeExecuted=False, referenceCheckoutChecked=False, originalExecuted=False)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--checkpoint', choices=CHECKPOINTS, help='Audit one checkpoint instead of all nine')
    parser.add_argument('--output', type=Path, help='Optional new report file')
    args = parser.parse_args()
    try:
        checkpoints = {args.checkpoint: CHECKPOINTS[args.checkpoint]} if args.checkpoint else CHECKPOINTS
        result = audit_history(Path(__file__).resolve().parents[2], checkpoints)
    except subprocess.CalledProcessError as error:
        message = error.stderr or str(error)
        print(message.decode('utf-8', errors='replace') if isinstance(message, bytes) else message, file=sys.stderr)
        return 1
    report = json.dumps(result, indent=2) + '\n'
    if args.output:
        with args.output.open('x', encoding='utf-8') as output:
            output.write(report)
    print(report, end='')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
