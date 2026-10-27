"""ZOD-128 classified descriptions; no production comparator or original-game oracle."""
import copy
import itertools
import json
from pathlib import Path
import unittest
from unittest.mock import patch

from verify_ai_comparison_contract import (FIXTURE, audit, calculate, compare, delay, eligible,
    proximity, proposed_pair, validate, MAX)
from verify_ai_score_contract import proposed_scoring
from verify_ai_decision_contract import draws, signed16

ROOT = Path(__file__).resolve().parents[2]


class AiComparisonContractTests(unittest.TestCase):
    def contract(self): return json.loads((ROOT/FIXTURE).read_text(encoding='utf-8'))
    def row(self, name): return next(r for r in self.contract()['vectors'] if r['id'] == name)
    def data(self, name='pair-exact-tie'): return self.row(name)['inputs']
    def result(self, name): return self.row(name)['expected']

    def test_inventory_audit_and_historical_baseline(self):
        self.assertEqual(dict(vectors=60, families=5, implementationCasesUnexecuted=14), validate(self.contract()))
        r = audit(ROOT, check_managed_baseline=True)
        self.assertEqual((110,31,0,0),(r['preservedFiles'],r['managedBaselineFilesChecked'],
            r['referenceFilesChecked'],r['referenceFunctionsChecked']))
        for k in ('productionImplemented','fullAiExecuted','originalExecuted'): self.assertFalse(r[k])

    def test_classification_inventory_expectations_and_execution_claims_fail_closed(self):
        for kind in ('classification','duplicate','expectation','execution','case'):
            c = self.contract()
            if kind == 'classification': c['vectors'][0]['expectationKind'] = 'original-observation'
            if kind == 'duplicate': c['vectors'][1]['id'] = c['vectors'][0]['id']
            if kind == 'expectation': c['vectors'][0]['expected']['firstIsHigher'] = False
            if kind == 'execution': c['productionImplemented'] = True
            if kind == 'case': c['implementationCases'][0]['status'] = 'passed'
            with self.assertRaises(ValueError): validate(c)

    def test_decided_signed_priority_hit_and_proximity_literal_order(self):
        for name,order,higher in [('decided-precedes-priority',1,True),('candidate-below-decided',-1,False),
            ('signed-negative-below-zero',-1,False),('signed-minimum-below-maximum',-1,False),
            ('negative-improves-negative',1,True),('priority-before-hit',1,True),('hit-before-proximity',1,True),
            ('lower-proximity-wins',0,True),('higher-proximity-loses',0,False),('exact-tie-not-higher',0,False),
            ('zero-is-legitimate-priority',1,True),('sentinel-is-signed-number-only',1,True)]:
            self.assertEqual(dict(rankOrder=order,firstIsHigher=higher,additionalDraws=0), self.result(name))

    def test_strict_comparison_is_asymmetric_and_equal_is_never_an_improvement(self):
        ranks = [dict(decided=0,priority=p,hit=h,proximity=x) for p,h,x in
            itertools.product((-160,-71,0,64,320),(1,50,100),(0,8,9))]
        for a,b in itertools.product(ranks,repeat=2):
            higher = compare(dict(first=a,second=b))['firstIsHigher']
            reverse = compare(dict(first=b,second=a))['firstIsHigher']
            self.assertFalse(higher and reverse)
            self.assertEqual(a != b, higher or reverse)

    def test_reconciles_all_historical_ZOD116_rank_kernels_without_rewriting_them(self):
        old = json.loads((ROOT/'docs/fixtures/behavior/ai-decision-contract-v1.json').read_text(encoding='utf-8'))
        for row in old['vectors']:
            if row['family'] != 'rank': continue
            data = copy.deepcopy(row['inputs'])
            for key in ('first','second'): data[key]['priority'] = signed16(data[key]['priority'])
            r = compare(data)
            self.assertEqual(row['expected']['comparison'],r['rankOrder'])
            self.assertEqual(row['expected']['firstIsHigher'],r['firstIsHigher'])

    def test_widths_are_source_types_but_chosen_marker_and_sentinel_are_not_managed_absence(self):
        for key,value in [('decided',256),('priority',32768),('priority',-32769),('hit',-1),('proximity',256)]:
            data = self.data('exact-tie-not-higher'); data['first'][key] = value
            with self.assertRaises(ValueError): compare(data)
        self.assertEqual(255,self.data('decided-precedes-priority')['first']['decided'])
        self.assertFalse(self.contract()['productionImplemented'])

    def test_turn_delay_ceiling_post_pick_actor_CT_and_byte_cache(self):
        for ct,speed,ticks in [(0,1,100),(0,255,1),(0,10,10),(1,10,10),(99,1,1),(99,255,1),
            (100,1,0),(254,1,0),(0,9,12)]:
            self.assertEqual(dict(delay=ticks,actorCacheByte=ticks,additionalDraws=0),delay(dict(ct=ct,speed=speed)))
        # Active actor has already had its CT reduced modulo 100, not an assumed delay of zero.
        d = self.data(); r = proposed_pair(d)
        self.assertEqual((17,1,True),(r['baselineActorDelay'],r['baselineTargetDelay'],r['eligibleEnemy']))
        for ct,speed in [(-1,1),(255,1),(0,0),(0,256),(True,1)]:
            with self.assertRaises(ValueError): delay(dict(ct=ct,speed=speed))

    def test_eligibility_equal_delay_is_inclusive_and_each_exclusion_is_independent(self):
        self.assertTrue(self.result('equal-delay-included')['eligible'])
        self.assertTrue(self.result('earlier-included')['eligible'])
        for name in ('later-excluded','non-enemy-excluded','out-of-play-excluded','stop-excluded',
                     'dontAct-excluded','sleep-excluded','chicken-excluded','charm-excluded'):
            self.assertFalse(self.result(name)['eligible'])
        d = self.data('equal-delay-included'); d['stop'] = 1
        with self.assertRaises(ValueError): eligible(d)

    def test_proximity_is_clipped_distance_sum_not_distance_or_an_enemy_count(self):
        self.assertEqual([10,9,1,0,0],[c['value'] for c in self.result('distance-cutoff')['cells']])
        self.assertEqual(3,self.result('diagonal-distance')['cells'][0]['value'])
        self.assertEqual([9,9],[c['value'] for c in self.result('layers-ignore-height')['cells']])
        self.assertEqual(18,self.result('two-contributions-isolated-roster-kernel')['cells'][0]['value'])

    def test_unmarked_cache_cells_retain_stale_bytes_and_byte_wrap_is_only_diagnostic(self):
        r = self.result('unmarked-cell-retains-stale-byte')['cells']
        self.assertEqual([(233,False),(0,True)],[(c['value'],c['written']) for c in r])
        self.assertEqual(0,self.result('ineligible-foe-zero')['cells'][0]['value'])
        self.assertEqual(4,self.result('byte-wrap-isolated-roster-kernel')['cells'][0]['value'])
        # Managed two-opponent profile has at most ONE eligible foe; its cache cannot wrap.
        for name in ('pair-exact-tie','pair-proximity-improvement','pair-no-imminent-foe'):
            self.assertTrue(all(0 <= x <= 10 for x in self.result(name)['proximity']))

    def test_profile_pairing_and_membership_fail_before_score_or_rng_work(self):
        for name,reason in [('pair-invalid-comparison-profile','UnsupportedComparisonProfile'),
            ('pair-mismatched-identities','UnsupportedComparisonProfile'),('pair-unwritten-cache-cell','InvalidCacheDestination')]:
            with patch('verify_ai_comparison_contract.proposed_scoring') as scorer:
                r = proposed_pair(self.data(name)); scorer.assert_not_called()
            self.assertEqual(('Rejected',reason,0,1,7,None),tuple(r[k] for k in
                ('disposition','reason','scoreCalls','rngWord','rngCount','evaluations')))

    def test_two_fresh_scores_carry_rng_but_share_original_state_not_first_projection(self):
        data = self.data(); before = copy.deepcopy(data)
        with patch('verify_ai_comparison_contract.proposed_scoring',wraps=proposed_scoring) as scorer:
            r = proposed_pair(data); self.assertEqual(2,scorer.call_count)
            first,second = [a.args[0]['candidate'] for a in scorer.call_args_list]
        self.assertEqual((1,7),(first['rngWord'],first['rngCount']))
        self.assertEqual((draws(1,5)[0],12),(second['rngWord'],second['rngCount']))
        self.assertEqual((draws(1,10)[0],17),(r['rngWord'],r['rngCount']))
        self.assertEqual(first['targetHp'],second['targetHp'])
        self.assertEqual(before,data)
        self.assertEqual(0,r['additionalComparisonDraws'])

    def test_only_strict_challenger_improvements_replace_incumbent(self):
        for name,index in [('pair-exact-tie',0),('pair-critical-target',0),('pair-negative-priority',0),
            ('pair-hit-improvement',1),('pair-proximity-improvement',1),('pair-no-imminent-foe',0)]:
            self.assertEqual(index,self.result(name)['retained'])
        self.assertEqual([-71,-71],[e['score']['priority'] for e in self.result('pair-negative-priority')['evaluations']])
        d = self.data('pair-proximity-improvement'); d['candidates'].reverse()
        self.assertEqual((0,[8,9]),(proposed_pair(d)['retained'],proposed_pair(d)['proximity']))

    def test_both_orders_use_destination_and_cache_does_not_change_after_lethal_projection(self):
        for name in ('pair-move-then-act','pair-act-then-move','pair-actor-slot-permutation'):
            r = self.result(name); self.assertEqual((0,[9,9],17),(r['retained'],r['proximity'],r['rngCount']))
        r = self.result('pair-lethal-retains-baseline-foe')
        self.assertEqual((True,[9,9],19),(r['eligibleEnemy'],r['proximity'],r['rngCount']))
        self.assertEqual([320,320],[e['score']['priority'] for e in r['evaluations']])
        r = self.result('pair-proximity-improvement')
        self.assertEqual([9,8],r['proximity'])  # Challenger ActThenMove still attacks from [0,0].

    def test_identified_actor_role_and_source_slot_are_independent_of_collection_order(self):
        baseline = self.result('pair-exact-tie')
        self.assertEqual(baseline,self.result('pair-reverse-collection'))
        self.assertEqual('Delita',baseline['eligibleEnemyId'])
        other = self.result('pair-other-actor-ID')
        self.assertEqual((1,17,False,None,[0,0]),tuple(other[k] for k in
            ('baselineActorDelay','baselineTargetDelay','eligibleEnemy','eligibleEnemyId','proximity')))
        self.assertEqual([64,64],[e['score']['priority'] for e in other['evaluations']])
        d = self.data(); d['actorId'] = 'unknown'
        self.assertEqual('UnsupportedComparisonProfile',proposed_pair(d)['reason'])

    def test_NotValuable_is_absent_score_and_keeps_draws_even_when_discarded(self):
        for name,index,count in [('pair-incumbent-not-valuable',1,16),('pair-challenger-not-valuable',0,16),
                                ('pair-both-not-valuable',None,15)]:
            r = self.result(name); self.assertEqual((index,count),(r['retained'],r['rngCount']))
            self.assertEqual(draws(1,count-7)[0],r['rngWord'])
            for e in r['evaluations']:
                if e['disposition'] == 'NotValuable': self.assertIsNone(e['score'])
        self.assertEqual('NoScoredCandidate',self.result('pair-both-not-valuable')['disposition'])

    def test_first_failure_short_circuits_and_second_failure_rolls_back_whole_pair_with_reason(self):
        for name,index,calls in [('pair-first-evaluation-rejected',0,1),('pair-second-evaluation-rejected',1,2)]:
            r = self.result(name)
            self.assertEqual(('Rejected',index,calls,1,7,None),tuple(r[k] for k in
                ('disposition','failedIndex','scoreCalls','rngWord','rngCount','evaluations')))
            if index == 0: self.assertEqual('UnsupportedScoreProfile',r['evaluationRejection']['reason'])
            else: self.assertEqual('TickCountExhausted',r['evaluationRejection']['projectionReason'])
        r = self.result('pair-second-overflow-atomic')
        self.assertEqual((1,MAX-5,None),(r['failedIndex'],r['rngCount'],r['evaluations']))
        self.assertEqual('RandomDrawCountExhausted',r['evaluationRejection']['projectionReason'])

    def test_exact_MAX_pair_budget_needs_no_comparison_draw_and_replays(self):
        d = self.data('pair-exact-MAX-draw-budget'); r = proposed_pair(d)
        self.assertEqual(('Compared',MAX,0),tuple(r[k] for k in ('disposition','rngCount','additionalComparisonDraws')))
        self.assertEqual(r,proposed_pair(d))

    def test_external_results_cache_and_contradictory_baselines_are_not_accepted(self):
        for key in ('scores','cache','projectionResult'):
            d = self.data(); d[key] = {}
            with self.assertRaisesRegex(ValueError,'No external'): proposed_pair(d)
        d = self.data(); d['candidates'][1]['scoring']['candidate']['targetHp'] = 49
        with self.assertRaisesRegex(ValueError,'One original baseline'): proposed_pair(d)
        d = self.data(); d['candidates'][1]['scoring']['actorHp'] = 49
        with self.assertRaisesRegex(ValueError,'One original actor HP'): proposed_pair(d)

    def test_all_vectors_preserve_inputs_and_replay_independent_branches(self):
        for row in self.contract()['vectors']:
            before = copy.deepcopy(row['inputs']); result = calculate(row['family'],row['inputs'])
            self.assertEqual(result,calculate(row['family'],row['inputs']))
            self.assertEqual(before,row['inputs'])


if __name__ == '__main__': unittest.main()
