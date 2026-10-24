"""Finite, muted, controller-disconnected original boot readiness check; not a battle comparison."""
import argparse
import json
from pathlib import Path
import subprocess
import sys

import prepare_retroarch_reference as p


def options(text):
    import re
    result = {}
    for line in text.splitlines():
        if not line.strip() or line.lstrip().startswith('#'):
            continue
        match = re.fullmatch(r'([a-z0-9_]+)\s*=\s*"([^"\r\n]*)"\s*', line)
        p.require(match is not None, 'Malformed core option')
        key, value = match.groups()
        p.require(key not in result, 'Duplicate core option')
        result[key] = value
    return result


def command(profile, profile_root, output, frames):
    p.require(type(frames) is int and 1 <= frames <= 1800, 'Frame budget must be 1..1800')
    return [profile['executable'], '--config', str(profile_root / 'retroarch.cfg'), '--appendconfig', str(output / 'boot.cfg'),
            '-L', profile['coreLibrary'], '--no-patch', '--sram-mode', 'noload-nosave', '--nodevice', '1', '--nodevice', '2',
            '--verbose', '--log-file', str(output / 'retroarch.log'), '--record-replay', str(output / 'input.bsv'),
            '--max-frames', str(frames), '--max-frames-ss', '--max-frames-ss-path', str(output / 'frame.png'), profile['cue']]


def verify(profile_root, output, frames):
    p.require(sys.platform == 'win32', 'This installed RetroArch boot check supports Windows only')
    profile_root, output = p.scratch_path(profile_root), p.scratch_path(output)
    p.require(not output.exists(), 'Output already exists; choose a fresh directory')
    p.require(type(frames) is int and 1 <= frames <= 1800, 'Frame budget must be 1..1800')
    source = profile_root / 'profile.json'
    p.require(source.stat().st_size <= 65536, 'Profile metadata exceeds budget')
    profile = json.loads(source.read_bytes())
    p.require(profile['format'] == 'fft.retroarch-reference-profile' and profile['schemaVersion'] == 1, 'Unsupported profile')
    for item in profile['inputs']:
        p.require(p.file_hash(Path(item['path'])) == item['sha256'], 'Pinned source/config changed before boot')
    normal_before = {item['path']: p.file_hash(Path(item['path'])) for item in profile['normalConfigIdentities']}
    for name, digest in profile['generatedSha256'].items():
        p.require('/' not in name and '\\' not in name and name in ('retroarch.cfg', 'core-options.cfg', 'launch.ps1'), 'Unsupported generated path')
        p.require(p.file_hash(profile_root / name) == digest, 'Prepared capture policy changed')
    p.require(p.file_hash(profile_root / 'system/scph5501.bin') == p.BIOS_HASH, 'Actual core BIOS changed')
    output.mkdir(parents=True)
    core_options = (profile_root / 'core-options.cfg').read_bytes()
    (output / 'core-options.cfg').write_bytes(core_options)
    (output / 'boot.cfg').write_text(p.config(dict(input_driver='null', input_joypad_driver='null', global_core_options='true',
                                                  core_options_path=(output / 'core-options.cfg').as_posix())), encoding='utf-8', newline='\n')
    startup = subprocess.STARTUPINFO(); startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW; startup.wShowWindow = 0
    result = subprocess.run(command(profile, profile_root, output, frames), cwd=Path(profile['executable']).parent,
                            capture_output=True, timeout=55, startupinfo=startup)
    (output / 'stdout.txt').write_bytes(result.stdout); (output / 'stderr.txt').write_bytes(result.stderr)
    p.require(result.returncode == 0, 'RetroArch boot process failed; inspect local logs')
    p.require((output / 'frame.png').is_file() and (output / 'input.bsv').stat().st_size > 0, 'Missing raw screenshot/replay recording')
    log = (output / 'retroarch.log').read_text(encoding='utf-8', errors='replace')
    p.require('Controller 1: Unplugged' in log and 'Controller 2: Unplugged' in log, 'Boot input isolation not confirmed')
    desired = options(core_options.decode())
    effective = options((output / 'core-options.cfg').read_text(encoding='utf-8'))
    p.require(all(effective.get(key) == value for key, value in desired.items()), 'Saved effective core options differ from the selected profile')
    for item in profile['normalConfigIdentities']:
        p.require(p.file_hash(Path(item['path'])) == normal_before[item['path']], 'Installed config changed during boot check')
    for name, digest in profile['generatedSha256'].items():
        p.require(p.file_hash(profile_root / name) == digest, 'Prepared capture policy changed during boot')
    from PIL import Image
    with Image.open(output / 'frame.png') as image:
        size, mode = image.size, image.mode
    report = dict(format='fft.retroarch-boot-check', schemaVersion=1, profileSha256=p.file_hash(source),
                  verifierSha256=p.file_hash(Path(__file__)), frontendFrameBudget=frames,
                  controllersDisconnected=True, normalConfigsUnchanged=True, selectedCoreOptionsRetained=True,
                  normalConfigBefore=normal_before,
                  pngSize=list(size), pngMode=mode, originalBattleCheckpointReached=False,
                  originalComparisonPerformed=False, replayPlaybackVerified=False,
                  logDiagnostics=[line for line in log.splitlines() if '[ERROR]' in line or '[WARN]' in line],
                  files={name: dict(bytes=(output / name).stat().st_size, sha256=p.file_hash(output / name))
                         for name in ('frame.png', 'input.bsv', 'retroarch.log', 'core-options.cfg', 'boot.cfg')})
    (output / 'check.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8', newline='\n')
    print(f'Boot readiness captured: {frames} frontend frames, raw PNG {size}; no battle comparison')
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--profile', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--frames', type=int, default=1200)
    args = parser.parse_args()
    try:
        verify(args.profile, args.output, args.frames)
    except (ValueError, OSError, KeyError, TypeError, subprocess.SubprocessError) as error:
        parser.exit(2, 'Boot readiness check failed: ' + str(error) + '\n')
