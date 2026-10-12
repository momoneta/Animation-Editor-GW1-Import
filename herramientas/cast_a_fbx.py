"""Reproduce en Blender (sin interfaz) lo que hace un usuario: importa un .cast exportado por el
Animation Editor, opcionalmente deforma un hueso para que el cambio se note, y exporta un FBX.

    blender -b --factory-startup --python cast_a_fbx.py -- <entrada.cast> <salida.fbx> [opciones]

Opciones:
    girar=<hueso>:<vueltas>   añade un giro progresivo a ese hueso durante toda la animación
    ajustes=defecto           exporta con los ajustes por defecto de Blender (por omisión: los recomendados)
"""
import math
import sys

import bpy
from mathutils import Quaternion

args = sys.argv[sys.argv.index("--") + 1:]
entrada, salida = args[0], args[1]
opciones = dict(a.split("=", 1) for a in args[2:])

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.preferences.addon_enable(module="io_scene_cast")
escena = bpy.context.scene

# El .cast de animación solo trae curvas: hace falta una armadura ya cargada y seleccionada,
# normalmente la del FBX de malla que exporta Frosty.
if "armadura" in opciones:
    bpy.ops.import_scene.fbx(filepath=opciones["armadura"])
    armaduras = [o for o in escena.objects if o.type == "ARMATURE"]
    if not armaduras:
        sys.exit("el FBX no trae armadura")
    bpy.ops.object.select_all(action="DESELECT")
    armaduras[0].select_set(True)
    bpy.context.view_layer.objects.active = armaduras[0]
    print("FBX de armadura: %s, %d huesos, escala %s, rotacion %s" % (
        armaduras[0].name, len(armaduras[0].data.bones), tuple(round(v, 4) for v in armaduras[0].scale),
        tuple(round(math.degrees(v), 2) for v in armaduras[0].rotation_euler)))
bpy.ops.import_scene.cast(filepath=entrada)

armaduras = [o for o in escena.objects if o.type == "ARMATURE"]
if not armaduras:
    sys.exit("el .cast no ha creado ninguna armadura")
arm = armaduras[0]
accion = arm.animation_data.action if arm.animation_data else None
if accion is None:
    sys.exit("la armadura no tiene animacion")
inicio, fin = (int(round(v)) for v in accion.frame_range)
escena.frame_start, escena.frame_end = inicio, fin
print("CAST: armadura %s, %d huesos, accion '%s' fotogramas %d-%d, fps %s" % (
    arm.name, len(arm.pose.bones), accion.name, inicio, fin, escena.render.fps))

if "girar" in opciones:
    hueso, vueltas = opciones["girar"].split(":")
    pb = arm.pose.bones[hueso]
    pb.rotation_mode = "QUATERNION"
    bpy.context.view_layer.objects.active = arm
    originales = []
    for f in range(inicio, fin + 1):
        escena.frame_set(f)
        originales.append(pb.rotation_quaternion.copy())
    for i, f in enumerate(range(inicio, fin + 1)):
        angulo = 2 * math.pi * float(vueltas) * i / max(1, fin - inicio)
        pb.rotation_quaternion = Quaternion((0.0, 1.0, 0.0), angulo) @ originales[i]
        pb.keyframe_insert("rotation_quaternion", frame=f)
    print("CAST: giro de %s vueltas aplicado a %s" % (vueltas, hueso))

bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
if opciones.get("ajustes") == "defecto":
    bpy.ops.export_scene.fbx(filepath=salida)
else:
    bpy.ops.export_scene.fbx(
        filepath=salida,
        use_selection=True,
        object_types={"ARMATURE"},
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
    )
print("FBX: " + salida)
