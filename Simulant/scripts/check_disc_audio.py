"""Check exact audio names and lengths in mkdcdisc's finished Joliet directory records."""
from pathlib import Path
import sys


def check(disc, manifest):
    image = Path(disc).read_bytes()
    records = []
    for line in Path(manifest).read_text().splitlines():
        if not line.startswith('clip '):
            continue
        _, _, pcm_bytes, name = line.split()
        records.append((name, int(pcm_bytes) + 44))
    from stage_props import read_bundle
    props, textures = read_bundle(Path(manifest).parent)
    records.append(('sample.props', len(props)))
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
