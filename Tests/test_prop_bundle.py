"""Static prop staging regressions using the real Unity export."""
from pathlib import Path
import sys,tempfile,shutil,struct,hashlib
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Simulant/scripts'))
from stage_props import read_bundle,write_bundle
with tempfile.TemporaryDirectory(prefix='prop-bundle-') as tmp:
 source=Path(tmp)/'export';target=Path(tmp)/'target'
 shutil.copytree(ROOT/'Unity/Dreamcast-Horror/Assets/StreamingAssets',source)
 data,files=read_bundle(source)
 target.mkdir();write_bundle(target,data,files)
 assert (target/'sample.props').read_bytes()==data
 assert set(p.name for p in (target/'prop-textures').iterdir())==set(files)
 # Test textured dependencies independently of the selected scene (the stairs
 # fixture deliberately has no textures). Keep validating the real export above.
 image=b'DCT1'+struct.pack('<II',8,8)+b'\xff\xff'*64
 name=hashlib.sha256(image).hexdigest()[:32]+'.rgb'
 (source/'prop-textures').mkdir(exist_ok=True)
 (source/'prop-textures'/name).write_bytes(image)
 vertices=b''.join(struct.pack('<5fI',*v,0xffffffff) for v in
     [(0,0,0,0,0),(1,0,0,1,0),(0,1,0,0,1)])
 fixture=b'DCP1'+struct.pack('<II',1,1)+struct.pack('<II',8,8)+name.encode('ascii')+struct.pack('<Ii',3,0)+vertices
 (source/'sample.props').write_bytes(fixture)
 data,files=read_bundle(source);write_bundle(target,data,files)
 name=next(iter(files));p=source/'prop-textures'/name;original=p.read_bytes()
 p.write_bytes(original[:-1]+bytes([original[-1]^1]))
 try:read_bundle(source);raise AssertionError('Corrupt texture accepted')
 except ValueError:pass
 p.unlink()
 try:read_bundle(source);raise AssertionError('Missing texture accepted')
 except FileNotFoundError:pass
 p.write_bytes(original)
 for bad in (data[:-1],data+b'\0',b'DCP1'+struct.pack('<II',9,0),b'DCP2'+struct.pack('<II',0,0),b'DCP2'+struct.pack('<III',0,0,2)):
  (source/'sample.props').write_bytes(bad)
  try:read_bundle(source);raise AssertionError('Invalid prop bundle accepted')
  except ValueError:pass
 empty=b'DCP1'+struct.pack('<II',0,0)
 (source/'sample.props').write_bytes(empty)
 data,files=read_bundle(source);write_bundle(target,data,files)
 assert list((target/'prop-textures').iterdir())==[]
 (source/'sample.props').write_bytes(b'DCP2'+struct.pack('<III',0,0,1))
 assert read_bundle(source)[0][:4]==b'DCP2'
print('Prop bundle checks passed: real export, corrupt/missing dependencies, malformed geometry and stale texture removal.')
