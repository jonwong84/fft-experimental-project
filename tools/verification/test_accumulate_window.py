import copy
import unittest
from verify_accumulate_window import validate_transition


class AccumulateWindowAuditTests(unittest.TestCase):
    def fixture(self, pa=5):
        before = dict(Combatants=[dict(PhysicalAttack=pa, Hp=50), dict(PhysicalAttack=5, Hp=50)],
                      Turns=dict(Units=[dict(ActionTaken=False, MovementTaken=False)]),
                      RngState=dict(Value=123, DrawCount=1))
        after = copy.deepcopy(before)
        after['Combatants'][0]['PhysicalAttack'] = min(99, pa + 1)
        after['Turns']['Units'][0]['ActionTaken'] = True
        events = ['ImmediateAbilityResolvedEvent']
        if pa < 99:
            events.append('AttackPhysicalAttackChangedEvent')
        return before, dict(state=after, accepted=True, events=events,
                            command=dict(ActorId=dict(Value='sample:unit/first'), AbilityId={}))

    def test_admits_boost_and_consumed_no_change_at_cap(self):
        for pa in (5, 98, 99):
            self.assertTrue(validate_transition(*self.fixture(pa)))

    def test_rejects_wrong_pa(self):
        before, step = self.fixture()
        step['state']['Combatants'][0]['PhysicalAttack'] = 7
        with self.assertRaises(ValueError):
            validate_transition(before, step)

    def test_rejects_rng_or_unrelated_state_changes(self):
        for key in ('Hp', 'rng'):
            before, step = self.fixture()
            if key == 'rng':
                step['state']['RngState']['DrawCount'] += 1
            else:
                step['state']['Combatants'][1][key] -= 1
            with self.assertRaises(ValueError):
                validate_transition(before, step)

    def test_rejects_cap_without_act_or_with_changed_event(self):
        for event in (False, True):
            before, step = self.fixture(99)
            if event:
                step['events'].append('AttackPhysicalAttackChangedEvent')
            else:
                step['state']['Turns']['Units'][0]['ActionTaken'] = False
            with self.assertRaises(ValueError):
                validate_transition(before, step)

    def test_requires_atomic_rejection(self):
        before, step = self.fixture()
        step.update(accepted=False, events=[])
        with self.assertRaises(ValueError):
            validate_transition(before, step)
        step['state'] = copy.deepcopy(before)
        self.assertFalse(validate_transition(before, step))

    def test_requires_resolution_event_and_granted_actor(self):
        for actor in (False, True):
            before, step = self.fixture()
            if actor:
                step['command']['ActorId']['Value'] = 'sample:unit/second'
            else:
                step['events'] = []
            with self.assertRaises(ValueError):
                validate_transition(before, step)
