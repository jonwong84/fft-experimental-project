"""Authored handoff-receipt negatives; no original assets, frames or game execution."""
import copy
import json
import unittest

import verify_intro_battle as tool


def receipt():
    initial = {'ct': [0, 0], 'rng': {'seed': 1, 'draws': 0}}
    digest = tool.v.sha(json.dumps(initial, separators=(',', ':')).encode())
    return dict(format='fft.introduction-battle-capture', schemaVersion=1,
                definition={'GeneralHold': 0, 'ReadyHold': 0}, circleTicks=[], availabilityTick=42,
                availabilityPublications=1, battleFrozenThroughHandoff=True, transitionSampleForwarded=False,
                resourcesRebuiltDuringIntroduction=True, focusDuringIntroduction=True, focusAfterHandoff=True,
                heldInputBoundaryChecked=True, rendererTaskAcknowledgments=0, initialBattle=initial,
                captures=[dict(tick=i, file=f'intro/tick-{i:03}.png', Exited=i == 42,
                               battleAvailable=i == 42, battleSha256=digest, journalEntries=0) for i in range(43)],
                finalIntroduction={'BattleAvailable': True, 'Introduction': {'Event': {'Exited': True, 'Tasks': {'LiveTasks': 0}}}},
                introductionTraceSha256='a'*64)


class IntroBattleAuditTests(unittest.TestCase):
    def test_only_event_exit_opens_ownership_once(self):
        self.assertEqual(42, tool.boundary(receipt())['eventExit'])

    def test_early_late_and_duplicate_handoffs_reject(self):
        for key, value in [('availabilityTick', 40), ('availabilityTick', 41), ('availabilityTick', 43),
                           ('availabilityPublications', 0), ('availabilityPublications', 2), ('availabilityTick', True)]:
            d=receipt(); d[key]=value; self.assertRaises(ValueError, tool.boundary, d)

    def test_child_or_dispatcher_visibility_cannot_open_frame_gate(self):
        for tick in (0, 39, 40, 41):
            for key in ('Exited', 'battleAvailable'):
                d=receipt(); d['captures'][tick][key]=True; self.assertRaises(ValueError, tool.boundary, d)

    def test_changed_battle_or_queued_command_rejects(self):
        for key, value in [('battleSha256', 'b'*64), ('journalEntries', 1), ('journalEntries', False)]:
            d=receipt(); d['captures'][-1][key]=value; self.assertRaises(ValueError, tool.boundary, d)

    def test_presentation_input_or_renderer_shortcuts_reject(self):
        for key, value in [('transitionSampleForwarded', True), ('rendererTaskAcknowledgments', 1),
                           ('heldInputBoundaryChecked', False), ('focusAfterHandoff', False),
                           ('resourcesRebuiltDuringIntroduction', False)]:
            d=receipt(); d[key]=value; self.assertRaises(ValueError, tool.boundary, d)

    def test_incomplete_capture_wrong_path_and_live_final_worker_reject(self):
        for kind in ('count', 'path', 'tick', 'live', 'terminal'):
            d=receipt()
            if kind=='count': d['captures'].pop()
            if kind=='path': d['captures'][0]['file']='../outside.png'
            if kind=='tick': d['captures'][0]['tick']=True
            if kind=='live': d['finalIntroduction']['Introduction']['Event']['Tasks']['LiveTasks']=1
            if kind=='terminal': d['finalIntroduction']['Introduction']['Event']['Exited']=False
            self.assertRaises(ValueError, tool.boundary, d)

    def test_audit_is_read_only_and_old_format_is_not_accepted(self):
        d=receipt(); before=copy.deepcopy(d); tool.boundary(d); self.assertEqual(before,d)
        d['format']='fft.banner-ready-sequence-capture'; self.assertRaises(ValueError, tool.boundary, d)


if __name__ == '__main__':
    unittest.main()
