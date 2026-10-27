import copy
import json
from pathlib import Path
import unittest

from verify_ai_candidate_contract import (FIXTURE, MAX, action_order, first_clock,
    ownership, proposed_candidate, status_result, validate, verify_status_records)

ROOT = Path(__file__).resolve().parents[2]


class AiCandidateContractTests(unittest.TestCase):
    def contract(self): return json.loads((ROOT/FIXTURE).read_text(encoding='utf-8'))

    def vector(self, name):
        return copy.deepcopy(next(r for r in self.contract()['vectors'] if r['id'] == name))

    def data(self, name='stay'): return self.vector(name)['inputs']

    def test_current_description_vectors_are_consistent(self):
        result = validate(self.contract())
        self.assertEqual((44, 5, 16), tuple(result[k] for k in ('vectors','families',
            'implementationCasesUnexecuted')))

    def test_classification_duplicate_and_changed_vector_are_rejected(self):
        for mutation in ('classification','duplicate','expected'):
            c = self.contract()
            if mutation == 'classification': c['vectors'][0]['expectationKind'] = 'original-observation'
            if mutation == 'duplicate': c['vectors'][1]['id'] = c['vectors'][0]['id']
            if mutation == 'expected': c['vectors'][0]['expected']['hp'] = 999
            with self.assertRaises(ValueError): validate(c)

    def test_critical_threshold_uses_integer_floor_and_dead_replaces_critical(self):
        for maximum in (1,4,5,49,50,51,65535):
            for remaining in (0,maximum//5,min(maximum,maximum//5+1)):
                # Keep subtraction within the closed capped-damage envelope.
                initial = min(maximum, remaining+10)
                d = dict(hp=initial,maxHp=maximum,damage=initial-remaining,hasAccuracy=True,
                    status='Critical' if initial <= maximum//5 else 'None',deathCounter=255)
                result = status_result(d)
                self.assertEqual('Dead' if remaining == 0 else 'Critical' if remaining <= maximum//5 else 'None',result['status'])
                self.assertEqual(3 if remaining == 0 else 255,result['deathCounter'])

    def test_overkill_clamps_and_zero_accuracy_skips_status_application(self):
        d = dict(hp=10,maxHp=50,damage=999,hasAccuracy=True,status='Critical',deathCounter=255)
        self.assertEqual(dict(hp=0,status='Dead',deathCounter=3),status_result(d))
        self.assertEqual(dict(hp=10,status='Critical',deathCounter=255),status_result(dict(d,hasAccuracy=False)))

    def test_order_and_failed_act_first_never_moves_or_advances_clock(self):
        d = dict(order='MoveThenAct',origin=[0,0],destination=[1,0],hasAccuracy=True,facing='West')
        self.assertEqual(['PlaceCoordinates','MoveTaken','ProjectAction','ActTaken'],action_order(d)['steps'][:4])
        self.assertEqual(['ProjectAction','ActTaken','PlaceCoordinates','MoveTaken'],action_order(dict(d,order='ActThenMove'))['steps'][:4])
        self.assertEqual(['ProjectAction','RestoreBaseline'],action_order(dict(d,order='ActThenMove',hasAccuracy=False))['steps'])
        self.assertEqual(['PlaceCoordinates','MoveTaken','ProjectAction','RestoreBaseline'],action_order(dict(d,hasAccuracy=False))['steps'])

    def test_pick_unit_precedes_addition_and_ready_flags_clear_without_active_turn(self):
        result = first_clock(self.data('pick-before-add'))
        self.assertEqual((0,1,1,None),(result['elapsedTicks'],result['upkeepDraws'],result['readySlot'],result['activeUnit']))
        self.assertEqual([20,0],[u['ct'] for u in result['units']])
        self.assertFalse(result['units'][1]['moved']); self.assertFalse(result['units'][1]['acted'])
        self.assertTrue(result['units'][0]['moved']); self.assertTrue(result['units'][0]['acted'])

    def test_highest_ct_then_original_slot_tie_is_independent_of_collection_order(self):
        tie = self.data('lower-slot-tie')
        for units in (tie['units'],list(reversed(tie['units']))):
            self.assertEqual(0, first_clock(dict(units=units))['readySlot'])
        self.assertEqual(1,first_clock(self.data('highest-ct'))['readySlot'])
        self.assertEqual(1,first_clock(self.data('highest-ct'))['units'][1]['ct'])

    def test_ct_byte_cap_and_modulo_use_literal_boundaries(self):
        result = first_clock(self.data('byte-cap'))
        self.assertEqual([dict(slot=1,ctBefore=254,ctAfter=54,dead=False)],result['picks'])
        self.assertEqual([1,54],[u['ct'] for u in result['units']])
        self.assertEqual(10,first_clock(self.data('already-ready'))['units'][0]['ct'])

    def test_dead_upkeep_draws_are_retained_and_no_death_counter_decay(self):
        result = first_clock(self.data('dead-picked-without-ready'))
        self.assertEqual((16,2,0),(result['elapsedTicks'],result['upkeepDraws'],result['readySlot']))
        self.assertEqual([True,False],[p['dead'] for p in result['picks']])
        self.assertFalse(result['deathCountersChanged'])
        fast = first_clock(self.data('dead-fast'))
        self.assertEqual((100,101,0),(fast['elapsedTicks'],fast['upkeepDraws'],fast['readySlot']))
        self.assertEqual([0,54],[u['ct'] for u in fast['units']])

    def test_both_candidate_orders_preserve_faces_and_turn_end_refund(self):
        first = proposed_candidate(self.data('move-first')); last = proposed_candidate(self.data('act-first'))
        self.assertEqual(first,last)
        for name,ct in [('stay',35),('move-first',15),('prior-move-stay',15)]:
            outcome = proposed_candidate(self.data(name))['outcome']
            self.assertEqual((ct,'West','East'),(outcome['turnEndCt'],outcome['facing'],outcome['targetFacing']))
        self.assertEqual(60,proposed_candidate(dict(self.data(),actorCt=99))['outcome']['turnEndCt'])

    def test_partial_accuracy_is_full_damage_and_lethal_candidate_remains_admitted(self):
        for name,hp,status in [('partial-accuracy',25,'None'),('candidate-critical',10,'Critical'),('candidate-ko',0,'Dead')]:
            result = proposed_candidate(self.data(name))
            self.assertEqual('Projected',result['disposition'])
            self.assertEqual((hp,status),(result['outcome']['target']['hp'],result['outcome']['target']['status']))
            self.assertFalse(result['outcome']['terminalPolicyApplied'])
        lethal = proposed_candidate(self.data('candidate-ko-fast'))
        self.assertEqual((105,112,3),(lethal['rngDraws'],lethal['rngCount'],lethal['outcome']['target']['deathCounter']))
        self.assertTrue(lethal['outcome']['targetKnockedOut'])

    def test_literal_bios_draws_and_certain_evade_are_distinct_from_rejection(self):
        result = proposed_candidate(self.data())
        self.assertEqual((4182499122,12,5),(result['rngWord'],result['rngCount'],result['rngDraws']))
        result = proposed_candidate(self.data('not-valuable'))
        self.assertEqual(('NotValuable',3295386429,11,4,None),tuple(result[k] for k in ('disposition','rngWord','rngCount','rngDraws','outcome')))
        # No clock/tick publication is needed on the zero-accuracy path.
        self.assertEqual('NotValuable',proposed_candidate(dict(self.data('not-valuable'),elapsedTicks=MAX))['disposition'])

    def test_terminal_unavailable_profile_and_candidate_rejections_precede_draws(self):
        for name,reason in [('terminal-input','TerminalInput'),('action-used','UnavailableAction'),
                ('unsupported-profile','UnsupportedProfile'),('invalid-placement','InvalidCandidate'),('move-used','InvalidCandidate')]:
            result = proposed_candidate(self.data(name))
            self.assertEqual(('Rejected',reason,1,7,0,None),tuple(result[k] for k in
                ('disposition','reason','rngWord','rngCount','rngDraws','outcome')))
        self.assertEqual('TerminalInput',proposed_candidate(dict(self.data(),actorStatus='Dead'))['reason'])
        self.assertEqual('TerminalInput',proposed_candidate(dict(self.data(),targetStatus='Dead',targetHp=0))['reason'])

    def test_early_and_late_rng_or_tick_exhaustion_are_atomic(self):
        for change,reason in [(dict(rngCount=MAX-3),'RandomDrawCountExhausted'),
                (dict(rngCount=MAX-4),'RandomDrawCountExhausted'),(dict(elapsedTicks=MAX),'TickCountExhausted')]:
            data = dict(self.data(),**change); before = copy.deepcopy(data)
            result = proposed_candidate(data)
            self.assertEqual(data,before); self.assertEqual(reason,result['reason'])
            self.assertEqual((data['rngWord'],data['rngCount'],0,None),tuple(result[k] for k in ('rngWord','rngCount','rngDraws','outcome')))
        # A supported zero-accuracy candidate fits exactly four remaining draws.
        self.assertEqual(MAX,proposed_candidate(dict(self.data('not-valuable'),rngCount=MAX-4))['rngCount'])
        # Already-ready target requires no tick and therefore fits MAX ticks.
        self.assertEqual('Projected',proposed_candidate(dict(self.data('candidate-target-ready'),elapsedTicks=MAX))['disposition'])

    def test_discarded_state_keeps_explicit_decision_rng_and_replay_is_stable(self):
        for name in ('discard-carry-replay','discard-noaccuracy-carry'):
            data = self.data(name); before = copy.deepcopy(data); result = ownership(data)
            self.assertEqual(data,before); self.assertTrue(result['replayEqual'])
            self.assertFalse(result['discardedStateReused']); self.assertTrue(result['carriedDecisionRng'])
            self.assertEqual(result['first']['rngCount']+result['second']['rngDraws'],result['second']['rngCount'])
            self.assertNotEqual(result['first']['rngWord'],result['second']['rngWord'])
        self.assertEqual(proposed_candidate(self.data()),proposed_candidate(self.data()))

    def test_source_clock_rejects_quick_zero_speed_and_no_living_survivor(self):
        for key,value in [('ct',255),('speed',0),('status','Stop')]:
            data = self.data('lower-slot-tie'); data['units'][0][key] = value
            with self.assertRaises(ValueError): first_clock(data)
        data = self.data('dead-fast'); data['units'][0]['status'] = 'Dead'
        with self.assertRaises(ValueError): first_clock(data)

    def test_all_supported_vectors_preserve_inputs_and_choice_state_is_hypothetical(self):
        from verify_ai_candidate_contract import calculate
        for vector in self.contract()['vectors']:
            before = copy.deepcopy(vector['inputs'])
            calculate(vector['family'],vector['inputs'])
            self.assertEqual(before,vector['inputs'])
        result = proposed_candidate(self.data('second-actor'))
        self.assertEqual(0,result['outcome']['clock']['readySlot'])
        self.assertTrue(result['inputUnchanged']); self.assertTrue(result['outcome']['clockGlobalsRestored'])

    def test_status_metadata_decoder_checks_header_and_hash_before_any_record_fact(self):
        # Malformed metadata must fail before indexing a status record.
        import tempfile
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)/'bad.exe'; path.write_bytes(b'not a game file')
            record = dict(exeByteLength=356352,exeSha256='0'*64,loadAddress=0x80010000,
                payloadByteLength=354304,address=0x80065de4,byteLength=640,recordSha256='0'*64,facts={})
            with self.assertRaises(ValueError): verify_status_records(path,record)


if __name__ == '__main__': unittest.main()
