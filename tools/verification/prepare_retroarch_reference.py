"""Prepare a private, hash-pinned RetroArch/Beetle PSX capture profile under scratch.

Developer tooling only. Never edits the installed emulator or launches a game.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import time
import uuid

ROOT = Path(__file__).resolve().parents[2]
IMAGE_HASH = '29e63bb6d2e7b842fe87f7c0ee03b7076681c34bcc47095cca27ec53236bf4f9'
BIOS_HASH = '11052b6499e466bbf0a709b1f9cb6834a9418e66680387912451e971cf8a1fef'


def require(condition, message):
    if not condition:
        raise ValueError(message)


def file_hash(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def scratch_path(path):
    path = Path(path).absolute()
    scratch = (ROOT / 'scratch').resolve()
    require(path.resolve().is_relative_to(scratch) and path.resolve() != scratch, 'Input/output must remain below repository scratch/')
    for part in (path, *path.parents):
        require(not part.is_symlink() and not part.is_junction(), 'Reparse input/output paths are unsupported')
    return path


def config(values):
    for value in values.values():
        require(not any(c in str(value) for c in '"\r\n'), 'Unsafe configuration value')
    return ''.join(f'{key} = "{value}"\n' for key, value in sorted(values.items()))


def frontend_config(root, installation):
    path = lambda name: (root / name).as_posix()
    values = dict(config_save_on_exit='false', core_options_path=path('core-options.cfg'),
        libretro_directory=(installation / 'cores').as_posix(), libretro_info_path=(installation / 'info').as_posix(),
        assets_directory=(installation / 'assets').as_posix(), system_directory=path('system'),
        savefile_directory=path('saves'), savestate_directory=path('states'), screenshot_directory=path('screenshots'),
        log_dir=path('logs'), cache_directory=path('cache'), rgui_config_directory=path('config'),
        input_remapping_directory=path('remaps'), recording_output_directory=path('recordings'),
        playlist_directory=path('playlists'), content_history_directory=path('playlists'),
        auto_overrides_enable='false', auto_remaps_enable='false', auto_shaders_enable='false', game_specific_options='false', global_core_options='true',
        history_list_enable='false', content_runtime_log='false', content_runtime_log_aggregate='false',
        run_ahead_enabled='false', preemptive_frames_enable='false', rewind_enable='false', cheevos_enable='false',
        network_cmd_enable='false', stdin_cmd_enable='false', input_overlay_enable='false', input_overlay_pointer_enable='false',
        video_driver='gl', video_fullscreen='false', video_windowed_fullscreen='false', video_scale_integer='true',
        video_scale='3', video_smooth='false', video_shader_enable='false', video_filter='',
        video_gpu_screenshot='false', video_font_enable='false', video_threaded='false', video_vsync='true',
        audio_enable='true', audio_driver='wasapi', audio_mute_enable='true', audio_volume='-80.0', microphone_enable='false', midi_driver='null',
        pause_nonactive='false', savestate_auto_load='false', savestate_auto_save='false',
        savestate_auto_index='false', autosave_interval='0', state_slot='0', log_verbosity='true', log_to_file='true',
        log_to_file_timestamp='false', frontend_log_level='1', libretro_log_level='1',
        input_pause_toggle='p', input_frame_advance='k', input_screenshot='f8', input_save_state='f2', input_load_state='f4',
        input_exit_emulator='escape', input_player1_a='x', input_player1_b='z', input_player1_x='s', input_player1_y='a',
        input_player1_start='enter', input_player1_select='rshift', input_player1_up='up', input_player1_down='down',
        input_player1_left='left', input_player1_right='right', input_player1_l='q', input_player1_r='w')
    for name in ('content_history_path', 'content_favorites_path', 'content_music_history_path', 'content_video_history_path', 'content_image_history_path'):
        values[name] = path('playlists/' + name + '.lpl')
    return config(values)


def core_config():
    return config(dict(beetle_psx_internal_resolution='1x(native)', beetle_psx_dither_mode='1x(native)',
        beetle_psx_pgxp_mode='disabled', beetle_psx_pgxp_nclip='disabled', beetle_psx_widescreen_hack='disabled',
        beetle_psx_cpu_dynarec='disabled', beetle_psx_cpu_freq_scale='100%', beetle_psx_gpu_overclock='1x(native)',
        beetle_psx_gte_overclock='disabled', beetle_psx_cd_fastload='2x(native)', beetle_psx_cd_access_method='sync',
        beetle_psx_frame_duping='disabled', beetle_psx_skip_bios='disabled', beetle_psx_override_bios='disabled',
        beetle_psx_enable_multitap_port1='disabled', beetle_psx_enable_multitap_port2='disabled',
        beetle_psx_enable_memcard1='disabled', beetle_psx_shared_memory_cards='disabled',
        beetle_psx_use_mednafen_memcard0_method='libretro', beetle_psx_display_osd='disabled',
        beetle_psx_display_internal_fps='disabled', beetle_psx_aspect_ratio='uncorrected',
        beetle_psx_crop_overscan='disabled', beetle_psx_image_crop='disabled', beetle_psx_image_offset='disabled',
        beetle_psx_initial_scanline='0', beetle_psx_last_scanline='239', beetle_psx_core_timing_fps='auto_toggle',
        beetle_psx_line_render='default', beetle_psx_deinterlacer='weave'))


def ps_quote(value):
    return "'" + str(value).replace("'", "''") + "'"


def launcher(root, exe, core, cue, identities):
    checks = '\n'.join(f"if ((Get-FileHash -LiteralPath {ps_quote(path)} -Algorithm SHA256).Hash -ne {ps_quote(digest)}) {{ throw 'Pinned input changed; prepare a new profile.' }}"
                       for path, digest in identities)
    return f'''# Private project profile. Run manually to play/capture; normal RetroArch settings are not loaded.
$ErrorActionPreference = 'Stop'
{checks}
$sessionRoot = Join-Path {ps_quote(root / 'sessions')} ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $sessionRoot | Out-Null
$sessionOptions = Join-Path $sessionRoot 'core-options.cfg'
Copy-Item -LiteralPath {ps_quote(root / 'core-options.cfg')} -Destination $sessionOptions
$sessionOptionsForConfig = $sessionOptions.Replace([char]92, [char]47)
$sessionConfig = Join-Path $sessionRoot 'session.cfg'
[System.IO.File]::WriteAllText($sessionConfig, "global_core_options = `"true`"`ncore_options_path = `"$sessionOptionsForConfig`"`n", [System.Text.UTF8Encoding]::new($false))
Write-Host "Capture session: $sessionRoot"
Write-Host 'P pause; K advance one paused frame; F8 raw screenshot; F2 save state; F4 load state; Esc quit.'
Push-Location {ps_quote(exe.parent)}
try {{
    & {ps_quote(exe)} --config {ps_quote(root / 'retroarch.cfg')} --appendconfig $sessionConfig -L {ps_quote(core)} --no-patch --verbose --log-file (Join-Path $sessionRoot 'retroarch.log') --record-replay (Join-Path $sessionRoot 'input.bsv') {ps_quote(cue)}
}} finally {{ Pop-Location }}
'''


def prepare(installation, output):
    installation = Path(installation).resolve()
    output = scratch_path(output)
    require(not output.exists(), 'Output already exists; choose a fresh directory')
    exe, core = installation / 'retroarch.exe', installation / 'cores/mednafen_psx_libretro.dll'
    require(exe.is_file() and core.is_file(), 'Installed RetroArch and Beetle PSX software core are required')
    cue = scratch_path(ROOT / 'scratch/fft-clean-base-game.cue')
    image = scratch_path(ROOT / 'scratch/fft-clean-base-game.bin')
    bios = scratch_path(ROOT / 'scratch/bios/SCPH-5501.bin')
    require(image.stat().st_size == 541315152 and file_hash(image) == IMAGE_HASH, 'SCUS-94221 image identity mismatch')
    require(bios.stat().st_size == 524288 and file_hash(bios) == BIOS_HASH, 'US BIOS identity mismatch')
    cue_text = cue.read_text(encoding='utf-8-sig')
    require(re.fullmatch(r'\s*FILE "fft-clean-base-game\.bin" BINARY\s+TRACK 01 MODE2/2352\s+INDEX 01 00:00:00\s*', cue_text),
            'Expected verified single-track Mode 2 CUE')
    version = subprocess.run([str(exe), '--version'], cwd=installation, capture_output=True, text=True, timeout=10,
                             creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
    require(version.returncode == 0 and 'Version: 1.22.2 (Git 59e4fc7)' in version.stdout,
            'Review this installed RetroArch version before preparing the profile: ' + version.stdout + version.stderr)
    identities = [(exe, file_hash(exe)), (core, file_hash(core)), (image, IMAGE_HASH), (cue, file_hash(cue)), (bios, BIOS_HASH)]
    global_files = [installation / 'retroarch.cfg', installation / 'config/Beetle PSX/Beetle PSX.opt']
    provenance = dict(format='fft.retroarch-reference-profile', schemaVersion=1, edition='SCUS-94221',
                      retroarchVersion=version.stdout.strip(), core='Beetle PSX software',
                      executable=str(exe), coreLibrary=str(core), cue=str(cue),
                      preparationToolSha256=file_hash(Path(__file__)),
                      inputs=[dict(path=str(path), sha256=digest) for path, digest in identities],
                      normalConfigIdentities=[dict(path=str(path), sha256=file_hash(path)) for path in global_files if path.is_file()],
                      policy='Native software/dither, no enhancements/run-ahead/auto overrides; raw core screenshots; uncropped overscan; muted audio. No original comparison yet.')
    files = {'retroarch.cfg': frontend_config(output, installation).encode(), 'core-options.cfg': core_config().encode()}
    pinned = identities + [(output / 'system/scph5501.bin', BIOS_HASH)] + [(output / name, hashlib.sha256(data).hexdigest()) for name, data in files.items()]
    files['launch.ps1'] = launcher(output, exe, core, cue, pinned).encode()
    provenance['generatedSha256'] = {name: hashlib.sha256(data).hexdigest() for name, data in files.items()}
    files['profile.json'] = (json.dumps(provenance, indent=2) + '\n').encode()
    output.parent.mkdir(parents=True, exist_ok=True)
    staging = output.parent / ('.retroarch-reference-' + uuid.uuid4().hex); staging.mkdir()
    try:
        for name in ('system', 'saves', 'states', 'screenshots', 'logs', 'cache', 'config', 'remaps', 'recordings', 'playlists', 'sessions'):
            (staging / name).mkdir()
        (staging / 'system/scph5501.bin').write_bytes(bios.read_bytes())
        for name, data in files.items():
            (staging / name).write_bytes(data)
        # Windows scanners can briefly hold a newly written executable script or BIOS.
        for attempt in range(4):
            require(not output.exists(), 'Output appeared while preparing; refusing to replace it')
            try:
                staging.rename(output)
                break
            except PermissionError:
                if attempt == 3:
                    raise
                time.sleep(0.25 * (attempt + 1))
    finally:
        if staging.exists():
            (staging / 'system/scph5501.bin').unlink(missing_ok=True)
            for name in files: (staging / name).unlink(missing_ok=True)
            for child in staging.iterdir(): child.rmdir()
            staging.rmdir()
    print('Prepared private reference profile: ' + str(output))
    return provenance


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--retroarch-root', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    try:
        prepare(args.retroarch_root, args.output)
    except (ValueError, OSError, subprocess.SubprocessError) as error:
        parser.exit(2, 'RetroArch reference preparation failed: ' + str(error) + '\n')
