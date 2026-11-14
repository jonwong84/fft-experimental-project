"""Offline ZOD-333 formula/source inventory. Generated catalog stays under ignored scratch; no C execution."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import subprocess
from verify_sword_knockback_contract import fingerprint, function_fingerprint, require, REVISION

ROOT = Path(__file__).resolve().parents[2]
RECEIPT = ROOT / 'docs/evidence/combat/ability-formula-source-v1.json'
EXECUTABLE_SHA256 = '287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562'
# Navigation buckets, not proposed runtime interfaces or claims of interchangeable behavior.
BUCKETS = {
    'weapon-and-delivery': [*range(1,8),99,100],
    'faith-magic': [*range(8,17),18,*range(20,24)],
    'equipment-theft-and-stat-attacks': [26,27,*range(37,49),63],
    'song-and-dance': [28,29],
    'multi-hit-magic': [30,31,94,95],
    'draw-out-and-elemental': list(range(32,37)),
    'physical-recovery-and-self-buffs': list(range(49,61)),
    'special-result-rules': [61,62,64,65,66,67,68,69,71],
    'consumable-recovery': list(range(72,76)),
    'monster-and-other-effects': [*range(76,94),96,97,98],
    'empty-dispatch-handlers': [17,19,24,25,70],
}

def parse_dispatch(source):
    clean = re.sub(r'/\*.*?\*/|//[^\n]*', '', source, flags=re.S)
    match = re.search(r'\bs_formulas\s*\[[^\]]+\]\)\(void\)\s*=\s*\{([^}]+)\}', clean)
    require(match is not None, 'One-based formula dispatch table')
    names = [v.strip() for v in match.group(1).split(',') if v.strip()]
    require(len(names) == 100 and len(set(names)) == 100 and all(re.fullmatch(r'formula_\w+', n) for n in names), '100 distinct formula slots')
    return [dict(id=i, handler=n) for i,n in enumerate(names,1)]

def normalize_formula(value):
    require(type(value) is int and 0 <= value <= 255, 'Unsigned formula byte')
    return value if 1 <= value < 101 else 1

def decode_secondary(data):
    require(len(data) > 0 and len(data) % 14 == 0, 'Complete 14-byte secondary rows')
    return [dict(id=i//14, formula=data[i+8], effectiveFormula=normalize_formula(data[i+8]),
                 x=data[i+9], y=data[i+10], range=data[i], area=data[i+1], vertical=data[i+2],
                 flags=list(data[i+3:i+7]), element=data[i+7], status=data[i+11], ct=data[i+12], mp=data[i+13])
            for i in range(0,len(data),14)]

def summarize(rows):
    counts = Counter(r['formula'] for r in rows)
    return dict(secondarySlots=len(rows), distinctRawFormulaBytes=len(counts),
                rawFormulaCounts={str(k):v for k,v in sorted(counts.items())})

def output_path(value):
    path = Path(value).resolve()
    require(path.is_relative_to((ROOT/'scratch').resolve()), 'Generated inventories must remain under ignored scratch')
    return path

def audit_source(reference):
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip() == REVISION, 'Pinned reference')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(), 'Clean read-only reference')
    receipt = json.loads(RECEIPT.read_text(encoding='utf-8'))
    require(receipt['referenceRevision'] == REVISION and receipt['ticket'] == 'ZOD-333', 'Source receipt identity')
    for row in receipt['sourceFiles']:
        require(fingerprint(reference,row['path']) == row, 'Source file '+row['path'])
    for row in receipt['sourceFunctions']:
        actual = function_fingerprint(reference/row['path'],row['symbol'])
        require(all(row[k] == v for k,v in actual.items()), 'Source function '+row['symbol'])
    dispatch = parse_dispatch((reference/'src/battle/combat/resolution.c').read_text(encoding='utf-8'))
    require(dispatch[54]['handler'] == 'formula_damage_random_y_times_pa', 'Shared random PA dispatch')
    require(dispatch[7]['handler'] == 'formula_magical_damage' and dispatch[11]['handler'] == 'formula_magical_heal', 'Shared spell dispatch')
    members = [n for values in BUCKETS.values() for n in values]
    require(sorted(members) == list(range(1,101)), 'Non-overlapping navigation buckets')
    return dispatch

def inventory(reference, executable):
    dispatch = audit_source(reference)
    data = executable.read_bytes()
    require(len(data) == 356352 and hashlib.sha256(data).hexdigest() == EXECUTABLE_SHA256, 'Prepared SCUS_942.21 size/hash')
    offset = 0x8005fbf0 - 0x80010000 + 0x800
    rows = decode_secondary(data[offset:offset+368*14]); summary = summarize(rows)
    require(summary['distinctRawFormulaBytes'] == 82, 'Observed ordinary-table formula count')
    require(summary['rawFormulaCounts']['8'] == 41 and summary['rawFormulaCounts']['12'] == 6 and summary['rawFormulaCounts']['55'] == 4, 'Selected shared family counts')
    return dict(ticket='ZOD-333', referenceRevision=REVISION, referenceClean=True, originalExecuted=False,cExecuted=False,
                preparedSha256=EXECUTABLE_SHA256, scope='368 ordinary secondary slots, not complete ability reachability or menu/equipment coverage',
                summary=summary, navigationBuckets=BUCKETS, dispatch=dispatch, records=rows)

if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference',type=Path,required=True)
    parser.add_argument('--prepared-executable',type=Path,required=True)
    parser.add_argument('--output',type=output_path,required=True)
    args=parser.parse_args(); result=inventory(args.reference,args.prepared_executable)
    with args.output.open('x',encoding='utf-8') as stream: json.dump(result,stream,indent=2);stream.write('\n')
    print(json.dumps(dict(dispatchSlots=len(result['dispatch']),**result['summary'],referenceRevision=REVISION,referenceClean=True)))
