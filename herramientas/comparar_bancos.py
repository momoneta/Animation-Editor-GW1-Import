"""Compara un banco modificado con el original: qué secciones cambian y, para animaciones y clips,
claves y duración. Uso: python -I comparar_bancos.py <original> <modificado> [nombre de clip a mostrar]"""
import importlib.util
import os
import struct
import sys

aqui = os.path.dirname(os.path.abspath(__file__))
s = importlib.util.spec_from_file_location('banco', os.path.join(aqui, 'banco.py'))
m = importlib.util.module_from_spec(s)
s.loader.exec_module(m)

A, B = m.Banco(sys.argv[1]), m.Banco(sys.argv[2])
cl = {c['nombre']: c for c in A.clases.values()}
ov = {f['nom']: f['off'] for f in cl['VbrAnimationAsset']['campos']}
oc = {f['nom']: f['off'] for f in cl.get('ClipControllerAsset', {'campos': []})['campos']}
oa = {f['nom']: f['off'] for f in cl.get('ActorControllerAsset', {'campos': []})['campos']}


def nombre(bk, sec, off):
    o = sec[0] + 0x30
    n, p = bk.u32(o + off['__name']), bk.u64(o + off['__name'] + 8)
    return bk.b[sec[0] + 16 + p:sec[0] + 16 + p + n - 1].decode('latin1')


print('original', len(A.b), 'bytes | modificado', len(B.b), 'bytes')
for i, (sa, sb) in enumerate(zip(A.secciones, B.secciones)):
    if sa[1] != 'GD.DATA' or A.b[sa[0]:sa[0] + sa[2]] == B.b[sb[0]:sb[0] + sb[2]]:
        continue
    c = A.objeto(i)[2]
    for bk, sec, et in ((A, sa, 'original  '), (B, sb, 'modificado')):
        o = sec[0] + 0x30
        if c['nombre'] == 'VbrAnimationAsset':
            print(et, c['nombre'], nombre(bk, sec, ov), 'claves', bk.u16(o + ov['NumKeys']), 'bytes', sec[2])
        elif c['nombre'] == 'ClipControllerAsset':
            print(et, c['nombre'], nombre(bk, sec, oc), 'NumTicks', struct.unpack_from('>f', bk.b, o + oc['NumTicks'])[0])
        elif c['nombre'] == 'ActorControllerAsset':
            print(et, c['nombre'], nombre(bk, sec, oa), 'Length', struct.unpack_from('>f', bk.b, o + oa['Length'])[0])
        else:
            print(et, c['nombre'])
if len(sys.argv) > 3:
    aguja = sys.argv[3].encode() + b'\0'
    for i, (p, et, tam, ind, big) in enumerate(B.secciones):
        c = B.objeto(i)[2] if et == 'GD.DATA' else None
        if c is not None and c['nombre'] == 'ClipControllerAsset' and aguja in B.b[p:p + tam]:
            print('clip', sys.argv[3], 'en el modificado: NumTicks', struct.unpack_from('>f', B.b, p + 0x30 + oc['NumTicks'])[0])
