import copy
import hashlib
import json
from pathlib import Path
import unittest

import verify_world_checkpoint as world


class WorldCheckpointAuditTests(unittest.TestCase):
    def fixture(self, version=2):
        path = Path(__file__).resolve().parents[2] / f'docs/fixtures/saves/synthetic-world-idle-v{version}.json'
        return json.loads(path.read_bytes())

    def encode(self, envelope, rehash=True):
        if rehash:
            envelope['sha256'] = hashlib.sha256(json.dumps(envelope['snapshot'], separators=(',', ':')).encode()).hexdigest()
        return json.dumps(envelope).encode()

    def test_independent_fixture_equivalence(self):
        self.assertEqual(world.snapshot(self.encode(self.fixture(1))), world.snapshot(self.encode(self.fixture(2))))

    def test_checksum_summary_capability_and_profile_mismatches_reject(self):
        for field, value in [('funds', 99), ('rngPolicy', 'seed-on-load'), ('profileId', 'sample:wrong'), ('routeId', 'sample:wrong')]:
            envelope = self.fixture(); envelope['snapshot'][field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                world.snapshot(self.encode(envelope))
        envelope = self.fixture(); envelope['snapshot']['funds'] = 99
        with self.assertRaises(ValueError):
            world.snapshot(self.encode(envelope, False))

    def test_unknown_duplicate_and_oversized_envelopes_reject(self):
        envelope = self.fixture(); envelope['schemaVersion'] = 3
        with self.assertRaises(ValueError): world.snapshot(self.encode(envelope))
        with self.assertRaises(ValueError): world.snapshot(b'{"format":1,"format":2}')
        with self.assertRaises(ValueError): world.snapshot(b' ' * 65537)


if __name__ == '__main__':
    unittest.main()
