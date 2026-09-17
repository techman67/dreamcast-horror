"""Check exact audio names and lengths in mkdcdisc's finished Joliet directory records."""
from pathlib import Path
import sys

def disc_files(image):
    """Walk the Joliet tree in this project's mkdcdisc output, preserving paths."""
    base=image.find(b'SEGA SEGAKATANA')
    primary=image.find(b'\x01CD001\x01')
    supplementary=image.find(b'\x02CD001\x01')
    stride=supplementary-primary
    if base<0 or primary<0 or stride not in (2048,2336,2352):
        raise ValueError('Unsupported/missing CDI ISO descriptors.')
    root=image[supplementary+156:supplementary+190]
    extent=int.from_bytes(root[2:6],'little'); offset=0; candidates=[]
    while (offset:=image.find(root,offset))>=0:
        if offset>=base and (offset-base)%stride==0: candidates.append(offset)
        offset+=1
    if len(candidates)!=1: raise ValueError('Cannot uniquely resolve Joliet root directory.')
    first_lba=extent-(candidates[0]-base)//stride
    def read(lba,size):
        result=bytearray()
        while size:
            start=base+(lba-first_lba)*stride; count=min(size,2048)
            if start<0 or start+count>len(image): raise ValueError('CDI extent outside image.')
            result.extend(image[start:start+count]); size-=count; lba+=1
        return bytes(result)
    result={}; visited=set()
    def walk(lba,size,path):
        if lba in visited or len(visited)>128 or size>65536: raise ValueError('Invalid CDI directory tree.')
        visited.add(lba); data=read(lba,size); at=0
        while at<len(data):
            length=data[at]
            if not length: at=(at//2048+1)*2048; continue
            record=data[at:at+length]
            if length<34 or len(record)!=length: raise ValueError('Truncated CDI directory record.')
            at+=length; name=record[33:33+record[32]]
            if name in (b'\x00',b'\x01'): continue
            name=name.decode('utf-16-be').removesuffix(';1'); full=path+'/'+name
            child=int.from_bytes(record[2:6],'little'); size=int.from_bytes(record[10:14],'little')
            if record[25]&2: walk(child,size,full)
            else: result[full]=(child,size)
    walk(extent,int.from_bytes(root[10:14],'little'),'')
    return result,read


def check(disc, manifest):
    image = Path(disc).read_bytes()
    bootstrap=image.find(b'SEGA SEGAKATANA')
    if bootstrap<0 or image[bootstrap+64:bootstrap+74]!=b'IND-DCH001':
        raise ValueError('CDI must use stable serial IND-DCH001 so per-game VMU saves survive rebuilding.')
    files,read=disc_files(image)
    assets=Path(manifest).parent
    world=assets/'sample.world'
    if world.exists():
        from stage_world import world_records
        from stage_room import audio_records
        from stage_props import read_bundle
        expected={'sample.world':world.read_bytes()}
        for ident in world_records(expected['sample.world']):
            folder=assets/'world'/str(ident); prefix=f'world/{ident}/'
            for name in ('sample.room','sample.audio','sample.props','sample.saves'): expected[prefix+name]=(folder/name).read_bytes()
            for _,name in audio_records((folder/'sample.audio').read_text()): expected[prefix+'room-audio/'+name]=(folder/'room-audio'/name).read_bytes()
            _,textures=read_bundle(folder)
            for name,data in textures.items(): expected[prefix+'prop-textures/'+name]=data
        for name,data in expected.items():
            entry=files.get('/assets/'+name)
            if entry is None or entry[1]!=len(data) or read(*entry)!=data: raise ValueError('CDI connected-room content/path mismatch: '+name)
        print(f'CDI connected-room check passed: {len(expected)} files verified by full path and exact bytes.')
    records = []
    for line in Path(manifest).read_text().splitlines():
        if not line.startswith('clip '):
            continue
        _, _, pcm_bytes, name = line.split()
        records.append((name, int(pcm_bytes) + 44))
    from stage_props import read_bundle
    props, textures = read_bundle(Path(manifest).parent)
    records.append(('sample.props', len(props)))
    saves=Path(manifest).parent / 'sample.saves'
    if saves.exists(): records.append(('sample.saves',saves.stat().st_size))
    records.extend((name, len(data)) for name, data in textures.items())
    for name, size in records:
        encoded = (name + ';1').encode('utf-16-be')
        offset = 0
        found = False
        while (offset := image.find(encoded, offset)) >= 0:
            start = offset - 33
            if (start >= 0 and image[offset - 1] == len(encoded)
                    and image[start] >= 33 + len(encoded)
                    and int.from_bytes(image[start + 10:start + 14], 'little') == size
                    and int.from_bytes(image[start + 14:start + 18], 'big') == size):
                found = True
                break
            offset += 1
        if not found:
            raise ValueError(f'CDI asset filename/size mismatch: {name}. Re-export with disc-safe filenames.')
    print('CDI directory check passed: exact audio, static prop and texture filenames/sizes.')


if __name__ == '__main__':
    check(*sys.argv[1:])
