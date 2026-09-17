"""Bounded connected-room manifest validation and transactional build staging."""
import math
import shutil
import struct
import tempfile
from pathlib import Path

def world_records(data):
    if not 12 <= len(data) <= 4096:
        raise ValueError('World manifest must be 12-4096 bytes.')
    offset = 0
    def read(fmt):
        nonlocal offset
        size = struct.calcsize(fmt)
        if offset + size > len(data): raise ValueError('Truncated world manifest.')
        result = struct.unpack_from(fmt, data, offset); offset += size
        return result
    magic, count, start = read('<III')
    if magic != 0x31525744 or not 1 <= count <= 8: raise ValueError('Invalid world version/count.')
    rooms = {}
    for _ in range(count):
        ident, fingerprint, scene, arrivals, links = read('<II96sII')
        text, sep, pad = scene.partition(b'\0')
        if not sep or any(pad) or text.startswith(b'/') or b'..' in text or b'\\' in text or b':' in text or any(c < 32 or c > 126 for c in text): raise ValueError('Invalid world scene path.')
        if not 1 <= ident <= 8 or ident in rooms or arrivals > 8 or links > 8: raise ValueError('Invalid room IDs or marker counts.')
        arrival_ids = set()
        for _ in range(arrivals):
            key, x, y, z, camera = read('<IfffI')
            if not key or key in arrival_ids or camera not in (1, 2) or not all(math.isfinite(v) and abs(v) <= 10000 for v in (x,y,z)): raise ValueError('Invalid arrival.')
            arrival_ids.add(key)
        destinations = []
        for _ in range(links):
            x,y,z,radius,target,arrival,door = read('<ffffIII')
            if not all(math.isfinite(v) and abs(v) <= 10000 for v in (x,y,z)) or not .25 <= radius <= 3 or door not in (0,1): raise ValueError('Invalid room link.')
            destinations.append((target,arrival))
        rooms[ident] = (fingerprint,arrival_ids,destinations)
    if offset != len(data) or start not in rooms: raise ValueError('Invalid starting room or trailing world data.')
    for _,_,links in rooms.values():
        for target,arrival in links:
            if target not in rooms or arrival not in rooms[target][1]: raise ValueError('Room link destination/arrival is missing.')
    return rooms

def stage_project(export, assets):
    from stage_room import stage
    manifest = export / 'sample.world'
    data = manifest.read_bytes() if manifest.exists() else None
    records = world_records(data) if data is not None else {}
    # Validate every room before changing the previous staged build.
    assets.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='room-stage-', dir=assets.parent) as temporary:
        candidate = Path(temporary)
        stage(export, candidate)
        for ident,(fingerprint,_,_) in records.items():
            source = export / 'world' / str(ident)
            room = (source / 'sample.room').read_bytes()
            hash_value = 2166136261
            for byte in room: hash_value = ((hash_value ^ byte) * 16777619) & 0xffffffff
            if hash_value != fingerprint: raise ValueError(f'Room {ident} no longer matches world export.')
            stage(source, candidate / 'world' / str(ident))
        assets.mkdir(parents=True, exist_ok=True)
        # Generated directories only; keep Simulant's engine resources.
        for name in ('world','room-audio','prop-textures'):
            target = assets / name
            if target.is_symlink(): raise ValueError('Refusing symlink in generated room bundle.')
            if target.exists(): shutil.rmtree(target)
        shutil.copytree(candidate, assets, dirs_exist_ok=True)
        if data is not None: (assets / 'sample.world').write_bytes(data)
        elif (assets / 'sample.world').exists(): (assets / 'sample.world').unlink()
