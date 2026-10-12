# Backt Form-Details (kein Rauschen) aus einer hochaufgelösten Version des MPFB-Körpers:
#  hi = unterteilt + "Unschärfe-Maske" der Geometrie (bestehende Anatomie wird weich betont:
#  Lidfalten, Lippenrand, Nasenflügel, Ohrmuschel, Schlüsselbeine, Halssehnen, Knöchel ...)
#  -> skin_normal.png (Tangent-Space, MPFB-UV), skin_detail.png (R Kavität, G Lippen-Maske)
# Aufruf: blender -b --python bake_detail_maps.py -- <Texturordner>
import bpy, bmesh, sys, numpy as np
from mathutils import Vector
OUT=sys.argv[-1]; RES=2048
bpy.ops.wm.open_mainfile(filepath="/workspace/mpfb_player/out/PlayerBase.blend")
sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.samples=4; sc.cycles.device='CPU'
low=bpy.data.objects["Body"]
for o in list(bpy.data.objects):
    if o!=low and o.type=='MESH': bpy.data.objects.remove(o)
# Arbeitskopie ohne Shape Keys / Armature
hi=low.copy(); hi.data=low.data.copy(); sc.collection.objects.link(hi)
for m in list(hi.modifiers): hi.modifiers.remove(m)
if hi.data.shape_keys:
    bpy.context.view_layer.objects.active=hi
    hi.shape_key_clear()
for m in list(low.modifiers):
    if m.type=='ARMATURE': low.modifiers.remove(m)
if low.data.shape_keys:
    for k in low.data.shape_keys.key_blocks: k.value=0
bpy.context.view_layer.objects.active=hi
sub=hi.modifiers.new("s",'SUBSURF'); sub.levels=2; sub.render_levels=2
bpy.ops.object.modifier_apply(modifier="s")
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
hi_co=s1+nr*d[:,None]*1.6                    # betonen
me.vertices.foreach_set("co",hi_co.ravel()); me.update()
# Kavität + Lippenmaske als Vertex-Farbe
cav=np.clip(0.5+d/0.004*0.5,0,1)
lowco=[v.co.copy() for v in low.data.vertices]
lu,ll=lowco[474],lowco[523]; mid=(lu+ll)/2
rel=co-np.array(mid)
# Ellipse: Breite x ~2.6cm, Höhe z ~1.3cm, nur vorne
q=(rel[:,0]/0.026)**2+(rel[:,2]/0.013)**2+(np.clip(-rel[:,1]-0.0,0,None)*0+ (np.clip(rel[:,1],0,None)/0.012)**2)
lip=np.clip(1.0-q,0,1)
lip=smooth(np.repeat(lip[:,None],3,1),3)[:,0]
ca=me.color_attributes.new("det",'FLOAT_COLOR','POINT')
col=np.stack([cav,np.clip(lip*1.6,0,1),np.zeros(n),np.ones(n)],1)
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
bk=sc.render.bake; bk.use_selected_to_active=True; bk.cage_extrusion=0.006; bk.max_ray_distance=0.02; bk.margin=8
bpy.ops.object.bake(type='NORMAL',normal_space='TANGENT')
img_n.filepath_raw=OUT+"/skin_normal.png"; img_n.file_format='PNG'; img_n.save()
ml.node_tree.nodes.active.image=img_d
bpy.ops.object.bake(type='EMIT')
img_d.filepath_raw=OUT+"/skin_detail.png"; img_d.file_format='PNG'; img_d.save()
print("DETAIL DONE")
