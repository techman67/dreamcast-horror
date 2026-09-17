"""Verify the receipt written only after the isolated KOS probe reboot test passes."""
import struct,sys
from pathlib import Path
image=Path(sys.argv[1]).read_bytes()
assert len(image)==131072, 'Expected a standard test VMU image'
records=[]
for offset in range(len(image)-35):
    magic=image[offset:offset+4]
    if magic not in (b'DSV1',b'DSV2'): continue
    length=36 if magic==b'DSV1' else 72
    if offset+length>len(image): continue
    fields=struct.unpack_from('<IIIfffIII',image,offset)
    checksum=2166136261
    for byte in image[offset:offset+length-4]: checksum=((checksum^byte)*16777619)&0xffffffff
    if struct.unpack_from('<I',image,offset+length-4)[0]==checksum and fields[1]==0x12345678: records.append(fields)
assert any(r[2]>=3 and r[3]==2 and r[5]==42 and r[7]==3 for r in records), 'Reboot receipt missing'
print('KOS VMU write/read, corrupt-record recovery and reboot persistence receipt verified.')
