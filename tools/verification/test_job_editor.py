import copy
import unittest
from verify_job_editor import validate_copy


class EditorAuditTests(unittest.TestCase):
    def setUp(self):
        self.source = dict(format='fft.content-metadata', schemaVersion=1, packageId='sample:package/base',
            profileId='sample:profile/demo', kind='baseline', requiredBaselinePackageId=None,
            jobs=[dict(id='sample:job/recruit', primarySkillsetId='sample:skillset/practice', baseMove=1,
                       baseJump=2, steppingStone=False, innateAbilityIds=['sample:ability/a'])],
            skillsets=[dict(id='sample:skillset/practice', slots=[dict(index=0, abilityId=None),
                       dict(index=3, abilityId='sample:ability/a'), dict(index=1, abilityId='sample:ability/a')])],
            abilities=[dict(id='sample:ability/a', jpCost=50, unknownTypeBitsRaw=16, aiFlagsRaw=[1, 2, 3, 4])])
        self.authored = copy.deepcopy(self.source)
        self.authored.update(packageId='editor.demo:package/copy', kind='addon', requiredBaselinePackageId=self.source['packageId'])
        self.authored['jobs'][0].update(id='editor.demo:job/recruit', primarySkillsetId='editor.demo:skillset/practice',
            baseMove=2, baseJump=4, innateAbilityIds=['editor.demo:ability/a'])
        self.authored['skillsets'][0]['id'] = 'editor.demo:skillset/practice'
        for slot in self.authored['skillsets'][0]['slots']:
            if slot['abilityId']: slot['abilityId'] = 'editor.demo:ability/a'
        self.authored['abilities'][0].update(id='editor.demo:ability/a', jpCost=123)

    def test_accepts_supported_copy_with_order_empty_and_repeated_references(self):
        validate_copy(self.source, self.authored)

    def test_rejects_opaque_metadata_mutation(self):
        self.authored['abilities'][0]['unknownTypeBitsRaw'] = 0
        with self.assertRaisesRegex(ValueError, 'unsupported metadata'):
            validate_copy(self.source, self.authored)

    def test_rejects_reference_reordering(self):
        self.authored['skillsets'][0]['slots'].reverse()
        with self.assertRaisesRegex(ValueError, 'ordered references'):
            validate_copy(self.source, self.authored)

    def test_rejects_wrong_baseline_binding(self):
        self.authored['requiredBaselinePackageId'] = 'sample:package/other'
        with self.assertRaisesRegex(ValueError, 'binding'):
            validate_copy(self.source, self.authored)
