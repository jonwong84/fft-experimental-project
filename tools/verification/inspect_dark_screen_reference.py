"""Closed original DarkScreen queued-frame inspector and material-only RAM control.

No emulator runs here. All original bytes, states and generated reports remain under scratch.
The control retains geometry and makes only the selected flat material additive black.
"""
import struct
import zlib

import inspect_dark_screen as source
import compare_banner_hide as frame
import compare_banner_background as bg
import analyze_banner_precision as p

v = source.event.v
d = source.event.d


def cells(tiles, phase, tick):
    v.require(phase in ('Entering','Leaving'), 'Unsupported body phase')
    v.integer(tick,0,54,'body resumes')
    result=[]
    for tile in tiles:
        active=max(0,tick-tile['initialDelay'])
        angle=min(512,active*64) if phase=='Entering' else max(0,512-active*64)
        radius=min(48,active*4) if phase=='Entering' else max(0,48-active*4)
        result.append((max(0,tile['initialDelay']-tick),radius,angle,tile['x'],tile['y'],tile['initialDelay']))
    return result


def body_tick(ram, tiles, phase, done):
    v.require(len(tiles)==288 and done in (0,1),'Invalid selected shape/done latch')
    actual=[struct.unpack_from('<6i',ram,(source.TILES&0x1fffffff)+i*72+48) for i in range(288)]
    matches=[tick for tick in range(55) if done==int(tick==54) and actual==cells(tiles,phase,tick)]
    v.require(len(matches)==1,'No unique complete source-body counter match')
    return matches[0]


