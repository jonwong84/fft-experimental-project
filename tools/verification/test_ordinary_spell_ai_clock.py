import copy,json,unittest
from verify_ordinary_spell_ai_clock import FIXTURE,simulate,validate


class OrdinarySpellAiClockTests(unittest.TestCase):
    def test_all_source_clock_descriptions(self):
        self.assertEqual(validate(json.loads(FIXTURE.read_text(encoding='utf-8')))['clockDescriptions'],10)

    def test_phase_and_refund_changes_are_detected(self):
        fixture=json.loads(FIXTURE.read_text(encoding='utf-8'))
        for field,value in [('ticks',14),('cts',[90,44]),('upkeepDraws',0),('eventCount',1)]:
            changed=copy.deepcopy(fixture);changed['cases'][0]['expected'][field]=value
            with self.assertRaises(ValueError):validate(changed)

    def test_simulated_turn_does_not_get_player_wait_refund(self):
        f=json.loads(FIXTURE.read_text(encoding='utf-8'));row=f['cases'][1]
        self.assertEqual(simulate(row['input'])['cts'],[10,52])
        self.assertEqual(row['input']['units'][0]['ct'],0)

    def test_cap_counts_events_not_charge_ticks(self):
        f=json.loads(FIXTURE.read_text(encoding='utf-8'));r=simulate(f['cases'][-1]['input'])
        self.assertEqual((r['eventCount'],r['remaining'],r['ticks']),(255,102,35))
        self.assertTrue(r['limit'])
