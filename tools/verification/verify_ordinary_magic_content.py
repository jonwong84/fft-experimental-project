"""Audit selected ordinary spell bindings against pinned source and verified prepared data."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from verify_sword_knockback_contract import REVISION,fingerprint,function_fingerprint,require

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/elemental-spell-bindings-v1.json'
SOURCE=ROOT/'docs/evidence/combat/ordinary-magic-content-source-v1.json'
HEALING_FIXTURE=ROOT/'docs/fixtures/behavior/ordinary-healing-bindings-v1.json'
HEALING_SOURCE=ROOT/'docs/evidence/combat/ordinary-healing-source-v1.json'

def validate(fixture):
    require(fixture['schemaVersion']==1 and fixture['referenceRevision']==REVISION,'Fixture identity')
    selection=fixture.get('selection','elemental');require(selection in ['elemental','healing','fire','learning'],'Selected binding family')
    rows=fixture['cases'];require([r['id'] for r in rows]==([1,16] if selection=='healing' else [16] if selection=='fire' else [16,19] if selection=='learning' else list(range(16,28))),'Explicit prepared selection order')
    for row in rows:
        healing=row['id']==1;tier=(row['id']-16)%4;fourth=not healing and tier==3
        require(row['formula']==(12 if healing else 8) and row['status']==0,'Selected ordinary formula and empty status')
        require(row['range']==4 and row['area']==(2 if fourth else 1),'Reviewed range and area')
        require(row['flags1']==0 and row['flags2']==(1 if fourth else 0),'Reviewed targeting flags')
        require(row['flags3']==(34 if fourth else 226) and row['flags4']==(0 if healing else 2 if fourth else 66),'Reviewed secondary attributes')
        key='expectedHealingAtMa8Faith70' if healing else 'expectedDamageAtMa8Faith70'
        require(row['power']*8*70*70//10000==row[key],'Independent ordinary amount')
        flags=row['primaryAiFlags']
        require(flags==([129,117,0,128] if healing else [130,21 if fourth else 85,2,128]),'Reviewed primary AI metadata')
        require(not flags[1]&2 and row['learnOnHit']==fourth,'Primary evade and learning distinction')
        require(row['serializedClassRaw']==1 and row['showName'] and row['canLearnWithJp'] and not row['unknownTypeBit4'],'Reviewed type metadata')
    if selection=='elemental':require(rows[5]['mp']==10 and rows[1]['mp']==12 and rows[9]['mp']==12,'Bolt2 cost difference')
    return dict(bindingVectors=len(rows),sharedFormulas=len({r['formula'] for r in rows}))

def audit(reference,executable,content_root,selection='elemental'):
    require(selection in ['elemental','healing','fire','learning'],'Selected binding family')
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==REVISION,'Pinned reference')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(),'Clean reference')
    source=json.loads((HEALING_SOURCE if selection=='healing' else SOURCE).read_text(encoding='utf-8'))
    for row in source['sourceFiles']:require(fingerprint(reference,row['path'])==row,'File '+row['path'])
    for row in source['sourceFunctions']:
        actual=function_fingerprint(reference/row['path'],row['symbol'])
        require(all(row[k]==v for k,v in actual.items()),'Function '+row['symbol'])
    data=executable.read_bytes();require(len(data)==356352 and hashlib.sha256(data).hexdigest()=='287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562','Prepared SCUS-94221')
    fixture=json.loads((HEALING_FIXTURE if selection=='healing' else FIXTURE).read_text(encoding='utf-8'));report=validate(fixture)
    if selection=='fire':fixture=dict(fixture,selection='fire',cases=fixture['cases'][:1]);report=validate(fixture)
    if selection=='learning':fixture=dict(fixture,selection='learning',cases=[fixture['cases'][0],fixture['cases'][3]]);report=validate(fixture)
    package=json.loads((content_root/'ordinary-magic.package.json').read_text(encoding='utf-8'))
    manifest=json.loads((content_root/'assets.manifest.json').read_text());metadata=None
    for entry in manifest['assets']:
        asset=(content_root/entry['relativePath']).read_bytes()
        require(len(asset)==entry['byteLength'] and hashlib.sha256(asset).hexdigest()==entry['sha256'],'Prepared manifest integrity')
        if entry['id']==package.get('metadataPackageId'):metadata=json.loads(asset)
    if package['schemaVersion']>=2:
        require(metadata is not None and metadata['packageId']==package['metadataPackageId'],'Required metadata companion')
        receipt=json.loads((ROOT/'docs/evidence/combat/ordinary-spell-primary-metadata-source-v1.json').read_text())
        for row in receipt['sourceFiles']:require(fingerprint(reference,row['path'])==row,'Metadata source '+row['path'])
        for row in receipt['sourceFunctions']:
            actual=function_fingerprint(reference/row['path'],row['symbol'])
            require(all(row[k]==v for k,v in actual.items()),'Metadata function '+row['symbol'])
    require([s['originalId'] for s in package['spells']]==[r['id'] for r in fixture['cases']],'Prepared source slot order')
    for row,spell in zip(fixture['cases'],package['spells']):
        start=0x8005fbf0+row['id']*14-0x80010000+0x800
        expected=bytes([row['range'],row['area'],row['vertical'],row['flags1'],row['flags2'],row['flags3'],row['flags4'],row['element'],row['formula'],0,row['power'],row['status'],row['charge'],row['mp']])
        require(data[start:start+14]==expected,'Source spell '+row['name'])
        start=0x8005ebf0+row['id']*8-0x80010000+0x800
        require(int.from_bytes(data[start:start+2],'little')==row['jp'],'JP '+row['name'])
        primary=data[start:start+8]
        require(primary[2]==row['learnOnHitRateRaw'] and primary[3]&15==row['serializedClassRaw'],'Primary learning fields '+row['name'])
        require(bool(primary[3]&32)==row['learnOnHit'] and list(primary[4:8])==row['primaryAiFlags'],'Primary source flags '+row['name'])
        if metadata is not None:
            definition=next(a for a in metadata['abilities'] if a['id']==spell['id'])
            expected_flags={'show-name'}|({'learn-on-hit'} if row['learnOnHit'] else set())
            require(definition['jpCost']==row['jp'] and definition['learnOnHitRateRaw']==row['learnOnHitRateRaw'] and
                definition['serializedClassRaw']==row['serializedClassRaw'] and definition['unknownTypeBitsRaw']==0 and
                set(definition['flags'])==expected_flags and definition['aiFlagsRaw']==row['primaryAiFlags'],'Prepared primary metadata '+row['name'])
        require(spell['mpCost']==row['mp'] and spell['chargeTime']==row['charge'] and spell['formula']['power']==row['power'],'Prepared parameters '+row['name'])
        require(spell['name']==row['name'] and spell['formula']['kind']==('Healing' if row['formula']==12 else 'Damage') and spell['formula']['elements']=={0:'None',128:'Fire',64:'Lightning',32:'Ice'}[row['element']],'Prepared formula '+row['name'])
        require(spell['formula']['secondaryEvadeable']==bool(row['flags4']&2) and spell['affectedBySilence'],'Prepared execution flags '+row['name'])
        require(all(spell['targeting'][k]==row[k] for k in ['range','area','vertical']),'Prepared geometry '+row['name'])
        require(spell['targeting']['cannotHitCaster']==bool(row['flags2']&1),'Caster exclusion '+row['name'])
    if metadata is not None:
        require({a['id'] for a in metadata['abilities']}=={s['id'] for s in package['spells']} and not metadata['jobs'],'Exact metadata binding without duplicate jobs')
        for skillset in metadata['skillsets']:
            index={'fft.ps1:skillset/basic-skill':5,'fft.ps1:skillset/black-magic':11,'fft.ps1:skillset/white-magic':10}[skillset['id']]
            start=0x80064a94+index*25-0x80010000+0x800;row=data[start:start+25];expected=[]
            for slot in range(22):
                ability=row[3+slot] | (256 if row[slot//8] & (128>>(slot%8)) else 0)
                identifier=f'fft.ps1:ability/source-{ability:04d}'
                if identifier in {s['id'] for s in package['spells']}:expected.append(dict(index=slot,abilityId=identifier))
            require(skillset['slots']==expected,'Selected source skillset slots')
    require(set(package['units'][0]['learnedSpells'])=={s['id'] for s in package['spells']},'Explicit selected learned grants')
    if selection=='learning':
        require(package['schemaVersion']==3 and package['seed']==10,'Explicit learning scene identity')
        require([u['learnedPrimarySlots'] for u in package['units']]==[[0,3],[]],'Explicit current-job learned slots')
        require(package['units'][1]['jobId']=='fft.ps1:job/wizard' and not package['units'][1]['learnedSpells'],'Eligible unlearned Wizard recipient')
    provenance=json.loads((content_root/('fire.import.json' if selection=='fire' else 'ordinary-magic.import.json')).read_text())
    require(provenance['verifiedInputSha256']==hashlib.sha256(data).hexdigest() and provenance['referenceRevision']==REVISION,'Verified import provenance')
    expected_records=(38 if selection=='healing' else 34 if selection=='fire' else 36 if selection=='learning' else 56)+(1 if package['schemaVersion']>=2 else 0)
    require(len(provenance['records'])==expected_records,'Complete selected record receipt')
    for row in provenance['records']:
        require(row['FileOffset']==row['VirtualAddress']-0x80010000+0x800,'Source address mapping')
        require(data[row['FileOffset']:row['FileOffset']+row['ByteLength']].hex()==row['RawHex'],'Source record '+row['Table'])
    return dict(report,referenceRevision=REVISION,referenceClean=True,sourceFiles=len(source['sourceFiles']),sourceFunctions=len(source['sourceFunctions']),preparedRecords=len(provenance['records']),metadataRecordsChecked=0 if metadata is None else len(metadata['abilities']),manifestVerified=True,originalExecuted=False,cExecuted=False)

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--prepared-executable',type=Path,required=True);p.add_argument('--content-root',type=Path,required=True);p.add_argument('--selection',choices=['elemental','healing','fire','learning'],default='elemental');p.add_argument('--output',type=Path)
    a=p.parse_args();r=audit(a.reference,a.prepared_executable,a.content_root,a.selection)
    if a.output:
        with a.output.open('x',encoding='utf-8') as stream:json.dump(r,stream,indent=2);stream.write('\n')
    print(json.dumps(r))
