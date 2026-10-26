import copy
import unittest
from verify_basic_skill_window import validate_step, validate_availability


class BasicSkillWindowAuditTests(unittest.TestCase):
    def fixture(self):
        from test_basic_skill_content import BasicSkillAuditTests
        before, after = BasicSkillAuditTests().fixture()
        step = dict(state=after, accepted=True, Restart=False, kind='UseTargetedAbility',
                    command=dict(ActorId=dict(Value='sample:unit/first'), TargetId=dict(Value='sample:unit/second')),
                    events=['ThrowStoneResolvedEvent', 'ThrowStoneKnockbackEvent'])
        return before, step

    def test_accepts_independent_stone_delta(self):
        before, step = self.fixture()
        self.assertEqual(dict(damage=12, attempted=True, moved=True), validate_step(before, step, before))

    def test_requires_matching_resolution_and_push_events(self):
        for events in ([], ['ThrowStoneResolvedEvent']):
            before, step = self.fixture()
            step['events'] = events
            with self.assertRaises(ValueError):
                validate_step(before, step, before)

    def test_selection_and_rejection_must_be_atomic(self):
        for accepted in (None, False):
            before, step = self.fixture()
            step['accepted'] = accepted
            step['events'] = None if accepted is None else []
            with self.assertRaises(ValueError):
                validate_step(before, step, before)
            step['state'] = copy.deepcopy(before)
            self.assertIsNone(validate_step(before, step, before))

    def test_restart_must_restore_all_state(self):
        before, step = self.fixture()
        step.update(Restart=True, command=None)
        with self.assertRaises(ValueError):
            validate_step(before, step, before)
        step['state'] = copy.deepcopy(before)
        self.assertIsNone(validate_step(before, step, before))

    def test_rng_copies_must_match_even_on_selection(self):
        before, step = self.fixture()
        step.update(state=copy.deepcopy(before), accepted=None, events=None)
        step['state']['Turns']['RngState']['DrawCount'] += 1
        with self.assertRaises(ValueError):
            validate_step(before, step, before)

    def test_independent_range_and_availability_reject_tampering(self):
        before, _ = self.fixture()
        a = dict(Value='sample:unit/first'); b = dict(Value='sample:unit/second')
        before['units'][0]['Id'] = a; before['units'][1]['Id'] = b
        before['Turns']['ActiveUnitId'] = a
        step = dict(state=before, SelectedUnit=a, targetedAvailable=True, available=True, targets=[b],
                    range=[dict(X=x, Y=y, Layer=0) for y in range(5) for x in range(7) if 1 <= abs(x-2)+abs(y-2) <= 4])
        validate_availability(step)
        for key, bad in (('range', []), ('targetedAvailable', False), ('targets', []), ('available', False)):
            altered = copy.deepcopy(step); altered[key] = bad
            with self.assertRaises(ValueError):
                validate_availability(altered)
