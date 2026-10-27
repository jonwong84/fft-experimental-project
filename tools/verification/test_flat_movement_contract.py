import copy
import hashlib
import itertools
import json
from pathlib import Path
import struct
import tempfile
import unittest

from verify_flat_movement_contract import (FIXTURE, audit, calculate, flat_route,
    placement, preferred_predecessor, validate, verify_retail)

ROOT = Path(__file__).resolve().parents[2]


class FlatMovementContractTests(unittest.TestCase):
    def contract(self): return json.loads((ROOT/FIXTURE).read_text(encoding='utf-8'))

    def vector(self, name):
        return copy.deepcopy(next(r for r in self.contract()['vectors'] if r['id']==name))

    def result(self, name):
        row=self.vector(name)
        return calculate(row['family'],row['inputs'])

    def test_inventory_history_and_managed_baseline_without_original_execution(self):
        result=audit(ROOT,check_managed_baseline=True)
        self.assertEqual((48,5,18,68,21),(result['vectors'],result['families'],
            result['implementationCasesUnexecuted'],result['preservedFiles'],result['managedBaselineFilesChecked']))
        self.assertEqual(0,result['referenceFunctionsChecked'])
        for key in ('productionImplemented','fullAiExecuted','originalExecuted'):self.assertFalse(result[key])

    def test_zero_move_and_byte_caps_have_positive_origin_residual(self):
        zero=self.result('move-zero')
        self.assertEqual((1,[],[[2,2]]),(zero['originRemainingRange'],zero['physicalDestinations'],zero['aiOrigins']))
        self.assertEqual([dict(tile=[2,2],remaining=1)],zero['remainingRanges'])
        a=self.result('move-cap-124');b=self.result('move-cap-255')
        self.assertEqual(a,b)
        self.assertEqual((124,125,3),(a['effectiveMove'],a['originRemainingRange'],a['jumpHalf']))
        self.assertEqual(dict(tile=[2,1],remaining=122),next(r for r in a['remainingRanges'] if r['tile']==[2,1]))
        maximum=flat_route(dict(width=16,depth=16,origin=[0,0],enemy=[15,15],destination=[15,14],
                                move=255,jump=255,moved=False))
        self.assertEqual(254,len(maximum['physicalDestinations']))
        self.assertEqual([192]*14+[0]*15,maximum['packedSteps'])

    def test_literal_cardinal_steps_and_straight_run_is_not_compressed(self):
        for name,packed,heading in [('east-one',[0],'East'),('west-one',[64],'West'),
            ('north-one',[192],'North'),('south-one',[128],'South'),
            ('straight-uncompressed',[0,0,0],'East')]:
            result=self.result(name)
            self.assertEqual((packed,heading),(result['packedSteps'],result['executionFinalHeading']))
        self.assertEqual([[0,2],[1,2],[2,2],[3,2]],self.result('straight-uncompressed')['path'])

    def test_literal_diagonal_paths_and_final_step_heading(self):
        for name,packed,heading in [('diagonal-ne',[192,0],'East'),('diagonal-nw',[192,64],'West'),
            ('diagonal-sw',[128,64],'West'),('diagonal-se',[0,128],'South'),
            ('long-diagonal-ne',[192,192,0,0],'East')]:
            result=self.result(name)
            self.assertEqual((packed,heading),(result['packedSteps'],result['executionFinalHeading']))

    def test_enemy_detour_is_four_steps_and_manhattan_prototype_overestimates(self):
        short=self.result('enemy-detour-outside-budget');long=self.result('enemy-detour-at-budget')
        self.assertNotIn([2,1],short['physicalDestinations'])
        self.assertIn([2,1],short['defaultPrototypeExtraDestinations'])
        self.assertEqual([[0,1],[0,2],[1,2],[2,2],[2,1]],long['path'])
        self.assertEqual([192,0,0,128],long['packedSteps'])
        self.assertEqual([],long['defaultPrototypeExtraDestinations'])
        blocked=self.result('one-row-enemy-disconnects')
        self.assertEqual([],blocked['physicalDestinations'])
        self.assertEqual([[2,0],[3,0],[4,0]],blocked['defaultPrototypeExtraDestinations'])

    def test_small_rectangles_against_independent_all_pairs_graph_distances(self):
        # Floyd-Warshall graph oracle, independent of the description's queue/range traversal.
        for width,depth in ((3,3),(4,2),(1,4)):
            tiles=list(itertools.product(range(width),range(depth)))
            for enemy in tiles:
                nodes=[p for p in tiles if p!=enemy]
                cost={(p,q):0 if p==q else 1 if abs(p[0]-q[0])+abs(p[1]-q[1])==1 else 1000
                      for p in nodes for q in nodes}
                for k in nodes:
                    for p in nodes:
                        for q in nodes:cost[p,q]=min(cost[p,q],cost[p,k]+cost[k,q])
                for origin in nodes:
                    for budget in range(6):
                        destination=nodes[-1]
                        data=dict(width=width,depth=depth,origin=list(origin),enemy=list(enemy),
                                  destination=list(destination),move=budget,jump=7,moved=False)
                        result=flat_route(data)
                        expected={p for p in nodes if p!=origin and cost[origin,p]<=budget}
                        self.assertEqual(expected,{tuple(p) for p in result['physicalDestinations']})
                        if result['path']:
                            self.assertEqual(cost[origin,destination],len(result['packedSteps']))
                            self.assertEqual(list(origin),result['path'][0])
                            self.assertEqual(list(destination),result['path'][-1])
                            codes={(1,0):0,(-1,0):64,(0,1):192,(0,-1):128}
                            for a,b,code in zip(result['path'],result['path'][1:],result['packedSteps']):
                                self.assertNotEqual(list(enemy),b)
                                self.assertEqual(codes[b[0]-a[0],b[1]-a[1]],code)

    def test_stay_occupied_and_unreachable_are_not_physical_move_paths(self):
        for name in ('stay-is-not-physical-move','occupied-destination','enemy-detour-outside-budget'):
            result=self.result(name)
            self.assertIsNone(result['path']);self.assertIsNone(result['executionFinalHeading'])
            self.assertIsNone(result['packedSteps'])
        result=self.result('stay-is-not-physical-move')
        self.assertIn([2,2],result['aiOrigins']);self.assertNotIn([2,2],result['physicalDestinations'])

    def test_moved_filters_ai_origins_only_and_acted_does_not_change_geometry(self):
        data=self.vector('moved-origin-only')['inputs']
        result=flat_route(data)
        self.assertEqual([[2,2]],result['aiOrigins'])
        self.assertGreater(len(result['physicalDestinations']),1)
        # Geometric path query is not command authorization, even after a prior Move.
        data['destination']=[3,2]
        self.assertEqual([0],flat_route(data)['packedSteps'])
        self.assertEqual(self.result('acted-false-does-not-shrink-range'),self.result('acted-true-does-not-shrink-range'))

    def test_reverse_distance_ignores_enemy_and_writes_whole_selected_rows(self):
        result=self.result('enemy-detour-outside-budget')
        self.assertEqual([dict(y=0,values=[2,1,2]),dict(y=1,values=[1,0,1]),dict(y=2,values=[2,1,2])],
                         result['targetDistanceRows'])
        self.assertNotIn([1,1],result['aiOrigins'])
        self.assertEqual([dict(y=2,values=[6,5,4,3,2])],self.result('moved-origin-only')['targetDistanceRows'])
        self.assertEqual([dict(y=0,values=[1,0,1,2,3])],self.result('one-row-enemy-disconnects')['targetDistanceRows'])

    def test_jump_cap_does_not_enable_flat_enemy_hopping(self):
        original=self.result('enemy-detour-at-budget');jump=self.result('jump-cap-does-not-pass-enemy')
        original['jumpHalf']=3
        self.assertEqual(original,jump)
        data=self.vector('enemy-detour-at-budget')['inputs']
        for value in range(256):
            result=flat_route(dict(data,jump=value))
            self.assertEqual(min(7,value)//2,result['jumpHalf'])
            self.assertEqual([192,0,0,128],result['packedSteps'])

    def test_predecessor_ties_prefer_continuation_then_fixed_scan_order(self):
        order=['East','North','West','South']
        for bits in itertools.product((False,True),repeat=4):
            eligible=[d for d,yes in zip(order,bits) if yes]
            for preferred in order:
                expected=preferred if preferred in eligible else eligible[0] if eligible else None
                for permutation in itertools.permutations(eligible):
                    self.assertEqual(expected,preferred_predecessor(preferred,list(permutation)))

    def test_destination_scan_ties_use_last_y_x_and_preserve_priority_modes(self):
        expected={'destination-last-x-on-tie':'right','destination-last-y-on-tie':'top',
            'destination-priority-before-distance':'priority','destination-distance-before-priority':'closer',
            'destination-limit-is-inclusive':'edge','destination-empty-within-limit':None}
        for name,winner in expected.items():
            row=self.vector(name);row['inputs']['tiles'].reverse()
            self.assertEqual(dict(selected=winner),calculate(row['family'],row['inputs']))

    def test_both_command_orders_stay_and_completion_are_separate_from_paths(self):
        for name,command,redecide in [('move-before-act-first','Move',False),
            ('move-before-act-after-move','Act',False),('act-before-move-first','Act',False),
            ('act-before-move-after-act','Move',True),('stay-dispatch-skips-move','Act',False),
            ('both-choices-consumed-end','EndTurn',True)]:
            self.assertEqual(dict(command=command,redecide=redecide),self.result(name))

    def test_ai_projection_keeps_supplied_facing_while_execution_path_has_heading(self):
        self.assertEqual(dict(position=[1,1],facing='West',movementTaken=True,rngDraws=0),
                         self.result('preview-ne-keeps-west'))
        self.assertEqual('East',self.result('diagonal-ne')['executionFinalHeading'])
        self.assertFalse(self.result('preview-stay-no-move')['movementTaken'])
        self.assertTrue(self.result('preview-stay-preserves-prior-move')['movementTaken'])

    def test_post_push_uses_current_enemy_tile_and_frees_previous_tile(self):
        self.assertIsNone(self.result('pre-push-current-enemy')['path'])
        after=self.result('post-push-old-tile-is-free')
        self.assertEqual([[0,1],[1,1]],after['path'])
        self.assertNotIn([2,1],after['physicalDestinations'])
        data=self.vector('post-push-old-tile-is-free')['inputs']
        swapped=dict(data,origin=data['enemy'],enemy=data['origin'])
        self.assertEqual([[2,1],[1,1]],flat_route(swapped)['path'])

    def test_queries_repeat_without_mutation_and_helper_rng_claim_is_narrow(self):
        for row in self.contract()['vectors']:
            before=copy.deepcopy(row['inputs'])
            for _ in range(2):self.assertEqual(row['expected'],calculate(row['family'],row['inputs']))
            self.assertEqual(before,row['inputs'])
            if row['family'] in ('flat-route','simulation-placement'):self.assertEqual(0,row['expected']['rngDraws'])
            else:self.assertNotIn('rngDraws',row['expected'])

    def test_closed_input_domain_rejects_values_instead_of_coercing(self):
        data=self.vector('east-one')['inputs']
        for changes in (dict(width=17),dict(depth=0),dict(width=True),dict(move=-1),dict(move=256),
            dict(move=True),dict(jump=256),dict(origin=[4,4]),dict(enemy=[-1,0]),
            dict(destination=[1.5,2]),dict(moved=1)):
            with self.assertRaises(ValueError):flat_route(dict(data,**changes))
        with self.assertRaises(ValueError):preferred_predecessor('East',['East','East'])
        with self.assertRaises(ValueError):placement(dict(origin=[0,0],destination=[1,0],facing='Bad',movementTaken=False))

    def test_fixture_tampering_and_false_implementation_claims_fail(self):
        for mutation in ('expected','duplicate','family','classification','production','case','count'):
            contract=self.contract()
            if mutation=='expected':contract['vectors'][0]['expected']['originRemainingRange']=0
            elif mutation=='duplicate':contract['vectors'][1]['id']=contract['vectors'][0]['id']
            elif mutation=='family':contract['vectors'][0]['family']='runtime-ai'
            elif mutation=='classification':contract['vectors'][0]['expectationKind']='original-executed'
            elif mutation=='production':contract['productionImplemented']=True
            elif mutation=='case':contract['implementationCases'][0]['status']='passed'
            else:contract['vectorCount']+=1
            with self.assertRaises(ValueError):validate(contract)

    def test_optional_retail_metadata_audit_checks_identity_bounds_and_decoded_values(self):
        # Tiny synthetic prepared files exercise the verifier; these are not retail observations.
        digest=lambda b:hashlib.sha256(b).hexdigest()
        with tempfile.TemporaryDirectory() as directory:
            overlay=Path(directory)/'overlay';exe=Path(directory)/'exe'
            overlay_data=struct.pack('<4I',11,22,33,44);overlay.write_bytes(overlay_data)
            exe_data=bytearray(0x801);exe_data[:8]=b'PS-X EXE'
            struct.pack_into('<II',exe_data,0x18,0x80010000,1);exe_data[-1]=1;exe.write_bytes(exe_data)
            records=dict(spreadPresets=dict(byteLength=16,sha256=digest(overlay_data),address=0x1000,
                loadAddress=0x1000,recordByteLength=16,recordSha256=digest(overlay_data),
                decodedOrder=[dict(address=n) for n in (11,22,33,44)]),
                naturalCost=dict(exeByteLength=len(exe_data),exeSha256=digest(exe_data),loadAddress=0x80010000,
                payloadByteLength=1,address=0x80010000,byteLength=1,recordSha256=digest(bytes([1])),decodedDefaultCost=1))
            verify_retail(overlay,exe,records)
            for field,value in (('sha256','bad'),('address',0x0fff),('decodedOrder',[dict(address=0)]*4)):
                changed=copy.deepcopy(records);changed['spreadPresets'][field]=value
                with self.assertRaises(ValueError):verify_retail(overlay,exe,changed)
            changed=copy.deepcopy(records);changed['naturalCost']['decodedDefaultCost']=2
            with self.assertRaises(ValueError):verify_retail(overlay,exe,changed)


if __name__=='__main__':unittest.main()
