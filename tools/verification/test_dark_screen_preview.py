"""Authored independent scanline, timeline and receipt negatives; no original media."""
import unittest
import copy
import verify_dark_screen_preview as tool


class DarkScreenPreviewTests(unittest.TestCase):
    def test_rectangle_shared_diagonal_is_single_coverage(self):
        rows=list(tool.triangle_rows(((0,0),(8,0),(0,8))))+list(tool.triangle_rows(((8,0),(0,8),(8,8))))
        covered=[(x,y) for y,left,right in rows for x in range(left,right+1)]
        self.assertEqual(64,len(covered));self.assertEqual(64,len(set(covered)))

    def test_negative_clipping_and_reversed_winding_are_identical(self):
        a=tuple(tool.triangle_rows(((-8,-8),(8,-8),(-8,8))))
        b=tuple(tool.triangle_rows(((-8,-8),(-8,8),(8,-8))))
        self.assertEqual(a,b)
        self.assertTrue(all(0<=y<240 and 0<=left<=right<256 for y,left,right in a))

    def test_degenerate_has_no_coverage(self):
        self.assertEqual([],list(tool.triangle_rows(((1,1),(1,1),(1,1)))))

    def test_overlap_blends_twice_and_does_not_union(self):
        q=((0,0),(8,0),(0,8),(8,8));bg=tool.background()
        a=tool.compose((q,),bg);b=tool.compose((q,q),bg)
        self.assertNotEqual(a,b);self.assertEqual(((a[0]//8+6)//2)*8,b[0])
        self.assertEqual(bg[8*4],a[8*4])

    def test_entry_hold_release_and_exit_boundaries(self):
        self.assertEqual(('Holding','Tiles',1,100),tool.expected(55,44))
        self.assertEqual(('Holding','Flash',1,100),tool.expected(56,44))
        self.assertEqual(('Leaving','Flash',2,100),tool.expected(100,44))
        self.assertEqual(('Leaving','Tiles',2,100),tool.expected(155,44))
        self.assertEqual(('Completed','None',3,100),tool.expected(156,44))

    def test_invalid_timing_bounds_reject(self):
        for tick,hold in ((-1,44),(158,44),(0,256),(True,0)):self.assertRaises(ValueError,tool.expected,tick,hold)

    def test_body_model_uses_pre_update_counters_and_reverse_order(self):
        d=dict(tiles=[dict(x=0,y=0,initialDelay=0),dict(x=24,y=0,initialDelay=1)],
               rotations=[dict(ax=0,ay=4096,bx=-4096,by=0)]*9)
        self.assertEqual((((0,0),)*4,),tool.geometry(d,1,44))
        quads=tool.geometry(d,2,44);self.assertEqual(((24,0),)*4,quads[0])
        self.assertEqual((0,1),quads[1][0])

    def test_receipt_rejects_wrong_profile_before_other_fields(self):
        self.assertRaises(ValueError,tool.validate_receipt,dict(format='wrong',schemaVersion=1),{})

    @staticmethod
    def receipt():
        definition=dict(holdFrames=0,id='sample:effect/test',source=dict(kind='authored-test'))
        captures=[]
        for tick in range(114):
            phase,kind,revision,_=tool.expected(tick,0)
            captures.append(dict(tick=tick,file=f'tick-{tick:03}.png',phase=phase,drawKind=kind,taskRevision=revision,rgbaSha256='0'*64))
        return definition,dict(format='fft.dark-screen-capture',schemaVersion=1,profile='authored-dark-screen-preview-v1',
            policy='untextured-rgb5-flat-quad-v1',definitionId=definition['id'],Source=dict(Kind='authored-test'),holdFrames=0,
            completionTick=113,finalEventExited=True,finalWorkerPhase='Completed',resourceRebuiltAtTick=5,
            gpuReadbackPerformed=True,rendererTaskAcknowledgments=0,originalComparison='not performed',
            backgroundSha256=tool.v.sha(tool.background()),captures=captures,traceSha256='0'*64,finalStateSha256='0'*64)

    def test_complete_authored_receipt_is_admitted(self):
        definition,receipt=self.receipt();tool.validate_receipt(receipt,definition)

    def test_wider_receipt_claims_or_bad_bindings_reject(self):
        definition,receipt=self.receipt()
        for key,value in (('definitionId','wrong'),('holdFrames',44),('gpuReadbackPerformed',False),
                          ('rendererTaskAcknowledgments',1),('originalComparison','exact'),('completionTick',112),('resourceRebuiltAtTick',4)):
            candidate=copy.deepcopy(receipt);candidate[key]=value
            self.assertRaises(ValueError,tool.validate_receipt,candidate,definition)

    def test_missing_frames_bad_file_names_and_phase_relabels_reject(self):
        definition,receipt=self.receipt()
        for kind in ('missing','file','phase','revision'):
            candidate=copy.deepcopy(receipt)
            if kind=='missing':candidate['captures'].pop()
            elif kind=='file':candidate['captures'][0]['file']='../outside.png'
            elif kind=='phase':candidate['captures'][55]['phase']='Entering'
            else:candidate['captures'][56]['taskRevision']=1
            self.assertRaises(ValueError,tool.validate_receipt,candidate,definition)


if __name__=='__main__':unittest.main()
