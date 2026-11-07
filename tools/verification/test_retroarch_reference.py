"""Authored configuration/path/command regressions; no emulator, BIOS or game needed."""
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import prepare_retroarch_reference as p
import verify_retroarch_boot as b


class RetroArchReferenceTests(unittest.TestCase):
    def test_capture_policy_is_isolated_and_has_no_enhancement_or_autoload(self):
        root, install = Path('/capture'), Path('/installed')
        values = b.options(p.frontend_config(root, install))
        for key in ('config_save_on_exit', 'auto_overrides_enable', 'auto_remaps_enable', 'auto_shaders_enable',
                    'run_ahead_enabled', 'preemptive_frames_enable', 'rewind_enable', 'video_gpu_screenshot',
                    'video_shader_enable', 'video_smooth', 'savestate_auto_load', 'savestate_auto_save', 'network_cmd_enable'):
            self.assertEqual('false', values[key], key)
        self.assertEqual('true', values['global_core_options'])
        for key in ('system_directory', 'savefile_directory', 'savestate_directory', 'screenshot_directory',
                    'core_options_path', 'rgui_config_directory', 'log_dir', 'input_remapping_directory', 'content_history_path'):
            self.assertTrue(values[key].startswith('/capture/'), key)

    def test_selected_software_core_policy_preserves_native_precision_and_speed(self):
        values = b.options(p.core_config())
        self.assertEqual('1x(native)', values['beetle_psx_internal_resolution'])
        self.assertEqual('1x(native)', values['beetle_psx_dither_mode'])
        self.assertEqual('100%', values['beetle_psx_cpu_freq_scale'])
        for key in ('beetle_psx_pgxp_mode', 'beetle_psx_widescreen_hack', 'beetle_psx_crop_overscan', 'beetle_psx_skip_bios'):
            self.assertEqual('disabled', values[key])

    def test_config_values_cannot_inject_new_settings(self):
        for value in ('a"\nb="c', 'a\rconfig_save_on_exit=true', 'a\n', '"'):
            with self.subTest(value=value): self.assertRaises(ValueError, p.config, {'key': value})

    def test_duplicate_or_malformed_effective_options_cannot_claim_policy_retention(self):
        for value in ('a="one"\na="two"', 'a=unquoted', 'not a setting'):
            with self.subTest(value=value): self.assertRaises(ValueError, b.options, value)

    def test_output_reuse_preserves_prior_files_before_reading_any_game_or_executable(self):
        base = p.ROOT / 'scratch/test-temp'; base.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='retro-profile-', dir=base) as directory:
            root = Path(directory); output = root / 'scratch/existing'; output.mkdir(parents=True)
            (output / 'sentinel').write_text('preserve')
            with patch.object(p, 'ROOT', root):
                self.assertRaises(ValueError, p.prepare, root / 'missing-install', output)
            self.assertEqual('preserve', (output / 'sentinel').read_text())

    def test_paths_outside_scratch_are_rejected(self):
        base = p.ROOT / 'scratch/test-temp'; base.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='retro-path-', dir=base) as directory:
            root = Path(directory); (root / 'scratch').mkdir()
            with patch.object(p, 'ROOT', root):
                self.assertRaises(ValueError, p.scratch_path, root / 'outside')
                self.assertRaises(ValueError, p.scratch_path, root / 'scratch/../outside')
                self.assertRaises(ValueError, p.scratch_path, root / 'scratch')

    def test_boot_command_has_a_finite_budget_no_user_controllers_and_no_sram_writes(self):
        profile = dict(executable='retroarch.exe', coreLibrary='core.dll', cue='game.cue')
        args = b.command(profile, Path('/profile space'), Path('/capture space'), 1200)
        self.assertEqual('1200', args[args.index('--max-frames') + 1])
        self.assertEqual('noload-nosave', args[args.index('--sram-mode') + 1])
        self.assertEqual(2, args.count('--nodevice')); self.assertIn('--no-patch', args)
        self.assertEqual(str(Path('/capture space') / 'frame.png'), args[args.index('--max-frames-ss-path') + 1])
        for frames in (0, -1, 1801, True, 1.5):
            with self.subTest(frames=frames): self.assertRaises(ValueError, b.command, profile, Path('/profile'), Path('/output'), frames)

    def test_launcher_uses_session_option_copy_and_pins_supplied_inputs(self):
        script = p.launcher(Path('/profile'), Path('/app/retroarch.exe'), Path('/app/core.dll'), Path('/scratch/a.cue'),
                            [(Path("/scratch/a'b.bin"), 'a' * 64)])
        self.assertIn("a''b.bin", script)
        self.assertIn('Get-FileHash -LiteralPath', script)
        self.assertIn('Copy-Item -LiteralPath', script)
        self.assertIn('--appendconfig $sessionConfig', script)
        self.assertIn('--record-replay', script)
        self.assertNotIn('--entryslot', script)


if __name__ == '__main__':
    unittest.main()
