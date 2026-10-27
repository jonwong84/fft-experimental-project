import copy
import json
from pathlib import Path
import unittest
from unittest.mock import patch

from verify_ai_candidate_contract import proposed_candidate, MAX
from verify_ai_score_contract import (FIXTURE, audit, calculate, enemy_relation, hit_accounting,
    proposed_scoring, signed16, state_score, unit_score, validate, verify_retail)

ROOT = Path(__file__).resolve().parents[2]


class AiScoreContractTests(unittest.TestCase):
    def contract(self): return json.loads((ROOT/FIXTURE).read_text(encoding='utf-8'))

    def vector(self,name): return copy.deepcopy(next(r for r in self.contract()['vectors'] if r['id']==name))

    def data(self,name='scored-normal'): return self.vector(name)['inputs']

    def result(self,name):
        row=self.vector(name); return calculate(row['family'],row['inputs'])

    def test_inventory_and_immutable_history_without_source_or_managed_execution(self):
        result=audit(ROOT,check_managed_baseline=True)
        self.assertEqual((44,5,14,101,29),tuple(result[k] for k in ('vectors','families',
            'implementationCasesUnexecuted','preservedFiles','managedBaselineFilesChecked')))
        self.assertEqual(0,result['referenceFunctionsChecked'])
        for k in ('productionImplemented','fullAiExecuted','originalExecuted'): self.assertFalse(result[k])

    def test_classification_duplicate_changed_expected_and_claimed_execution_are_rejected(self):
        for mutation in ('classification','duplicate','expected','implemented'):
            c=self.contract()
            if mutation=='classification': c['vectors'][0]['expectationKind']='original-observation'
            if mutation=='duplicate': c['vectors'][1]['id']=c['vectors'][0]['id']
            if mutation=='expected': c['vectors'][0]['expected']['unitTotal']=0
            if mutation=='implemented': c['productionImplemented']=True
            with self.assertRaises(ValueError): validate(c)

    def test_literal_HP_status_and_equipment_components(self):
        for name,terms in [('full',(128,0,357,485)),('half',(64,0,357,421)),
                ('critical',(25,-32,357,350)),('dead',(0,-192,357,165)),
                ('ushort-low',(0,-32,357,325)),('minimum-maxhp',(128,0,357,485))]:
            self.assertEqual(terms,tuple(self.result(name)[k] for k in ('hpTerm','statusTerm','equipmentTerm','unitTotal')))

    def test_odd_maxHP_rounding_and_large_product_are_integer_not_floating_or_percent(self):
        self.assertEqual((25,350),(self.result('odd-at-critical')['hpTerm'],self.result('odd-at-critical')['unitTotal']))
        self.assertEqual((27,384),(self.result('odd-above-critical')['hpTerm'],self.result('odd-above-critical')['unitTotal']))
        self.assertEqual(128,self.result('ushort-full')['hpTerm'])
        self.assertEqual(8388480,65535*128)  # Source signed32 product remains in range.
        # Equal HP fractions give equal HP terms despite unequal maxima.
        self.assertEqual(0,self.result('unequal-maxhp')['priority'])

    def test_four_historical_score_literals_reconcile_with_ZOD116(self):
        for name,priority in [('balanced',0),('normal-hit',64),('critical-hit',135),('lethal-hit',320)]:
            self.assertEqual(priority,self.result(name)['priority'])
        old=json.loads((ROOT/'docs/fixtures/behavior/ai-decision-contract-v1.json').read_text(encoding='utf-8'))
        for row in old['vectors']:
            if row['family']=='state-score':
                units=[dict(slot=i,hp=u['hp'],maxHp=u['maxHp'],status=u['status'],monster=False,
                    keptSlots=u['keptEquipmentSlots'],enemy=u['enemy']) for i,u in enumerate(row['inputs']['units'])]
                self.assertEqual(row['expected']['priority'],state_score(dict(units=units))['priority'])

    def test_initial_Critical_actor_proves_absolute_state_score_is_not_damage_delta(self):
        before=self.result('initial-actor-critical')['priority']; after=self.result('actor-critical-after-hit')['priority']
        self.assertEqual((-135,-71,64),(before,after,after-before))
        self.assertEqual(-71,self.result('scored-critical-actor')['score']['priority'])
        self.assertEqual(0,self.result('both-critical')['priority'])

    def test_seven_kept_slots_include_empty_entries_and_monsters_have_no_equipment_bonus(self):
        self.assertEqual(357,self.result('seven-empty-kept')['equipmentTerm'])
        self.assertEqual(306,self.result('changed-one-slot')['equipmentTerm'])
        self.assertEqual((0,128),(self.result('monster-not-human')['equipmentTerm'],self.result('monster-not-human')['unitTotal']))
        self.assertEqual(51,self.result('full')['unitTotal']-self.result('changed-one-slot')['unitTotal'])
        # These diagnostic kernels do not admit altered/monster cases to the managed proposal.
        for name in ('unsupported-monster','unsupported-equipment-change'):
            self.assertEqual('UnsupportedScoreProfile',self.result(name)['reason'])

    def test_enemy_sign_uses_original_target_and_current_actor_team_with_self_exception(self):
        for name,enemy in [('different-original-team',True),('same-original-team',False),
                ('initial-team-not-current-target',True),('self-never-enemy',False)]:
            self.assertEqual(dict(enemy=enemy),self.result(name))
        self.assertEqual(dict(enemy=False),enemy_relation(dict(actorCurrentTeam=0x18,targetInitialTeam=0x13,self=False)))

    def test_original_order_is_stable_and_closed_bounds_fit_signed16_without_wrap(self):
        self.assertEqual(self.result('critical-hit'),self.result('reverse-input-order'))
        self.assertEqual(-160,self.result('minimum-closed-priority')['priority'])
        # Exhaust representative consistent HP/status pairs and both source orders.
        for maximum in (1,5,50,51,255,256,65535):
            for ally_hp in set((1,max(1,maximum//5),maximum)):
                for enemy_hp in set((0,1,maximum//5,maximum)):
                    def unit(hp,enemy,slot): return dict(hp=hp,maxHp=maximum,
                        status='Dead' if hp==0 else 'Critical' if hp<=maximum//5 else 'None',
                        monster=False,keptSlots=7,enemy=enemy,slot=slot)
                    units=[unit(ally_hp,False,0),unit(enemy_hp,True,1)]
                    score=state_score(dict(units=units))
                    self.assertTrue(-160<=score['priority']<=320)
                    self.assertEqual(score,state_score(dict(units=list(reversed(units)))))

    def test_signed16_storage_kernel_is_distinct_from_supported_profile_range(self):
        for value,expected in [(32767,32767),(32768,-32768),(-32768,-32768),(-32769,32767),(65535,-1)]:
            self.assertEqual(expected,signed16(value))

    def test_positive_hit_percent_remains_separate_and_mean_truncates_counted_pairs(self):
        for percent in (1,50,100):
            d=self.data(); d['candidate']['hitPercent']=percent
            score=proposed_scoring(d)['score']; self.assertEqual((64,percent),(score['priority'],score['hitPercent']))
        self.assertEqual(dict(valuable=True,countedPairs=2,baseHitPercent=50,additionalDraws=0),
            hit_accounting(dict(accuracies=[50,51,0],valuable=True)))
        self.assertIsNone(hit_accounting(dict(accuracies=[100],valuable=False))['baseHitPercent'])

    def test_NotValuable_has_no_score_and_keeps_four_draws_including_exact_MAX_budget(self):
        d=self.data('not-valuable'); d['candidate']['rngCount']=MAX-4
        result=proposed_scoring(d)
        self.assertEqual(('NotValuable',None,MAX,0),tuple(result[k] for k in
            ('disposition','score','rngCount','additionalScoreDraws')))

    def test_unsupported_score_profiles_reject_before_any_projection_or_draw(self):
        for key,value in [('monster',True),('keptSlots',6),('mp',1),('maxMp',1),('golem',1),('baselineGolem',1)]:
            d=self.data(); d['profile']['units'][1][key]=value
            with patch('verify_ai_score_contract.proposed_candidate',wraps=proposed_candidate) as projection:
                result=proposed_scoring(d); projection.assert_not_called()
            self.assertEqual(('Rejected','UnsupportedScoreProfile',0,1,7,None),tuple(result[k] for k in
                ('disposition','reason','projectionCalls','rngWord','rngCount','score')))
        for key in ('stableOpponents','unchangedTeams','idsMatch'):
            d=self.data();d['profile'][key]=False
            self.assertEqual('UnsupportedScoreProfile',proposed_scoring(d)['reason'])
        for change in (dict(monster=None),dict(keptSlots=None),dict(mp=False),dict(golem=0.0)):
            d=self.data();d['profile']['units'][0].update(change)
            self.assertEqual('UnsupportedScoreProfile',proposed_scoring(d)['reason'])
        d=self.data();del d['profile']['units'][0]['monster']
        self.assertEqual('UnsupportedScoreProfile',proposed_scoring(d)['reason'])

    def test_projection_rejection_and_late_overflow_are_forwarded_without_a_zero_score(self):
        for change,reason in [(dict(terminal=True),'TerminalInput'),(dict(rngCount=MAX-4),'RandomDrawCountExhausted'),
                (dict(elapsedTicks=MAX),'TickCountExhausted')]:
            d=self.data();d['candidate'].update(change);r=proposed_scoring(d)
            self.assertEqual(('Rejected','ProjectionRejected',reason,None),tuple(r[k] for k in
                ('disposition','reason','projectionReason','score')))
            self.assertEqual((d['candidate']['rngWord'],d['candidate']['rngCount']),(r['rngWord'],r['rngCount']))

    def test_fresh_projection_once_and_no_extra_draws_even_at_MAX(self):
        d=self.data();d['candidate']['rngCount']=MAX-5
        expected=proposed_candidate(d['candidate'])
        with patch('verify_ai_score_contract.proposed_candidate',wraps=proposed_candidate) as projection:
            r=proposed_scoring(d);projection.assert_called_once_with(d['candidate'])
        self.assertEqual(('Scored',MAX,0),(r['disposition'],r['rngCount'],r['additionalScoreDraws']))
        self.assertEqual(expected['rngWord'],r['rngWord'])
        d['projectionResult']=dict(disposition='Projected',score=999)
        with self.assertRaisesRegex(ValueError,'External projection'): proposed_scoring(d)

    def test_clock_choices_pick_identity_and_corpse_draws_are_not_direct_score_terms(self):
        before=self.result('scored-normal')
        for changes in (dict(actorCt=99),dict(targetCt=254),dict(actorSlot=1),dict(moved=True)):
            d=self.data();d['candidate'].update(changes);r=proposed_scoring(d)
            self.assertEqual(before['score']['priority'],r['score']['priority']);self.assertEqual(0,r['additionalScoreDraws'])
        d=self.data('scored-ko');d['candidate'].update(actorCt=0,actorSpeed=1,targetCt=0,targetSpeed=255,destination=[1,0])
        r=proposed_scoring(d)
        self.assertEqual((320,112,0),(r['score']['priority'],r['rngCount'],r['additionalScoreDraws']))
        self.assertEqual(165,next(u['unitTotal'] for u in r['score']['contributions'] if u['slot']==1))

    def test_replay_and_all_vectors_leave_input_data_unchanged(self):
        for row in self.contract()['vectors']:
            before=copy.deepcopy(row['inputs']);first=calculate(row['family'],row['inputs'])
            self.assertEqual(first,calculate(row['family'],row['inputs']));self.assertEqual(before,row['inputs'])

    def test_malformed_normalized_HP_status_and_prepared_records_fail_closed(self):
        d=self.data('full')
        for change in (dict(maxHp=0),dict(hp=-1),dict(hp=51),dict(status='Dead'),dict(keptSlots=8)):
            with self.assertRaises(ValueError): unit_score(dict(d,**change))
        import tempfile
        with tempfile.TemporaryDirectory() as directory:
            p=Path(directory)/'bad.exe';p.write_bytes(b'bad')
            with self.assertRaises(ValueError):
                verify_retail(p,[dict(exeByteLength=356352,exeSha256='0'*64,loadAddress=0x80010000,
                    payloadByteLength=354304,address=0x8005ebf0,byteLength=8,recordSha256='0'*64)])


if __name__=='__main__': unittest.main()
