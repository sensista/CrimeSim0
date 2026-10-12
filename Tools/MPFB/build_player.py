import bpy, bmesh, numpy as np, json, os, gzip, time
bpy.ops.wm.read_factory_settings(use_empty=True)
import addon_utils; addon_utils.enable("bl_ext.user_default.mpfb")
from bl_ext.user_default.mpfb.services.humanservice import HumanService as H
from bl_ext.user_default.mpfb.services.targetservice import TargetService as T
from bl_ext.user_default.mpfb.services.locationservice import LocationService as L
from bl_ext.user_default.mpfb.entities.objectproperties import HumanObjectProperties as P
MP=os.path.expanduser("~/.config/blender/4.2/extensions/user_default/mpfb/data")
UD=os.path.expanduser("~/.config/blender/4.2/mpfb/data")
OUT="/workspace/mpfb_player/out"; os.makedirs(OUT,exist_ok=True)
NB=13380; t0=time.time()
h=H.create_human(mask_helpers=False, feet_on_ground=True)
NV=len(h.data.vertices)
def coords():
    kb=h.data.shape_keys.key_blocks; n=NV*3
    b=np.empty(n,np.float32); kb[0].data.foreach_get("co",b); c=b.copy()
    for k in kb[1:]:
        if k.value:
            a=np.empty(n,np.float32); k.data.foreach_get("co",a); c+=k.value*(a-b)
    return c.reshape(-1,3).astype(np.float64)
N={"gender":0.5,"age":0.5,"muscle":0.5,"weight":0.5,"height":0.5,"proportions":0.5,"cupsize":0.5,"firmness":0.5,"african":1/3,"asian":1/3,"caucasian":1/3}
def setm(d):
    x=dict(N); x.update(d)
    for k,v in x.items(): P.set_value(k,v,entity_reference=h)
    T.reapply_macro_details(h); return coords()
base=setm({})
shapes=[]  # (name, delta, meta)
G={0:"0",0.5:"05",1:"1"}
for g in (0,0.5,1):
  for m in (0,0.5,1):
    for w in (0,0.5,1):
      if (g,m,w)==(0.5,0.5,0.5): continue
      shapes.append((f"macro_g{G[g]}_m{G[m]}_w{G[w]}",setm({"gender":g,"muscle":m,"weight":w})-base,dict(type="macro_grid",category="Körper",grid=dict(gender=g,muscle=m,weight=w))))
for g in (0,0.5,1):
  for a in (0.75,1.0):
    shapes.append((f"macro_age{int(a*100)}_g{G[g]}",setm({"gender":g,"age":a})-setm({"gender":g}),dict(type="macro_age",category="Alter",gender=g,age=a)))
for g in (0,1):
  gb=setm({"gender":g})
  for r in ("african","asian","caucasian"):
    rr={k:0 for k in ("african","asian","caucasian")}; rr[r]=1; rr["gender"]=g
    shapes.append((f"macro_race_{r}_g{G[g]}",setm(rr)-gb,dict(type="macro_race",category="Herkunft",race=r,gender=g)))
fb=setm({"gender":0})
for c in (0,0.5,1):
  for f in (0,0.5,1):
    if (c,f)==(0.5,0.5): continue
    shapes.append((f"macro_breast_c{G[c]}_f{G[f]}",setm({"gender":0,"cupsize":c,"firmness":f})-fb,dict(type="macro_breast",category="Brust",cupsize=c,firmness=f,gender=0)))
for p in (0,1):
    shapes.append((f"macro_proportions_{G[p]}",setm({"proportions":p})-base,dict(type="macro_proportions",category="Proportionen",value=p)))
