"""Inspect native architecture/imports/exports without loading the DLL."""
import json
import struct
import sys
from pathlib import Path


def inspect(path):
    data = Path(path).read_bytes()
    u16 = lambda offset: struct.unpack_from('<H', data, offset)[0]
    u32 = lambda offset: struct.unpack_from('<I', data, offset)[0]
    pe = u32(60)
    assert data[pe:pe + 4] == b'PE\0\0', 'Invalid PE signature'
    optional = pe + 24
    assert u16(optional) == 0x10B, 'Expected PE32'
    section_start = optional + u16(pe + 20)
    sections = []
    for i in range(u16(pe + 6)):
        offset = section_start + i * 40
        sections.append((u32(offset + 12), max(u32(offset + 8), u32(offset + 16)), u32(offset + 20)))

    def file_offset(rva):
        for address, size, raw in sections:
            if address <= rva < address + size:
                return raw + rva - address
        if rva < u32(optional + 60):
            return rva
        raise ValueError('Unmapped RVA')

    def string(rva):
        offset = file_offset(rva)
        return data[offset:data.index(b'\0', offset)].decode('ascii')

    imports = []
    import_rva = u32(optional + 104)
    if import_rva:
        offset = file_offset(import_rva)
        while u32(offset + 12):
            imports.append(string(u32(offset + 12)))
            offset += 20
    exports = []
    export_rva = u32(optional + 96)
    if export_rva:
        offset = file_offset(export_rva)
        names = file_offset(u32(offset + 32))
        exports = [string(u32(names + i * 4)) for i in range(u32(offset + 24))]
    assert u16(pe + 4) == 0x14C, 'Expected x86 machine'
    return {'path': str(Path(path).resolve()), 'machine': 'x86', 'imports': imports, 'exports': exports}


for name in sys.argv[1:]:
    print(json.dumps(inspect(name), indent=2))
