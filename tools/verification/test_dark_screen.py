"""Authored inspector/model checks; no original tables or game execution."""
import struct
import unittest

import inspect_dark_screen as tool


def holding_fixture():
    ram=bytearray(tool.event.d.RAM_SIZE)
    def word(at,value):struct.pack_into('<I',ram,at & 0x1fffffff,value)
    base=tool.event.THREADS+2048
    for at,value in ((tool.event.c.THREADS_POINTER,tool.event.THREADS),(base+4,tool.event.BLOCK+795),
                     (base+0x44,0x801cae9c),(base+0x48,1),(base+0x4c,55),(base+0x3c,base+0x300),
                     (tool.event.d.CURRENT_BUFFER,tool.event.d.BUFFER_BASE),
                     (tool.event.d.BUFFER_BASE+0xe5b4,0x80056b18),(0x80056b18+0x17f*4,tool.MODES & 0x1fffffff)):
        word(at,value)
    ram[0x1d0078:0x1d007b]=bytes([48,40,16]);tiles=[];table=bytearray(16384)
    struct.pack_into('<hh',table,1024*4,4096,0);struct.pack_into('<hh',table,2048*4,0,-4096)
    for row in range(12):
        for col in range(24):
            x=col*12-12;y=row*24-(24 if col&1 else 12);tile=dict(x=x,y=y,delay=0);tiles.append(tile)
            at=0x1d8c3c+(row*24+col)*72
            struct.pack_into('<6i',ram,at+48,0,48,512,x,y,0)
            for bank in (0,1):
                poly=at+bank*24;word(poly,0x05ffffff);word(poly+4,0x2a102830)
                struct.pack_into('<8h',ram,poly+8,x+128,y+12,x+116,y,x+140,y,x+128,y-12)
    for bank in (0,1):
        at=0x1d8c0c+bank*24;word(at,0x05ffffff);word(at+4,0x2a102830)
        struct.pack_into('<8h',ram,at+8,128,0,384,0,128,256,384,256)
        at=0x1d71d4+bank*12;word(at,(2<<24)|((tool.FLASH+bank*24)&0x1fffffff))
        word(at+4,0xe1000006);word(at+8,0xe2000000)
    return ram,tiles,table,word


