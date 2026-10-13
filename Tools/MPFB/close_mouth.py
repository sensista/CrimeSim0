# Nach build_player.py/add_anatomy_morph.py: schließt den Lippenspalt in JEDEM Shape Key (Basis + alle Deltas),
# so dass auch jede Mischung (weiblich, Lippenvolumen ...) geschlossen bleibt (lineare Mischung von geschlossenen Paaren).
# Aufruf: blender -b --python close_mouth.py
import bpy, bmesh, numpy as np
OUT="/workspace/mpfb_player/out"
bpy.ops.wm.open_mainfile(filepath=f"{OUT}/PlayerBase.blend")
body=bpy.data.objects["Body"]; me=body.data; n=len(me.vertices); kb=me.shape_keys.key_blocks
def get(k): a=np.zeros(n*3,np.float32); k.data.foreach_get("co",a); return a.reshape(-1,3).astype(float)
base=get(kb[0])
from mathutils import kdtree
kd=kdtree.KDTree(n)
for i,c in enumerate(base): kd.insert(c,i)
kd.balance()
MIR=[kd.find((-c[0],c[1],c[2]))[1] for c in base]
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
# Nur echte Gegenüber-Paare (Lippenspalt ~1-3 mm). Weiter entfernte "Paare" an den Winkeln wurden früher
# zusammengezogen und haben dort Geometrie verknotet (dunkle Stacheln in Unity).
pairs=[(i,j) for i,j in pairs if np.linalg.norm(base[i]-base[j])<0.0035]
print("PAIRS",len(pairs))
paired={v for p in pairs for v in p}
for k in kb:
    C=get(k); C0=C.copy()
    for _ in range(30):  # gemeinsame Paare iterativ zusammenziehen
        acc=np.zeros_like(C); c=np.zeros(n)
        for i,j in pairs:
            m=(C[i]+C[j])/2; acc[i]+=m; acc[j]+=m; c[i]+=1; c[j]+=1
        idx=c>0; C[idx]=acc[idx]/c[idx,None]
    mv=C-C0
    # Nicht kollabieren: Ober-/Unterkante bleiben 0.25 mm getrennt und liegen leicht nach innen versetzt,
    # sonst entstehen Null-Flächen -> kaputte Normalen (dunkle "Stacheln" an den Mundwinkeln in Unity).
    # Zu den Mundwinkeln hin weich ausblenden (die Winkel sind in der Basis schon geschlossen).
    for i,j in pairs:
        ax=abs(C0[i,0]); t=min(max((ax-0.014)/(0.024-0.014),0),1); fade=1-t*t*(3-2*t)
        for v,sgn in ((i,1),(j,-1)):
            mv[v]=(mv[v]+np.array([0,0.0009,-sgn*0.00006]))*fade
    C=C0
    ring=np.zeros_like(C); cnt=np.zeros(n)
    for v in paired:
        for w in nbr(v):
            if w not in paired: ring[w]+=mv[v]*0.5; cnt[w]+=1
    ring[cnt>0]/=cnt[cnt>0,None]
    C=C+mv+ring
    k.data.foreach_set("co",C.astype(np.float32).ravel())

# --- Mundwinkel-Naht (v7.4): Die Kommissur ist in MPFB-Basis offen (verknotete Innenlippen-Geometrie
# zwischen |x|~14.5 und ~24 mm). Ursache der dunklen Zacken in Unity: man sieht durch die Öffnung
# Innenlippen-Rückseiten und Zähne. Lösung: Ober- und Unterlippenkante im Winkel in JEDEM Key auf die
# Naht-Mittellinie verschweißen (nur Winkel, Mitte bleibt getrennt -> Mund kann später öffnen).
corner=[]
for v in range(n):
    x,y,z=base[v]
    if 0.0060<abs(x)<0.0250 and -0.1446<y<-0.1362: corner.append(v)
def zmid(C,x):
    sgn=1 if x>0 else -1
    a=C[[474,523],2].mean(); i0=469 if sgn<0 else MIR[469]; j0=519 if sgn<0 else MIR[519]; c=3737 if sgn<0 else MIR[3737]
    z0=(C[i0,2]+C[j0,2])/2; x0=abs(C[i0,0]); x1=abs(C[c,0])
    if abs(x)<x0: return a+(z0-a)*abs(x)/x0
    t=min(max((abs(x)-x0)/(x1-x0),0),1)
    return z0+(C[c,2]-z0)*t
for k in kb:
    C=get(k)
    for v in corner:
        x=base[v,0]; zm=zmid(C,C[v,0]); d=abs(base[v,2]-zmid(base,x))
        if d>0.0016: continue
        w=1-min(max((d-0.0011)/0.0005,0),1)
        tin=min(max((abs(x)-0.0060)/0.0025,0),1)   # sanft aus der Mitte einblenden
        w*=tin
        C[v,2]=C[v,2]+(zm-np.sign(base[v,2]-zmid(base,x))*0.00006-C[v,2])*w
        # Oberlippenkante minimal VOR die Unterlippe (wie echte Lippen): auch von schräg oben/unten kein Spalt
        C[v,1]-=np.sign(base[v,2]-zmid(base,x))*0.0003*w
    k.data.foreach_set("co",C.astype(np.float32).ravel())
me.vertices.foreach_set("co",get(kb[0]).astype(np.float32).ravel()); me.update()
# Prüfen
for name in ("Basis","macro_g0_m05_w05"):
    C=get(kb[name]); print("GAPMM",name,round(max(np.linalg.norm(C[i]-C[j]) for i,j in pairs)*1000,3))
bpy.ops.wm.save_as_mainfile(filepath=f"{OUT}/PlayerBase.blend")
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=f"{OUT}/PlayerBase.fbx",use_selection=True,add_leaf_bones=False,use_armature_deform_only=True,bake_anim=False,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',mesh_smooth_type='FACE',use_mesh_modifiers=False,path_mode='COPY',embed_textures=False,global_scale=1.0,colors_type='LINEAR')
print("CLOSE DONE")
