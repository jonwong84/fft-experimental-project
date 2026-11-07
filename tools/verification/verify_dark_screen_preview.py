"""Offline original-math and independent scanline audit of bounded prepared C# captures.

No original instructions execute. Original data, captures and generated reports stay in scratch.
Integer-sample/top-left coverage is a selected provisional policy, not original pixel equivalence.
"""
import argparse
from functools import lru_cache
import json
from pathlib import Path
import struct
import subprocess
import sys

import inspect_dark_screen as source
from PIL import Image

v = source.event.v
ROOT = Path(__file__).resolve().parents[2]


def read_json(path, limit=1048576):
    raw = path.read_bytes()
    v.require(len(raw) <= limit, 'Oversized metadata')
    return json.loads(raw, object_pairs_hook=v.unique, parse_constant=lambda _: v.require(False, 'Invalid constant'))


def background():
    data = bytearray(256 * 240 * 4)
    for y in range(240):
        for x in range(256):
            at = (y * 256 + x) * 4; shade = ((x // 16 + y // 16) & 1) * 16
            data[at:at+4] = bytes((64+shade, 96+shade, 128+shade, 255))
    return bytes(data)


def triangle_rows(vertices):
    """Solve integer edge half-planes into scanline intervals; no per-pixel edge tests."""
    a, b, c = vertices
    area = (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
    if not area: return
    if area < 0: b, c = c, b
    for y in range(max(0, min(a[1], b[1], c[1])), min(239, max(a[1], b[1], c[1]))+1):
        left, right = 0, 255
        for p, q in ((a,b),(b,c),(c,a)):
            dx, dy = q[0]-p[0], q[1]-p[1]
            inclusive = dy < 0 or dy == 0 and dx > 0
            rhs = dx*(y-p[1])+dy*p[0]-(0 if inclusive else 1)
            if dy > 0: right = min(right, rhs//dy)
            elif dy < 0: left = max(left, -((-rhs)//dy))
            elif rhs < 0: left, right = 1, 0; break
        if left <= right: yield y, left, right


def compose(quads, bg):
    v.require(len(bg) == 256*240*4, 'Bad background')
    result = bytearray(bg)
    for a,b,c,d in quads:
        for tri in ((a,b,c),(b,c,d)):
            for y,left,right in triangle_rows(tri):
                for at in range((y*256+left)*4, (y*256+right+1)*4, 4):
                    for ch, color in enumerate((6,5,2)):
                        result[at+ch] = (((result[at+ch]>>3)+color)>>1)<<3
    return bytes(result)


def expected(tick, hold):
    v.integer(hold,0,255,'hold');v.integer(tick,0,113+hold,'tick')
    release=56+hold
    phase = 'Entering' if tick<55 else 'Holding' if tick<release else 'Leaving' if tick<release+56 else 'Completed'
    kind = 'None' if tick==0 or tick>=release+56 else 'Tiles' if tick<=55 or tick>release else 'Flash'
    return phase,kind,{'Entering':0,'Holding':1,'Leaving':2,'Completed':3}[phase],release


def validate_receipt(receipt, definition):
    v.require(receipt['format']=='fft.dark-screen-capture' and receipt['schemaVersion']==1 and
              receipt['profile']=='authored-dark-screen-preview-v1' and receipt['policy']=='untextured-rgb5-flat-quad-v1', 'Wrong capture profile')
    hold=definition['holdFrames'];v.integer(hold,0,255,'hold')
    provenance={key[0].lower()+key[1:]:value for key,value in receipt['Source'].items()}
    v.require(receipt['definitionId']==definition['id'] and provenance==definition['source'] and receipt['holdFrames']==hold,
              'Capture/definition binding differs')
    v.require(receipt['completionTick']==113+hold and receipt['finalEventExited'] is True and receipt['finalWorkerPhase']=='Completed' and
              receipt['resourceRebuiltAtTick']==5 and receipt['gpuReadbackPerformed'] is True and
              receipt['rendererTaskAcknowledgments']==0 and receipt['originalComparison']=='not performed', 'Incomplete or wider capture claim')
    v.require(receipt['backgroundSha256']==v.sha(background()),'Background differs')
    v.require(len(receipt['captures'])==114+hold, 'Missing/extra accepted frames')
    v.hex_hash(receipt['traceSha256']);v.hex_hash(receipt['finalStateSha256'])
    for tick, entry in enumerate(receipt['captures']):
        phase,kind,revision,_=expected(tick,hold)
        v.require(entry['tick']==tick and entry['file']==f'tick-{tick:03}.png' and entry['phase']==phase and
                  entry['drawKind']==kind and entry['taskRevision']==revision, 'Frame/timing identity differs')
        v.hex_hash(entry['rgbaSha256'])


def geometry(definition, tick, hold):
    _,kind,_,release=expected(tick,hold)
    if kind=='None': return ()
    if kind=='Flash': return (((0,0),(256,0),(0,256),(256,256)),)
    quads=[]
    for tile in reversed(definition['tiles']):
        delay=tile['initialDelay']
        if tick<=55 and tick<=delay: continue
        active=max(0,(tick if tick<=55 else tick-release)-1-delay)
        angle=min(512,active*64) if tick<=55 else max(0,512-active*64)
        radius=min(48,active*4) if tick<=55 else max(0,48-active*4)
        axes=definition['rotations'][angle//64]
        ax,ay,bx,by=(source.trunc_div(axes[k]*radius,16384) for k in ('ax','ay','bx','by'))
        x,y=tile['x'],tile['y']
        quads.append(((x+ax,y+ay),(x+bx,y+by),(x-bx,y-by),(x-ax,y-ay)))
    return tuple(quads)


@lru_cache(maxsize=128)
def pixels(quads): return compose(quads,background())


def audit(prepared, captures, executable, reference, output):
    paths=[source.event.c.profile_tool.scratch_path(p) for p in (prepared,executable,output)]
    prepared,executable,output=paths
    v.require(not output.exists() and 1<=len(captures)<=6,'Fresh output and bounded captures required')
    revision=subprocess.check_output(['git','-C',str(reference),'rev-parse','HEAD'],text=True).strip()
    v.require(revision==source.REVISION and not subprocess.check_output(['git','-C',str(reference),'status','--porcelain'],text=True).strip(),'Reference differs')
    raw=executable.read_bytes();v.require(len(raw)==356352 and v.sha(raw)=='287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562','Wrong original executable')
    sqrt=raw[0x1c0b8:0x1c0b8+384];sincos=raw[0x1c6e8:0x1c6e8+16384]
    tables=source.layout(struct.unpack('<192h',sqrt))
    definition=read_json(prepared/'dark-screen.json',65536)
    manifest=read_json(prepared/'assets.manifest.json',65536)
    # The production loader checks the manifest; the independent audit also binds its sole asset to disk.
    entries=manifest['assets'];v.require(len(entries)==1,'Expected one prepared asset')
    entry=entries[0];data=(prepared/'dark-screen.json').read_bytes()
    v.require(entry['id']==definition['id'] and entry['relativePath']=='dark-screen.json' and entry['byteLength']==len(data) and entry['sha256']==v.sha(data),'Prepared manifest differs')
    v.require(definition['executionProfile']=='authored-dark-screen-preview-v1' and definition['parameters']==dict(reserved=0,mode=1,divisor=12,angleStep=64,radiusStep=4,image=0),'Unsupported definition')
    v.require(definition['source']==dict(kind='verified-retail',referenceRevision=revision,executableSha256=v.sha(raw),
                sqrtSha256=v.sha(sqrt),sinCosSha256=v.sha(sincos)), 'Original math provenance differs')
    v.require(definition['tiles']==[dict(x=t['x'],y=t['y'],initialDelay=t['delay']) for t in tables],'Prepared delays/layout differ from original approximation')
    axes=[]
    for angle in range(0,513,64):
        ay,ax=struct.unpack_from('<hh',sincos,(angle+512)*4);by,bx=struct.unpack_from('<hh',sincos,(angle+1536)*4)
        axes.append(dict(angle=angle,ax=ax,ay=ay,bx=bx,by=by))
    v.require(definition['rotations']==axes,'Prepared rotation samples differ from original table')
    runs=[]
    for capture in captures:
        capture=source.event.c.profile_tool.scratch_path(capture);receipt=read_json(capture/'check.json')
        validate_receipt(receipt,definition)
        v.require(len(receipt['projections'])==117,'Missing C# projection lattice')
        for index,p in enumerate(receipt['projections']):
            angle,radius=index//13*64,index%13*4
            offsets=source.corners(angle,radius,sincos)
            actual=p['quad'];coords=[(actual[k]['X'],actual[k]['Y']) for k in 'ABCD']
            v.require(p['angle']==angle and p['radius']==radius and coords==[(x-12,y-12) for x,y in offsets],'Actual C# fixed-point projection differs')
        inventory=[source.event.c.identity(capture/'check.json')]
        for entry in receipt['captures']:
            path=capture/entry['file'];v.require(path.stat().st_size<=1048576,'Oversized PNG')
            with Image.open(path) as image:
                v.require(image.size==(256,240) and image.mode in ('RGB','RGBA'),'Wrong PNG canvas')
                rgba=image.convert('RGBA').tobytes()
            quads=geometry(definition,entry['tick'],definition['holdFrames'])
            v.require(entry['drawCount']==len(quads) and v.sha(rgba)==entry['rgbaSha256'] and rgba==pixels(quads),f'Independent pixels differ at {entry["tick"]}')
            inventory.append(source.event.c.identity(path))
        v.require({p.name for p in capture.iterdir()}=={Path(i['localPath']).name for i in inventory},'Unexpected capture files')
        runs.append(dict(receipt=source.event.c.identity(capture/'check.json'),frames=len(receipt['captures']),files=inventory,
                         traceSha256=receipt['traceSha256'],finalStateSha256=receipt['finalStateSha256']))
    report=dict(format='fft.dark-screen-preview-audit',schemaVersion=1,referenceRevision=revision,
        executable=source.event.c.identity(executable),prepared=[source.event.c.identity(prepared/p) for p in ('assets.manifest.json','dark-screen.json')],
        checkedDelays=288,checkedProjectionLattice=117,runs=runs,exact=True,coveragePolicy='provisional integer sample/top-left',
        originalPixelComparison=False,emulatorLaunched=False)
    output.mkdir(parents=True);(output/'report.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    return report


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for flag in ('prepared','executable','reference-root','output'):parser.add_argument('--'+flag,type=Path,required=True)
    parser.add_argument('--capture',action='append',type=Path,required=True);args=parser.parse_args()
    try:
        r=audit(args.prepared,args.capture,args.executable,args.reference_root,args.output)
        print(f'Exact: 288 delays, 117 actual C# projections, {sum(x["frames"] for x in r["runs"])} native/CPU/independent-model PNGs; no original pixel claim.')
    except (ValueError,KeyError,OSError,TypeError,IndexError,subprocess.CalledProcessError) as error:parser.exit(2,'Dark-screen audit failed: '+str(error)+'\n')
