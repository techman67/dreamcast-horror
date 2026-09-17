"""Validate portable static visuals before collecting dependencies for the disc."""
import hashlib
import math
import re
import struct


def read_bundle(export):
    data = (export / 'sample.props').read_bytes()
    if not 12 <= len(data) <= 300000:
        raise ValueError('Missing/oversized static props; export the scene again.')
    magic, textures, parts = struct.unpack_from('<III', data)
    if magic not in (0x31504344, 0x32504344) or textures > 8 or parts > 32:
        raise ValueError('Invalid static prop header/budget.')
    at, total, vertices = 12, 0, 0
    if magic == 0x32504344:
        if len(data) < 16 or struct.unpack_from('<I', data, 12)[0] != 1:
            raise ValueError('Invalid/truncated static visual flags.')
        at = 16
    files, used = {}, set()
    for _ in range(textures):
        if at + 44 > len(data):
            raise ValueError('Truncated prop texture record.')
        w, h = struct.unpack_from('<II', data, at)
        name = data[at+8:at+44].decode('ascii'); at += 44
        if any(n < 8 or n > 256 or n & (n-1) for n in (w,h)) or not re.fullmatch(r'[0-9a-f]{32}\.rgb', name) or name in files:
            raise ValueError('Invalid prop texture dimensions/name.')
        path = export / 'prop-textures' / name
        if path.stat().st_size != 12 + w*h*2:
            raise ValueError('Prop texture size mismatch: ' + name)
        image = path.read_bytes()
        if struct.unpack_from('<III', image) != (0x31544344,w,h) or hashlib.sha256(image).hexdigest()[:32] + '.rgb' != name:
            raise ValueError('Prop texture header/hash mismatch: ' + name)
        files[name] = image; total += w*h*2
    if total > 524288:
        raise ValueError('Prop textures exceed 512 KiB.')
    for _ in range(parts):
        if at + 8 > len(data):
            raise ValueError('Truncated prop part.')
        count, tex = struct.unpack_from('<Ii',data,at); at += 8
        vertices += count
        if count == 0 or count % 3 or vertices > 12288 or not -1 <= tex < textures or at + count*24 > len(data):
            raise ValueError('Invalid prop vertices/texture reference.')
        if tex >= 0:
            used.add(tex)
        for pos in range(at,at+count*24,24):
            *values, color = struct.unpack_from('<5fI',data,pos)
            if any(not math.isfinite(v) or abs(v)>10000 for v in values) or color >> 24 != 255:
                raise ValueError('Invalid prop position/UV/color.')
        at += count*24
    if at != len(data) or used != set(range(textures)):
        raise ValueError('Trailing prop data or unused textures.')
    return data, files


def write_bundle(assets, data, files):
    target = assets / 'prop-textures'
    if target.is_symlink():
        raise ValueError('Prop staging directory cannot be a symlink.')
    target.mkdir(parents=True,exist_ok=True)
    for name, content in files.items():
        (target/name).write_bytes(content)
    for path in target.iterdir():
        if path.is_file() and path.name not in files:
            path.unlink()
    (assets/'sample.props').write_bytes(data)
    print(f'Staged static props: {len(files)} textures, {len(data)} geometry bytes.')
