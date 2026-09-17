"""Validate generated placeholders before copying to Unity's AUTHORING folder.

Export the scene in Unity afterward. CDI builds use only that exported bundle.
"""
from pathlib import Path
import shutil
import wave

ROOT = Path(__file__).resolve().parent
NAMES = ('footstep', 'key', 'locked', 'unlock', 'ambient')
total = 0
for name in NAMES:
    with wave.open(str(ROOT / (name + '.wav')), 'rb') as sound:
        assert (sound.getnchannels(), sound.getsampwidth(), sound.getframerate()) == (1, 2, 11025)
        assert sound.getcomptype() == 'NONE'
        total += sound.getnframes() * 2
assert total <= 512 * 1024, f'Sample budget exceeded: {total}'
dest = ROOT.parent / 'Unity/Dreamcast-Horror/Assets/Audio'
dest.mkdir(parents=True, exist_ok=True)
for name in NAMES:
    shutil.copyfile(ROOT / (name + '.wav'), dest / (name + '.wav'))
print(f'Audio samples: {total} bytes / 524288 byte budget. Eight playback slots maximum.')
