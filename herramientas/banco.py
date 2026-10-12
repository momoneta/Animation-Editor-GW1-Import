"""Lector de bancos ANT (GD.DATA / GD.REFL, formato antiguo de GW1 y GW2) para estudiar el formato.

Uso: python -I banco.py <banco> resumen | clase <Nombre> | volcar <indice> | buscar <texto>
"""
import struct
import sys
from collections import Counter


class Banco:
    def __init__(self, ruta):
        self.b = open(ruta, 'rb').read()
        self.secciones = []  # (pos, etiqueta, tamaño, indices, big)
        self.clases = {}     # hash -> dict
        self._leer()

    def u32(self, o, big=True):
        return struct.unpack_from('>I' if big else '<I', self.b, o)[0]

    def u16(self, o, big=True):
        return struct.unpack_from('>H' if big else '<H', self.b, o)[0]

    def u64(self, o, big=True):
        return struct.unpack_from('>Q' if big else '<Q', self.b, o)[0]

    def i64(self, o, big=True):
        return struct.unpack_from('>q' if big else '<q', self.b, o)[0]

    def i32(self, o, big=True):
        return struct.unpack_from('>i' if big else '<i', self.b, o)[0]

    def cstr(self, o):
        f = self.b.index(b'\0', o)
        return self.b[o:f].decode('latin1')

    def _leer(self):
        b = self.b
        self.empaquetado = self.u32(0)
        p = b.find(b'GD.')
        self.inicio = p
        while p + 16 <= len(b) and b[p:p + 3] == b'GD.':
            etiqueta = b[p:p + 7].decode()
            big = b[p + 7] == ord('b')
            tam = self.u32(p + 8, big)
            ind = self.u32(p + 12, big)
            self.secciones.append((p, etiqueta, tam, ind, big))
            if etiqueta == 'GD.REFL':
                self._refl(p, big)
            # GD.STRM es un contenedor: su tamaño abarca las secciones que siguen
            p += 16 if etiqueta == 'GD.STRM' else tam
        self.fin = p

    def _layout(self, o, big):
        mn, mx = self.i32(o, big), self.i32(o + 4, big)
        n = mx - mn + 1
        d = dict(pos=o, min=mn, max=mx, tam=self.u32(o + 8, big), alin=self.u32(o + 12, big),
                 tabla=self.u32(o + 16, big), ltabla=self.u32(o + 20, big), reord=self.b[o + 24],
                 nativo=self.b[o + 25], hash=self.u32(o + 28, big), campos=[])
        q = o + 32
        for _ in range(n):
            c = dict(hash=self.u32(q, big), tam=self.u32(q + 4, big), off=self.u32(q + 8, big),
                     nombre=self.u32(q + 12, big), n=self.u16(q + 16, big), flags=self.u16(q + 18, big),
                     alin=self.u16(q + 20, big), rle=self.u16(q + 22, big), layout=self.i64(q + 24, big))
            # los huecos de la tabla (tamaño o cuenta a cero) no son campos
            if c['tam'] and c['n']:
                d['campos'].append(c)
            q += 32
        d['nombre'] = self.cstr(o + d['tabla'] + 1) if not d['campos'] else self.cstr(q + 1)
        for c in d['campos']:
            c['nom'] = self.cstr(o + d['tabla'] + c['nombre'])
        return d

    def _refl(self, p, big):
        base = p + 16
        n = self.u64(base, big)
        offs = [self.u64(base + 8 + 8 * i, big) for i in range(n)]
        self.refl_base = base
        for o in offs:
            d = self._layout(base + o, big)
            for c in d['campos']:
                t = self._layout(base + c['layout'], big)
                c['tipo'] = t['nombre']
                c['thash'] = t['hash']
            self.clases[d['hash']] = d

    def objeto(self, i):
        """Devuelve (pos, hash de tipo, clase) de la sección de datos i."""
        p, et, tam, ind, big = self.secciones[i]
        base = p + 16
        tipo = self.u64(base + 16, big) & 0xFFFFFFFF
        return p, tipo, self.clases.get(tipo)


def hexd(b, base=0, ancho=16):
    for i in range(0, len(b), ancho):
        t = b[i:i + ancho]
        print('%06x  %-48s %s' % (base + i, t.hex(' '), ''.join(chr(c) if 32 <= c < 127 else '.' for c in t)))


def main():
    bk = Banco(sys.argv[1])
    orden = sys.argv[2]
    if orden == 'resumen':
        print('bytes', len(bk.b), 'empaquetado', bk.empaquetado, 'primera seccion en', hex(bk.inicio), 'fin', hex(bk.fin))
        hexd(bk.b[:min(bk.inicio, 160)])
        print(Counter(s[1] + ('b' if s[4] else 'l') for s in bk.secciones))
        tipos = Counter()
        for i, s in enumerate(bk.secciones):
            if s[1] == 'GD.DATA':
                _, t, c = bk.objeto(i)
                tipos[c['nombre'] if c else hex(t)] += 1
        for k, v in tipos.most_common():
            print('%6d %s' % (v, k))
        for i, s in enumerate(bk.secciones):
            if s[1] != 'GD.DATA':
                print(i, s)
    elif orden == 'clase':
        for c in bk.clases.values():
            if c['nombre'] == sys.argv[3]:
                print(c['nombre'], 'hash %08x tam %d alin %d min %d max %d reord %d nativo %d' % (
                    c['hash'], c['tam'], c['alin'], c['min'], c['max'], c['reord'], c['nativo']))
                for f in sorted(c['campos'], key=lambda f: f['off']):
                    print('  +%3d %-22s %-12s tam %3d n %d flags %d alin %d rle %d' % (
                        f['off'], f['nom'], f['tipo'], f['tam'], f['n'], f['flags'], f['alin'], f['rle']))
    elif orden == 'volcar':
        i = int(sys.argv[3])
        p, et, tam, ind, big = bk.secciones[i]
        _, t, c = bk.objeto(i)
        print(et, 'pos', hex(p), 'tam', tam, 'indices', ind, 'tipo', c['nombre'] if c else hex(t))
        hexd(bk.b[p:p + min(tam, int(sys.argv[4]) if len(sys.argv) > 4 else 4096)])
    elif orden == 'buscar':
        txt = sys.argv[3].encode()
        for i, s in enumerate(bk.secciones):
            if s[1] == 'GD.DATA' and txt in bk.b[s[0]:s[0] + s[2]]:
                _, t, c = bk.objeto(i)
                print(i, hex(s[0]), s[2], c['nombre'] if c else hex(t))


if __name__ == '__main__':
    main()
