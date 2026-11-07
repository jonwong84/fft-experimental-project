"""Thirteen finite source-bound original DarkScreen scanouts versus actual prepared C#.

Private restored-state captures; no physical input or modified original instructions.
Material-neutral RAM controls are altered copies, with strict post-run isolation.
"""
import argparse
import json
from pathlib import Path
import subprocess
import sys

from PIL import Image
import inspect_dark_screen_reference as r
import verify_dark_screen_preview as model

v=r.v
ROOT=Path(__file__).resolve().parents[2]
TICKS=(2,6,14,26,42,46,163,165,167,171,179,211,213)


def metrics(a,b):
    v.require(len(a)==len(b)==256*240*4,'Wrong canvas size')
    changed=[i for i in range(256*240) if a[i*4:i*4+4]!=b[i*4:i*4+4]]
    return dict(exact=not changed,changedPixels=len(changed),maxChannelDelta=max((abs(x-y) for x,y in zip(a,b,strict=True)),default=0),
        changedBounds=None if not changed else [min(i%256 for i in changed),min(i//256 for i in changed),max(i%256 for i in changed),max(i//256 for i in changed)])


def geometry(draws):
    return tuple(tuple((x-128,y) for x,y in item['xy']) for item in draws)


def footprint(quads):
    result=set()
    for a,b,c,d in quads:
        for tri in ((a,b,c),(b,c,d)):
            for y,left,right in model.triangle_rows(tri):result.update(y*256+x for x in range(left,right+1))
    return result


def compare(original,background,native,quads):
    for data in (original,background,native):
        v.require(len(data)==256*240*4 and all(data[i+3]==255 and not ((data[i]|data[i+1]|data[i+2])&7) for i in range(0,len(data),4)),'Non-RGB5 canvas')
    full=metrics(original,native);independent=metrics(model.compose(quads,background),native);points=footprint(quads)
    outside=sum(original[i*4:i*4+4]!=background[i*4:i*4+4] for i in range(256*240) if i not in points)
    return dict(passed=full['exact'] and independent['exact'] and outside==0,fullCanvas=full,independentModel=independent,
        coveredPixels=len(points),outsideFootprintChanges=outside,backgroundSensitiveChangedPixels=sum(original[i*4:i*4+4]!=background[i*4:i*4+4] for i in points),tolerance=0)


def native_frames(output,checkpoints,backgrounds,expected_source):
    receipt=r.bg.v.parse((output/'check.json').read_bytes())
    v.fields(receipt,'format schemaVersion policy renderer source holdFrames checkpoints originalComparison gpuReadbackPerformed','native receipt')
    v.require(receipt['format']=='fft.dark-screen-background-cpu-check' and receipt['schemaVersion']==1 and
        receipt['policy']=='untextured-rgb5-flat-quad-v1' and receipt['renderer']=='DarkScreenPresentation.Compose' and
        receipt['holdFrames']==108 and receipt['originalComparison']=='pending' and receipt['gpuReadbackPerformed'] is False and len(receipt['checkpoints'])==len(TICKS),'Wrong actual C# receipt')
    v.require({k[0].lower()+k[1:]:value for k,value in receipt['source'].items()}==expected_source,'Actual C# source provenance differs')
    results=[]
    for tick,item,source,bg in zip(TICKS,receipt['checkpoints'],checkpoints,backgrounds,strict=True):
        v.fields(item,'logicalTick file geometryFile geometrySha256 rgbaSha256 backgroundSha256 drawCount phase drawKind','native checkpoint')
        v.require(item['logicalTick']==tick and item['file']==f'tick-{tick:03}.rgba' and item['geometryFile']==f'tick-{tick:03}.quads.json' and
            item['backgroundSha256']==v.sha(bg) and item['phase']==source['task']['phase'] and
            item['drawKind']==('Flash' if tick==163 else 'Tiles'),'Wrong actual C# anchor')
        path=output/item['file'];v.require(path.stat().st_size==256*240*4,'Native canvas exceeds/wrong size')
        pixels=path.read_bytes();v.require(v.sha(pixels)==item['rgbaSha256'],'Native pixel fingerprint differs')
        path=output/item['geometryFile'];v.require(path.stat().st_size<=262144,'Oversized actual C# geometry')
        raw=path.read_bytes();v.require(v.sha(raw)==item['geometrySha256'],'Native geometry fingerprint differs')
        actual=json.loads(raw,object_pairs_hook=v.unique)
        expected=[dict(**{key:dict(X=x-128,Y=y) for key,(x,y) in zip('ABCD',q['xy'],strict=True)},Red=48,Green=40,Blue=16) for q in source['task']['draws']]
        v.require(actual==expected and item['drawCount']==len(expected),'Actual C# differs from original packet geometry/order/color')
        results.append(pixels)
    return receipt,results


def run(plan_path,reference,output):
    plan_path,output=map(r.bg.profile_tool.scratch_path,(plan_path,output));v.require(not output.exists(),'Output exists')
    plan=v.parse(plan_path.read_bytes());v.fields(plan,'format schemaVersion referenceRevision profile prepared archive sourceEvidence checkpoints','comparison plan')
    v.require(plan['format']=='fft.dark-screen-reference-plan' and plan['schemaVersion']==1 and plan['referenceRevision']==r.source.REVISION and
        [x['logicalTick'] for x in plan['checkpoints']]==list(TICKS),'Unsupported finite plan')
    for item in plan['checkpoints']:
        v.fields(item,'logicalTick state frontendFrames','input checkpoint');v.integer(item['frontendFrames'],4,5,'selected endpoint budget')
    evidence=plan['sourceEvidence']
    v.require(evidence['localPath']=='docs/evidence/events/dark-screen-source-v1.json','Unexpected source-evidence path')
    raw=(ROOT/evidence['localPath']).read_bytes()
    v.require(len(raw)==evidence['bytes'] and v.sha(raw)==evidence['sha256'],'Source evidence identity differs')
    source_evidence=json.loads(raw)
    archive=r.source.event.hide.verified(plan['archive']);v.require(v.sha(archive)==r.source.event.ARCHIVE_SHA,'Original archive differs')
    def reference_identity():
        v.require(subprocess.check_output(['git','-C',str(reference),'rev-parse','HEAD'],text=True).strip()==r.source.REVISION and
            not subprocess.check_output(['git','-C',str(reference),'status','--porcelain'],text=True).strip(),'Reference changed')
        for f in source_evidence['sourceFiles']:
            data=(reference/f['path']).read_bytes();v.require(len(data)==f['bytes'] and v.sha(data)==f['sha256'],'Pinned source file changed')
    reference_identity()
    prepared=r.bg.profile_tool.scratch_path(ROOT/plan['prepared']['root'])
    for f in plan['prepared']['files']:r.source.event.hide.verified(f)
    definition=v.parse((prepared/'dark-screen.json').read_bytes());v.require(definition['holdFrames']==108,'Expected authored release at 164')
    profile_path=r.bg.profile_tool.scratch_path(ROOT/plan['profile']['localPath']);profile=v.parse(r.source.event.hide.verified(plan['profile']));profile_root=profile_path.parent
    v.require(profile['format']=='fft.retroarch-reference-profile' and profile['schemaVersion']==1 and profile['edition']=='SCUS-94221','Wrong reference profile')
    for key,name in [('executable','retroarch.exe'),('coreLibrary','mednafen_psx_libretro.dll'),('cue','fft-clean-base-game.cue')]:
        matches=[x['path'] for x in profile['inputs'] if Path(x['path']).name.lower()==name];v.require(len(matches)==1,'Ambiguous runtime path');profile[key]=matches[0]
    def profile_identity():
        for item in profile['inputs']:v.require(r.bg.profile_tool.file_hash(Path(item['path']))==item['sha256'],'Original runtime/input differs')
        for name,digest in profile['generatedSha256'].items():v.require(name in ('retroarch.cfg','core-options.cfg','launch.ps1') and r.bg.profile_tool.file_hash(profile_root/name)==digest,'Private profile changed')
        v.require(r.bg.profile_tool.file_hash(profile_root/'system/scph5501.bin')==r.bg.profile_tool.BIOS_HASH,'Actual BIOS differs')
    profile_identity();normal={x['path']:r.bg.profile_tool.file_hash(Path(x['path'])) for x in profile['normalConfigIdentities']}
    tools=[r.bg.continuation.identity(Path(module.__file__)) for module in (sys.modules[__name__],r,r.source,r.frame,r.bg,r.p,v,model)]
    tools += [r.bg.continuation.identity(ROOT/p) for p in ('tools/banner-blend-check/DarkScreenSequence.cs','tools/banner-blend-check/Program.cs',
        'src/Fft.Client/Presentation/DarkScreenPresentation.cs','src/Zodiac.Framework/Events/DarkScreenState.cs','src/Zodiac.Framework/Events/DarkScreenExecutor.cs')]
    host=ROOT/'tools/banner-blend-check/bin/Release/net10.0';assemblies=[r.bg.continuation.identity(host/(name+'.dll')) for name in ('BannerBlendCheck','Fft.Client','Zodiac.Content','Zodiac.Framework')]
    sources=[r.source.event.hide.verified(x['state']) for x in plan['checkpoints']]
    contexts=[r.snapshot(state,definition,archive,source_evidence['residentFunctions']) for state in sources]
    v.require([c['task']['logicalTick'] for c in contexts]==list(TICKS),'Selected queued source contexts differ')
    output.mkdir(parents=True)
    execution=dict(format='fft.dark-screen-reference-execution-plan',schemaVersion=1,referenceRevision=r.source.REVISION,
        inputPlan=r.bg.continuation.identity(plan_path),tools=tools,assemblies=assemblies,maximumOriginalProcesses=2*len(TICKS),
        frontendFramesPerCheckpoint=[dict(logicalTick=x['logicalTick'],frontendFrames=x['frontendFrames']) for x in plan['checkpoints']],timeoutSeconds=55,
        controllersDisconnected=True,muted=True,sourceStatesPreserved=True,
        intervention='Only selected queued RGB bytes become zero and selected E1 changes average to additive. Geometry, code, links, counters, palettes and timing are unchanged at input.',
        fullCpuIdentityRequired=True,
        binding='Pinned pre-submission VSync stack/environment, source counters/queued geometry, fixed two-earlier submitted front buffer.',
        scope='Thirteen selected entry/hold/release scanouts, actual C# over strictly isolated controlled backgrounds; no original duration or full startup claim.')
    (output/'plan.json').write_text(json.dumps(execution,indent=2)+'\n',encoding='utf-8')
    originals=[];backgrounds=[];records=[]
    try:
        for tick,state,context,input_point in zip(TICKS,sources,contexts,plan['checkpoints'],strict=True):
            addresses=[x['address'] for x in context['task']['draws']];probe,intervention=r.control(state,addresses)
            (output/f'control-{tick:03}.state').write_bytes(probe)
            original,pixels,left=r.bg.capture(profile,profile_root,output/f'original-{tick:03}',state,input_point['frontendFrames'])
            control,background,right=r.bg.capture(profile,profile_root,output/f'control-{tick:03}',probe,input_point['frontendFrames'])
            current=r.snapshot(original,definition,archive,source_evidence['residentFunctions'])
            binding=r.frame.bind_display(current,context)
            v.require(binding['displayedLogicalTick']==tick and left['inputStateSha256']==v.sha(state) and right['inputStateSha256']==v.sha(probe),'Restore/display binding differs')
            isolate=r.isolation(original,control,intervention['allowedRamOffsets']);v.require(isolate['passed'],'Material-control isolation failed')
            v.require(r.frame.scanout(original)==pixels and r.frame.scanout(control)==background,'PNG differs from saved scanout')
            lgpu,rgpu=(r.p.gpu_records(r.p.unpack_state(x))['&GPURAM[0][0]'] for x in (original,control))
            outside=sum(lgpu[i]!=rgpu[i] for i in range(len(lgpu)) if i//2%1024>=256 or i//2048>=480)
            v.require(outside==0,'Control changes non-framebuffer VRAM')
            path=output/f'background-{tick:03}.rgba';path.write_bytes(background)
            originals.append(pixels);backgrounds.append(background)
            records.append(dict(logicalTick=tick,sourceContext=context,currentContext=current,displayed=binding,intervention=intervention,
                isolation=isolate,nonFramebufferVramChanges=outside,originalCapture=left,controlCapture=right,
                background=r.bg.continuation.identity(path)))
            print(f'tick {tick}: {context["task"]["phase"]}, {len(addresses)} source quads, isolation accepted; CPU exact={isolate["fullCpuExact"]}',flush=True)
        native_plan=dict(format='fft.dark-screen-backgrounds',schemaVersion=1,checkpoints=[dict(logicalTick=t,backgroundPath=str(output/f'background-{t:03}.rgba'),backgroundSha256=v.sha(b)) for t,b in zip(TICKS,backgrounds,strict=True)])
        (output/'backgrounds.json').write_text(json.dumps(native_plan,indent=2)+'\n',encoding='utf-8')
        args=['dotnet',str(host/'BannerBlendCheck.dll'),'--dark-screen-sequence',str(prepared),str(output/'backgrounds.json'),str(output/'native')]
        (output/'native-command.json').write_text(json.dumps(args,indent=2)+'\n',encoding='utf-8')
        result=subprocess.run(args,cwd=ROOT,capture_output=True,timeout=55)
        (output/'native-stdout.txt').write_bytes(result.stdout);(output/'native-stderr.txt').write_bytes(result.stderr)
        v.require(result.returncode==0,'Actual C# composition failed')
        receipt,native=native_frames(output/'native',contexts,backgrounds,definition['source']);cards=[]
        for tick,record,original,background,pixels in zip(TICKS,records,originals,backgrounds,native,strict=True):
            record.update(compare(original,background,pixels,geometry(record['sourceContext']['task']['draws'])))
            images=[]
            for name,data in [('original',original),('background',background),('csharp',pixels)]:
                file=f'{tick:03}-{name}.png';Image.frombytes('RGBA',(256,240),data).save(output/file)
                images.append(f'<div>{name}<br><img width="512" height="480" src="{file}"></div>')
            cards.append(f'<h2>{record["sourceContext"]["task"]["phase"]} · logical tick {tick} · exact={record["passed"]}</h2><section>'+''.join(images)+'</section>')
        report=dict(format='fft.dark-screen-original-comparison',schemaVersion=1,referenceRevision=r.source.REVISION,
            passed=all(x['passed'] for x in records),plan=r.bg.continuation.identity(output/'plan.json'),checkpoints=records,tools=tools,assemblies=assemblies,
            nativeReceipt=r.bg.continuation.identity(output/'native/check.json'),selectedRuntimePolicy='untextured-rgb5-flat-quad-v1',
            newOriginalCaptures=True,controlledMaterialProbe=True,originalInstructionsModified=False,sourceStatesPreserved=True,
            normalConfigsUnchanged=True,gpuReadbackPerformed=False,originalDurationEstablished=False,fullStartupEstablished=False,ownerReview='pending')
        report.update(fullCpuIdentityClaim=True)
        review='<!doctype html><meta charset="utf-8"><title>Original dark-screen comparison</title><style>body{background:#17202a;color:#eee;font:16px system-ui;margin:24px}section{display:flex;gap:16px;flex-wrap:wrap}img{image-rendering:pixelated;max-width:100%}</style><h1>DarkScreen: original / controlled background / C#</h1><p>Thirteen source-bound native 256×240 scanouts at 2×. The background comes from a copied state with only selected flat RGB bytes zeroed and selected blend mode additive. Counters, memory outside those fields, DMA, audio, timers and GPU registers are checked. All CPU fields, including COP0 TAR, match between each original/control endpoint. Original timing is not established. Rendering uses prepared data and the actual C# compositor. Exact zero-tolerance comparisons cover selected entry, hold and release anchors, including the late tail. Birth/transition/exit endpoints, duration, map rendering and full startup remain separate.</p>'+''.join(cards)
        (output/'report.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8');(output/'review.html').write_text(review,encoding='utf-8')
    finally:
        for path,digest in normal.items():v.require(r.bg.profile_tool.file_hash(Path(path))==digest,'Installed configuration changed')
        profile_identity();reference_identity()
        for item in tools+assemblies:v.require(r.bg.profile_tool.file_hash(ROOT/item['localPath'])==item['sha256'],'Tool/assembly changed during comparison')
        for item,state in zip(plan['checkpoints'],sources,strict=True):v.require(r.source.event.hide.verified(item['state'])==state,'Archived source state changed')
    print(str(len(TICKS))+' source-bound comparisons passed='+str(report['passed']),flush=True)
    return 0 if report['passed'] else 1


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for key in ('plan','reference-root','output'):parser.add_argument('--'+key,type=Path,required=True)
    args=parser.parse_args()
    try:sys.exit(run(args.plan,args.reference_root,args.output))
    except (ValueError,KeyError,OSError,TypeError,IndexError,subprocess.SubprocessError) as error:parser.exit(2,'Dark-screen original comparison failed (diagnostics retained): '+str(error)+'\n')
