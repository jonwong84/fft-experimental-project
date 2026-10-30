"""ZOD-122 source helpers and proposed confirmed-Move lifecycle; never executes C or managed movement."""
import argparse
import json
from pathlib import Path
import subprocess

from verify_sword_knockback_contract import REVISION, fingerprint, function_fingerprint, require
from verify_flat_movement_contract import flat_route, placement
from verify_turn_facing_contract import clock_end, direction

FIXTURE = 'docs/fixtures/behavior/voluntary-move-contract-v1.json'
EVIDENCE = 'docs/evidence/ai/voluntary-move-contract-v1.json'
MAX = (1 << 64) - 1
FACES = ('East','North','West','South')
PACKED = {0:'East',64:'West',128:'South',192:'North'}


def walk(data):
    steps=data['packedSteps']
    require(1<=len(steps)<=124 and all(type(b) is int and b in PACKED for b in steps),'Closed adjacent path bytes')
    require(data['incomingFacing'] in FACES,'Facing')
    return dict(facing=data['incomingFacing'] if data['actionExecute'] else PACKED[steps[-1]],
                facingWritten=not data['actionExecute'],rngDraws=0)


def confirm(data):
    require(data['incomingFacing'] in FACES and data['walkFacing'] in FACES and 0<=data['ct']<=99,'Confirmation domain')
    if not data['confirmed']:
        return dict(position=data['origin'],facing=data['incomingFacing'],moved=False,acted=data['acted'],
                    ct=data['ct'],hasTurn=True,order=['RestoreTileAndFacing'],rngDraws=0)
    return dict(position=data['destination'],facing=data['walkFacing'],moved=True,acted=data['acted'],
                ct=min(60,data['ct']) if data['acted'] else data['ct'],hasTurn=not data['acted'],
                order=['TileAndFacing','MoveTaken']+(['TurnEnd'] if data['acted'] else [])+['PostMoveEvents'],rngDraws=0)


def proposed_move(data):
    require(type(data['revision']) is int and 0<=data['revision']<=MAX,'Revision domain')
    require(type(data['ct']) is int and 0<=data['ct']<=99,'Active CT domain')
    rng=dict(rngWord=data['rngWord'],rngCount=data['rngCount'],rngDraws=0)
    require(0<=data['rngWord']<=0xffffffff and 0<=data['rngCount']<=MAX,'RNG domain')
    def reject(reason):return dict(accepted=False,reason=reason,unchanged=True,events=[],path=None,**rng)
    # New method's proposed precedence, distinct from the phase-independent ZOD-121 query.
    for failed,reason in [(data['pending'],'FacingPending'),(data['revision']==MAX,'RevisionExhausted'),
        (not data['knownActor'],'UnknownUnit'),(not data['activeActor'],'InactiveUnit'),
        (data['moved'],'MovementAlreadyTaken'),
        (not data['actorLiving'] or not data['enemyLiving'] or not data['profileSupported'],'UnsupportedProfile')]:
        if failed:return reject(reason)
    route=flat_route(data['route'])
    if route['path'] is None:return reject('InvalidDestination')
    ended=data['acted']
    return dict(accepted=True,position=data['route']['destination'],facing=route['executionFinalHeading'],
        moved=True,acted=data['acted'],ct=min(60,data['ct']) if ended else data['ct'],hasTurn=not ended,
        revision=data['revision']+1,pending=ended,
        receiptBinding='FullySettledSnapshotAndLineage' if ended else None,
        events=['Movement','PathCompleted','MoveConsumed']+(['TurnEnded'] if ended else []),
        path=route['path'],packedSteps=route['packedSteps'],**rng)


def proposed_finish(data):
    def reject(reason):return dict(accepted=False,reason=reason,unchanged=True,events=[],rngDraws=0)
    if not data['pending']:return reject('MissingCompletion')
    if not all(data[k] for k in ('identityMatches','snapshotMatches','lineageMatches','revisionMatches')):return reject('StaleCompletion')
    if not data['actorMatches']:return reject('WrongActor')
    if data['active']:return reject('InconsistentCompletion')
    if not data['revisionAvailable']:return reject('RevisionExhausted')
    choice=data['choice']
    if choice=='ChooseClosedEnemy' and not data['enemyLiving']:return reject('NoLivingEnemy')
    if choice not in FACES+('KeepCurrent','ChooseClosedEnemy'):return reject('UnsupportedFacing')
    face=direction(data['actorPosition'],data['enemyPosition']) if choice=='ChooseClosedEnemy' else data['currentFacing'] if choice=='KeepCurrent' else choice
    require(face in FACES,'Facing')
    return dict(accepted=True,facing=face,pending=False,ct=data['ct'],events=['FacingSettled'],
                receiptConsumed=True,ctAppliedAgain=False,rngDraws=0)


