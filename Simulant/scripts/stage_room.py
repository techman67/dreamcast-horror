"""Stage only the Unity-exported room bundle and referenced audio; no authoring dependencies."""
import hashlib
import math
import re
import sys
import wave
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from stage_props import read_bundle, write_bundle


def audio_records(text):
    """Reject malformed references/settings before publishing a staged bundle."""
    tokens = iter(text.split())
    def expect(word):
        if next(tokens) != word:
            raise ValueError('Expected audio token: ' + word)
    def number(low, high):
        value = float(next(tokens))
        if not math.isfinite(value) or not low <= value <= high:
            raise ValueError('Invalid audio volume, position or range.')
        return value
    try:
        expect('dreamcast_audio'); version = int(next(tokens))
        if version not in (1, 2):
            raise ValueError('Unsupported audio version.')
        expect('clips'); count = int(next(tokens))
        if not 0 <= count <= 8:
            raise ValueError('Audio clip budget exceeded.')
        records = []
        for i in range(count):
            expect('clip')
            if int(next(tokens)) != i:
                raise ValueError('Invalid audio clip ID.')
            records.append((int(next(tokens)), next(tokens)))
        used = set()
        for i in range(4):
            expect('cue')
            if int(next(tokens)) != i:
                raise ValueError('Invalid audio cue ID.')
            clip = int(next(tokens)); number(0, 1)
            if not -1 <= clip < count:
                raise ValueError('Invalid audio cue reference.')
            if clip >= 0: used.add(clip)
        expect('sources'); sources = int(next(tokens))
        if not 0 <= sources <= 2:
            raise ValueError('Placed sound budget exceeded.')
        for _ in range(sources):
            expect('source'); clip, loop = int(next(tokens)), int(next(tokens))
            if not 0 <= clip < count or loop not in (0, 1):
                raise ValueError('Invalid placed audio reference/loop.')
            used.add(clip); number(0, 1)
            for _ in range(3): number(-3.402823466e38, 3.402823466e38)
            outer = number(0, 10000)
            inner = number(0, 10000) if version == 2 else 0
            if (outer == 0 and inner != 0) or (outer > 0 and inner >= outer):
                raise ValueError('Invalid audio full-volume range.')
        expect('end')
        if next(tokens, None) is not None or used != set(range(count)):
            raise ValueError('Trailing audio data or unreferenced clips.')
        return records
    except (StopIteration, OverflowError) as error:
        raise ValueError('Incomplete or invalid audio manifest. Export the room again.') from error


def stage(export: Path, assets: Path):
    room = (export / 'sample.room').read_bytes()
    manifest = (export / 'sample.audio').read_bytes()
    if len(room) > 65536 or len(manifest) > 8192:
        raise ValueError('Export exceeds room/manifest text budget.')
    records = audio_records(manifest.decode('utf-8'))
    count = len(records)
    files = {}
    total = 0
    for size, name in records:
        if not re.fullmatch(r'[0-9a-f]{32}\.wav', name):
            raise ValueError('Invalid exported clip record.')
        if size < 16 or size > 65534 * 2 or size % 2 or name in files:
            raise ValueError('Invalid, duplicate or oversized audio clip.')
        path = export / 'room-audio' / name
        if path.stat().st_size != size + 44:
            raise ValueError('Exported WAV size mismatch: ' + name)
        content = path.read_bytes()
        if hashlib.sha256(content).hexdigest()[:32] + '.wav' != name:
            raise ValueError('Exported WAV hash mismatch: ' + name)
        with wave.open(str(path), 'rb') as wav:
            if (wav.getnchannels(), wav.getsampwidth(), wav.getframerate(), wav.getnframes() * 2) != (1, 2, 11025, size):
                raise ValueError('Exported WAV format mismatch: ' + name)
        files[name] = content
        total += size
    if total > 512 * 1024:
        raise ValueError('Audio bank exceeds 512 KiB.')
    props, prop_textures = read_bundle(export)
    # Every dependency was validated before touching the previous staged bundle.
    assets.mkdir(parents=True, exist_ok=True)
    write_bundle(assets, props, prop_textures)
    target = assets / 'room-audio'
    if target.is_symlink():
        raise ValueError('Refusing a symlink for the generated audio directory.')
    target.mkdir(exist_ok=True)
    for name, content in files.items():
        (target / name).write_bytes(content)
    for old in target.iterdir():
        if old.is_file() and old.name not in files:
            old.unlink() # Only inside this generated room-audio directory.
    for name in ('footstep', 'key', 'locked', 'unlock', 'ambient'):
        old = assets / 'audio' / (name + '.wav')
        if old.is_file():
            old.unlink() # Remove the previous hard-coded bank from the staged CDI.
    (assets / 'sample.audio').write_bytes(manifest)
    (assets / 'sample.room').write_bytes(room)
    print(f'Staged exported room: {count} audio assets, {total} PCM bytes (512 KiB limit).')


if __name__ == '__main__':
    stage(Path(sys.argv[1]), Path(sys.argv[2]))
