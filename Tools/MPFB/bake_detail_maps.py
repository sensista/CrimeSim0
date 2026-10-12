# Backt Form-Details (kein Rauschen) aus einer hochaufgelösten Version des MPFB-Körpers:
#  hi = unterteilt + "Unschärfe-Maske" der Geometrie (bestehende Anatomie wird weich betont:
#  Lidfalten, Lippenrand, Nasenflügel, Ohrmuschel, Schlüsselbeine, Halssehnen, Knöchel ...)
#  -> skin_normal.png (Tangent-Space, MPFB-UV), skin_detail.png (R Kavität, G Lippen-Maske)
# Aufruf: blender -b --python bake_detail_maps.py -- <Texturordner>
import bpy, bmesh, sys, numpy as np
from mathutils import Vector
OUT=sys.argv[-2]; VAR=sys.argv[-1]; RES=2048; STRENGTH=3.0  # VAR = female | male
bpy.ops.wm.open_mainfile(filepath="/workspace/mpfb_player/out/PlayerBase.blend")
sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.samples=4; sc.cycles.device='CPU'
low=bpy.data.objects["Body"]
import sys as _s; _s.path.insert(0,"/workspace/CrimeSim0/Tools/MPFB"); import anatomy as A
_eo=bpy.data.objects.get("Eyes")
_ev=np.array([_eo.matrix_world@v.co for v in _eo.data.vertices]) if _eo else None
EYES=[_ev[_ev[:,0]<0].mean(0),_ev[_ev[:,0]>0].mean(0)] if _eo else None
RIG=bpy.data.objects.get("PlayerRig")
CLAV=A.clavicle_landmarks(RIG)
for o in list(bpy.data.objects):
    if o!=low and o.type=='MESH': bpy.data.objects.remove(o)
# Arbeitskopie ohne Shape Keys / Armature
for m in list(low.modifiers):
    if m.type=='ARMATURE': low.modifiers.remove(m)
# Standardform der Variante (wie im Spiel): Makro + weibliche Gesichts-/Hals-Defaults
import json
man=json.load(open("/workspace/mpfb_player/out/PlayerBase.shapes.json"))
SL={}
for sh in man["shapes"]:
    if sh.get("type")!="modifier": continue
    nm=sh["name"]; side="l-" if nm.startswith("l-") else "r-" if nm.startswith("r-") else ""
    SL.setdefault(side+sh["slider"],{})[sh.get("direction","positive")]=nm
vals={"l-eye-scale-decr-incr":0.3,"r-eye-scale-decr-incr":0.3,"mouth-upperlip-volume-decr-incr":0.25,"mouth-lowerlip-volume-decr-incr":0.3}
keys={}
if VAR=="female":
    keys["macro_g0_m05_w05"]=1.0
    for k,v in json.load(open("/workspace/CrimeSim0/Tools/MPFB/female_face_defaults.json")).items(): vals[k]=vals.get(k,0)+v
else:
    keys["macro_g1_m05_w05"]=1.0
for k,v in vals.items():
    d=SL.get(k,{}); t=d.get("positive" if v>0 else "negative")
    if t: keys[t]=abs(v)
if low.data.shape_keys:
    for k in low.data.shape_keys.key_blocks[1:]: k.value=keys.get(k.name,0)
# Glatte Normalen wie in Unity (Smoothing 180°) auf Low-Poly
for pl in low.data.polygons: pl.use_smooth=True
if hasattr(low.data,"normals_domain") and low.data.has_custom_normals:
    bpy.context.view_layer.objects.active=low; bpy.ops.mesh.customdata_custom_splitnormals_clear()
dg=bpy.context.evaluated_depsgraph_get()
hi=bpy.data.objects.new("hi",bpy.data.meshes.new_from_object(low.evaluated_get(dg))); sc.collection.objects.link(hi)
hi.matrix_world=low.matrix_world
bpy.context.view_layer.objects.active=hi
sub=hi.modifiers.new("s",'SUBSURF'); sub.levels=2; sub.render_levels=2
bpy.ops.object.modifier_apply(modifier="s")
for pl in hi.data.polygons: pl.use_smooth=True
me=hi.data; n=len(me.vertices); print("HI verts",n)
co=np.zeros(n*3); me.vertices.foreach_get("co",co); co=co.reshape(-1,3)
me.calc_normals() if hasattr(me,"calc_normals") else None
nr=np.zeros(n*3); me.vertices.foreach_get("normal",nr); nr=nr.reshape(-1,3)
# Nachbarn
e=np.zeros(len(me.edges)*2,dtype=np.int64); me.edges.foreach_get("vertices",e); e=e.reshape(-1,2)
def smooth(p,it):
    for _ in range(it):
        acc=np.zeros_like(p); cnt=np.zeros(n)
        np.add.at(acc,e[:,0],p[e[:,1]]); np.add.at(acc,e[:,1],p[e[:,0]])
        np.add.at(cnt,e[:,0],1); np.add.at(cnt,e[:,1],1)
        p=0.5*p+0.5*acc/np.maximum(cnt,1)[:,None]
    return p
