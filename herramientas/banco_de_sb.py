"""Saca de un superbundle .sb (juegos sin CAS, como GW1) los bancos de animación que contiene,
estén guardados sin comprimir, con zlib o con LZ4, para comprobar qué va a leer el juego.

Los recursos van en bloques [u32 tamaño descomprimido][u16 tipo][u16 tamaño]: tipo 0x0070/0x0071
sin comprimir, 0x0270 zlib, 0x0970 LZ4 (el que usa GW1 en sus propios datos).

Uso: python -I banco_de_sb.py <archivo.sb> [salida.antstate]   (guarda el banco más grande)
"""
import struct
import sys
import zlib

b = open(sys.argv[1], 'rb').read()
INICIO_BLOQUE = bytes([0, 1, 0, 0])        # un bloque completo descomprime a 0x10000 bytes


def lz4(src, tam):
    """Descomprime un bloque LZ4 (formato de bloque, sin cabecera de trama)."""
    out = bytearray()
    i, n = 0, len(src)
    while i < n:
        t = src[i]
        i += 1
        lit = t >> 4
        if lit == 15:
            while True:
                x = src[i]
                i += 1
                lit += x
                if x != 255:
                    break
        out += src[i:i + lit]
        i += lit
        if i >= n:
            break
        off = src[i] | (src[i + 1] << 8)
        i += 2
        if off == 0:
            raise ValueError('desplazamiento 0')
        m = t & 15
        if m == 15:
            while True:
                x = src[i]
                i += 1
                m += x
                if x != 255:
                    break
        m += 4
        ini = len(out) - off
        if ini < 0:
            raise ValueError('desplazamiento fuera del bloque')
        if off >= m:
            out += out[ini:ini + m]
        else:
            for k in range(m):
                out.append(out[ini + k])
    if len(out) != tam:
        raise ValueError('tamaño distinto')
    return bytes(out)


def bloque(q):
    """Devuelve (datos, siguiente posición) del bloque en q, o None si no lo es."""
    if q + 8 > len(b):
        return None
    tam, tipo, ctam = struct.unpack_from('>IHH', b, q)
    if tam == 0 or tam > 0x10000:
        return None
    if tipo in (0x0070, 0x0071):
        ctam = ctam or tam
        return b[q + 8:q + 8 + ctam], q + 8 + ctam
    try:
        if tipo == 0x0270:
            datos = zlib.decompress(b[q + 8:q + 8 + ctam])
            return (datos, q + 8 + ctam) if len(datos) == tam else None
        if tipo == 0x0970:
            return lz4(b[q + 8:q + 8 + ctam], tam), q + 8 + ctam
    except (ValueError, IndexError, zlib.error):
        pass
    return None


mejor = None
encontrados = 0
pos = 0
while True:
    # el primer bloque de un banco grande es completo y empieza por 00 00 00 01 (o 03)
    pos = b.find(INICIO_BLOQUE, pos)
    if pos < 0:
        break
    r = bloque(pos)
    if r is None or r[0][:3] != bytes(3) or r[0][3] not in (1, 3):
        pos += 1
        continue
    cab = struct.unpack_from('>I', r[0], 4)[0] + 4
    if cab + 16 > len(r[0]) or r[0][cab:cab + 7] != b'GD.STRM':
        pos += 1
        continue
    datos = bytearray(r[0])
    q = r[1]
    total = cab + struct.unpack_from('>I', datos, cab + 8)[0]
    while len(datos) < total:
        r2 = bloque(q)
        if r2 is None:
            break
        datos += r2[0]
        q = r2[1]
    encontrados += 1
    print('banco en 0x%x: empaquetado %d, %d bytes (esperados %d)' % (pos, datos[3], len(datos), total))
    if len(datos) == total and (mejor is None or len(datos) > len(mejor)):
        mejor = bytes(datos)
    pos = q
print('bancos encontrados:', encontrados)
if len(sys.argv) > 2 and mejor is not None:
    open(sys.argv[2], 'wb').write(mejor)
    print('guardado el de %d bytes en %s' % (len(mejor), sys.argv[2]))
