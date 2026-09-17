"""Connected-room graph validation and failure isolation in build staging."""
import shutil
import struct
import sys
import tempfile
from pathlib import Path
root=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(root/'Simulant/scripts'))
from stage_world import world_records,stage_project
source=root/'Unity/Dreamcast-Horror/Assets/StreamingAssets'
data=(source/'sample.world').read_bytes()
assert len(world_records(data))==2
for size in range(len(data)):
    try: world_records(data[:size])
    except ValueError: pass
    else: raise AssertionError('Truncated world accepted')
for offset,value in ((4,9),(8,99),(12,0)):
    bad=bytearray(data); struct.pack_into('<I',bad,offset,value)
    try: world_records(bad)
    except ValueError: pass
    else: raise AssertionError('Invalid world reference accepted')
with tempfile.TemporaryDirectory(prefix='dreamcast-world-bundle-') as folder:
    folder=Path(folder); export=folder/'export'; target=folder/'assets'
    shutil.copytree(source,export); stage_project(export,target)
    before={str(p.relative_to(target)):p.read_bytes() for p in target.rglob('*') if p.is_file()}
    (export/'world/2/sample.audio').write_text('broken audio')
    try: stage_project(export,target)
    except ValueError: pass
    else: raise AssertionError('Invalid destination room staged')
    after={str(p.relative_to(target)):p.read_bytes() for p in target.rglob('*') if p.is_file()}
    assert before==after,'Failed world staging changed the previous build'
print('World bundle tests passed: bounded graph, truncation, bad references, transactional room staging.')
