"""Reconstruct the frozen checkpoint's RAM ordering table; not a live GPU trace."""
import struct

import analyze_banner_precision as p
import compare_visual_checkpoints as v

RAM_SIZE = 2 * 1024 * 1024
BUFFER_BASE, BUFFER_STRIDE = 0x800fc55c, 60968
CURRENT_BUFFER, SCRIPT_OT = 0x8011a2d4, 0x80173f44
BANNER_BASE, BANNER_BANK_STRIDE = 0x8004c6c8, 2080
BURST_MODE_BASE = 0x801d71d4


def word(ram, address):
    # Accept only RAM's ordinary physical/KSEG aliases, never mirror arbitrary pointers.
    v.require(type(address) is int and (0 <= address < RAM_SIZE or 0x80000000 <= address < 0x80200000 or
                                       0xa0000000 <= address < 0xa0200000), 'Non-RAM DMA address')
    offset = address & 0x1fffffff
    v.require(offset % 4 == 0 and offset + 4 <= len(ram), 'Unaligned/out-of-range RAM read')
    return struct.unpack_from('<I', ram, offset)[0]


def walk(ram, root):
    v.require(len(ram) == RAM_SIZE, 'Expected 2 MiB main RAM')
    address, seen, packets = root, set(), []
    while address != 0xffffff:
        physical = address & 0x1fffffff
        v.require(physical not in seen and len(seen) < 8192, 'Cyclic/oversized DMA chain')
        seen.add(physical)
        tag = word(ram, address); length = tag >> 24
        words = [word(ram, physical + 4 + i * 4) for i in range(length)]
        if words: packets.append((physical, words))
        address = tag & 0xffffff
    return packets, len(seen)


def trace(state, definition, geometry=None):
    if geometry is not None:
        v.require(type(geometry) is list and len(geometry) == 6, 'Expected six explicit submitted crops')
        for index, item in enumerate(geometry):
            v.fields(item, 'PieceIndex CropTop VisibleRows', 'submitted crop')
            v.require(type(item['PieceIndex']) is int and item['PieceIndex'] == index, 'Unexpected submitted piece order')
            v.integer(item['CropTop'], 0, definition['pieces'][index]['height'], 'submitted crop top')
            v.integer(item['VisibleRows'], 0, definition['pieces'][index]['height'], 'submitted visible rows')
            v.require(item['CropTop'] + item['VisibleRows'] == definition['pieces'][index]['height'], 'Invalid submitted crop')
    core = p.unpack_state(state)
    records = p.subsystem_records(core, b'MAIN')
    ram = records.get('MainRAM.data8', b'')
    v.require(len(ram) == RAM_SIZE, 'Missing main RAM record')
    current = word(ram, CURRENT_BUFFER)
    v.require(current in (BUFFER_BASE, BUFFER_BASE + BUFFER_STRIDE), 'Unexpected render-buffer pointer')
    ot = word(ram, current + 0xe5b4)
    v.require(ot in (0x80056b18, 0x80057118), 'Unexpected battle OT pointer')
    packets, nodes = walk(ram, ot + 0x17f * 4)
    script_ot = word(ram, SCRIPT_OT)
    v.require(script_ot == ot, 'Frozen script and render OT disagree')
    mode, banners = None, []
    for address, data in packets:
        op = data[0] >> 24
        if op == 0xe1:
            v.require(len(data) == 2 and (data[1] == 0 or data[1] >> 24 == 0xe2), 'Unsupported draw-mode packet')
            mode = dict(address=address, command=data[0], ditherEnabled=bool(data[0] & 0x200))
        elif op == 0x3e and any((address - (BANNER_BASE & 0xffffff) - bank * BANNER_BANK_STRIDE) in range(0, 6 * 52, 52) for bank in (0, 1)):
            v.require(mode is not None and len(data) == 12, 'Banner lacks preceding known draw mode')
            delta = address - (BANNER_BASE & 0xffffff)
            bank, index = delta // BANNER_BANK_STRIDE, delta % BANNER_BANK_STRIDE // 52
            piece = definition['pieces'][index]
            crop = 0 if geometry is None else geometry[index]['CropTop']
            rows = piece['height'] - crop
            # Bind all vertices, including cropped/degenerate submissions when explicitly selected.
            for vertex, (dx, dy) in enumerate(((0, 0), (piece['width'], 0), (0, rows), (piece['width'], rows))):
                rgb, xy, uv = data[vertex * 3:vertex * 3 + 3]
                color = piece['colors'][vertex]
                v.require(rgb & 0xffffff == color['r'] | color['g'] << 8 | color['b'] << 16, 'Banner vertex color mismatch')
                x, y = struct.unpack('<hh', struct.pack('<I', xy))
                v.require((x, y) == (piece['x'] + 128 + dx, piece['y'] + crop + dy), 'Banner geometry mismatch')
                v.require(uv & 0xffff == (piece['u'] + dx) | ((piece['v'] + crop + dy) & 255) << 8, 'Banner UV mismatch')
            tpage = data[5] >> 16
            v.require(tpage == (0x26 if piece['material'] == 'add' else 0x46), 'Banner material mismatch')
            banners.append(dict(pieceIndex=index, packetAddress=address, bank=bank, precedingMode=dict(mode)))
        # Only fixed-size polygon packets are known not to hide another command.
        # Unknown payloads invalidate tracked state until another validated E1.
        if op != 0xe1 and mode is not None:
            vertices = 4 if op & 8 else 3
            polygon_words = 1 + vertices * (1 + bool(op & 4)) + ((vertices - 1) if op & 16 else 0)
            if not (0x20 <= op <= 0x3f and len(data) == polygon_words): mode = None
    v.require([x['pieceIndex'] for x in banners] == list(range(6)), 'Expected exactly six ordered banner draws')
    v.require(len({x['bank'] for x in banners}) == 1, 'Mixed banner banks')
    v.require(all(x['precedingMode']['address'] in ((BURST_MODE_BASE & 0xffffff), (BURST_MODE_BASE & 0xffffff) + 12) and
                  x['precedingMode']['command'] == 0xe1000006 for x in banners), 'Unexpected banner-time draw mode')
    gpu = p.gpu_records(core)
    v.require(gpu['dtd'] == b'\0', 'Frozen GPU flag differs from reconstructed banner state')
    return dict(format='fft.banner-draw-state-trace', schemaVersion=1, sourceRevision=p.b.REVISION,
        evidence='frozen RAM DMA-chain reconstruction; not live command execution',
        renderBuffer=current, otag=ot, root=ot + 0x17f * 4, dmaNodes=nodes, nonemptyPackets=len(packets),
        mainRamSha256=v.sha(ram), banners=banners,
        writerSymbol='g_conditions_burst_draw_modes[1]' if banners[0]['precedingMode']['address'] & 0xffffff == (BURST_MODE_BASE & 0xffffff) + 12 else 'g_conditions_burst_draw_modes[0]',
        initializer='src/event/require/entry_dark_screen.c::require_dark_screen_entrypoint',
        enqueue='gfx_append_gpu_primitive_to_secondary_otag; reverse head insertion',
        universalBannerDitherDefaultEstablished=False, originalEvent401Verified=False)
