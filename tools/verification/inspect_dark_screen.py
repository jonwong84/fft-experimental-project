"""Closed source/math/holding-state audit for DarkScreen; no game or emulator execution.

Original tables, blocks, states and generated reports belong under scratch.
The source-derived phase model below is verification tooling, not runtime code.
"""
import argparse
from collections import Counter
import json
import math
from pathlib import Path
import re
import struct
import subprocess
import sys

import inspect_banner_event as event

REVISION = event.c.observed.REVISION
TILES, FLASH, MODES, COLOR = 0x801d8c3c, 0x801d8c0c, 0x801d71d4, 0x801d0078
SQRT, SINCOS = 0x8002b8b8, 0x8002bee8
SELECTED = bytes([0, 1, 12, 64, 0, 4])
SOURCES = ('src/event/require/entry_dark_screen.c', 'src/event/require/screen_support.c',
    'src/lib/event/scene/menu_overlays.c', 'src/battle/event/interpreter.c',
    'src/lib/event/script/control.c', 'src/lib/thread/scheduler.c', 'src/lib/menu/display.c',
    'src/platform/psyq/libgte/SquareRoot0.c', 'src/platform/psyq/libgte/RotMatrix.c',
    'src/platform/psyq/libgte/ApplyMatrixLV.c', 'src/platform/psyq/libgpu/SetPolyF4.c',
    'src/platform/psyq/libgpu/SetSemiTrans.c', 'src/platform/psyq/libgpu/SetDrawMode.c',
    'include/psx/gte_inline.h', 'include/psx/libgpu.h', 'include/fft/after_battle.h',
    'include/fft/thread.h', 'target/battle.yaml', 'target/event.yaml', 'target/main.yaml')


def parameters(raw):
    event.v.require(raw == SELECTED, 'Only reserved-0/origin/divisor-12/angle-64/radius-4 is selected')
    return dict(reserved=raw[0], mode=raw[1], divisor=raw[2], angleStep=int.from_bytes(raw[3:5], 'little'), radiusStep=raw[5])


def sqrt0(value, table):
    event.v.integer(value, 0, 127296, 'selected squared-distance envelope')
    event.v.require(len(table) == 192 and all(type(x) is int and 0 < x < 32768 for x in table), 'Expected positive signed16 sqrt table')
    if value == 0: return 0
    even = (32 - value.bit_length()) & ~1
    exponent, difference = (31 - even) >> 1, even - 24
    normalized = value << difference if difference >= 0 else value >> -difference
    return ((table[normalized - 64] << exponent) & 0xffffffff) // 4096