heights={f"height_{G[v]}":setm({"height":v})-base for v in (0,1)}
setm({})
# modifiers
tj=json.load(open(f"{MP}/targets/target.json"))
DE={"nose":"Nase","mouth":"Mund","eyes":"Augen","eye":"Auge","ears":"Ohren","ear":"Ohr","chin":"Kinn","cheek":"Wange","forehead":"Stirn","head":"Kopf","neck":"Hals","torso":"Oberkörper","stomach":"Bauch","hip":"Hüfte","pelvis":"Becken","buttocks":"Gesäß","legs":"Beine","arms":"Arme","hands":"Hände","feet":"Füße","breast":"Brust","eyebrows":"Augenbrauen","asym":"Asymmetrie","scale":"Größe","trans":"Position","horiz":"horizontal","vert":"vertikal","depth":"Tiefe","incr":"+","decr":"−","in":"innen","out":"außen","up":"hoch","down":"runter","forward":"vor","backward":"zurück","l":"links","r":"rechts","angle":"Winkel","width":"Breite","height":"Höhe","volume":"Volumen","size":"Größe","point":"Spitze","nostrils":"Nasenlöcher","lip":"Lippe","upperlip":"Oberlippe","lowerlip":"Unterlippe","jaw":"Kiefer","upperarm":"Oberarm","lowerarm":"Unterarm","upperleg":"Oberschenkel","lowerleg":"Unterschenkel","thigh":"Oberschenkel","calf":"Wade","shoulder":"Schulter","finger":"Finger","foot":"Fuß","hand":"Hand","bridge":"Nasenrücken","tip":"Spitze","septumangle":"Septumwinkel","lobe":"Ohrläppchen","rot":"Rotation","bulge":"Wölbung","fat":"Fett","muscle":"Muskel","bone":"Knochen","pregnant":"Schwanger","waist":"Taille","nipple":"Brustwarze","dist":"Abstand","length":"Länge","round":"rund","square":"eckig","oval":"oval","triangular":"dreieckig","age":"Alter","flaring":"Weitung","compression":"Kompression","curve":"Kurve","greek":"griechisch","hump":"Höcker","dimples":"Grübchen","cupid":"Amorbogen","eyefold":"Lidfalte","epicanthus":"Epikanthus","bag":"Tränensack","prominent":"markant","cleft":"Kerbe","temple":"Schläfe","nubian":"nubisch","shape":"Form","twist":"Drehung","weight":"Gewicht","scale-depth":"Tiefe"}
def de(name): return " ".join(DE.get(t,t) for t in name.replace("_","-").split("-"))
GDE={k:DE.get(k,k) for k in tj}
missing=[]
for grp,info in tj.items():
    if grp in ("genitals","measure"): continue
    for c in info["categories"]:
        op=c.get("opposites",{})
        pair={}
        for side in ("left","right","unsided"):
            n,p=op.get("negative-"+side,""),op.get("positive-"+side,"")
            if n: pair[n]=p
            if p: pair[p]=n
        for t in c["targets"]:
            fp=f"{MP}/targets/{grp}/{t}.target.gz"
            if not os.path.exists(fp): missing.append(t); continue
            T.load_target(h,fp,weight=1.0,name="tmp"); d=coords()-base
            h.shape_key_remove(h.data.shape_keys.key_blocks["tmp"])
            neg = t in [op.get("negative-"+s) for s in ("left","right","unsided")]
            shapes.append((t,d,dict(type="modifier",category=GDE[grp],group=grp,slider=c["name"],direction="negative" if neg else "positive",pair=pair.get(t) or None,displayName=de(t))))
print("SHAPES",len(shapes),"missing",missing,time.time()-t0)
# joints
vg=json.load(open(f"{MP}/mesh_metadata/basemesh_vertex_groups.json"))
def idx(g): return np.concatenate([np.arange(a,b+1) for a,b in vg[g]])
rj=json.load(open(f"{MP}/rigs/standard/rig.game_engine.json"))
heads={b:idx(v["head"]["cube_name"]) for b,v in rj.items() if v["head"].get("cube_name") in vg}
def joints(d):
    o={}
    for b,ix in heads.items():
        v=d[ix].mean(0)
        if np.abs(v).max()>2e-4: o[b]=[round(float(x),5) for x in v]
    return o
bh={b:[round(float(x),5) for x in base[ix].mean(0)] for b,ix in heads.items()}
# rig + proxies on neutral human
bpy.context.view_layer.objects.active=h
rig=H.add_builtin_rig(h,"game_engine"); rig.name="PlayerRig"
def ensure_neutral():
    pass
prox={"Eyes":(f"{UD}/eyes/low-poly/low-poly.mhclo","Eyes"),"Eyebrows":(f"{UD}/eyebrows/eyebrow001/eyebrow001.mhclo","Eyebrows"),
"Eyelashes":(f"{UD}/eyelashes/eyelashes01/eyelashes01.mhclo","Eyelashes"),"Teeth":(f"{UD}/teeth/teeth_base/teeth_base.mhclo","Teeth"),
"Underwear_Panties":(f"{UD}/clothes/wolgade_female_panties_01/wolgade_female_panties_01.mhclo","Clothes"),
"Underwear_Top":(f"{UD}/clothes/wolgade_female_top_01/wolgade_female_top_01.mhclo","Clothes")}
from bl_ext.user_default.mpfb.entities.clothes.mhclo import Mhclo
pobjs={}
def fit(m,co):
    vs=[m.verts[i] for i in sorted(m.verts)]
    sx=abs(co[m.x_scale[0],0]-co[m.x_scale[1],0])/m.x_scale[2]
    sy=abs(co[m.y_scale[0],2]-co[m.y_scale[1],2])/m.y_scale[2]
    sz=abs(co[m.z_scale[0],1]-co[m.z_scale[1],1])/m.z_scale[2]
    V=np.array([v["verts"] for v in vs]); W=np.array([v["weights"] for v in vs]); O=np.array([tuple(v["offsets"]) for v in vs])
    p=(co[V]*W[...,None]).sum(1)
    off=np.stack([O[:,0]*sx,O[:,1]*sz,O[:,2]*sy],1)
    return p+off, (V,W,O)
