"""Llama a una orden del puente MCP de Frosty (FrostyMcpPlugin) por su tubería, con Frosty abierto.

    python -I puente.py <orden> [clave=valor ...]

Ejemplos:
    python -I puente.py ping
    python -I puente.py import_ant_animation file=C:\\ruta\\anim.fbx "animation=PeaShooter_Taunt_01 Anim" bank=level_frontend_gen3
    python -I puente.py launch_game
"""
import json
import struct
import sys
import time

TUBERIA = r"\\.\pipe\Frosty_MCP_Bridge_v1"


def pedir(orden, parametros):
    cuerpo = json.dumps({"jsonrpc": "2.0", "method": orden, "params": parametros, "id": 1}).encode("utf-8")
    for _ in range(40):
        try:
            tubo = open(TUBERIA, "r+b", buffering=0)
            break
        except OSError:
            time.sleep(0.5)
    else:
        sys.exit("no se pudo abrir la tuberia: Frosty cerrado o el puente ocupado")
    with tubo:
        tubo.write(struct.pack("<I", len(cuerpo)) + cuerpo)
        cabecera = tubo.read(4)
        falta = struct.unpack("<I", cabecera)[0]
        partes = []
        while falta > 0:
            trozo = tubo.read(falta)
            if not trozo:
                break
            partes.append(trozo)
            falta -= len(trozo)
    respuesta = json.loads(b"".join(partes).decode("utf-8"))
    resultado = respuesta.get("result", respuesta)
    if isinstance(resultado, str):
        resultado = json.loads(resultado)
    return resultado


if __name__ == "__main__":
    params = {}
    for a in sys.argv[2:]:
        clave, _, valor = a.partition("=")
        params[clave] = valor
    print(json.dumps(pedir(sys.argv[1], params), indent=1, ensure_ascii=False))