def queued(ram, definition, phase, tick):
    current=d.word(ram,d.CURRENT_BUFFER)
    v.require(current in (d.BUFFER_BASE,d.BUFFER_BASE+d.BUFFER_STRIDE),'Render buffer differs')
    ot=d.word(ram,current+0xe5b4);v.require(ot in (0x80056b18,0x80057118),'OT differs')
    chain,nodes=d.walk(ram,ot+0x17f*4)
    tile_addresses={(source.TILES&0x1fffffff)+i*72+bank*24:(i,bank) for i in range(288) for bank in (0,1)}
    flash_addresses={(source.FLASH&0x1fffffff)+bank*24:bank for bank in (0,1)}
    selected=[(pos,at,words) for pos,(at,words) in enumerate(chain) if at in tile_addresses or at in flash_addresses]
    v.require(selected,'No selected DarkScreen packets')
    bank=(flash_addresses[selected[0][1]] if phase=='Holding' else tile_addresses[selected[0][1]][1])
    first=selected[0][0]
    v.require(first>0 and chain[first-1]==((source.MODES&0x1fffffff)+bank*12,[0xe1000006,0xe2000000]),'Missing selected material before burst')
    v.require([x[0] for x in selected]==list(range(first,first+len(selected))),'Interleaved burst packets')
    if phase=='Holding':
        expected=[(None,[(128,0),(384,0),(128,256),(384,256)])]
        v.require(len(selected)==1 and selected[0][1] in flash_addresses,'Expected one flash')
    else:
        expected=[]
        for i in reversed(range(288)):
            tile=definition['tiles'][i];delay=tile['initialDelay']
            if phase=='Entering' and tick<=delay:continue
            active=max(0,tick-1-delay)
            angle=min(512,active*64) if phase=='Entering' else max(0,512-active*64)
            radius=min(48,active*4) if phase=='Entering' else max(0,48-active*4)
            axes=definition['rotations'][angle//64]
            ax,ay,bx,by=(source.trunc_div(axes[k]*radius,16384) for k in ('ax','ay','bx','by'))
            x,y=tile['x']+128,tile['y']
            expected.append((i,[(x+ax,y+ay),(x+bx,y+by),(x-bx,y-by),(x-ax,y-ay)]))
        v.require(len(expected)==len(selected),'Wrong submitted burst count')
    packets=[]
    for (pos,at,words),(index,xy) in zip(selected,expected,strict=True):
        v.require(len(words)==5 and words[0]==0x2a102830,'Flat untextured RGB packet differs')
        actual=[struct.unpack('<hh',struct.pack('<I',word)) for word in words[1:]]
        v.require(actual==xy,'Submitted pre-update geometry differs')
        if phase!='Holding':v.require(tile_addresses.get(at)==(index,bank),'Burst order/bank differs')
        packets.append(dict(address=at,tileIndex=index,xy=xy))
    return dict(bank=bank,draws=packets,dmaNodes=nodes,renderBuffer=current,otag=ot,mode=[0xe1000006,0xe2000000])


def snapshot(state, definition, archive, functions, *, birth_frame=8909, release_frame=9073):
    core=p.unpack_state(state);ram,lengths=source.event.read_ram(state)
    for f in functions:
        at=f['address']&0x1fffffff
        v.require(v.sha(ram[at:at+f['bytes']])==f['sha256'],'Resident original code differs: '+f['name'])
    for at,size,sha in frame.CODE:
        start=at&0x1fffffff;v.require(v.sha(ram[start:start+size])==sha,'Frame/VSync code differs')
    event=source.event;word=d.word;parent=event.THREADS+1024;base=event.THREADS+2048
    v.require(word(ram,event.c.THREADS_POINTER)==event.THREADS and word(ram,base+4)==event.BLOCK+795 and word(ram,base+0x48)==1,'Worker identity differs')
    raw=archive[4*8192:5*8192];live=ram[event.BLOCK&0x1fffffff:(event.BLOCK&0x1fffffff)+8192]
    v.require(word(ram,event.VARIABLES+0x27*4)==word(ram,0x801696fc)==4 and live==b'\xf2'*4+raw[4:] and
        [i for i in range(500) if archive[i*8192+4:(i+1)*8192]==live[4:]]==[4],'Unique original event-4 payload differs')
    records=event.instructions(raw,lengths)[1]
    v.require((794,0x76,source.SELECTED) in records and (816,0x77,b'') in records and (817,0xe5,b'\x36\0') in records,'Original admission/release instructions differ')
    source_frame=word(ram,frame.FRAME_COUNTER);task=word(ram,base+0x4c);pc=word(ram,base+0x44);sp=word(ram,base+0x3c)
    v.require(sp==0x8016a3b4 and [word(ram,sp+x) for x in (0x20,0x28,0x30,0x38)]==[41,12,64,4],'Worker selected stack differs')
    v.require(ram[source.COLOR&0x1fffffff:(source.COLOR&0x1fffffff)+3]==bytes([48,40,16]),'Shared RGB differs')
    wait=word(ram,parent+0x40)
    v.require([word(ram,parent+x) for x in (0x20,0x44,0x48,0x4c)]==[0xe5,0x8014597c,1,68],'Parent event wait differs')
    if task==55:
        v.require(pc==0x801cae9c and wait==813 and word(ram,parent+0x28)==56,'Holding parent/worker differs')
        phase='Holding';tick=source_frame-birth_frame
        v.require(tick>=56 and cells(definition['tiles'],'Entering',54)==[struct.unpack_from('<6i',ram,(source.TILES&0x1fffffff)+i*72+48) for i in range(288)],'Holding counters differ')
    else:
        v.require(task==54 and pc==0x801ca9d4 and wait in (801,817) and word(ram,parent+0x28)==54,'Unsupported transition/caller')
        phase='Entering' if wait==801 else 'Leaving';body=body_tick(ram,definition['tiles'],phase,word(ram,sp+0x40))
        v.require(source_frame==(birth_frame if phase=='Entering' else release_frame)+body,'Body/source-frame epoch differs')
        tick=body if phase=='Entering' else release_frame-birth_frame+body
    v.require(word(ram,base+0x1c)==source_frame-birth_frame,'Saved animation ordinal differs')
    task_info=dict(phase=phase,logicalTick=tick,draws=[],sourceFrame=source_frame,workerSlot=2,parentOffset=wait)
    task_info.update(queued(ram,definition,phase,body if phase!='Holding' else tick))
    cpu=p.subsystem_records(core,b'CPU');ra=struct.unpack('<32I',cpu['GPR'])[31]
    gpu=p.gpu_records(core)
    for key in ('MaskSetOR','MaskEvalAND','tww','twh','twx','twy','TexDisable'):
        v.require(not any(gpu[key]),'Unsupported selected GPU state: '+key)
    context=frame.context(core,ram,task_info,first_vsync_wait=ra==0x8001dc40)
    context.update(epoch=v.sha(state),eventId=4)
    return context


def neutral_ram(ram, addresses):
    v.require(len(ram)==d.RAM_SIZE and 1<=len(addresses)<=288 and len(set(addresses))==len(addresses),'Invalid bounded packet selection')
    tiles={(source.TILES&0x1fffffff)+i*72+b*24:b for i in range(288) for b in (0,1)}
    flashes={(source.FLASH&0x1fffffff)+b*24:b for b in (0,1)}
    v.require(all(at in tiles or at in flashes for at in addresses),'Unknown selected packet')
    groups={('flash',flashes[at]) if at in flashes else ('tiles',tiles[at]) for at in addresses}
    v.require(len(groups)==1 and (next(iter(groups))[0]!='flash' or len(addresses)==1),'Mixed banks or packet families')
    bank=next(iter(groups))[1];mode=(source.MODES&0x1fffffff)+bank*12+4
    v.require(d.word(ram,mode)==0xe1000006 and d.word(ram,mode+4)==0xe2000000,'Wrong selected mode')
    v.require(all(d.word(ram,at)>>24==5 and d.word(ram,at+4)==0x2a102830 for at in addresses),'Wrong selected flat material')
    changed=bytearray(ram);offsets=[]
    for at in addresses:
        changed[at+4:at+7]=bytes(3);offsets.extend(range(at+4,at+7))
    changed[mode]=0x26;offsets.append(mode)
    v.require(all(a==b or i in offsets for i,(a,b) in enumerate(zip(ram,changed,strict=True))),'Control escaped material bytes')
    return bytes(changed),offsets


def control(state, addresses):
    core=p.unpack_state(state);ram=p.subsystem_records(core,b'MAIN')['MainRAM.data8'];changed,offsets=neutral_ram(ram,addresses)
    v.require(core.count(ram)==1,'Ambiguous RAM record')
    at=core.index(ram);newcore=core[:at]+changed+core[at+len(ram):]
    raw=bytearray();pos=20
    while pos<len(state):
        size=struct.unpack_from('<I',state,pos)[0];pos+=4;raw.extend(zlib.decompress(state[pos:pos+size]));pos+=size
    v.require(raw.count(core)==1,'Ambiguous MEM block')
    at=raw.index(core);raw[at:at+len(core)]=newcore
    result=bytearray(b'#RZIPv\x01#'+struct.pack('<IQ',131072,len(raw)))
    for at in range(0,len(raw),131072):
        block=zlib.compress(raw[at:at+131072]);result.extend(struct.pack('<I',len(block)));result.extend(block)
    result=bytes(result);v.require(p.unpack_state(result)==newcore,'Control round trip differs')
    return result,dict(kind='selected flat RGB zero and selected E1 additive; geometry unchanged',packetAddresses=addresses,allowedRamOffsets=offsets,
        changedRamBytes=sum(a!=b for a,b in zip(ram,changed,strict=True)),instructionsChanged=False,countersChanged=False,linksChanged=False,geometryChanged=False)


def isolation(left_state,right_state,allowed):
    left=p.unpack_state(left_state);right=p.unpack_state(right_state)
    lram=p.subsystem_records(left,b'MAIN')['MainRAM.data8'];rram=p.subsystem_records(right,b'MAIN')['MainRAM.data8'];allowed=set(allowed)
    changes=[i for i,(a,b) in enumerate(zip(lram,rram,strict=True)) if a!=b]
    lsections,rsections=bg.section_hashes(left),bg.section_hashes(right)
    v.require(set(lsections)==set(rsections),'Subsystem sets differ')
    sections={name:lsections[name]==rsections[name] for name in lsections if name not in ('MAIN','GPU')}
    lmain,rmain=(p.subsystem_records(c,b'MAIN') for c in (left,right))
    v.require(set(lmain)==set(rmain),'MAIN record sets differ')
    main={name:lmain[name]==rmain[name] for name in lmain if name!='MainRAM.data8'}
    lgpu,rgpu=p.gpu_records(left),p.gpu_records(right)
    v.require(set(lgpu)==set(rgpu),'GPU record sets differ')
    registers={name:lgpu[name]==rgpu[name] for name in lgpu if name!='&GPURAM[0][0]'}
    return dict(onlyAllowedRamChanges=all(i in allowed for i in changes),changedRamBytes=len(changes),outsideAllowedRamChanges=sum(i not in allowed for i in changes),
        otherMainRecords=main,nonMainGpuSubsystems=sections,gpuRegisters=registers,
        fullCpuExact=sections.get('CPU',False),
        passed=all(i in allowed for i in changes) and all(main.values()) and all(sections.values()) and all(registers.values()))
