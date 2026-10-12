# Schreibt die UV-Polygone (JSON) -> dilate_textures.py macht daraus die Abdeckungsmaske. Schreibt die UV-Abdeckung des Körpers als Maske (weiß = belegt) für das Ausbluten der Texturen über UV-Nähte.
import bpy, sys
import json
OUT=sys.argv[-1]; R=2048
bpy.ops.wm.open_mainfile(filepath="/workspace/mpfb_player/out/PlayerBase.blend")
me=bpy.data.objects["Body"].data; uv=me.uv_layers.active.data
polys=[[[uv[i].uv[0],uv[i].uv[1]] for i in p.loop_indices] for p in me.polygons]
json.dump(polys,open(OUT,"w"))
print("UVMASK DONE")
