"""Normalized geometry/cast composition, without original/C/managed execution."""
import argparse,copy,json
from pathlib import Path
import verify_spell_targeting as geometry
import verify_magic_casts as casts
from verify_sword_knockback_contract import require,REVISION

FIXTURE=Path(__file__).resolve().parents[2]/'docs/fixtures/behavior/spell-geometry-cast-contract-v1.json'

def resolve(d):
    def reject():return dict(accepted=False,rejection='UnsupportedContext',outcome=None,word=d['word'],count=d['count'])
    if not all(d['entry'].values()) or not d['geometryBound'] or d['definition']['originalId']==329:return reject()
    if not all(d['profile'].values()) or not d['complete']:return reject()
    units=[r['occupant'] for r in d['units']]
    for u in d['units']:
        if u['current'] is not None and u['current']['id']!=u['occupant']['id']:return reject()
    actor=next((u for u in d['units'] if u['occupant']['id']==d['caster']['id']),None)
    if actor is None or actor['current']!=d['caster']:return reject()
    selected=geometry.selection(dict(d,units=units,casterId=d['caster']['id'],caster=actor['occupant']['position'],nearest=False))
    by_id={u['occupant']['id']:u['current'] for u in d['units']}
    targets=[by_id[id] for id in selected['ids']]
    if any(t is None for t in targets):return reject()
    return casts.resolve(dict(d,targets=targets,targetProfile=dict(order=True,noEarlier=True)))

def validate(f):
    require(f['ticket']=='ZOD-362' and f['referenceRevision']==REVISION and f['productionImplemented'] is False,'Fixture identity')
    for r in f['casts']:require(resolve(copy.deepcopy(r))==r['expected'],'Geometry cast '+r['id'])
    return dict(geometryCastDescriptions=len(f['casts']),originalExecuted=False,cExecuted=False,managedExecuted=False)

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--prepared-executable',type=Path,required=True);p.add_argument('--output',type=Path);a=p.parse_args()
    r=dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),targetingSource=geometry.audit(a.reference),priorCastSource=casts.audit(a.reference,a.prepared_executable))
    if a.output:
        with a.output.open('x',encoding='utf-8') as stream:json.dump(r,stream,indent=2);stream.write('\n')
    print(json.dumps(r))
