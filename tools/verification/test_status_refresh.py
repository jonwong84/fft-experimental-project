import copy,json,unittest
import verify_status_refresh as v
class StatusRefreshTests(unittest.TestCase):
    def test_literals(self):v.validate(json.loads(v.FIXTURE.read_text(encoding='utf-8')))
    def test_tampered_critical_boundary_fails(self):
        f=json.loads(v.FIXTURE.read_text(encoding='utf-8'));f['cases'][0]['expected']['current']=[]
        with self.assertRaises(ValueError):v.validate(f)
    def test_replay_leaves_capture_unchanged(self):
        f=json.loads(v.FIXTURE.read_text(encoding='utf-8'))
        for row in f['cases']:
            before=copy.deepcopy(row['input']);self.assertEqual(v.refresh(before),v.refresh(before));self.assertEqual(before,row['input'])
if __name__=='__main__':unittest.main()
