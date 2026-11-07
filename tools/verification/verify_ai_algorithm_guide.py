"""ZOD-212 structural source/document inventory; no original, C or managed AI execution."""
import argparse
import json
import re
import subprocess
from pathlib import Path

from verify_sword_knockback_contract import fingerprint, function_fingerprint, require

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'docs/evidence/ai/algorithm-guide-v1.json'
GUIDE = ROOT / 'docs/architecture/original-ai-algorithm.md'
CORE = tuple('src/battle/ai/' + name + '.c' for name in ('abilities', 'turn', 'evaluation'))


def definitions(source):
    # Function definitions only; excludes prototypes, comments and indented control statements.
    return [match.group(1) for match in re.finditer(
        r'(?m)^(?:static\s+)?[A-Za-z_][\w\s*]*?\b([A-Za-z_]\w*)\([^;{}]*\)\s*\{', source)]


def direct_random_sites(reference, functions):
    sites = []
    for path in CORE:
        for line, content in enumerate((reference / path).read_text(encoding='utf-8').splitlines(), 1):
            if re.search(r'\brand\s*\(', content):
                owners = [row for row in functions if row['path'] == path and row['startLine'] <= line <= row['endLine']]
                require(len(owners) == 1, 'One core owner for direct rand site')
                sites.append(dict(path=path, symbol=owners[0]['symbol'], line=line, expression=content.strip()))
    return sites


def audit(reference, evidence=None, guide=None):
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8')) if evidence is None else evidence
    guide = GUIDE.read_text(encoding='utf-8') if guide is None else guide
    require(evidence['schemaVersion'] == 1 and evidence['ticket'] == 'ZOD-212' and
            evidence['kind'] == 'structural-algorithm-guide' and evidence['productionImplemented'] is False,
            'Source/document study only')
    revision = subprocess.check_output(['git', '-C', str(reference), 'rev-parse', 'HEAD'], text=True).strip()
    require(revision == evidence['referenceRevision'] and revision in guide, 'Pinned guide/source revision')
    require(not subprocess.check_output(['git', '-C', str(reference), 'status', '--porcelain'], text=True).strip(),
            'Read-only reference must be clean')
    for row in evidence['sourceFiles']:
        require(fingerprint(reference, row['path']) == row, 'Guide source file: ' + row['path'])
    functions = evidence['sourceFunctions']
    keys = [(row['path'], row['symbol']) for row in functions]
    expected = {(path, symbol) for path in CORE for symbol in definitions((reference / path).read_text(encoding='utf-8'))}
    actual = {(path, symbol) for path, symbol in keys if path in CORE}
    require(expected == actual and len(keys) == len(set(keys)), 'Complete unique core definition inventory')
    for row in functions:
        actual_function = function_fingerprint(reference / row['path'], row['symbol'])
        require(all(actual_function[key] == row[key] for key in ('startLine', 'endLine', 'normalizedLfSha256')),
                'Guide function: ' + row['symbol'])
        require(row['chapter'] in evidence['chapters'] and evidence['chapters'][row['chapter']] in guide,
                'Function chapter: ' + row['symbol'])
    require(direct_random_sites(reference, functions) == evidence['directRandomSites'], 'Complete direct rand inventory')
    for target in re.findall(r'\]\(([^)]+)\)', guide):
        if '://' in target:
            require(target.startswith('https://github.com/adamrt/fft_decomp/blob/' + revision + '/'), 'Pinned reference link')
        elif not target.startswith('#'):
            require((GUIDE.parent / target.split('#')[0]).exists(), 'Guide local link: ' + target)
    return dict(referenceRevision=revision, referenceClean=True, guideCoreFunctions=len(expected),
                guideExternalFunctions=len(functions) - len(expected), guideSourceFiles=len(evidence['sourceFiles']),
                guideChapters=len(evidence['chapters']), directCoreRandSites=len(evidence['directRandomSites']),
                originalExecuted=False, cExecuted=False, managedExecuted=False,
                scope='Structural inventory and source/document integrity; not full behavioral equivalence')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    report = audit(args.reference)
    if args.output:
        with args.output.open('x', encoding='utf-8') as stream:
            json.dump(report, stream, indent=2); stream.write('\n')
    print(json.dumps(report))
