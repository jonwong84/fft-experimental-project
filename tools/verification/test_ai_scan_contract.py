"""ZOD-130 source/managed-design descriptions; never an executed managed scan or original oracle."""
import copy
import json
from pathlib import Path
import unittest
from unittest.mock import patch

from verify_ai_scan_contract import (FIXTURE,EVIDENCE,MAX,CERTIFICATES,audit,calculate,proposed_scan,
    source_policy,source_masks,source_resume,source_roster_bound,validate,verify_retail)
from verify_ai_score_contract import proposed_scoring
from verify_ai_decision_contract import draws

ROOT=Path(__file__).resolve().parents[2]


class AiScanContractTests(unittest.TestCase):
    def contract(self):return json.loads((ROOT/FIXTURE).read_text(encoding='utf-8'))
    def row(self,name):return next(r for r in self.contract()['vectors'] if r['id']==name)
    def data(self,name='scan-four-origins'):return self.row(name)['inputs']
    def result(self,name):return self.row(name)['expected']

    def test_inventory_and_preserved_managed_baseline_audit(self):
        self.assertEqual(dict(vectors=56,families=5,implementationCasesUnexecuted=16),validate(self.contract()))
        r=audit(ROOT,check_managed_baseline=True)
        self.assertEqual((119,33,0,0),(r['preservedFiles'],r['managedBaselineFilesChecked'],r['referenceFilesChecked'],r['retailRecordsChecked']))
        for k in ('productionImplemented','fullAiExecuted','originalExecuted'):self.assertFalse(r[k])

    def test_corrupt_classification_expected_identity_and_execution_claims_are_rejected(self):
        for kind in ('classification','duplicate','expected','identity','implemented','case'):
            c=self.contract()
            if kind=='classification':c['vectors'][0]['expectationKind']='original-observation'
            if kind=='duplicate':c['vectors'][1]['id']=c['vectors'][0]['id']
            if kind=='expected':c['vectors'][0]['expected']['usage']=0
            if kind=='identity':c['ticket']='ZOD-131'
            if kind=='implemented':c['productionImplemented']=True
            if kind=='case':c['implementationCases'][0]['status']='passed'
            with self.assertRaises(ValueError):validate(c)

    def test_weapon_summary_and_runtime_usage_produce_exhaustive_tile_attack(self):
        r=self.result('ordinary-sword-policy')
        self.assertEqual(dict(usage=0x84,consideredFlags=0x84010282,exhaustive=True,followTarget=False,usable=True,additionalDraws=0),r)
        self.assertEqual(r,self.result('initial-low-bits-cleared'))
        self.assertFalse(self.result('not-usable-source-skip')['usable'])
        self.assertEqual(0x83,self.result('nonevadeable-forced-consume')['usage'])
        for name in ('direct-trajectory-not-this-scan','arc-trajectory-not-this-scan'):
            self.assertFalse(self.result(name)['exhaustive'])
        self.assertTrue(self.result('shape-forces-exhaustive')['exhaustive'])

    def test_secondary_attack_range_zero_does_not_replace_weapon_range_one(self):
        e=json.loads((ROOT/EVIDENCE).read_text(encoding='utf-8'))
        secondary=next(r for r in e['retailRecords'] if r['symbol']=='g_ability_range_data[0]')
        self.assertEqual(dict(staticRange=0,staticAoe=0,secondaryEvadeable=True,cannotHitCaster=False,canTargetEnemies=False),secondary['facts'])
        self.assertEqual(1,self.data('ordinary-sword-policy')['range'])
        self.assertEqual(14,secondary['byteLength'])
        import tempfile
        with tempfile.TemporaryDirectory() as directory:
            p=Path(directory)/'bad.exe';p.write_bytes(b'bad')
            with self.assertRaises(ValueError):verify_retail(p,e['retailRecords'])

    def test_y_x_origin_order_puts_actor_third_and_never_sorts_by_final_destination(self):
        r=self.result('four-origins-actor-is-third')
        self.assertEqual([[1,0],[0,1],[2,1],[1,2]],r['castOrigins'])
        self.assertEqual(['MoveThenAct','MoveThenAct','ActThenMove','MoveThenAct'],[c['order'] for c in r['emitted']])
        self.assertEqual([[1,0],[0,1],[1,0],[1,2]],[c['destination'] for c in r['emitted']])
        self.assertEqual(r,self.result('reverse-origin-input'))
        self.assertFalse(r['aimConsumed'])

    def test_stay_is_one_ActThenMove_and_distinct_origins_keep_duplicate_destinations(self):
        r=self.result('stay-is-one-ActThenMove')['emitted']
        self.assertEqual(1,len(r));self.assertEqual('ActThenMove',r[0]['order']);self.assertEqual([2,1],r[0]['destination'])
        r=self.result('own-origin-destination-duplicates-another-origin')['emitted']
        self.assertEqual(4,len(r));self.assertEqual(r[2]['destination'],r[3]['destination'])
        self.assertNotEqual(r[2]['origin'],r[3]['origin'])
        self.assertEqual(['MoveThenAct','MoveThenAct'],[c['order'] for c in self.result('actor-origin-absent-scenario')['emitted']])

    def test_radius_one_mask_bit_order_at_columns_zero_and_fifteen(self):
        self.assertEqual([0x4000,0x8000,0],self.result('edge-column-zero')['castRows'])
        r=self.result('edge-column-fifteen');self.assertEqual([1,2],r['castRows'][-2:])
        self.assertEqual([[15,14],[14,15]],r['castOrigins'])
        self.assertEqual([[15,15]],r['aimTiles'])

    def test_cast_mask_is_only_a_prefilter_and_actual_weapon_range_excludes_distance_zero(self):
        for name in ('empty-source-mask','inactive-target-no-calls','not-useful-no-calls','far-origins-filtered'):
            self.assertEqual([],self.result(name)['emitted'])
        r=self.result('range-zero-panel-excluded')
        self.assertEqual([[1,1]],r['castOrigins']);self.assertEqual([[1,1]],r['aimTiles']);self.assertEqual([],r['emitted'])
        self.assertEqual(2,len(self.result('scenario-subset')['emitted']))

    def test_each_candidate_is_freshly_scored_once_with_carried_rng_on_original_hp(self):
        data=self.data();before=copy.deepcopy(data)
        with patch('verify_ai_scan_contract.proposed_scoring',wraps=proposed_scoring) as scorer:
            r=proposed_scan(data);self.assertEqual(4,scorer.call_count)
            calls=[c.args[0]['candidate'] for c in scorer.call_args_list]
        self.assertEqual([7,12,17,22],[c['rngCount'] for c in calls])
        self.assertEqual([draws(1,n)[0] for n in (0,5,10,15)],[c['rngWord'] for c in calls])
        self.assertEqual([50]*4,[c['targetHp'] for c in calls]);self.assertEqual(before,data)
        self.assertEqual((draws(1,20)[0],27,0),(r['rngWord'],r['rngCount'],r['additionalScanDraws']))

    def test_NotValuable_keeps_four_draws_and_later_origins_still_evaluate_same_aim(self):
        r=self.result('scan-front-certain-evade-continues');e=r['outcome']['evaluations']
        self.assertEqual(['Scored','Scored','NotValuable','Scored'],[v['disposition'] for v in e])
        self.assertEqual([12,17,21,26],[v['rngCount'] for v in e]);self.assertIsNone(e[2]['score'])
        self.assertEqual(4,r['scoreCalls']);self.assertEqual(0,r['outcome']['retainedIndex'])
        r=self.result('scan-only-NotValuable')
        self.assertEqual(('NoScoredCandidate',11,None),(r['disposition'],r['rngCount'],r['outcome']['retainedIndex']))

    def test_private_incumbent_is_strict_and_hit_precedes_baseline_proximity(self):
        self.assertEqual(0,self.result('scan-four-origins')['outcome']['retainedIndex'])
        r=self.result('scan-ActThenMove-proximity-improves')['outcome']
        self.assertEqual([9,9,7,9],[e['proximity'] for e in r['evaluations']]);self.assertEqual(2,r['retainedIndex'])
        r=self.result('scan-hit-precedes-proximity')['outcome']
        self.assertEqual([100,100,50,100],[e['score']['hitPercent'] for e in r['evaluations']]);self.assertEqual(0,r['retainedIndex'])

    def test_negative_and_zero_scores_beat_missing_source_sentinel_without_becoming_absent(self):
        for name,priority in [('scan-negative-absolute-priority',-71),('scan-legitimate-zero-priority',0)]:
            r=self.result(name)
            self.assertEqual(('Scanned',0),(r['disposition'],r['outcome']['retainedIndex']))
            self.assertEqual([priority]*4,[e['score']['priority'] for e in r['outcome']['evaluations']])

    def test_lethal_projections_never_remove_original_target_or_rebuild_baseline_cache(self):
        d=self.data('scan-lethal-still-evaluates-original-foe')
        with patch('verify_ai_scan_contract.proposed_scoring',wraps=proposed_scoring) as scorer:r=proposed_scan(d)
        self.assertEqual([50]*4,[c.args[0]['candidate']['targetHp'] for c in scorer.call_args_list])
        self.assertEqual([320]*4,[e['score']['priority'] for e in r['outcome']['evaluations']])
        self.assertEqual((4,31,'B'),(r['scoreCalls'],r['rngCount'],r['outcome']['eligibleEnemyId']))
        self.assertEqual([9]*4,[e['proximity'] for e in r['outcome']['evaluations']])
        r=self.result('scan-no-imminent-enemy')['outcome'];self.assertIsNone(r['eligibleEnemyId'])
        self.assertEqual([0]*4,[e['proximity'] for e in r['evaluations']])

    def test_empty_inactive_and_filtered_scans_do_not_call_scorer_or_reserve_rng(self):
        for name in ('scan-empty-mask','scan-inactive-target','scan-filtered-far-origins','scan-empty-at-exhausted-count'):
            with patch('verify_ai_scan_contract.proposed_scoring') as scorer:
                d=self.data(name);r=proposed_scan(d);scorer.assert_not_called()
            self.assertEqual(('NoCandidates',0,None),(r['disposition'],r['scoreCalls'],r['outcome']['retainedIndex']))
            self.assertEqual((d['rngWord'],d['rngCount']),(r['rngWord'],r['rngCount']))

    def test_each_missing_false_certificate_rejects_even_before_an_empty_scan(self):
        for name in CERTIFICATES:
            for missing in (True,False):
                d=self.data('scan-empty-mask')
                if missing:del d['profile'][name]
                else:d['profile'][name]=False
                with patch('verify_ai_scan_contract.proposed_scoring') as scorer:
                    r=proposed_scan(d);scorer.assert_not_called()
                self.assertEqual(('Rejected','UnsupportedScanProfile',7,None),(r['disposition'],r['reason'],r['rngCount'],r['outcome']))
        for value in (None,1,'false'):
            d=self.data();d['profile']['targetActive']=value
            self.assertEqual('UnsupportedScanProfile',proposed_scan(d)['reason'])

    def test_bad_scenario_membership_duplicates_destination_and_moved_actor_reject_before_scoring(self):
        for name in ('scan-duplicate-mask-cell','scan-occupied-foe-in-scenario','scan-destination-outside-scenario'):
            self.assertEqual(('InvalidScenario',0),(self.result(name)['reason'],self.result(name)['scoreCalls']))
        for origins,destination in [([], [2,1]),([[2,1]],None),([[-1,0]],[-1,0])]:
            d=self.data();d['scenario'].update(origins=origins,destination=destination)
            self.assertEqual('InvalidScenario',proposed_scan(d)['reason'])
        d=self.data();d['state']['units'][0]['moved']=True
        self.assertEqual('InvalidScenario',proposed_scan(d)['reason'])
        self.assertEqual('Scanned',self.result('scan-already-moved-stay')['disposition'])

    def test_every_prefix_budget_rolls_back_whole_scan_or_completes_at_exact_MAX(self):
        for name,costs in [('scan-four-origins',[5,5,5,5]),('scan-front-certain-evade-continues',[5,5,4,5])]:
            total=sum(costs)
            for remaining in range(total+1):
                d=self.data(name);d['rngCount']=MAX-remaining;r=proposed_scan(d)
                if remaining==total:
                    self.assertEqual(('Scanned',MAX,0),(r['disposition'],r['rngCount'],r['additionalScanDraws']))
                else:
                    budget=remaining;index=0
                    while budget>=costs[index]:budget-=costs[index];index+=1
                    self.assertEqual(('Rejected',index,MAX-remaining,None),(r['disposition'],r['failedIndex'],r['rngCount'],r['outcome']))
                    self.assertEqual('RandomDrawCountExhausted',r['evaluationRejection']['projectionReason'])

    def test_late_tick_failure_has_no_partial_incumbent_and_ready_clock_can_scan_at_MAX_ticks(self):
        for name,index in [('scan-first-candidate-tick-overflow',0),('scan-late-candidate-tick-overflow',1)]:
            r=self.result(name)
            self.assertEqual((index,7,None),(r['failedIndex'],r['rngCount'],r['outcome']))
            self.assertEqual('TickCountExhausted',r['evaluationRejection']['projectionReason'])
        d=self.data();d['state']['ticks']=MAX;d['state']['units'][1]['ct']=254
        self.assertEqual(('Scanned',27),(proposed_scan(d)['disposition'],proposed_scan(d)['rngCount']))

    def test_source_resume_descriptions_keep_current_candidate_and_draw_prefix_without_replaying_formula(self):
        for name,index,count,reentry in [('resume-before-origins',0,7,'begin_evaluation'),('resume-before-aim',1,12,'origin_aim_tiles_evaluate'),
            ('resume-after-formula-before-clock',1,16,'evaluate_priority'),('resume-after-upkeep-before-score',2,21,'evaluate_priority')]:
            r=self.result(name);self.assertEqual((index,count,reentry),(r['nextIndex'],r['rngCount'],r['reentry']))
            for key in ('rebuildCastAndAimMasks','replayCompleted','restartFormula'):self.assertFalse(r[key])
        d=self.data('resume-before-aim');d['partialDraws']=1
        with self.assertRaises(ValueError):source_resume(d)

    def test_original_21_slot_erratum_rules_out_historical_26_foe_wrap_without_changing_old_evidence(self):
        self.assertEqual(dict(maximumEligibleEnemies=20,maximumProximity=200,byteWrapReachable=False,historical26FoeWrapAdmitted=False),
                         source_roster_bound(dict(unitSlots=21,actorExcluded=True)))
        with self.assertRaises(ValueError):source_roster_bound(dict(unitSlots=32,actorExcluded=True))
        old=json.loads((ROOT/'docs/fixtures/behavior/ai-comparison-contract-v1.json').read_text(encoding='utf-8'))
        row=next(r for r in old['vectors'] if r['id']=='byte-wrap-isolated-roster-kernel')
        self.assertEqual(26,len(row['inputs']['foes']));self.assertEqual(4,row['expected']['cells'][0]['value'])
        # The old arithmetic remains intact; this was never an admissible original roster.
        self.assertGreater(len(row['inputs']['foes']),21)

    def test_both_actor_IDs_collection_orders_replays_and_external_result_rejection(self):
        baseline=self.result('scan-four-origins')
        self.assertEqual(baseline,self.result('scan-reversed-origins-and-units'))
        other=self.result('scan-other-actor-ID')
        self.assertEqual('A',other['outcome']['eligibleEnemyId'])
        self.assertEqual([64]*4,[e['score']['priority'] for e in other['outcome']['evaluations']])
        for key in ('scores','cache','candidates','projectionResult'):
            d=self.data();d[key]=[]
            with self.assertRaisesRegex(ValueError,'No external'):proposed_scan(d)
        for row in self.contract()['vectors']:
            before=copy.deepcopy(row['inputs']);result=calculate(row['family'],row['inputs'])
            self.assertEqual(result,calculate(row['family'],row['inputs']));self.assertEqual(before,row['inputs'])


if __name__=='__main__':unittest.main()
