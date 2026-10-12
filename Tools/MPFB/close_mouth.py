# Nach build_player.py/add_anatomy_morph.py: schließt den Lippenspalt in JEDEM Shape Key (Basis + alle Deltas),
# so dass auch jede Mischung (weiblich, Lippenvolumen ...) geschlossen bleibt (lineare Mischung von geschlossenen Paaren).
# Aufruf: blender -b --python close_mouth.py
import bpy, bmesh, numpy as np
OUT="/workspace/mpfb_player/out"
bpy.ops.wm.open_mainfile(filepath=f"{OUT}/PlayerBase.blend")
body=bpy.data.objects["Body"]; me=body.data; n=len(me.vertices); kb=me.shape_keys.key_blocks
def get(k): a=np.zeros(n*3,np.float32); k.data.foreach_get("co",a); return a.reshape(-1,3).astype(float)
base=get(kb[0])
bm=bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
nbr=lambda i:[e.other_vert(bm.verts[i]).index for e in bm.verts[i].link_edges]
def loop(start,zmin,zmax):
    out={start}
    for sgn in (-1,1):
        cur=start
        for _ in range(14):
            c=[j for j in nbr(cur) if j not in out and abs(base[j,0])<0.028 and sgn*base[j,0]>sgn*base[cur,0] and zmin<=base[j,2]<=zmax and abs(base[j,1]-base[cur,1])<0.006]
            if not c: break
            cur=min(c,key=lambda j:abs(base[j,2]-base[cur,2])); out.add(cur)
    return sorted(out)
U,Lo=474,523; zU=base[U,2]; zL=base[Lo,2]
up=loop(U,zU-0.002,zU+0.006); lo=loop(Lo,zL-0.006,zL+0.002)
pairs=[]
for i in up:
    j=min(lo,key=lambda k:abs(base[k,0]-base[i,0]))
    if abs(base[j,0]-base[i,0])<0.004: pairs.append((i,j))
for j in lo:
    i=min(up,key=lambda k:abs(base[k,0]-base[j,0]))
    if abs(base[j,0]-base[i,0])<0.004 and (i,j) not in pairs: pairs.append((i,j))
print("PAIRS",len(pairs))
paired={v for p in pairs for v in p}
for k in kb:
    C=get(k); C0=C.copy()
    for _ in range(30):  # gemeinsame Paare iterativ zusammenziehen
        acc=np.zeros_like(C); c=np.zeros(n)
        for i,j in pairs:
            m=(C[i]+C[j])/2; acc[i]+=m; acc[j]+=m; c[i]+=1; c[j]+=1
        idx=c>0; C[idx]=acc[idx]/c[idx,None]
    mv=C-C0; C=C0
    ring=np.zeros_like(C); cnt=np.zeros(n)
    for v in paired:
        for w in nbr(v):
            if w not in paired: ring[w]+=mv[v]*0.5; cnt[w]+=1
    ring[cnt>0]/=cnt[cnt>0,None]
    C=C+mv+ring
    k.data.foreach_set("co",C.astype(np.float32).ravel())
me.vertices.foreach_set("co",get(kb[0]).astype(np.float32).ravel()); me.update()
# Prüfen
for name in ("Basis","macro_g0_m05_w05"):
    C=get(kb[name]); print("GAPMM",name,round(max(np.linalg.norm(C[i]-C[j]) for i,j in pairs)*1000,3))
bpy.ops.wm.save_as_mainfile(filepath=f"{OUT}/PlayerBase.blend")
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=f"{OUT}/PlayerBase.fbx",use_selection=True,add_leaf_bones=False,use_armature_deform_only=True,bake_anim=False,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',mesh_smooth_type='FACE',use_mesh_modifiers=False,path_mode='COPY',embed_textures=False,global_scale=1.0,colors_type='LINEAR')
print("CLOSE DONE")
