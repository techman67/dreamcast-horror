"""Save sidecar validation and immutable staging on bad input."""
import importlib.util,struct,tempfile,shutil
from pathlib import Path
root=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('stage_room',root/'Simulant/scripts/stage_room.py'); stage=importlib.util.module_from_spec(spec);spec.loader.exec_module(stage)
room=b'dreamcast_room test\n'
empty=stage.save_manifest(room)
point=struct.pack('<I4f48s',1,0,.9,0,1.5,b'Use journal')
valid=empty[:8]+struct.pack('<I',1)+point
assert stage.save_manifest(room,valid)==valid
bad=[valid[:-1],valid+b'!',valid[:4]+b'\0'*4+valid[8:],valid[:8]+struct.pack('<I',17)+point]
for offset,value in [(12,0),(16,float('nan')),(28,4.)]:
    candidate=bytearray(valid); struct.pack_into('<I' if offset==12 else '<f',candidate,offset,value); bad.append(bytes(candidate))
bad.append(valid[:8]+struct.pack('<I',2)+point+point)
bad.append(valid[:32]+b'x'*48)
for data in bad:
    try: stage.save_manifest(room,data)
    except ValueError: pass
    else: raise AssertionError('Invalid save manifest accepted')
with tempfile.TemporaryDirectory(prefix='dreamcast-save-bundle-') as temporary:
    folder=Path(temporary); source=folder/'source'; target=folder/'target'
    shutil.copytree(root/'Unity/Dreamcast-Horror/Assets/StreamingAssets',source)
    stage.stage(source,target); original=(target/'sample.saves').read_bytes()
    (source/'sample.saves').write_bytes(b'bad')
    try: stage.stage(source,target)
    except ValueError: pass
    else: raise AssertionError('Broken save export staged')
    assert (target/'sample.saves').read_bytes()==original
print('Save bundle tests passed: IDs, ranges, text, truncation, room mismatch, count and failure isolation.')
