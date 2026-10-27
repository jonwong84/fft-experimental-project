import copy
import json
from pathlib import Path
import unittest

from verify_turn_facing_contract import (FIXTURE, MANAGED, calculate, choose,
    clock_end, completion_gate, direction, validate, viability)

ROOT = Path(__file__).resolve().parents[2]


class TurnFacingContractTests(unittest.TestCase):
    def contract(self): return json.loads((ROOT/FIXTURE).read_text(encoding='utf-8'))

    def vector(self, name):
        return copy.deepcopy(next(row for row in self.contract()['vectors'] if row['id'] == name))

    def test_current_description_vectors_are_consistent(self):
        result = validate(self.contract())
        self.assertEqual((70, 5, 18), (result['vectors'], result['families'],
            result['implementationCasesUnexecuted']))

    def test_cardinals_diagonal_ties_and_names_are_literal(self):
        for target, face in [([2,0],'South'),([0,2],'West'),([2,4],'North'),([4,2],'East'),
            ([1,1],'South'),([1,3],'West'),([3,3],'North'),([3,1],'East')]:
            self.assertEqual(face, direction([2,2], target))
        self.assertEqual({'East':0,'North':1,'West':2,'South':3}, MANAGED)
        self.assertEqual('Overlap', direction([2,2],[2,2]))

    def test_signed_coordinate_byte_wrap_is_observed_and_outside_first_profile(self):
        self.assertEqual('West', direction([127,2],[128,2]))
        self.assertEqual('East', direction([126,2],[127,2]))
        for point in ([-1,2],[256,2],[1.5,2]):
            with self.assertRaisesRegex(ValueError,'byte domain'): direction([2,2],point)

    def test_viability_rise_boundary_and_unrestricted_descent(self):
        row = self.vector('flat-all-directions')['inputs']
        row['adjacent'][0]['height'] = 12
        self.assertEqual(['South','West','North','East'],viability(row)['viable'])
        row['adjacent'][0]['height'] = 13
        self.assertEqual(['West','North','East'],viability(row)['viable'])
        row['adjacent'][0]['height'] = 0
        self.assertEqual(['South','West','North','East'],viability(row)['viable'])

    def test_lower_tile_test_ignores_occupancy_half_height_and_voluntary_stop_flags(self):
        for name in ('occupied-untargetable-cannot-stop-still-viable','half-slope-depth-ignored'):
            row = self.vector(name)
            self.assertEqual(dict(viable=['South','West','North','East'],allBlockedFallback=False),
                calculate(row['family'],row['inputs']))

    def test_corner_and_all_blocked_normalization_preserve_original_direction_order(self):
        row=self.vector('corner-byte-underflow')
        self.assertEqual(dict(viable=['North','East'],allBlockedFallback=False),viability(row['inputs']))
        row=self.vector('all-four-blocked-restores-four')
        self.assertEqual(dict(viable=['South','West','North','East'],allBlockedFallback=True),viability(row['inputs']))

    def test_fixed_and_keep_hints_do_not_need_enemy_or_viability(self):
        for hint,face in [('South','South'),('West','West'),('North','North'),('East','East'),('KeepCurrent','North')]:
            self.assertEqual(dict(supported=True,facing=face,managedFacing=MANAGED[face],visited=[]),
                choose(dict(hint=hint,current='North')))

    def test_imminent_scan_retries_but_any_enemy_scan_is_only_once(self):
        for name,face,visited in [('retry-imminent-after-bad-direction','North',[1,2]),
            ('any-search-reconsiders-rejected-imminent','North',[1,1]),
            ('any-nearest-once-then-current','North',[1]),
            ('any-nearest-once-then-last-source-direction','East',[1])]:
            result=choose(self.vector(name)['inputs'])
            self.assertEqual((face,visited),(result['facing'],result['visited']))

    def test_equal_distance_uses_original_slot_instead_of_input_collection_order(self):
        data=self.vector('nearest-tie-first-original-slot')['inputs']
        self.assertEqual(('West',[2]),(choose(data)['facing'],choose(data)['visited']))
        data['enemies'].reverse()
        self.assertEqual(('West',[2]),(choose(data)['facing'],choose(data)['visited']))

    def test_no_enemy_and_overlap_are_admission_limits_not_invented_original_outcomes(self):
        for name,reason in [('no-enemy-overlap-sentinel','NoEnemyStackByte'),
            ('coincident-imminent-lookup','OverlapStackByte'),('coincident-any-lookup','OverlapStackByte')]:
            row=self.vector(name)
            self.assertEqual('managed-admission-boundary',row['expectationKind'])
            self.assertEqual((False,reason),(choose(row['inputs'])['supported'],choose(row['inputs'])['reason']))

    def test_turn_end_refunds_only_unused_choices_and_repeated_call_is_no_op(self):
        for moved,acted,ct in [(False,False,55),(True,False,35),(False,True,35),(True,True,15)]:
            data=dict(ct=15,hasTurn=True,moved=moved,acted=acted)
            result=clock_end(data)
            self.assertEqual(ct,result['ct']);self.assertEqual(0,result['rngDraws'])
            self.assertEqual(result,clock_end({k:v for k,v in result.items() if k!='rngDraws'}))
        self.assertEqual(60,clock_end(dict(ct=59,hasTurn=True,moved=False,acted=False))['ct'])

    def test_pending_completion_prevents_advance_and_any_further_turn_command(self):
        base=self.vector('auto-complete-then-facing')['inputs']
        for command in ('Advance','Move','Act','WaitWithFacing'):
            self.assertEqual(dict(accepted=False,reason='FacingPending'),completion_gate(dict(base,command=command)))
        self.assertEqual(dict(accepted=True,operation='FacingOnly',rngDraws=0),completion_gate(base))

    def test_wrong_stale_missing_and_consumed_authorization_are_distinct(self):
        for name,reason in [('pending-wrong-actor','WrongActor'),
            ('stale-revision-or-cross-branch-receipt','StaleCompletion'),
            ('unattributed-ended-snapshot','MissingCompletion'),
            ('duplicate-after-receipt-consumed','MissingCompletion')]:
            self.assertEqual(dict(accepted=False,reason=reason),completion_gate(self.vector(name)['inputs']))

    def test_counter_and_unsupported_choice_fail_before_any_proposed_wait_publication(self):
        for name,reason in [('finish-counter-exhausted','RevisionExhausted'),
            ('wait-counter-exhausted','RevisionExhausted'),('unsupported-choice-preserves-pending','UnsupportedFacing'),
            ('wait-unsupported-before-refund','UnsupportedFacing')]:
            self.assertEqual(dict(accepted=False,reason=reason),completion_gate(self.vector(name)['inputs']))

    def test_descriptions_are_repeatable_queries_and_do_not_mutate_inputs(self):
        for row in self.contract()['vectors']:
            before=copy.deepcopy(row['inputs'])
            self.assertEqual(row['expected'],calculate(row['family'],row['inputs']))
            self.assertEqual(row['expected'],calculate(row['family'],row['inputs']))
            self.assertEqual(before,row['inputs'])

    def test_future_delegated_advance_or_attack_rng_is_not_claimed_to_be_zero(self):
        result=completion_gate(self.vector('settled-allows-next-advance')['inputs'])
        self.assertEqual(dict(accepted=True,operation='Delegate'),result)
        self.assertNotIn('rngDraws',result)

    def test_tampered_vectors_inventory_classification_and_implementation_claims_reject(self):
        for mutation in ('expected','duplicate','family','classification','production','case','count'):
            contract=self.contract()
            if mutation=='expected':contract['vectors'][0]['expected']['facing']='East'
            elif mutation=='duplicate':contract['vectors'][1]['id']=contract['vectors'][0]['id']
            elif mutation=='family':contract['vectors'][0]['family']='runtime-ai'
            elif mutation=='classification':contract['vectors'][-1]['expectationKind']='source-derived'
            elif mutation=='production':contract['productionImplemented']=True
            elif mutation=='case':contract['implementationCases'][0]['status']='passed'
            else:contract['vectorCount']+=1
            with self.assertRaises(ValueError):validate(contract)


if __name__=='__main__':unittest.main()
