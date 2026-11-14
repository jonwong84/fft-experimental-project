import copy,json,unittest
from verify_ordinary_spell_ai_clock import FIXTURE,REVISION,simulate,validate,prepared_case,validate_inspection


class DefaultAbilityAiClockTests(unittest.TestCase):
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

    def test_prepared_timing_and_score_description_has_literal_boundary_checks(self):
        fire=prepared_case(16,'fire','Unit');fourth=prepared_case(19,'elemental','Unit')
        self.assertEqual((fire['clock']['Ticks'],fire['priority'],fire['exitRng']),
                         (13,49,dict(Value=3996037883,DrawCount=6)))
        self.assertEqual((fourth['clock']['Ticks'],fourth['priority'],fourth['formulaEntryRng']['DrawCount']),(19,97,3))
        self.assertEqual([u['Ct'] for u in fourth['clock']['units']],[10,52])

    def report(self):
        rows=[]
        for mode in ['Unit','Tile']:
            row=prepared_case(16,'fire',mode);priority=row.pop('priority');damage=row.pop('damage')
            row['Score']=None if priority is None else dict(Priority=priority,HitPercent=100)
            row['frames']=[dict(targetId='guard',Result=dict(Hit=True,HpDamage=damage,Effects=dict(AddedEffect=False)))]
            rows.append(row)
        return dict(referenceRevision=REVISION,eachCaseRestartsFromSameStateAndRng=True,
                    cExecuted=False,sourceOriginalExecuted=False,cases=rows)

    def test_delayed_validator_detects_phase_wait_refund_rng_and_score_regressions(self):
        report=self.report();self.assertEqual(validate_inspection(report,'fire')['preparedDelayedHypotheses'],2)
        for key in ['clock','formulaEntryRng','exitRng','Score']:
            changed=copy.deepcopy(report)
            if key=='clock':changed['cases'][0][key]['units'][0]['Ct']+=40
            elif key=='Score':changed['cases'][0][key]['Priority']+=1
            else:changed['cases'][0][key]['DrawCount']+=1
            with self.assertRaises(ValueError):validate_inspection(changed,'fire')

    def test_delayed_validator_requires_tile_exclusion_and_retained_draws(self):
        for key,value in [('disposition','Scored'),('Score',dict(Priority=49,HitPercent=100)),('frames',[])]:
            report=self.report();report['cases'][1][key]=value
            with self.assertRaises(ValueError):validate_inspection(report,'fire')

    def test_delayed_validator_rejects_conflating_independent_hypotheses_with_execution(self):
        for key in ['cExecuted','sourceOriginalExecuted','eachCaseRestartsFromSameStateAndRng']:
            report=self.report();report[key]=not report[key]
            with self.assertRaises(ValueError):validate_inspection(report,'fire')
