"""Audit authored intro pixels/ownership plus unchanged battle outputs; no original execution."""
import argparse
import json
from pathlib import Path
import re

import verify_banner_ready_sequence as sequence

v = sequence.v
paths = sequence.paths
BATTLE_PNGS = ('start.png', 'rejected.png', 'moves.png', 'targets.png', 'finished.png', 'terminal-rejected.png')


def boundary(receipt):
    v.require(receipt['format'] == 'fft.introduction-battle-capture' and type(receipt['schemaVersion']) is int
              and receipt['schemaVersion'] == 1, 'Unsupported introduction/battle receipt')
    times = sequence.timeline(receipt['definition']['GeneralHold'], receipt['definition']['ReadyHold'], receipt['circleTicks'])
    exit_tick = times['eventExit']
    v.integer(receipt['availabilityTick'], 0, 4096, 'availability tick')
    v.require(type(receipt['availabilityPublications']) is int and receipt['availabilityPublications'] == 1
              and receipt['availabilityTick'] == exit_tick, 'Ownership opened early, late or repeatedly')
    for flag in ('battleFrozenThroughHandoff', 'resourcesRebuiltDuringIntroduction', 'focusDuringIntroduction',
                 'focusAfterHandoff', 'heldInputBoundaryChecked'):
        v.require(receipt[flag] is True, 'Missing boundary check: ' + flag)
    v.require(receipt['transitionSampleForwarded'] is False and type(receipt['rendererTaskAcknowledgments']) is int
              and receipt['rendererTaskAcknowledgments'] == 0, 'Transition sample or renderer completed work')
    initial = json.dumps(receipt['initialBattle'], separators=(',', ':'), ensure_ascii=True).encode('utf-8')
    digest = v.sha(initial)
    v.require(type(receipt['captures']) is list and len(receipt['captures']) == exit_tick + 1, 'Incomplete intro captures')
    for tick, frame in enumerate(receipt['captures']):
        v.integer(frame['tick'], 0, 4096, 'capture tick')
        v.require(frame['tick'] == tick and frame['file'] == f'intro/tick-{tick:03}.png', 'Unexpected capture order/path')
        v.require(frame['Exited'] is (tick == exit_tick) and frame['battleAvailable'] is (tick == exit_tick),
                  'Image/worker completion was confused with event ownership')
        v.require(frame['battleSha256'] == digest and type(frame['journalEntries']) is int and frame['journalEntries'] == 0,
                  'Battle changed before or during handoff')
    final = receipt['finalIntroduction']
    v.require(final['BattleAvailable'] is True and final['Introduction']['Event']['Exited'] is True
              and final['Introduction']['Event']['Tasks']['LiveTasks'] == 0, 'Nonterminal ownership handoff')
    v.require(re.fullmatch('[0-9a-f]{64}', receipt['introductionTraceSha256']) is not None, 'Missing intro trace identity')
    return times


def verify(capture, general, ready, battle_reference):
    v.require((capture / 'check.json').stat().st_size <= 4 * 1024 * 1024, 'Receipt exceeds budget')
    receipt = json.loads((capture / 'check.json').read_bytes(), object_pairs_hook=v.unique)
    times = boundary(receipt)
    # Adapt only metadata in memory. The old public parser still rejects this format.
    canonical = dict(definition=receipt['definition'], circleTicks=receipt['circleTicks'],
                     captures=[dict(frame, file=frame['file'][6:]) for frame in receipt['captures']],
                     resourcesRebuiltAtTick5=receipt['resourcesRebuiltDuringIntroduction'],
                     finalState=receipt['finalIntroduction']['Introduction'])
    pixels = sequence.verify_sequence(capture / 'intro', general, ready, canonical)
    v.require((battle_reference / 'check.json').stat().st_size <= 1024 * 1024, 'Battle reference receipt exceeds budget')
    previous = json.loads((battle_reference / 'check.json').read_bytes(), object_pairs_hook=v.unique)
    battle = receipt['battle']
    for field in ('accepted', 'rejected', 'attempts', 'inputs', 'final', 'finalSha256',
                  'headlessMatched', 'replayMatched', 'renderingPreservedState', 'resizeChecked'):
        v.require(battle[field] == previous[field], 'Existing battle result changed: ' + field)
    v.require(battle['accepted'] == 12 and battle['rejected'] == 2 and battle['headlessMatched'] is True
              and battle['replayMatched'] is True, 'Battle/replay did not finish')
    repeated = []
    for name in BATTLE_PNGS:
        raw = (capture / name).read_bytes()
        v.require(raw == (battle_reference / name).read_bytes(), 'Historical battle PNG differs: ' + name)
        repeated.append(dict(file=name, bytes=len(raw), sha256=v.sha(raw)))
    return dict(format='fft.intro-battle-independent-check', passed=True, timing=times,
                introFrames=pixels['frames'], comparedPixels=pixels['comparedPixels'],
                independentIntro=pixels, unchangedBattlePngs=repeated, headlessReplayMatched=True,
                ownershipPublications=1, battleFrozenThroughHandoff=True, originalStartupCompared=False)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for flag in ('capture', 'general', 'ready', 'battle-reference', 'output'):
        parser.add_argument('--' + flag, type=Path, required=True)
    args = parser.parse_args()
    try:
        capture, general, ready, reference, output = map(paths.scratch_path,
            (args.capture, args.general, args.ready, args.battle_reference, args.output))
        v.require(not output.exists(), 'Output exists')
        result = verify(capture, general, ready, reference)
        output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
        print(f"PASS: {result['introFrames']} independent intro frames, one ownership change and six unchanged battle PNGs")
    except (ValueError, KeyError, OSError, TypeError, IndexError) as error:
        parser.exit(2, str(error) + '\n')
