"""Audit selected ordinary spell bindings against pinned source and verified prepared data."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from verify_sword_knockback_contract import REVISION,fingerprint,function_fingerprint,require

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/elemental-spell-bindings-v1.json'
SOURCE=ROOT/'docs/evidence/combat/ordinary-magic-content-source-v1.json'

def validate(fixture):
    require(fixture['schemaVersion']==1 and fixture['referenceRevision']==REVISION,'Fixture identity')
    rows=fixture['cases'];require([r['id'] for r in rows]==list(range(16,28)),'Explicit source skillset order')
    for row in rows:
        tier=(row['id']-16)%4;fourth=tier==3
        require(row['formula']==8 and row['status']==0,'Shared ordinary damage and empty status')
        require(row['range']==4 and row['area']==(2 if fourth else 1),'Reviewed range and area')
        require(row['flags1']==0 and row['flags2']==(1 if fourth else 0),'Reviewed targeting flags')
        require(row['flags3']==(34 if fourth else 226) and row['flags4']==(2 if fourth else 66),'Reviewed secondary attributes')
        require(row['power']*8*70*70//10000==row['expectedDamageAtMa8Faith70'],'Independent ordinary damage amount')
    require(rows[5]['mp']==10 and rows[1]['mp']==12 and rows[9]['mp']==12,'Bolt2 cost difference')
    return dict(bindingVectors=len(rows),sharedFormulas=1)

def audit(reference,executable,content_root):
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==REVISION,'Pinned reference')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(),'Clean reference')
    source=json.loads(SOURCE.read_text(encoding='utf-8'))
    for row in source['sourceFiles']:require(fingerprint(reference,row['path'])==row,'File '+row['path'])
    for row in source['sourceFunctions']:
        actual=function_fingerprint(reference/row['path'],row['symbol'])
        require(all(row[k]==v for k,v in actual.items()),'Function '+row['symbol'])
    data=executable.read_bytes();require(len(data)==356352 and hashlib.sha256(data).hexdigest()=='287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562','Prepared SCUS-94221')
    fixture=json.loads(FIXTURE.read_text(encoding='utf-8'));report=validate(fixture)
    package=json.loads((content_root/'ordinary-magic.package.json').read_text(encoding='utf-8'))
    require([s['originalId'] for s in package['spells']]==[r['id'] for r in fixture['cases']],'Prepared source slot order')
    for row,spell in zip(fixture['cases'],package['spells']):
        start=0x8005fbf0+row['id']*14-0x80010000+0x800
        expected=bytes([row['range'],row['area'],row['vertical'],row['flags1'],row['flags2'],row['flags3'],row['flags4'],row['element'],row['formula'],0,row['power'],row['status'],row['charge'],row['mp']])
        require(data[start:start+14]==expected,'Source spell '+row['name'])
        start=0x8005ebf0+row['id']*8-0x80010000+0x800
        require(int.from_bytes(data[start:start+2],'little')==row['jp'],'JP '+row['name'])
        require(spell['mpCost']==row['mp'] and spell['chargeTime']==row['charge'] and spell['formula']['power']==row['power'],'Prepared parameters '+row['name'])
        require(spell['name']==row['name'] and spell['formula']['kind']=='Damage' and spell['formula']['elements']=={128:'Fire',64:'Lightning',32:'Ice'}[row['element']],'Prepared formula '+row['name'])
        require(spell['formula']['secondaryEvadeable'] and spell['affectedBySilence'],'Prepared execution flags '+row['name'])
        require(all(spell['targeting'][k]==row[k] for k in ['range','area','vertical']),'Prepared geometry '+row['name'])
        require(spell['targeting']['cannotHitCaster']==bool(row['flags2']&1),'Caster exclusion '+row['name'])
    manifest=json.loads((content_root/'assets.manifest.json').read_text());entry=manifest['assets'][0]
    asset=(content_root/entry['relativePath']).read_bytes()
    require(len(asset)==entry['byteLength'] and hashlib.sha256(asset).hexdigest()==entry['sha256'],'Prepared manifest integrity')
    require(set(package['units'][0]['learnedSpells'])=={s['id'] for s in package['spells']},'Explicit selected learned grants')
    return dict(report,referenceRevision=REVISION,referenceClean=True,sourceFiles=len(source['sourceFiles']),sourceFunctions=len(source['sourceFunctions']),manifestVerified=True,originalExecuted=False,cExecuted=False)

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--prepared-executable',type=Path,required=True);p.add_argument('--content-root',type=Path,required=True);p.add_argument('--output',type=Path)
    a=p.parse_args();r=audit(a.reference,a.prepared_executable,a.content_root)
    if a.output:
        with a.output.open('x',encoding='utf-8') as stream:json.dump(r,stream,indent=2);stream.write('\n')
    print(json.dumps(r))
