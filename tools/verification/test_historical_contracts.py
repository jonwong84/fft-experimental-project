"""The historical audit must survive checkout edits and still detect corrupt history."""
import hashlib
import io
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
import zipfile

from verify_historical_contracts import audit_snapshot, extract_snapshot


class HistoricalContractTests(unittest.TestCase):
    def git(self, *args):
        return subprocess.check_output(['git', '-C', str(self.root), *args], text=True, stderr=subprocess.PIPE).strip()

    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.git('init', '--quiet')
        tools = self.root / 'tools' / 'verification'
        tools.mkdir(parents=True)
        (tools / 'verify_example_contract.py').write_text('''
import hashlib, json
def audit(root, check_managed_baseline=False):
    expected = json.loads((root / 'evidence.json').read_text())
    assert check_managed_baseline
    for name, digest in expected.items():
        assert hashlib.sha256((root / name).read_bytes()).hexdigest() == digest, name
    assert not (root / 'owner-draft.txt').exists()
    return dict(passed=True, managedBaselineFilesChecked=1)
''', encoding='utf-8')
        (self.root / 'managed.cs').write_bytes(b'original code')
        (self.root / 'fixture.json').write_bytes(b'{"damage":25}')
        (self.root / 'evidence.json').write_text(json.dumps({name: hashlib.sha256((self.root / name).read_bytes()).hexdigest()
            for name in ('managed.cs', 'fixture.json')}), encoding='utf-8')
        self.commit()
        self.revision = self.git('rev-parse', 'HEAD')

    def audit(self, revision):
        return audit_snapshot(self.root, revision, ('verify_example_contract',))

    def commit(self):
        self.git('add', 'tools', 'managed.cs', 'fixture.json', 'evidence.json')
        self.git('-c', 'user.name=Historical audit test', '-c', 'user.email=audit-test@example.invalid',
                 '-c', 'commit.gpgsign=false', 'commit', '--quiet', '-m', 'Test snapshot')

    def test_checkout_refactor_fixture_edits_and_owner_drafts_do_not_rewrite_history(self):
        (self.root / 'managed.cs').write_bytes(b'refactored code')
        (self.root / 'fixture.json').write_bytes(b'{"damage":30}')
        (self.root / 'owner-draft.txt').write_bytes(b'preserve this')
        before = self.git('status', '--porcelain', '--untracked-files=all')
        result = self.audit(self.revision)
        self.assertEqual(self.revision, result['revision'])
        self.assertEqual({'verify_example_contract': dict(passed=True, managedBaselineFilesChecked=1)}, result['audits'])
        self.assertFalse(result['currentRuntimeExecuted'])
        self.assertFalse(result['referenceCheckoutChecked'])
        self.assertEqual(before, self.git('status', '--porcelain', '--untracked-files=all'))
        self.assertEqual(b'refactored code', (self.root / 'managed.cs').read_bytes())
        self.assertEqual(b'preserve this', (self.root / 'owner-draft.txt').read_bytes())
        self.assertEqual([], list((self.root / 'scratch').iterdir()))

    def test_corrupt_recorded_fixture_fails_and_does_not_fall_back_to_a_good_revision(self):
        (self.root / 'fixture.json').write_bytes(b'{"damage":99}')
        self.commit()
        with self.assertRaises(subprocess.CalledProcessError) as error:
            self.audit(self.git('rev-parse', 'HEAD'))
        self.assertIn('fixture.json', error.exception.stderr)
        self.assertEqual([], list((self.root / 'scratch').iterdir()))
        self.assertTrue(self.audit(self.revision)['passed'])

    def test_missing_history_fails_without_a_checkout_fallback(self):
        with self.assertRaises(subprocess.CalledProcessError):
            self.audit('does-not-exist')

    def test_export_cannot_write_outside_its_temporary_directory(self):
        data = io.BytesIO()
        with zipfile.ZipFile(data, 'w') as archive:
            archive.writestr('../outside.txt', 'bad')
        with self.assertRaisesRegex(ValueError, 'Unsafe snapshot path'):
            extract_snapshot(data.getvalue(), self.root / 'export')
        self.assertFalse((self.root / 'outside.txt').exists())


if __name__ == '__main__':
    unittest.main()
