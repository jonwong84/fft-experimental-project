"""Authored structural/layer inspector negatives; future combined C# cases are unexecuted."""
import struct
import unittest

import inspect_dark_screen_intro as tool
import test_dark_screen as authored


def event_fixture(event_id=4):
    code = bytes([0x76, 0, 1, 12, 64, 0, 4, 0xe5, 54, 0, 0x78, 8, 60, 0xe5, 56, 0])
    if event_id == 401: code += bytes([0xe5, 4, 0, 0x63, 0xca, 0x19]) + bytes(16)
    code += bytes([0x78, 0, 60, 0xe5, 56, 0, 0x77, 0xe5, 54, 0, 0x1c, 1])
    if event_id == 401: code += bytes([0x63, 0x18])
    code += bytes([0xdb]); raw = bytearray(8192)
    struct.pack_into('<I', raw, 0, 4+len(code)); raw[4:4+len(code)] = code
    lengths = bytearray(244)
    for op, size in ((0x76,6),(0xe5,2),(0x78,2),(0x63,1),(0x19,16),(0x1c,1)): lengths[op] = size
    return raw, lengths


def layer_fixture(kind='General'):
    ram, _, _, write = authored.holding_fixture()
    d = tool.d; flash = tool.dark.FLASH & 0x1fffffff
    write(d.SCRIPT_OT, 0x80056b18)
    # Deliberately choose glyph bank 1 and flash bank 0: parity is not shared ownership.
    addresses = [(d.BANNER_BASE & 0x1fffffff)+d.BANNER_BANK_STRIDE+i*52 for i in range(6)]
    order = addresses if kind == 'General' else list(reversed(addresses))
    write(flash, (5 << 24) | order[0])
    for i, at in enumerate(order):
        write(at, (12 << 24) | (order[i+1] if i < 5 else 0xffffff))
        for k in range(12): write(at+4+k*4, 0)
        write(at+4, 0x3e000000); write(at+4+5*4, (0x26 if kind == 'General' else 6) << 16)
        if kind == 'Ready': write(at+4+2*4, 0x3f9a << 16)
    return ram, write, order


class DarkScreenIntroTests(unittest.TestCase):
    def test_event_four_requires_both_display_and_both_dark_barriers(self):
        raw, sizes = event_fixture(); result = tool.selected_sequence(raw, sizes, 4, 4)
        self.assertEqual((4,11,14,17,20,23,26,27,30,32,33), tuple(result[k] for k in
            ('darkOffset','entryWait','generalOffset','generalWait','readyOffset','readyWait','releaseOffset','releaseWait','speedOffset','endOffset','textOffset')))
        self.assertEqual([], result['cameraGap']); self.assertFalse(result['completePlaybackEstablished'])

    def test_common_event_camera_gap_is_preserved_as_three_separate_commands(self):
        raw, sizes = event_fixture(401); result = tool.selected_sequence(raw, sizes, 4, 401)
        self.assertEqual(['E5','63','19'], [x['opcode'] for x in result['cameraGap']])
        self.assertEqual([2,1,16], [x['operandBytes'] for x in result['cameraGap']])
        self.assertTrue(result['cameraRestore']); self.assertFalse(result['completePlaybackEstablished'])

    def test_wrong_event_or_operand_boundary_does_not_select_a_tail(self):
        raw, sizes = event_fixture()
        self.assertRaises(ValueError, tool.selected_sequence, raw, sizes, 5, 4)
        self.assertRaises(ValueError, tool.selected_sequence, raw, sizes, 4, 402)
        self.assertRaises(ValueError, tool.selected_sequence, raw, sizes, 4, 401)

    def test_dark_parameters_are_the_closed_origin_style(self):
        for at in range(5,11):
            raw, sizes = event_fixture(); raw[at] ^= 1
            self.assertRaises(ValueError, tool.selected_sequence, raw, sizes, 4, 4)

    def test_barrier_classes_and_unsigned_halfword_shape_cannot_change(self):
        for at in (12,13,18,19,24,25,28,29):
            raw, sizes = event_fixture(); raw[at] ^= 1
            self.assertRaises(ValueError, tool.selected_sequence, raw, sizes, 4, 4)

    def test_other_display_condition_or_hold_is_not_this_original_selection(self):
        for at in (15,16,21,22):
            raw, sizes = event_fixture(); raw[at] ^= 1
            self.assertRaises(ValueError, tool.selected_sequence, raw, sizes, 4, 4)

    def test_missing_release_restore_or_event_end_rejects(self):
        for at in (26,30,31,32):
            raw, sizes = event_fixture(); raw[at] = 0xd4
            self.assertRaises(ValueError, tool.selected_sequence, raw, sizes, 4, 4)

    def test_camera_work_cannot_be_erased_from_common_shape(self):
        raw, sizes = event_fixture(401)
        for at in (20,23,25):
            changed=bytearray(raw);changed[at]=0
            self.assertRaises(ValueError, tool.selected_sequence, changed, sizes, 4, 401)

    def test_lengths_and_text_boundary_are_checked(self):
        for op in (0x76,0xe5,0x78,0x1c,0x77,0xdb):
            raw, sizes=event_fixture();sizes[op]+=1
            self.assertRaises(ValueError, tool.selected_sequence, raw, sizes, 4, 4)
        raw,sizes=event_fixture();struct.pack_into('<I',raw,0,8192)
        self.assertRaises(ValueError,tool.selected_sequence,raw,sizes,4,4)

    def test_both_layer_orders_bind_held_flash_before_display_with_independent_banks(self):
        for kind in ('General','Ready'):
            ram,_,_=layer_fixture(kind);before=bytes(ram);result=tool.queued_layers(ram,kind)
            self.assertEqual(['Background','DarkScreen',kind],result['compositionOrder'])
            self.assertEqual((0,1),(result['flashBank'],result['displayBank']))
            self.assertEqual(before,bytes(ram));self.assertFalse(result['combinedDisplayedPixelsCompared'])

    def test_glyph_order_and_unknown_kind_reject(self):
        ram,_,_=layer_fixture('General')
        self.assertRaises(ValueError,tool.queued_layers,ram,'Ready')
        self.assertRaises(ValueError,tool.queued_layers,ram,'Other')

    def test_material_flash_geometry_and_ready_palette_must_match(self):
        for address,value in ((tool.dark.MODES+4,0xe1000206),(tool.dark.FLASH+8,129)):
            ram,write,_=layer_fixture();write(address,value)
            self.assertRaises(ValueError,tool.queued_layers,ram,'General')
        for field,value in ((0,0x3c000000),(5,0x260000),(2,0x3f990000)):
            ram,write,order=layer_fixture('Ready');write(order[0]+4+field*4,value)
            self.assertRaises(ValueError,tool.queued_layers,ram,'Ready')

    def test_missing_or_cyclic_draws_reject(self):
        for target in (0xffffff,tool.dark.FLASH&0x1fffffff):
            ram,write,_=layer_fixture();write(tool.dark.FLASH,(5<<24)|target)
            self.assertRaises(ValueError,tool.queued_layers,ram,'General')
        ram,write,_=layer_fixture();tile=tool.dark.TILES&0x1fffffff
        write(0x80056b18+0x17f*4,tile);write(tile,(5<<24)|(tool.dark.MODES&0x1fffffff))
        self.assertRaises(ValueError,tool.queued_layers,ram,'General')

    def test_script_and_render_ot_must_agree(self):
        ram,write,_=layer_fixture();write(tool.d.SCRIPT_OT,0x80057118)
        self.assertRaises(ValueError,tool.queued_layers,ram,'General')


if __name__ == '__main__': unittest.main()
