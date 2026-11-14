import copy
import json
import unittest
from verify_spell_list_integration import FIXTURE, general, human_list, insert, rank, usage, validate, walk


class SpellListIntegrationTests(unittest.TestCase):
    def fixture(self):
        return json.loads(FIXTURE.read_text(encoding='utf-8'))

    def test_source_literals_preserve_the_input(self):
        document = self.fixture(); before = copy.deepcopy(document)
        self.assertEqual(validate(document)['total'], 20)
        self.assertEqual(document, before)

    def test_human_full_list_preserves_attack_duplicate_slots_and_frog(self):
        rows = self.fixture()['lists']
        self.assertEqual(human_list(rows[0]['input'])['entries'],
                         ['attack', 'primary:1:47', 'secondary:5:47', 'frog'])
        result = human_list(rows[2]['input'])
        self.assertEqual(len(result['entries']), 34)
        self.assertFalse(result['terminatorWritten'])

    def test_sentinel_and_capacity_stop_before_unread_storage(self):
        self.assertEqual(walk(self.fixture()['walks'][0]['input'])['outcomeCalls'], 3)
        data = dict(rows=[[1, 0, False] for _ in range(34)] + ['unread-tail'])
        result = walk(data)
        self.assertEqual(result['outcomeCalls'], 34)
        self.assertEqual(result['termination'], 'capacity')
        self.assertFalse(any(result['eligible']))

    def test_initialization_and_decision_completion_are_distinct_phases(self):
        rows = self.fixture()['usages']
        self.assertEqual(usage(rows[0]['input']), dict(initialized=248, completed=248))
        self.assertEqual(usage(rows[2]['input']), dict(initialized=20, completed=148))
        self.assertEqual(usage(rows[-1]['input']), dict(initialized=130, completed=2))
        changed = dict(rows[0]['input'], selfEffectNone=True)
        self.assertEqual(usage(changed)['completed'], 249)

    def test_general_does_not_dispatch_a_complement_list_or_rebuild_per_entry(self):
        result = general(self.fixture()['general'][0]['input'])
        self.assertEqual(result['listScenarios'], [1])
        self.assertEqual(result['baselineScenario'], 0)
        self.assertEqual(result['fallbackScenario'], 0)
        self.assertEqual(result['scenarioBuilds'], 1)

    def test_baseline_gate_stable_duplicate_insertion_and_full_tail_gate(self):
        rows = self.fixture()['insertions']
        self.assertEqual(insert(rows[0]['input'])['indices'], [None, 0])
        self.assertEqual(insert(rows[1]['input'])['slots'][:4], ['higher-hit', 'closer', 'first', 'second'])
        self.assertEqual(insert(rows[2]['input'])['indices'][-2:], [None, 7])
        self.assertGreater(rank(['marker', 1, -32768, 0, 0]), rank(['priority', 0, 32767, 255, 0]))

    def test_unproved_reset_payload_tie_and_nonordinary_or_short_lists_reject(self):
        with self.assertRaisesRegex(ValueError, 'Reset-coordinate'):
            insert(dict(baseline=[0, -32768, 0], bests=[['stale', 0, -32767, 0, 0]]))
        with self.assertRaises(ValueError):
            human_list(dict(self.fixture()['lists'][0]['input'], cleanDefaultHuman=False))
        with self.assertRaises(ValueError):
            walk(dict(rows=[[1, 0, True]]))

    def test_changed_literal_results_flags_widths_and_claims_reject(self):
        for change in [lambda d: d['general'][0]['expected']['listScenarios'].append(2),
                       lambda d: d['lists'][0]['expected']['entries'].reverse(),
                       lambda d: d['usages'][0]['input'].update(raw=256),
                       lambda d: d['usages'][0]['input'].update(trajectory=1),
                       lambda d: d['insertions'][0]['input']['bests'][0].__setitem__(2, 32768),
                       lambda d: d.update(runtimeImplemented=True)]:
            document = self.fixture(); change(document)
            with self.assertRaises(ValueError):
                validate(document)
