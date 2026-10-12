"""Vuelca un .toc de superbundle (formato binario de Frostbite) para ver qué bundles lleva
el parche y de dónde sale cada uno (delta = datos nuevos en el .sb del parche, base = del juego).
Uso: python -I ver_toc.py <archivo.toc> [otro.toc para comparar]"""
import os
import struct
import sys


def leer(p):
    b = open(p, 'rb').read()
    if b[:1] == b'\x00' and b[1:3] == b'\xd1\xce':
        b = b[0x22c:]
    return b


def uleb(b, i):
    r = s = 0
    while True:
        x = b[i]
        i += 1
        r |= (x & 0x7f) << s
        s += 7
        if not x & 0x80:
            return r, i


def valor(b, tipo, i):
    if tipo in (1, 2):
        tam, i = uleb(b, i)
        fin = i + tam
        res = {} if tipo == 2 else []
        while i < fin and b[i] != 0:
            t = b[i] & 0x1f
            i += 1
            if tipo == 2:
                j = b.index(b'\0', i)
                clave = b[i:j].decode('latin1')
                res[clave], i = valor(b, t, j + 1)
            else:
                v, i = valor(b, t, i)
                res.append(v)
        return res, fin
    if tipo == 6:
        return bool(b[i]), i + 1
    if tipo == 7:
        n, i = uleb(b, i)
        return b[i:i + n - 1].decode('latin1'), i + n
    if tipo == 8:
        return struct.unpack_from('<i', b, i)[0], i + 4
    if tipo == 9:
        return struct.unpack_from('<q', b, i)[0], i + 8
    if tipo == 0x0f:
        return b[i:i + 16].hex(), i + 16
    if tipo == 0x10:
        return b[i:i + 20].hex(), i + 20
    if tipo == 0x13:
        n, i = uleb(b, i)
        return b[i:i + n].hex(), i + n
    raise ValueError('tipo %x en %x' % (tipo, i))


def toc(p):
    b = leer(p)
    return valor(b, b[0] & 0x1f, 1)[0]


if __name__ == '__main__':
    tocs = [toc(p) for p in sys.argv[1:]]
    for p, t in zip(sys.argv[1:], tocs):
        print('==', p, os.path.getsize(p), 'bytes')
        for clave, lista in t.items():
            if not isinstance(lista, list):
                print('  ', clave, lista)
                continue
            print('  ', clave, len(lista))
            for e in lista:
                print('     ', e)
    if len(tocs) == 2:
        a, c = tocs
        dif = [(x, y) for x, y in zip(a.get('bundles', []), c.get('bundles', [])) if x != y]
        print('bundles distintos entre los dos:', len(dif))
        for x, y in dif:
            print('   antes ', x)
            print('   ahora ', y)