# Detail mittlerer Frequenz (Form, nicht Poren): Differenz zweier Glättungsstufen
s1=smooth(co,3); s2=smooth(co,40)
d=np.einsum('ij,ij->i',s1-s2,nr)            # >0 konvex, <0 konkav
d=np.clip(d,-0.004,0.004)
d=smooth(np.repeat(d[:,None],3,1),4)[:,0]   # weich
# Kopf/Hals/Schlüsselbein-Zone etwas stärker
zt=co[:,2].max(); zone=np.clip((co[:,2]-(zt-0.42))/0.06,0,1)
# Gezielte anatomische Details (glatt): Gesicht + Hals + Schlüsselbeine (Morph ist schon im Mesh, hier nur Schärfe)
S=A.Surf(co,nr)
bd,notch=A.body_offsets(co,nr,CLAV,S,0.5,1.0)
fd=A.face_offsets(co,nr,S,474,523,EYES,notch,1.4)
print("TARGETED mm",bd.min()*1000,bd.max()*1000,fd.min()*1000,fd.max()*1000)
d=d*STRENGTH+bd+fd
hi_co=co+nr*d[:,None]
d=d/STRENGTH  # für Kavität                    # betonen
me.vertices.foreach_set("co",hi_co.ravel()); me.update()
# Kavität + Lippenmaske als Vertex-Farbe
cav=np.clip(0.5+d/0.004*0.5*STRENGTH/1.6,0,1)
lowco=[v.co.copy() for v in low.data.vertices]
lu,ll=lowco[474],lowco[523]; mid=(lu+ll)/2
rel=co-np.array(mid)
# Ellipse: Breite x ~2.6cm, Höhe z ~1.3cm, nur vorne
q=(rel[:,0]/0.03)**2+(rel[:,2]/0.017)**2+(np.clip(-rel[:,1]-0.0,0,None)*0+ (np.clip(rel[:,1],0,None)/0.012)**2)
lip=np.clip(1.0-q,0,1)
lip=smooth(np.repeat(lip[:,None],3,1),3)[:,0]
ca=me.color_attributes.new("det",'FLOAT_COLOR','POINT')
GL,RED,WARM,COOL=A.face_zones(co,nr,474,523,EYES)
col=np.stack([cav,np.clip(lip*1.6,0,1),GL,np.ones(n)],1)   # B = Glanz-Zonen (Glätte)
cv=me.color_attributes.new("var",'FLOAT_COLOR','POINT')
cv.data.foreach_set("color",np.stack([RED,WARM,COOL,np.ones(n)],1).ravel())
ca.data.foreach_set("color",col.ravel())
# Material für Bake
def setup(o,img):
    m=bpy.data.materials.new("bk"+o.name); m.use_nodes=True; o.data.materials.clear(); o.data.materials.append(m)
    N=m.node_tree.nodes
    t=N.new('ShaderNodeTexImage'); t.image=img; N.active=t
    return m
img_n=bpy.data.images.new("n",RES,RES,float_buffer=True); img_n.colorspace_settings.name='Non-Color'
img_d=bpy.data.images.new("d",RES,RES); img_d.colorspace_settings.name='Non-Color'
mh=bpy.data.materials.new("hi"); mh.use_nodes=True; hi.data.materials.clear(); hi.data.materials.append(mh)
a=mh.node_tree.nodes.new('ShaderNodeAttribute'); a.attribute_name="det"
em=mh.node_tree.nodes.new('ShaderNodeEmission'); mh.node_tree.links.new(a.outputs[0],em.inputs[0])
mh.node_tree.links.new(em.outputs[0],mh.node_tree.nodes["Material Output"].inputs[0])
ml=setup(low,img_n)
bpy.ops.object.select_all(action='DESELECT'); hi.select_set(True); low.select_set(True); bpy.context.view_layer.objects.active=low
bk=sc.render.bake; bk.use_selected_to_active=True; bk.cage_extrusion=0.03; bk.max_ray_distance=0.06; bk.margin=8
bpy.ops.object.bake(type='NORMAL',normal_space='TANGENT')
img_n.filepath_raw=OUT+f"/skin_normal_{VAR}.png"; img_n.file_format='PNG'; img_n.save()
ml.node_tree.nodes.active.image=img_d
bpy.ops.object.bake(type='EMIT')
img_d.filepath_raw=OUT+f"/skin_detail_{VAR}.png"; img_d.file_format='PNG'; img_d.save()
# Gemalte Farbzonen (R rosig, G warm, B neutral/kühl) -> skin_variation_{VAR}.png
a.attribute_name="var"
img_v=bpy.data.images.new("v",RES,RES); img_v.colorspace_settings.name='Non-Color'
ml.node_tree.nodes.active.image=img_v
bpy.ops.object.bake(type='EMIT')
img_v.filepath_raw=OUT+f"/skin_variation_{VAR}.png"; img_v.file_format='PNG'; img_v.save()
print("DETAIL DONE")