def calculate(family,data):
    if family=='source-walk':return walk(data)
    if family=='source-confirm':return confirm(data)
    if family=='source-clock':return clock_end(data)
    if family=='source-preview':return placement(data)
    if family=='proposed-move':return proposed_move(data)
    if family=='proposed-finish':return proposed_finish(data)
    raise ValueError('Unsupported family: '+family)


def validate(contract):
    require(contract['format']=='fft.voluntary-move-source-description' and contract['schemaVersion']==1 and
            contract['ticket']=='ZOD-122' and contract['referenceRevision']==REVISION,'Contract identity')
    require(contract['execution']=='offline-source-and-proposed-adapter-description' and contract['productionImplemented'] is False,'Execution boundary')
    seen=set();families=set()
    for row in contract['vectors']:
        require(row['id'] not in seen,'Duplicate vector');seen.add(row['id']);families.add(row['family'])
        kind='managed-design-proposal' if row['family'].startswith('proposed-') else 'source-derived-closed-domain'
        require(row['expectationKind']==kind,'Expectation classification')
        require(calculate(row['family'],row['inputs'])==row['expected'],'Vector mismatch: '+row['id'])
    require(len(seen)==contract['vectorCount']==60 and families=={'source-walk','source-confirm','source-clock',
            'source-preview','proposed-move','proposed-finish'},'Vector inventory')
    cases=contract['implementationCases']
    require([r['id'] for r in cases]==[f'VME-{n:03}' for n in range(1,19)] and
            all(r['status']=='unexecuted' for r in cases),'Unexecuted production inventory')
    return dict(vectors=len(seen),families=len(families),implementationCasesUnexecuted=18)


def audit(root,reference=None,check_managed_baseline=False):
    evidence=json.loads((root/EVIDENCE).read_text(encoding='utf-8'))
    require(evidence['ticket']=='ZOD-122' and evidence['reference']['revision']==REVISION,'Evidence identity')
    require(evidence['fixture']==fingerprint(root,FIXTURE),'Fixture identity')
    result=validate(json.loads((root/FIXTURE).read_text(encoding='utf-8')))
    for row in evidence['preservedFiles']:require(fingerprint(root,row['path'])==row,'History changed: '+row['path'])
    result.update(preservedFiles=len(evidence['preservedFiles']),managedBaselineFilesChecked=0,referenceFilesChecked=0,referenceFunctionsChecked=0)
    if check_managed_baseline:
        for row in evidence['managedBaseline']:require(fingerprint(root,row['path'])==row,'Managed baseline changed: '+row['path'])
        result['managedBaselineFilesChecked']=len(evidence['managedBaseline'])
    if reference is not None:
        revision=subprocess.check_output(['git','-C',str(reference),'rev-parse','HEAD'],text=True).strip()
        status=subprocess.check_output(['git','-C',str(reference),'status','--porcelain'],text=True).strip()
        require(revision==REVISION and not status,'Reference revision/clean boundary')
        for row in evidence['reference']['files']:
            require(fingerprint(reference,row['path'])=={k:v for k,v in row.items() if k!='functions'},'Reference file changed: '+row['path'])
            for function in row['functions']:
                require(function_fingerprint(reference/row['path'],function['symbol'])==function,'Reference function changed: '+function['symbol'])
                result['referenceFunctionsChecked']+=1
            result['referenceFilesChecked']+=1
    result.update(format='fft.voluntary-move-source-audit',schemaVersion=1,passed=True,productionImplemented=False,
                  fullAiExecuted=False,originalExecuted=False)
    return result


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference',type=Path);parser.add_argument('--check-managed-baseline',action='store_true')
    parser.add_argument('--output',type=Path,required=True);args=parser.parse_args()
    result=audit(Path(__file__).resolve().parents[2],args.reference,args.check_managed_baseline)
    with args.output.open('x',encoding='utf-8') as output:output.write(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result))


if __name__=='__main__':main()
