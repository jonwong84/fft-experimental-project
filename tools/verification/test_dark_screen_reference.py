"""Authored reference-inspector/control/receipt negatives; no original media or emulator."""
import json
from pathlib import Path
import struct
import tempfile
import unittest

import inspect_dark_screen_reference as r
import compare_dark_screen_reference as comparison
import test_dark_screen as authored
import test_banner_precision as framing


def tiles():
    return [dict(x=i%24*12-12,y=i//24*24-(24 if i&1 else 12),initialDelay=41 if i==287 else 1 if i==1 else 0) for i in range(288)]


def ram_cells(phase,tick):
    ram=bytearray(r.d.RAM_SIZE)
    for i,cell in enumerate(r.cells(tiles(),phase,tick)):struct.pack_into('<6i',ram,(r.source.TILES&0x1fffffff)+i*72+48,*cell)
    return ram


def state_fixture():
    ram,_,_,_=authored.holding_fixture()
    core=bytearray(framing.core())
    for tag,name,data in ((b'MAIN','MainRAM.data8',bytes(ram)),(b'CPU','authored-registers',b'cpu-state')):
        body=framing.field(name,data);core.extend(tag.ljust(32,b'\0')+struct.pack('<I',len(body))+body)
    struct.pack_into('<I',core,20,len(core));return framing.container(bytes(core))


class DarkScreenReferenceTests(unittest.TestCase):
    def test_complete_entry_arrays_have_one_source_body_ordinal(self):
        for tick in (0,2,6,14,26,42,46,53,54):
            self.assertEqual(tick,r.body_tick(ram_cells('Entering',tick),tiles(),'Entering',int(tick==54)))

    def test_leaving_arrays_keep_delayed_full_shapes_and_match_tail(self):
        for tick in (1,3,7,15,47,49,53,54):
            self.assertEqual(tick,r.body_tick(ram_cells('Leaving',tick),tiles(),'Leaving',int(tick==54)))
        self.assertEqual((38,48,512),r.cells(tiles(),'Leaving',3)[287][:3])

    def test_done_latch_disambiguates_identical_clamped_arrays(self):
        ram=ram_cells('Entering',53)
        self.assertEqual(53,r.body_tick(ram,tiles(),'Entering',0))
        self.assertEqual(54,r.body_tick(ram,tiles(),'Entering',1))
        self.assertRaises(ValueError,r.body_tick,ram,tiles(),'Entering',2)

    def test_partial_or_corrupt_source_body_is_never_rounded_to_a_tick(self):
        ram=ram_cells('Entering',14);struct.pack_into('<i',ram,(r.source.TILES&0x1fffffff)+48,99)
        self.assertRaises(ValueError,r.body_tick,ram,tiles(),'Entering',0)
        self.assertRaises(ValueError,r.body_tick,ram,tiles()[:-1],'Entering',0)

    def test_holding_queue_binds_untextured_material_and_exact_flash(self):
        ram,_,_,word=authored.holding_fixture();result=r.queued(ram,{},'Holding',163)
        self.assertEqual(1,len(result['draws']));self.assertEqual([0xe1000006,0xe2000000],result['mode'])
        word(r.source.MODES+4,0xe1000206)
        self.assertRaises(ValueError,r.queued,ram,{},'Holding',163)

    def test_neutral_control_preserves_geometry_headers_links_counters_and_input(self):
        ram,_,_,_=authored.holding_fixture();before=bytes(ram);at=r.source.FLASH&0x1fffffff
        changed,allowed=r.neutral_ram(ram,[at])
        self.assertEqual(before,bytes(ram));self.assertEqual(before[at:at+4],changed[at:at+4]);self.assertEqual(before[at+7:at+24],changed[at+7:at+24])
        self.assertEqual(bytes(3),changed[at+4:at+7]);self.assertEqual(0xe1000026,r.d.word(changed,r.source.MODES+4));self.assertEqual(4,len(allowed))

    def test_unknown_mixed_bank_or_mixed_family_controls_reject(self):
        ram,_,_,_=authored.holding_fixture();flash=r.source.FLASH&0x1fffffff;tile=r.source.TILES&0x1fffffff
        for addresses in ([],[0],[flash,flash],[flash,flash+24],[flash,tile],[tile,tile+24]):
            self.assertRaises(ValueError,r.neutral_ram,ram,addresses)

    def test_repacked_control_is_only_material_memory_with_exact_other_subsystems(self):
        source=state_fixture();control,evidence=r.control(source,[r.source.FLASH&0x1fffffff])
        result=r.isolation(source,control,evidence['allowedRamOffsets'])
        self.assertTrue(result['passed']);self.assertTrue(result['onlyAllowedRamChanges'])
        self.assertEqual(0,result['outsideAllowedRamChanges']);self.assertFalse(evidence['instructionsChanged']);self.assertFalse(evidence['countersChanged'])
        self.assertFalse(r.isolation(source,control,[])['passed'])

    def test_selected_mode_and_material_must_be_original(self):
        ram,_,_,word=authored.holding_fixture();at=r.source.FLASH&0x1fffffff
        for address,value in ((r.source.MODES+4,0xe1000026),(r.source.MODES+8,0xe2000001),(r.source.FLASH+4,0x2a102831)):
            changed=bytearray(ram);struct.pack_into('<I',changed,address&0x1fffffff,value)
            self.assertRaises(ValueError,r.neutral_ram,changed,[at])

    def test_any_cpu_record_difference_fails_full_cpu_isolation(self):
        source=state_fixture();core=r.p.unpack_state(source)
        # A CPU record is opaque to isolation: no TAR or other register exception.
        for byte in range(len(b'cpu-state')):
            data=bytearray(b'cpu-state');data[byte]^=1
            changed=framing.container(core.replace(b'cpu-state',bytes(data)))
            result=r.isolation(source,changed,[])
            self.assertFalse(result['passed']);self.assertFalse(result['fullCpuExact'])

    def test_material_allowlist_does_not_allow_geometry_or_counters(self):
        source=state_fixture();control,evidence=r.control(source,[r.source.FLASH&0x1fffffff]);core=r.p.unpack_state(control)
        ram=r.p.subsystem_records(core,b'MAIN')['MainRAM.data8']
        for at in ((r.source.FLASH&0x1fffffff)+8,(r.source.TILES&0x1fffffff)+48):
            changed=bytearray(ram);changed[at]^=1
            altered=framing.container(core.replace(ram,bytes(changed)))
            self.assertFalse(r.isolation(source,altered,evidence['allowedRamOffsets'])['passed'])

    def test_control_isolation_rejects_unrelated_cpu_or_gpu_state(self):
        state=state_fixture();control,evidence=r.control(state,[r.source.FLASH&0x1fffffff]);core=r.p.unpack_state(control)
        self.assertFalse(r.isolation(state,framing.container(core.replace(b'cpu-state',b'cpu-fault')),evidence['allowedRamOffsets'])['passed'])

    def test_comparison_requires_exact_pixels_and_confines_control_to_coverage(self):
        bg=comparison.model.background();q=(((0,0),(8,0),(0,8),(8,8)),);original=comparison.model.compose(q,bg)
        self.assertTrue(comparison.compare(original,bg,original,q)['passed'])
        wrong=bytearray(original);wrong[0]^=8
        self.assertFalse(comparison.compare(original,bg,bytes(wrong),q)['passed'])
        wrong=bytearray(original);wrong[(200*256+200)*4]^=8
        self.assertFalse(comparison.compare(bytes(wrong),bg,bytes(wrong),q)['passed'])

    def test_metrics_keep_small_differences_and_bounds_without_tolerance(self):
        a=bytes([8,16,24,255])*256*240;b=bytearray(a);b[-4]=16
        result=comparison.metrics(a,bytes(b));self.assertEqual((1,8,[255,239,255,239]),(result['changedPixels'],result['maxChannelDelta'],result['changedBounds']))

    def test_native_receipt_rejects_other_profiles_before_pixel_loading(self):
        with tempfile.TemporaryDirectory() as name:
            p=Path(name);(p/'check.json').write_text(json.dumps(dict(format='wrong',schemaVersion=1,policy='wrong',renderer='wrong',source={},holdFrames=108,checkpoints=[],originalComparison='pending',gpuReadbackPerformed=False)))
            self.assertRaises(ValueError,comparison.native_frames,p,[],[],{})


if __name__=='__main__':unittest.main()
