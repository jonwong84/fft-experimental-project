import copy,json,math,unittest
from verify_roster_trajectory_contract import FIXTURE,describe,sqrt12,validate


class RosterTrajectoryContractTests(unittest.TestCase):
    def setUp(self):self.fixture=json.loads(FIXTURE.read_text(encoding='utf-8'))

    def test_fourteen_source_descriptions_preserve_input(self):
        before=copy.deepcopy(self.fixture);self.assertEqual(14,validate(self.fixture)['rosterTrajectoryDescriptions']);self.assertEqual(before,self.fixture)

    def test_strict_height_boundaries_and_sdk_approximation_are_observable(self):
        rows={r['id']:r['expected'] for r in self.fixture['vectors']}
        self.assertTrue(rows['obstacle-height-20-strict-top-boundary']['clear']);self.assertFalse(rows['obstacle-height-21-strict-top-boundary']['clear'])
        self.assertEqual([91,-140,42],rows['ally-center-box-blocks-before-target']['point']);self.assertEqual([104,-140,42],rows['negative-direction-arithmetic-rounding']['point'])
        self.assertEqual(361920,sqrt12(7840*4096));self.assertNotEqual(math.isqrt(7840*4096*4096),sqrt12(7840*4096))

    def test_missing_invalid_metadata_and_tampered_sampling_reject(self):
        d=copy.deepcopy(self.fixture['vectors'][0]['input']);d['units'][2]['height']=256
        with self.assertRaises(ValueError):describe(d)
        for value in (0,-1,0x80000000):
            with self.assertRaises(ValueError):sqrt12(value)
        f=copy.deepcopy(self.fixture);f['vectors'][0]['expected']['sample']-=1
        with self.assertRaises(ValueError):validate(f)


if __name__=='__main__':unittest.main()
