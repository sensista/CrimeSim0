# Ergänzt bei Proxies (Zähne/Zunge) fehlende Shape Keys aus dem Körper: Delta = gewichtetes Mittel der
# nächsten Körper-Vertices (Basis). So folgen Zähne Kinn/Mund/Kiefer-Formen und stechen nicht durch die Haut.
# Aufruf: blender -b --python fit_proxy_keys.py
import bpy, numpy as np
from mathutils.kdtree import KDTree
OUT="/workspace/mpfb_player/out"
bpy.ops.wm.open_mainfile(filepath=f"{OUT}/PlayerBase.blend")
body=bpy.data.objects["Body"]; bk=body.data.shape_keys.key_blocks; nb=len(body.data.vertices)
def get(kb,n): a=np.zeros(n*3); kb.data.foreach_get("co",a); return a.reshape(-1,3)
B=get(bk[0],nb)
kd=KDTree(nb); [kd.insert(c,i) for i,c in enumerate(B)]; kd.balance()
for name in ("Teeth",):
    o=bpy.data.objects[name]; me=o.data; n=len(me.vertices)
    if not me.shape_keys: o.shape_key_add(name="Basis")
    pk=me.shape_keys.key_blocks; P=get(pk[0],n)
    # Proxy-Koordinaten in Körper-Objektraum
    M=np.array(body.matrix_world.inverted()@o.matrix_world)
    Pb=(np.c_[P,np.ones(n)]@M.T)[:,:3]
    idx=[];w=[]
    for p in Pb:
        r=kd.find_n(p,8); ii=[x[1] for x in r]; d=np.array([x[2] for x in r])+1e-4
        idx.append(ii); ww=1/d**2; w.append(ww/ww.sum())
    idx=np.array(idx); w=np.array(w)
    have=set(k.name for k in pk); added=0
    R=np.array(o.matrix_world.inverted().to_3x3()@body.matrix_world.to_3x3())
    for k in bk[1:]:
        if k.name in have: continue
        D=get(k,nb)-B
        if np.abs(D).max()<1e-7: continue
        dp=(D[idx]*w[...,None]).sum(1)@R.T
        nk=o.shape_key_add(name=k.name,from_mix=False); nk.data.foreach_set("co",(P+dp).astype(np.float32).ravel()); added+=1
    print("PROXYKEYS",name,"added",added)
bpy.ops.wm.save_as_mainfile(filepath=f"{OUT}/PlayerBase.blend")
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=f"{OUT}/PlayerBase.fbx",use_selection=True,add_leaf_bones=False,use_armature_deform_only=True,bake_anim=False,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',mesh_smooth_type='FACE',use_mesh_modifiers=False,path_mode='COPY',embed_textures=False,global_scale=1.0,colors_type='LINEAR')
print("PROXY DONE")
