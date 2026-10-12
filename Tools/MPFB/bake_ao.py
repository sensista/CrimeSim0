# Backt eine weiche Ambient-Occlusion-Map des neutralen MPFB-Körpers (CC0-Basis) für den Stil-Haut-Shader.
import bpy, sys
bpy.ops.wm.open_mainfile(filepath="/workspace/mpfb_player/out/PlayerBase.blend")
sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=64
body=bpy.data.objects["Body"]
for o in bpy.data.objects:
    if o.type=='MESH' and o is not body: o.hide_render=True; o.hide_set(True)
img=bpy.data.images.new("AO",1024,1024,float_buffer=False)
mat=bpy.data.materials.new("bakeAO"); mat.use_nodes=True
n=mat.node_tree.nodes.new('ShaderNodeTexImage'); n.image=img; mat.node_tree.nodes.active=n
body.data.materials.clear(); body.data.materials.append(mat)
bpy.ops.object.select_all(action='DESELECT'); body.select_set(True); bpy.context.view_layer.objects.active=body
sc.render.bake.margin=16
sc.world=bpy.data.worlds.new("w") if sc.world is None else sc.world
bpy.ops.object.bake(type='AO')
img.filepath_raw=sys.argv[-1]; img.file_format='PNG'; img.save()
print("AO DONE")
