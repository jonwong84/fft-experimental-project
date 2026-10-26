import hashlib
import tempfile
import unittest
from pathlib import Path
from PIL import Image
from verify_world_menu import validate_checkpoint, validate_image


class WorldMenuAuditTests(unittest.TestCase):
    def test_checkpoint_rejects_wrong_continuation_and_missing_state(self):
        for state in (None, dict(Funds=92, ElapsedSeconds=53, LocationId={'Value': 'sample:location/hill'})):
            with self.assertRaises(ValueError):
                validate_checkpoint(state, 93, 53, 'hill')

    def test_png_digest_rejects_a_changed_pixel(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'screen.png'
            image = Image.new('RGBA', (960, 640), (14, 21, 34, 255)); image.putpixel((32, 32), (82, 202, 232, 255))
            digest = hashlib.sha256(image.tobytes()).hexdigest(); image.save(path)
            validate_image(path, digest)
            image.putpixel((400, 400), (0, 0, 0, 255)); image.save(path)
            with self.assertRaisesRegex(ValueError, 'digest'):
                validate_image(path, digest)

    def test_recomputed_digest_cannot_hide_transparency(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'screen.png'
            image = Image.new('RGBA', (960, 640), (14, 21, 34, 255)); image.putpixel((32, 32), (82, 202, 232, 255))
            image.putpixel((400, 400), (14, 21, 34, 0)); image.save(path)
            with self.assertRaisesRegex(ValueError, 'opaque'):
                validate_image(path, hashlib.sha256(image.tobytes()).hexdigest())
