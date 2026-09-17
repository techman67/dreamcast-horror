"""Packaging regressions: hashes, missing dependencies, stale-file exclusion."""
import importlib.util
import re
from pathlib import Path
import shutil
import tempfile

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('stage_room', ROOT / 'Simulant/scripts/stage_room.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
with tempfile.TemporaryDirectory(prefix='dreamcast-audio-bundle-') as temporary:
    root = Path(temporary).resolve()
    assert root.parent == Path(tempfile.gettempdir()).resolve()
    source, target = root / 'export', root / 'staged'
    shutil.copytree(ROOT / 'Unity/Dreamcast-Horror/Assets/StreamingAssets', source)
    module.stage(source, target)
    original = (target / 'sample.audio').read_bytes()
    names = {line.split()[3] for line in original.decode().splitlines() if line.startswith('clip ')}
    assert {p.name for p in (target / 'room-audio').iterdir()} == names
    first = source / 'room-audio' / sorted(names)[0]
    content = first.read_bytes()
    first.write_bytes(content[:-1] + bytes([content[-1] ^ 1]))
    try:
        module.stage(source, target)
        raise AssertionError('Corrupt export accepted')
    except ValueError:
        pass
    assert (target / 'sample.audio').read_bytes() == original
    first.unlink()
    try:
        module.stage(source, target)
        raise AssertionError('Missing dependency accepted')
    except FileNotFoundError:
        pass
    first.write_bytes(content)
    for malformed in ('', re.sub(r'cue 0 -?\d+', 'cue 0 99', original.decode()),
                      re.sub(r'sources \d+', 'sources 3', original.decode()),
                      original.decode().replace('end', 'end junk')):
        assert malformed != original.decode(), 'Malformed fixture did not change the manifest'
        (source / 'sample.audio').write_text(malformed)
        try:
            module.stage(source, target)
            raise AssertionError('Malformed audio manifest was staged')
        except ValueError:
            pass
        assert (target / 'sample.audio').read_bytes() == original
    # Removed assignments must remove their stale assets from the next CDI.
    (source / 'sample.audio').write_text(
        'dreamcast_audio 1\nclips 0\ncue 0 -1 1\ncue 1 -1 1\ncue 2 -1 1\ncue 3 -1 1\nsources 0\nend\n')
    module.stage(source, target)
    assert list((target / 'room-audio').iterdir()) == []
print('Audio bundle checks passed: referenced-only assets, corruption/missing rejection and stale removal.')
