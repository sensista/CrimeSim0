# Nach build_player.py ausführen: formt Schlüsselbeine, Grube darüber und Drosselgrube direkt ins
# Basis-Mesh (alle Shape Keys bekommen dieselbe Verschiebung => Deltas bleiben gleich) und exportiert neu.
# Aufruf: blender -b --python add_anatomy_morph.py
import bpy, sys, numpy as np
sys.path.insert(0,"/workspace/CrimeSim0/Tools/MPFB"); import anatomy as A
OUT="/workspace/mpfb_player/out"
bpy.ops.wm.open_mainfile(filepath=f"{OUT}/PlayerBase.blend")
body=bpy.data.objects["Body"]; rig=bpy.data.objects["PlayerRig"]
if body.data.get("anatomy_morph"): print("schon angewendet"); sys.exit(0)
me=body.data; n=len(me.vertices)
kb=me.shape_keys.key_blocks
co=np.zeros(n*3,np.float32); kb[0].data.foreach_get("co",co); co=co.reshape(-1,3).astype(float)
me.vertices.foreach_set("co",co.astype(np.float32).ravel()); me.update()
nr=np.zeros(n*3,np.float32); me.vertices.foreach_get("normal",nr); nr=nr.reshape(-1,3).astype(float)
S=A.Surf(co,nr)
d,notch=A.body_offsets(co,nr,A.clavicle_landmarks(rig),S,3.6,2.4)  # Low-Poly: breiter, etwas kräftiger
print("ANATOMY max mm",d.max()*1000,d.min()*1000,"verts>0.3mm",(np.abs(d)>3e-4).sum())
off=(nr*d[:,None]).astype(np.float32)
for k in kb:
    a=np.zeros(n*3,np.float32); k.data.foreach_get("co",a); k.data.foreach_set("co",(a.reshape(-1,3)+off).ravel())
me.vertices.foreach_set("co",(co+off).astype(np.float32).ravel()); me.update()
me["anatomy_morph"]=1
bpy.ops.wm.save_as_mainfile(filepath=f"{OUT}/PlayerBase.blend")
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=f"{OUT}/PlayerBase.fbx",use_selection=True,add_leaf_bones=False,use_armature_deform_only=True,bake_anim=False,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',mesh_smooth_type='FACE',use_mesh_modifiers=False,path_mode='COPY',embed_textures=False,global_scale=1.0,colors_type='LINEAR')
print("MORPH DONE")