for name,(f,typ) in prox.items():
    before=set(bpy.data.objects)
    try:
        H.add_mhclo_asset(f,h,asset_type=typ,subdiv_levels=0,material_type="MAKESKIN")
    except Exception as e: print("PROXFAIL",name,e); continue
    new=[o for o in bpy.data.objects if o not in before and o.type=='MESH']
    o=new[0]; o.name=name
    for md in list(o.modifiers):
        if md.type!='ARMATURE': o.modifiers.remove(md)
    m=Mhclo(); m.load(f)
    fitted,_=fit(m,base)
    pc=np.array([v.co for v in o.data.vertices]); pw=np.array(o.matrix_world)[:3,:3]@pc.T
    pw=pw.T+np.array(o.matrix_world)[:3,3]
    err=np.linalg.norm(pw-fitted,axis=1) if len(pw)==len(fitted) else None
    print("PROX",name,len(o.data.vertices),len(fitted),"fiterr_mm",None if err is None else round(float(err.max())*1000,3))
    pobjs[name]=(o,m)
# body: delete helpers, flatten neutral
h.shape_key_clear() if False else None
for k in list(h.data.shape_keys.key_blocks)[::-1]: h.shape_key_remove(k)
h.data.vertices.foreach_set("co",base.astype(np.float32).ravel())
for md in list(h.modifiers):
    if md.type!='ARMATURE': h.modifiers.remove(md)
bm=bmesh.new(); bm.from_mesh(h.data); bm.verts.ensure_lookup_table()
bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.index>=NB],context='VERTS'); bm.to_mesh(h.data); bm.free()
h.name="Body"; h.data.name="Body"
for g in list(h.vertex_groups):
    if g.name not in rig.data.bones: h.vertex_groups.remove(g)
# shape keys
man={"version":1,"basis":"MPFB2 neutral (alle Makros 0.5, Rassen je 1/3), Alter 25","units":"Meter, Blender-Koordinaten (X rechts, Y vorne=-Y, Z oben); Unity: (x,y,z)_unity = (-x, z, -y)","shapes":[],"boneHeadsNeutral":bh,"heightJointOffsets":{k:joints(v) for k,v in heights.items()},"heightTotal_m":{k:float((base+v)[:NB,2].max()) for k,v in heights.items()},"neutralHeight_m":float(base[:NB,2].max()),"proxies":{}}
h.shape_key_add(name="Basis")
for o,_ in pobjs.values(): o.shape_key_add(name="Basis")
B=base.astype(np.float32)
pfit={n:fit(m,base)[0] for n,(o,m) in pobjs.items()}
for n,d,meta in shapes:
    k=h.shape_key_add(name=n); k.data.foreach_set("co",(B[:NB]+d[:NB]).astype(np.float32).ravel())
    on=[]
    for pn,(o,m) in pobjs.items():
        if len(o.data.vertices)!=len(pfit[pn]): continue
        pd=fit(m,base+d)[0]-pfit[pn]
        if np.abs(pd).max()<1e-5: continue
        pk=o.shape_key_add(name=n); pk.data.foreach_set("co",(np.array([v.co for v in o.data.vertices])+pd).astype(np.float32).ravel()); on.append(pn)
    e=dict(name=n,**meta); e.setdefault("displayName",n); e["proxies"]=on; e["jointOffsets"]=joints(d); e["maxDisplacement_cm"]=round(float(np.abs(d[:NB]).max())*100,3)
    man["shapes"].append(e)
# weights
for o in [h]+[p[0] for p in pobjs.values()]:
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active=o
    if o.vertex_groups:
        bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL',limit=4)
        bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL',lock_active=False)
    o.parent=rig
    if not any(md.type=='ARMATURE' for md in o.modifiers): md=o.modifiers.new("Armature","ARMATURE"); md.object=rig
    man["proxies"][o.name]=dict(verts=len(o.data.vertices),shapeKeys=(len(o.data.shape_keys.key_blocks)-1) if o.data.shape_keys else 0)
for o in list(bpy.data.objects):
    if o.type not in('MESH','ARMATURE') or (o.type=='ARMATURE' and o is not rig): bpy.data.objects.remove(o)
bpy.ops.wm.save_as_mainfile(filepath=f"{OUT}/PlayerBase.blend")
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=f"{OUT}/PlayerBase.fbx",use_selection=True,add_leaf_bones=False,use_armature_deform_only=True,bake_anim=False,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',mesh_smooth_type='FACE',use_mesh_modifiers=False,path_mode='COPY',embed_textures=False,global_scale=1.0)
json.dump(man,open(f"{OUT}/PlayerBase.shapes.json","w"),ensure_ascii=False,indent=1)
print("DONE",len(man["shapes"]),man["proxies"],time.time()-t0)