class DarkScreenStudyTests(unittest.TestCase):
    def test_selected_operand_fields_are_unsigned_and_reserved_prefix_is_not_mode(self):
        self.assertEqual(dict(reserved=0,mode=1,divisor=12,angleStep=64,radiusStep=4),tool.parameters(bytes([0,1,12,64,0,4])))

    def test_other_modes_zero_steps_divisor_and_truncation_remain_unsupported(self):
        for raw in (b'',bytes([1,12,64,0,4]),bytes([1,1,12,64,0,4]),bytes([0,0,12,64,0,4]),
                    bytes([0,1,0,64,0,4]),bytes([0,1,12,0,0,4]),bytes([0,1,12,64,0,0]),bytes([0,1,12,64,1,4])):
            self.assertRaises(ValueError,tool.parameters,raw)

    def test_square_root_normalization_uses_authored_table_vectors(self):
        table=[4096]*192
        for normal,factor in ((81,4608),(100,5120),(144,6144),(225,7680)):table[normal-64]=factor
        for value,expected in ((0,0),(1,1),(4,2),(9,3),(25,5),(64,8),(81,9),(100,10),(144,12),(225,15)):
            self.assertEqual(expected,tool.sqrt0(value,table))

    def test_table_envelope_and_invalid_distance_fail_closed(self):
        for value in (-1,127297,True):self.assertRaises(ValueError,tool.sqrt0,value,[4096]*192)
        self.assertRaises(ValueError,tool.sqrt0,1,[4096]*191)
        self.assertRaises(ValueError,tool.sqrt0,1,[-1]*192)

    def test_signed_projection_division_truncates_toward_zero(self):
        for value,expected in ((16383,0),(-16383,0),(16384,1),(-16384,-1),(-32767,-1)):
            self.assertEqual(expected,tool.trunc_div(value,16384))

    def test_authored_cardinal_table_produces_full_diamond_and_degenerate_birth(self):
        table=bytearray(16384)
        struct.pack_into('<hh',table,1024*4,4096,0)
        struct.pack_into('<hh',table,2048*4,0,-4096)
        self.assertEqual([(0,12),(-12,0),(12,0),(0,-12)],tool.corners(512,48,table))
        self.assertEqual([(0,0)]*4,tool.corners(512,0,table))

    def test_projection_does_not_accept_unsupported_lattice_or_coefficient(self):
        table=bytearray(16384)
        for a,r in ((1,4),(64,1),(-64,4),(576,4),(64,52),(True,4)):
            self.assertRaises(ValueError,tool.corners,a,r,table)
        struct.pack_into('<hh',table,512*4,5000,0)
        self.assertRaises(ValueError,tool.corners,0,4,table)

    def test_phase_overshoot_and_extra_body_pass_are_both_preserved(self):
        for delays,expected in (([0],14),([1],15),([0,3,1],17),([41],55),([0,41,1],55)):
            for phase in (0,1):self.assertEqual(expected,tool.phase_scan(delays,phase))

    def test_parent_observes_entering_after_hold_birth_and_completion_after_exit_yield(self):
        result=tool.timing([0,41],100)
        self.assertEqual(dict(holdingFirstYield=55,firstFlashAndEntryBarrierObservation=56,releaseTick=100,
                              lastReleaseBody=155,taskClear=156,eventBarrierObservation=157),result)

    def test_release_cannot_be_modeled_before_first_hold_resume(self):
        for tick in (0,54,55):self.assertRaises(ValueError,tool.timing,[0,41],tick)
        self.assertEqual(56,tool.timing([0,41],56)['releaseTick'])

    def test_delay_shape_and_phase_domain_are_bounded(self):
        for delays in ([],[0]*289,[-1],[42],[True]):
            self.assertRaises(ValueError,tool.phase_scan,delays,0)
            self.assertRaises(ValueError,tool.timing,delays,100)
        self.assertRaises(ValueError,tool.phase_scan,[0],2)

    def test_models_leave_input_delays_and_tables_unchanged(self):
        delays=[0,41,3];table=[4096]*192
        self.assertEqual(tool.phase_scan(delays,0),tool.phase_scan(delays,1));tool.sqrt0(16,table)
        self.assertEqual([0,41,3],delays);self.assertEqual([4096]*192,table)

    def test_authored_holding_packets_have_no_texture_and_keep_both_banks(self):
        ram,tiles,table,_=holding_fixture();result=tool.inspect_hold(ram,tiles,table)
        self.assertEqual((288,576,2,0),tuple(result[k] for k in ('checkedTiles','checkedTilePackets','checkedFlashBanks','submittedFlashBank')))
        self.assertFalse(result['texturedPackets']);self.assertFalse(result['enterOrReleaseObserved'])

    def test_holding_context_counters_material_and_packet_corruption_reject(self):
        for kind in ('task','pc','stack','delay','texture','mode','flash'):
            ram,tiles,table,word=holding_fixture();base=tool.event.THREADS+2048
            if kind=='task':word(base+0x4c,54)
            if kind=='pc':word(base+0x44,0)
            if kind=='stack':word(base+0x3c,base+1024)
            if kind=='delay':word(tool.TILES+68,1)
            if kind=='texture':word(tool.TILES+4,0x2e102830)
            if kind=='mode':word(tool.MODES+4,0xe1000206)
            if kind=='flash':word(tool.FLASH+4,0x2a000000)
            self.assertRaises(ValueError,tool.inspect_hold,ram,tiles,table)


if __name__=='__main__':unittest.main()
