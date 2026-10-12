# Backt zwei Hilfstexturen für CrimeSim/StylizedSkin aus dem neutralen MPFB-Körper:
#  skin_variation.png  R = Rötung (Wangen, Nase, Ohren, Lippenumfeld, Knöchel, Ellbogen, Knie)
#                      G = gelblicher (Stirn), B = kühler (Kiefer, Augenhöhlen)
#  skin_thickness.png  hell = dünn/durchscheinend (Ohren, Nasenflügel, Finger) für Gegenlicht
import bpy, bmesh, numpy as np, sys, os
OUT=sys.argv[-1]
bpy.ops.wm.open_mainfile(filepath="/workspace/mpfb_player/out/PlayerBase.blend")
sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.device='CPU'; sc.cycles.samples=32
body=bpy.data.objects["Body"]; rig=bpy.data.objects["PlayerRig"]
for o in bpy.data.objects:
    if o.type=='MESH' and o is not body: o.hide_render=True
co=np.array([v.co for v in body.data.vertices])
def bone(n): return np.array(rig.matrix_world@rig.data.bones[n].head_local)
def g(p,r): d=np.linalg.norm(co-p,axis=1); return np.exp(-(d/r)**2)
top=co[:,2].max()
head=(co[:,2]>top-0.26)
# Nasenspitze = vorderster Punkt (min y) im Gesicht
f=np.where(head&(abs(co[:,0])<0.01))[0]; nose=co[f[np.argmin(co[f,1])]]
ears=[]
for s in (-1,1):
    hb=np.where(head&(co[:,2]<nose[2]+0.04)&(co[:,2]>nose[2]-0.02)&(s*co[:,0]>0))[0]; ears.append(co[hb[np.argmax(abs(co[hb,0]))]])
red=np.zeros(len(co))
red+=0.9*g(nose,0.018)
for s in (-1,1):
    red+=0.8*g(nose+np.array([s*0.042,0.022,0.008]),0.028)   # Wangen
    red+=1.0*g(ears[(s+1)//2],0.03)                          # Ohren
for b in ("lowerarm_l","lowerarm_r","calf_l","calf_r"): red+=0.55*g(bone(b),0.045)
for fn in ("index","middle","ring","pinky","thumb"):
    for s in ("l","r"):
        for k in ("01","02","03"):
            try: red+=0.5*g(bone(f"{fn}_{k}_{s}"),0.012)
            except KeyError: pass
red+=0.4*g(nose+np.array([0,0.02,-0.03]),0.02)  # Lippenumfeld/Kinn leicht
yel=0.9*g(nose+np.array([0,0.03,0.085]),0.05)    # Stirn
cool=np.zeros(len(co))
for s in (-1,1):
    cool+=0.7*g(nose+np.array([s*0.032,0.03,0.035]),0.016)  # Augenhöhlen
    cool+=0.6*g(nose+np.array([s*0.05,0.06,-0.06]),0.03)    # Kieferwinkel
cool+=0.5*g(nose+np.array([0,0.05,-0.085]),0.035)          # unter dem Kinn
C=np.stack([np.clip(red,0,1),np.clip(yel,0,1),np.clip(cool,0,1),np.ones(len(co))],1).astype(np.float32)
ca=body.data.color_attributes.new("Var",'FLOAT_COLOR','POINT'); ca.data.foreach_set("color",C.ravel())
def bake_emit(name):
    img=bpy.data.images.new(name,1024,1024,float_buffer=False); img.colorspace_settings.name='Non-Color'
    m=bpy.data.materials.new(name); m.use_nodes=True; nt=m.node_tree; nt.nodes.clear()
    a=nt.nodes.new('ShaderNodeVertexColor'); a.layer_name="Var"
    e=nt.nodes.new('ShaderNodeEmission'); o=nt.nodes.new('ShaderNodeOutputMaterial')
    nt.links.new(a.outputs[0],e.inputs[0]); nt.links.new(e.outputs[0],o.inputs[0])
    t=nt.nodes.new('ShaderNodeTexImage'); t.image=img; nt.nodes.active=t
    body.data.materials.clear(); body.data.materials.append(m); return img
bpy.ops.object.select_all(action='DESELECT'); body.select_set(True); bpy.context.view_layer.objects.active=body
sc.render.bake.margin=16
img=bake_emit("var"); bpy.ops.object.bake(type='EMIT'); img.filepath_raw=OUT+"/skin_variation.png"; img.file_format='PNG'; img.save()
# Dicke: AO mit umgedrehten Normalen (Strahlen ins Innere; dünne Stellen = schnell getroffen = dunkel)
bm=bmesh.new(); bm.from_mesh(body.data); bmesh.ops.reverse_faces(bm,faces=bm.faces); bm.to_mesh(body.data); bm.free()
sc.world=sc.world or bpy.data.worlds.new("w"); sc.world.light_settings.distance=0.06
img2=bpy.data.images.new("thick",1024,1024); img2.colorspace_settings.name='Non-Color'
m=body.data.materials[0]; t=m.node_tree.nodes.active; t.image=img2
bpy.ops.object.bake(type='AO'); img2.filepath_raw=OUT+"/skin_thickness_raw.png"; img2.file_format='PNG'; img2.save()
print("MAPS DONE", nose, ears)