def layout(table):
    tiles=[]
    for row in range(12):
        for col in range(24):
            x, y = col*12-12, row*24-(24 if col & 1 else 12)
            square=x*x+y*y; distance=sqrt0(square, table)
            tiles.append(dict(row=row, column=col, x=x, y=y, square=square, distance=distance,
                              delay=distance*distance//3072))
    return tiles


def trunc_div(value, divisor):
    return (1 if value >= 0 else -1) * (abs(value)//divisor)


def corners(angle, radius, sincos):
    event.v.require(type(angle) is int and angle in range(0, 513, 64) and type(radius) is int and radius in range(0, 49, 4),
                    'Outside selected angle/radius lattice')
    event.v.require(len(sincos) == 16384, 'Expected packed sin/cos table')
    offsets=[]
    for additional in (512,1536):
        sine, cosine = struct.unpack_from('<hh', sincos, (angle+additional)*4)
        event.v.require(abs(sine) <= 4096 and abs(cosine) <= 4096, 'Coefficient exceeds Q12 bounds')
        # Pure positive x-axis input radius*4096 reduces the two-pass ApplyMatrixLV
        # exactly to coefficient*radius; the caller then truncates signed /16384.
        offsets.append((trunc_div(cosine*radius,16384),trunc_div(sine*radius,16384)))
    (x0,y0),(x1,y1)=offsets
    return [(x0,y0),(x1,y1),(-x1,-y1),(-x0,-y0)]


def phase_scan(delays, phase):
    """Literal source-order counters; no rendering or input can complete them."""
    event.v.require(phase in (0,1) and type(phase) is int and 1 <= len(delays) <= 288, 'Invalid phase/model shape')
    for delay in delays: event.v.integer(delay,0,41,'selected delay')
    cells=[[d,0 if phase==0 else 512,0 if phase==0 else 48] for d in delays]
    done=False
    for tick in range(1,57):
        any_work=False
        for cell in cells:
            if cell[0]: cell[0]-=1;any_work=True;continue
            if phase==0:
                a,r=cell[1]+64,cell[2]+4
                any_work |= a < 513 or r < 49
                cell[1],cell[2]=min(512,a),min(48,r)
            else:
                a,r=cell[1]-64,cell[2]-4
                any_work |= a >= 0 or r >= 0
                cell[1],cell[2]=max(0,a),max(0,r)
        if done: return tick
        if not any_work: done=True
    raise ValueError('Selected finite phase did not complete')


def timing(delays, release_tick):
    for delay in delays: event.v.integer(delay,0,41,'selected delay')
    event.v.require(1 <= len(delays) <= 288, 'Expected nonempty bounded delay list')
    # Strict overshoot requires thirteen active draws and one extra body pass.
    holding=max(delays)+14
    event.v.integer(release_tick,holding+1,4096,'release after the holding birth tick')
    return dict(holdingFirstYield=holding, firstFlashAndEntryBarrierObservation=holding+1,
                releaseTick=release_tick, lastReleaseBody=release_tick+holding,
                taskClear=release_tick+holding+1, eventBarrierObservation=release_tick+holding+2)


def inspect_hold(ram, tiles, sincos):
    event.v.require(len(ram)==event.d.RAM_SIZE,'Missing main RAM')
    word=event.d.word
    event.v.require(word(ram,event.c.THREADS_POINTER)==event.THREADS,'Thread table differs')
    live=[s for s in range(1,16) if word(ram,event.THREADS+s*1024+0x48) and word(ram,event.THREADS+s*1024+0x4c)==55]
    event.v.require(live==[2],'Expected exactly the retained holding worker in slot 2')
    base=event.THREADS+2048
    event.v.require(word(ram,base+4)==event.BLOCK+795 and word(ram,base+0x44)==0x801cae9c,'DarkScreen parameter/coroutine site differs')
    sp=word(ram,base+0x3c)
    event.v.require(sp%4==0 and base+0x6c<=sp<base+1024,'Worker stack escapes slot')
    rgb=bytes(ram[COLOR & 0x1fffffff:(COLOR & 0x1fffffff)+3])
    event.v.require(rgb==bytes([48,40,16]),'Selected shared color differs')
    full=corners(512,48,sincos)
    for index,tile in enumerate(tiles):
        at=(TILES & 0x1fffffff)+index*72
        values=struct.unpack_from('<6i',ram,at+48)
        event.v.require(values==(0,48,512,tile['x'],tile['y'],tile['delay']),'Original tile counters/layout/delay differ')
        expected=tuple(v for ox,oy in full for v in (tile['x']+128+ox,tile['y']+oy))
        for bank in (0,1):
            poly=at+bank*24
            event.v.require(word(ram,poly)>>24==5 and ram[poly+4:poly+8]==rgb+b'\x2a'
                            and struct.unpack_from('<8h',ram,poly+8)==expected,'Holding tile packet differs')
    for bank in (0,1):
        at=(FLASH & 0x1fffffff)+bank*24
        event.v.require(word(ram,at)>>24==5 and ram[at+4:at+8]==rgb+b'\x2a'
                        and struct.unpack_from('<8h',ram,at+8)==(128,0,384,0,128,256,384,256),'Flash flat-color packet differs')
        event.v.require([word(ram,MODES+bank*12+o) for o in (4,8)]==[0xe1000006,0xe2000000],'Dither/window/average material differs')
    current=word(ram,event.d.CURRENT_BUFFER);event.v.require(current in (event.d.BUFFER_BASE,event.d.BUFFER_BASE+event.d.BUFFER_STRIDE),'Render buffer differs')
    ot=word(ram,current+0xe5b4);chain,nodes=event.d.walk(ram,ot+0x17f*4)
    flashes=[(i,at) for i,(at,_) in enumerate(chain) if at in (FLASH & 0x1fffffff,(FLASH+24)&0x1fffffff)]
    event.v.require(len(flashes)==1,'Expected one submitted hold flash')
    index,at=flashes[0];bank=(at-(FLASH & 0x1fffffff))//24
    event.v.require(index>0 and chain[index-1]==((MODES+bank*12)&0x1fffffff,[0xe1000006,0xe2000000]),'Flash is not preceded by its selected draw mode')
    return dict(slot=2,task=55,savedReturnAddress=word(ram,base+0x44),frameCounter=word(ram,event.hide.FRAME_COUNTER),
                rgb=list(rgb),checkedTiles=len(tiles),checkedTilePackets=2*len(tiles),
                checkedFlashBanks=2,submittedFlashBank=bank,dmaNodes=nodes,
                texturedPackets=False,enterOrReleaseObserved=False)


def run(plan_path, reference, output):
    plan_path,output=map(event.c.profile_tool.scratch_path,(plan_path,output))
    event.v.require(not output.exists() and plan_path.stat().st_size<=32768,'Existing output or oversized plan')
    plan=event.v.parse(plan_path.read_bytes())
    event.v.fields(plan,'format schemaVersion archive battle overlay executable bonus states','DarkScreen study plan')
    event.v.require(plan['format']=='fft.dark-screen-source-plan' and type(plan['schemaVersion']) is int and plan['schemaVersion']==1,'Unsupported plan')
    event.v.require(type(plan['states']) is list and 1<=len(plan['states'])<=4,'Expected 1..4 frozen holding states')
    revision=subprocess.check_output(['git','-C',str(reference),'rev-parse','HEAD'],text=True).strip()
    event.v.require(revision==REVISION and not subprocess.check_output(['git','-C',str(reference),'status','--porcelain'],text=True).strip(),'Reference differs or is dirty')
    sources=[dict(path=p,bytes=len(raw),sha256=event.v.sha(raw)) for p in SOURCES for raw in [(reference/p).read_bytes()]]
    inputs={key:event.hide.verified(plan[key]) for key in ('archive','battle','overlay','executable','bonus')}
    expected=dict(archive=event.ARCHIVE_SHA,battle=event.BATTLE_SHA,
                  overlay='97112c50c0b037b2cb5c9d93ae9e2a7506b582c5edbd86a1debfb7ab8e777630',
                  executable='287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562',
                  bonus='c5d881b1fb62c04d1d7f0e5238faff4131676165c08e35b5cadb68b21c89165f')
    for key,raw in inputs.items():event.v.require(event.v.sha(raw)==expected[key],'Original input differs: '+key)
    event.v.require(inputs['executable'][:8]==b'PS-X EXE' and struct.unpack_from('<I',inputs['executable'],0x18)[0]==0x80010000,'Executable load mapping differs')
    functions=[]
    groups=[('battle.yaml','battle',0x80067000,['script_execute_event','script_dark_screen_thread','thread_task_find','gfx_append_gpu_primitive_to_secondary_otag','thread_frames_wait','thread_current_exit']),
            ('event.yaml','overlay',0x801bf000,['require_dark_screen_entrypoint','conditions_load_banner_image']),
            ('main.yaml','executable',0x8000f800,['SquareRoot0','RotMatrix','ApplyMatrixLV','SetPolyF4','SetSemiTrans','SetDrawMode'])]
    for mapping,key,base,names in groups:
        text=(reference/'target'/mapping).read_text(encoding='utf-8')
        for name in names:
            matches=re.findall(r'addr: (0x[0-9a-f]+), size: (\d+), name: '+name+r'[,}]',text)
            event.v.require(len(matches)==1,'Ambiguous function map: '+name)
            at,size=int(matches[0][0],16),int(matches[0][1]);raw=inputs[key][at-base:at-base+size]
            event.v.require(len(raw)==size,'Function escapes verified file')
            functions.append(dict(name=name,address=at,bytes=size,sha256=event.v.sha(raw)))
    sqrt_raw=inputs['executable'][SQRT-0x8000f800:SQRT-0x8000f800+384]
    sincos=inputs['executable'][SINCOS-0x8000f800:SINCOS-0x8000f800+16384]
    table=struct.unpack('<192h',sqrt_raw);tiles=layout(table);delays=[t['delay'] for t in tiles]
    # Two independently structured timing calculations, checked without executing original instructions.
    phase_ticks=[phase_scan(delays,p) for p in (0,1)];event.v.require(phase_ticks==[55,55],'Selected literal loop timing differs')
    times=timing(delays,100);event.v.require(times['holdingFirstYield']==phase_ticks[0] and times['lastReleaseBody']==100+phase_ticks[1],'Closed-form timing differs')
    projections=[dict(angle=a,radius=r,corners=corners(a,r,sincos)) for a in range(0,513,64) for r in range(0,49,4)]
    observations=[];commands=None
    for state in plan['states']:
        ram,lengths=event.read_ram(event.hide.verified(state))
        for f in functions:
            at=f['address']&0x1fffffff;event.v.require(event.v.sha(ram[at:at+f['bytes']])==f['sha256'],'Resident function differs: '+f['name'])
        for at,raw in ((SQRT,sqrt_raw),(SINCOS,sincos)):
            event.v.require(ram[at&0x1fffffff:(at&0x1fffffff)+len(raw)]==raw,'Resident math table differs')
        current,loaded=(event.d.word(ram,at) for at in (event.VARIABLES+0x27*4,0x801696fc))
        raw=inputs['archive'][4*8192:5*8192];live=ram[event.BLOCK&0x1fffffff:(event.BLOCK&0x1fffffff)+8192]
        matches=[i for i in range(500) if inputs['archive'][i*8192+4:(i+1)*8192]==live[4:]]
        event.v.require(current==loaded==4 and live==b'\xf2'*4+raw[4:] and matches==[4],'Unique live event-4 payload differs')
        selected=[]
        for id,at,barrier,release in ((4,794,801,816),(401,154,161,198)):
            block=inputs['archive'][id*8192:(id+1)*8192];_,records=event.instructions(block,lengths)
            record=[r for r in records if r[0]==at]
            event.v.require(len(record)==1 and record[0][1]==0x76,'DarkScreen opcode differs')
            p=parameters(record[0][2]);event.v.require((barrier,0xe5,b'\x36\x00') in records and (release,0x77,b'') in records,'Entry barrier/release differs')
            selected.append(dict(eventId=id,rawBlockSha256=event.v.sha(block),darkOffset=at,entryBarrier=barrier,releaseOffset=release,parameters=p))
        event.v.require(commands is None or commands==selected,'Selected commands changed across states');commands=selected
        observations.append(dict(state=state,**inspect_hold(ram,tiles,sincos)))
    root_differences=sum(t['distance']!=math.isqrt(t['square']) for t in tiles)
    delay_differences=sum(t['delay']!=math.isqrt(t['square'])**2//3072 for t in tiles)
    report=dict(format='fft.dark-screen-source-audit',schemaVersion=1,referenceRevision=revision,sourceFiles=sources,
        managedBase=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),plan=event.c.identity(plan_path),inputs=plan,
        residentFunctions=functions,commands=commands,observations=observations,
        mathTables=[dict(address=at,bytes=len(raw),sha256=event.v.sha(raw)) for at,raw in ((SQRT,sqrt_raw),(SINCOS,sincos))],
        initialization=dict(tiles=288,delayMinimum=min(delays),delayMaximum=max(delays),delayHistogram=dict(sorted(Counter(delays).items())),
                            exactSqrtRootDifferences=root_differences,exactSqrtDelayDifferences=delay_differences,
                            tableDelaySha256=event.v.sha(struct.pack('<288i',*delays))),
        sourceModel=dict(phaseBodyTicks=phase_ticks,exampleRelease100=times,projectionLattice=117,
                         projectionSha256=event.v.sha(json.dumps(projections,separators=(',',':')).encode())),
        tools=[event.c.identity(Path(module.__file__)) for module in (sys.modules[__name__],event,event.hide,event.c,event.d,event.v)],
        originalEntryOrReleaseObserved=False,newOriginalCaptures=False,emulatorLaunched=False,runtimeChanged=False,
        originalDurationEstablished=False,originalTransitionPixelComparison=False)
    output.mkdir(parents=True)
    (output/'report.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    return report


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for flag in ('plan','reference-root','output'):parser.add_argument('--'+flag,type=Path,required=True)
    args=parser.parse_args()
    try:
        report=run(args.plan,args.reference_root,args.output)
        print(f"Audited {len(report['observations'])} holding states, 288 delays and 117 source-derived projections; no transition execution or pixel claim.")
    except (ValueError,KeyError,OSError,TypeError,IndexError,struct.error,subprocess.CalledProcessError) as error:
        parser.exit(2,'DarkScreen source audit failed: '+str(error)+'\n')
