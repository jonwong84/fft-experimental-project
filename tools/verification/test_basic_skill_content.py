import copy
import unittest
from verify_basic_skill_content import validate_stone


class BasicSkillAuditTests(unittest.TestCase):
    def fixture(self):
        before = dict(Outcome=0,WinnerId=None,Combatants=[
            dict(UnitId=dict(Value='sample:unit/first'),Hp=50,MaxHp=50,PhysicalAttack=6,Brave=70,ClassPhysicalEvade=0,Status=0,KoCount=0,Facing=0),
            dict(UnitId=dict(Value='sample:unit/second'),Hp=50,MaxHp=50,PhysicalAttack=5,Brave=70,ClassPhysicalEvade=0,Status=0,KoCount=0,Facing=2)],
            Turns=dict(Units=[dict(MovementTaken=False,ActionTaken=False),dict(MovementTaken=False,ActionTaken=False)],
                       RngState=dict(Value=3,DrawCount=0)),
            units=[dict(Position=dict(X=2,Y=2,Layer=0),HasMoved=False),dict(Position=dict(X=3,Y=2,Layer=0),HasMoved=False)],
            RngState=dict(Value=3,DrawCount=0))
        after = copy.deepcopy(before)
        after['Combatants'][1]['Hp'] = 38
        after['units'][1]['Position']['X'] = 4
        after['Turns']['Units'][0]['ActionTaken'] = True
        after['Turns']['RngState'] = after['RngState'] = dict(Value=273505290,DrawCount=7)
        return before,after

    def test_accepts_independent_known_seed_delta(self):
        self.assertEqual(dict(damage=12,attempted=True,moved=True),validate_stone(*self.fixture()))

    def test_rejects_wrong_damage(self):
        before,after=self.fixture(); after['Combatants'][1]['Hp']=44
        with self.assertRaises(ValueError):
            validate_stone(before,after)

    def test_rejects_skipped_or_changed_rng(self):
        before,after=self.fixture(); after['RngState']['DrawCount']=6
        with self.assertRaises(ValueError):
            validate_stone(before,after)

    def test_rejects_dropped_push(self):
        before,after=self.fixture(); after['units'][1]['Position']['X']=3
        with self.assertRaises(ValueError):
            validate_stone(before,after)

    def test_rejects_target_facing_or_choice_mutation(self):
        for facing in (False,True):
            before,after=self.fixture()
            if facing:
                after['Combatants'][1]['Facing']=0
            else:
                after['Turns']['Units'][1]['ActionTaken']=True
            with self.assertRaises(ValueError):
                validate_stone(before,after)

    def test_rejects_unsupported_nonzero_evade_audit_branch(self):
        before,after=self.fixture(); before['Combatants'][1]['ClassPhysicalEvade']=10
        with self.assertRaises(ValueError):
            validate_stone(before,after)
