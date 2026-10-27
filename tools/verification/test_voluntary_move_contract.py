import copy
import json
from pathlib import Path
import unittest

from verify_voluntary_move_contract import (FIXTURE, MAX, calculate, confirm, proposed_finish,
    proposed_move, validate, walk)
from verify_flat_movement_contract import flat_route

ROOT = Path(__file__).resolve().parents[2]


class VoluntaryMoveContractTests(unittest.TestCase):
    def contract(self): return json.loads((ROOT/FIXTURE).read_text(encoding='utf-8'))

    def vector(self, name):
        return copy.deepcopy(next(row for row in self.contract()['vectors'] if row['id']==name))

    def result(self, name):
        row=self.vector(name)
        return calculate(row['family'],row['inputs'])

    def test_current_description_vectors_are_consistent(self):
        result=validate(self.contract())
        self.assertEqual((60,6,18),(result['vectors'],result['families'],result['implementationCasesUnexecuted']))

    def test_walk_uses_literal_source_byte_mapping_and_ACTION_EXECUTE_preserves_facing(self):
        for code,face in ((0,'East'),(64,'West'),(128,'South'),(192,'North')):
            for initial in ('East','North','West','South'):
                for action in (False,True):
                    self.assertEqual(dict(facing=initial if action else face,facingWritten=not action,rngDraws=0),
                                     walk(dict(packedSteps=[code],incomingFacing=initial,actionExecute=action)))
        self.assertEqual('East',self.result('diagonal-north-east')['facing'])
        self.assertEqual('East',self.result('straight-east')['facing'])

    def test_confirm_publishes_tile_facing_before_Move_and_optional_turn_end(self):
        first=self.result('confirm-move-first');last=self.result('confirm-after-act')
        self.assertEqual(['TileAndFacing','MoveTaken','PostMoveEvents'],first['order'])
        self.assertEqual(['TileAndFacing','MoveTaken','TurnEnd','PostMoveEvents'],last['order'])
        self.assertEqual(([1,1],'East',True,False,15,True),tuple(first[k] for k in ('position','facing','moved','acted','ct','hasTurn')))
        self.assertEqual(([1,1],'East',True,True,15,False),tuple(last[k] for k in ('position','facing','moved','acted','ct','hasTurn')))
        self.assertEqual(60,self.result('confirm-after-act-cap60')['ct'])
        self.assertEqual(99,self.result('move-first-retains-99')['ct'])

    def test_source_cancel_restores_tile_and_facing_without_consuming_a_choice(self):
        for name,acted in [('cancel-restores-before-move',False),('cancel-after-act-keeps-choice',True)]:
            result=self.result(name)
            self.assertEqual(([0,0],'West',False,acted,15,True),tuple(result[k] for k in ('position','facing','moved','acted','ct','hasTurn')))
            self.assertEqual(['RestoreTileAndFacing'],result['order'])

    def test_turn_end_caps_once_and_repeat_call_does_not_refund_or_recap(self):
        for ct in range(100):
            for moved,acted in ((False,False),(False,True),(True,False),(True,True)):
                result=calculate('source-clock',dict(ct=ct,moved=moved,acted=acted,hasTurn=True))
                self.assertEqual(min(60,ct+20*(not moved)+20*(not acted)),result['ct'])
                self.assertFalse(result['hasTurn']);self.assertEqual(0,result['rngDraws'])
                self.assertEqual(result,calculate('source-clock',{k:v for k,v in result.items() if k!='rngDraws'}))
        self.assertEqual(99,self.result('repeat-end-no-cap')['ct'])

    def test_AI_preview_updates_coordinates_without_execution_heading(self):
        preview=self.result('preview-move')
        self.assertEqual(dict(position=[1,1],facing='West',movementTaken=True,rngDraws=0),preview)
        self.assertEqual('East',self.result('proposed-move-first')['facing'])
        self.assertFalse(self.result('preview-stay')['movementTaken'])
        self.assertTrue(self.result('preview-moved-stay')['movementTaken'])

    def test_proposed_both_orders_have_explicit_events_revision_and_receipt_binding(self):
        first=self.result('proposed-move-first');last=self.result('proposed-act-then-move')
        self.assertEqual(['Movement','PathCompleted','MoveConsumed'],first['events'])
        self.assertEqual(['Movement','PathCompleted','MoveConsumed','TurnEnded'],last['events'])
        self.assertEqual((11,False,True,None),(first['revision'],first['pending'],first['hasTurn'],first['receiptBinding']))
        self.assertEqual((11,True,False,'FullySettledSnapshotAndLineage'),
                         (last['revision'],last['pending'],last['hasTurn'],last['receiptBinding']))
        self.assertEqual([[0,0],[0,1],[1,1]],first['path'])
        self.assertEqual([192,0],first['packedSteps'])
        self.assertEqual(60,self.result('proposed-act-then-move-cap60')['ct'])
        self.assertEqual('West',self.result('proposed-second-actor')['facing'])

    def test_proposed_guard_precedence_is_distinct_from_geometric_query(self):
        data=self.vector('proposed-pending-precedes-all')['inputs']
        for key,value,reason in [('pending',False,'RevisionExhausted'),('revision',10,'UnknownUnit'),
            ('knownActor',True,'InactiveUnit'),('activeActor',True,'MovementAlreadyTaken'),('moved',False,'UnsupportedProfile')]:
            data[key]=value
            self.assertEqual(reason,proposed_move(data)['reason'])
        self.assertEqual('FacingPending',self.result('proposed-pending-precedes-all')['reason'])

    def test_failed_proposed_moves_have_no_partial_path_events_or_rng_progress(self):
        names={'reject-move-already-taken':'MovementAlreadyTaken','reject-inactive':'InactiveUnit',
            'reject-unknown':'UnknownUnit','reject-pending':'FacingPending','reject-revision-exhausted':'RevisionExhausted',
            'reject-unsupported-profile':'UnsupportedProfile','reject-origin':'InvalidDestination',
            'reject-occupied':'InvalidDestination','reject-over-budget':'InvalidDestination'}
        for name,reason in names.items():
            row=self.vector(name);result=proposed_move(row['inputs'])
            self.assertEqual(reason,result['reason']);self.assertTrue(result['unchanged'])
            self.assertEqual([],result['events']);self.assertIsNone(result['path'])
            self.assertEqual((57,500,0),(result['rngWord'],result['rngCount'],result['rngDraws']))

    def test_post_push_uses_current_enemy_and_exact_detour_final_heading(self):
        result=self.result('proposed-detour-heading-south')
        self.assertEqual([[0,1],[0,2],[1,2],[2,2],[2,1]],result['path'])
        self.assertEqual(([192,0,0,128],'South'),(result['packedSteps'],result['facing']))
        self.assertEqual([[0,1],[1,1]],self.result('proposed-post-push-old-tile-free')['path'])
        self.assertEqual('InvalidDestination',self.result('proposed-post-push-new-tile-blocked')['reason'])

    def test_geometric_moved_query_is_not_authorization_for_a_second_Move(self):
        data=self.vector('reject-move-already-taken')['inputs']
        route=flat_route(dict(data['route'],moved=True))
        self.assertEqual([192,0],route['packedSteps'])
        self.assertEqual([[0,0]],route['aiOrigins'])
        self.assertEqual('MovementAlreadyTaken',proposed_move(data)['reason'])

    def test_completion_requires_receipt_identity_snapshot_lineage_revision_and_actor(self):
        base=self.vector('finish-keep-path-heading')['inputs']
        for key,reason in [('pending','MissingCompletion'),('identityMatches','StaleCompletion'),
            ('snapshotMatches','StaleCompletion'),('lineageMatches','StaleCompletion'),
            ('revisionMatches','StaleCompletion'),('actorMatches','WrongActor')]:
            result=proposed_finish(dict(base,**{key:False}))
            self.assertEqual(reason,result['reason']);self.assertTrue(result['unchanged']);self.assertEqual([],result['events'])

    def test_Finish_keep_and_same_facing_consume_once_without_repeating_ct_or_move(self):
        for name,face in [('finish-keep-path-heading','East'),('finish-fixed-north','North'),('finish-same-face-consumes','East')]:
            result=self.result(name)
            self.assertEqual(dict(accepted=True,facing=face,pending=False,ct=15,events=['FacingSettled'],
                                  receiptConsumed=True,ctAppliedAgain=False,rngDraws=0),result)
        data=self.vector('finish-keep-path-heading')['inputs'];data['pending']=False
        self.assertEqual('MissingCompletion',proposed_finish(data)['reason'])

    def test_post_KO_move_admission_and_facing_only_completion_are_separate_limits(self):
        for name in ('reject-dead-enemy','reject-dead-actor'):
            self.assertEqual('UnsupportedProfile',self.result(name)['reason'])
        data=self.vector('finish-after-KO-fixed')['inputs']
        self.assertTrue(proposed_finish(data)['accepted'])
        self.assertEqual('East',proposed_finish(dict(data,choice='KeepCurrent'))['facing'])
        self.assertEqual('NoLivingEnemy',proposed_finish(dict(data,choice='ChooseClosedEnemy'))['reason'])
        self.assertEqual('North',proposed_finish(dict(data,choice='ChooseClosedEnemy',enemyLiving=True))['facing'])

    def test_counter_boundaries_follow_existing_wrapper_and_zero_draw_move(self):
        result=self.result('proposed-exhausted-rng-still-moves')
        self.assertEqual((True,0xffffffff,MAX,0),(result['accepted'],result['rngWord'],result['rngCount'],result['rngDraws']))
        for name,pending in [('proposed-last-revision-move',False),('proposed-last-revision-completes',True)]:
            result=self.result(name);self.assertEqual(MAX,result['revision']);self.assertEqual(pending,result['pending'])
        self.assertEqual('RevisionExhausted',self.result('finish-revision-exhausted')['reason'])
        for revision in (-1,MAX+1,True):
            with self.assertRaises(ValueError):proposed_move(dict(self.vector('proposed-move-first')['inputs'],revision=revision))

    def test_descriptions_are_repeatable_and_never_mutate_inputs(self):
        for row in self.contract()['vectors']:
            before=copy.deepcopy(row['inputs'])
            for _ in range(2):self.assertEqual(row['expected'],calculate(row['family'],row['inputs']))
            self.assertEqual(before,row['inputs'])
            self.assertEqual(0,row['expected']['rngDraws'])

    def test_source_helpers_reject_untraced_packed_steps_and_confirmation_domains(self):
        for steps in ([],[1],[32],[0xfe],[0]*125,[True]):
            with self.assertRaises(ValueError):walk(dict(packedSteps=steps,incomingFacing='East',actionExecute=False))
        data=self.vector('confirm-move-first')['inputs']
        for change in (dict(ct=-1),dict(ct=100),dict(walkFacing='Bad')):
            with self.assertRaises(ValueError):confirm(dict(data,**change))

    def test_tampering_classification_and_false_production_claims_reject(self):
        for mutation in ('expected','duplicate','family','classification','production','case','count'):
            contract=self.contract()
            if mutation=='expected':contract['vectors'][0]['expected']['facing']='West'
            elif mutation=='duplicate':contract['vectors'][1]['id']=contract['vectors'][0]['id']
            elif mutation=='family':contract['vectors'][0]['family']='runtime-move'
            elif mutation=='classification':contract['vectors'][-1]['expectationKind']='source-derived-closed-domain'
            elif mutation=='production':contract['productionImplemented']=True
            elif mutation=='case':contract['implementationCases'][0]['status']='passed'
            else:contract['vectorCount']+=1
            with self.assertRaises(ValueError):validate(contract)


if __name__=='__main__':unittest.main()
