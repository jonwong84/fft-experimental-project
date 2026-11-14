import copy,json,unittest
from verify_roster_stone_entry_contract import FIXTURE,describe,validate


class RosterStoneEntryContractTests(unittest.TestCase):
    def setUp(self):self.fixture=json.loads(FIXTURE.read_text(encoding='utf-8'))

    def test_eleven_composed_source_tapes_preserve_input(self):
        before=copy.deepcopy(self.fixture);self.assertEqual(11,validate(self.fixture)['rosterStoneEntryDescriptions']);self.assertEqual(before,self.fixture)

    def test_blocked_retry_zero_consumption_and_restored_ko_obstacle(self):
        rows={r['id']:r['expected'] for r in self.fixture['vectors']}
        retry=rows['blocked-first-origin-retries-aim-without-draws'];self.assertEqual([False,True],[v['clear'] for v in retry['visits']])
        self.assertEqual(11,retry['visits'][0]['count']);self.assertEqual(16,retry['visits'][1]['count'])
        self.assertIsNone(rows['blocked-then-zero-accuracy-consumes-aim']['best']);self.assertEqual(0,rows['blocked-then-zero-accuracy-consumes-aim']['remainingAims'])
        ko=rows['projected-ko-does-not-remove-next-trajectory-blocker'];self.assertEqual([True,False],[v['clear'] for v in ko['visits']]);self.assertEqual(1,ko['remainingAims'])

    def test_unknown_key_rollback_duplicate_isolation_and_tampered_result(self):
        rows={r['id']:r for r in self.fixture['vectors']}
        unknown=rows['unknown-actor-origin-priority-rolls-back-prefix']['expected'];self.assertEqual(('UnknownPriority',1,7),(unknown['rejection'],unknown['word'],unknown['count']))
        self.assertEqual(rows['actor-origin-one-clear-evaluated-aim']['expected'],rows['selected-duplicate-does-not-run-previous-occurrence']['expected'])
        f=copy.deepcopy(self.fixture);f['vectors'][0]['expected']['count']+=1
        with self.assertRaises(ValueError):validate(f)
        d=copy.deepcopy(self.fixture['vectors'][0]['input']);d['occurrence']=0
        with self.assertRaises(ValueError):describe(d)


if __name__=='__main__':unittest.main()
