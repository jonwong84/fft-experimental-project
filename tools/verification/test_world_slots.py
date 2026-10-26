import unittest
import hashlib
import json
import tempfile
from pathlib import Path
from verify_world_slots import validate_modal, storage


class WorldSlotAuditTests(unittest.TestCase):
    def test_confirmation_requires_cancel_focus(self):
        view = dict(Screen=8, Focus=7, Entries=[dict(Action=7), dict(Action=8)])
        validate_modal(view, 8)
        view['Focus'] = 8
        with self.assertRaisesRegex(ValueError, 'Cancel'): validate_modal(view, 8)

    def test_confirmation_rejects_parent_actions(self):
        view = dict(Screen=10, Focus=7, Entries=[dict(Action=7), dict(Action=8), dict(Action=3)])
        with self.assertRaisesRegex(ValueError, 'parent'): validate_modal(view, 10)

    def test_storage_requires_preserved_damaged_primary_and_identical_recovered_checkpoint(self):
        fixture = Path(__file__).resolve().parents[2] / 'docs/fixtures/saves/synthetic-world-idle-v2.json'
        envelope = json.loads(fixture.read_text()); state = envelope['snapshot']
        state.update(locationId='sample:location/hill', funds=93, elapsedSeconds=53)
        state['summary'].update(locationId=state['locationId'], funds=93, elapsedSeconds=53)
        envelope['sha256'] = hashlib.sha256(json.dumps(state, separators=(',', ':')).encode()).hexdigest()
        raw = json.dumps(envelope).encode()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for i in range(1, 11): (root / f'slot-{i:02}.lock').write_bytes(b'')
            (root / 'slot-01.bak').write_bytes(raw); (root / 'slot-02.json').write_bytes(raw)
            (root / 'slot-01.json').write_bytes(b'{"corrupt":true}'); storage(root)
            (root / 'slot-01.json').write_bytes(raw)
            with self.assertRaisesRegex(ValueError, 'damaged primary'): storage(root)
