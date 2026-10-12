# Annäherung von CrimeSim/StylizedSkin in Blender-EEVEE (Shader-to-RGB) für Vergleichsbilder.
# Aufruf: blender -b --python skin_shader_preview.py -- <ausgabeordner>
import bpy, math, sys, os
OUT=sys.argv[-1]; T="/workspace/CrimeSim0/Assets/_Project/Art/Textures/Character/"
bpy.ops.wm.open_mainfile(filepath="/workspace/mpfb_player/out/PlayerBase.blend")
sc=bpy.context.scene; sc.render.engine='BLENDER_EEVEE_NEXT'
sc.view_settings.view_transform='AgX'; sc.view_settings.look='AgX - Base Contrast'
sc.render.resolution_x=520; sc.render.resolution_y=640
sc.eevee.use_shadows=True
body=bpy.data.objects["Body"]
for o in bpy.data.objects:
    if o.type=='MESH': o.hide_render = o.name.startswith("Underwear") or o.name=="Teeth"
# Gesicht leicht weiblich + Stil-Defaults
kb=body.data.shape_keys.key_blocks
def setkeys(d):
    for o in bpy.data.objects:
        if o.type=='MESH' and o.data.shape_keys:
            for k in o.data.shape_keys.key_blocks[1:]: k.value=d.get(k.name,0)
import json
man=json.load(open("/workspace/mpfb_player/out/PlayerBase.shapes.json"))
SL={}
for sh in man["shapes"]:
    if sh.get("type")!="modifier": continue
    n=sh["name"]; side="l-" if n.startswith("l-") else "r-" if n.startswith("r-") else ""
    SL.setdefault(side+sh["slider"],{})[sh.get("direction","positive")]=n
base_def={"l-eye-scale-decr-incr":0.3,"r-eye-scale-decr-incr":0.3,"mouth-upperlip-volume-decr-incr":0.25,"mouth-lowerlip-volume-decr-incr":0.3}
fem=json.load(open("/workspace/CrimeSim0/Tools/MPFB/female_face_defaults.json"))
keys={"macro_g0_m05_w05":1.0}
for k in set(base_def)|set(fem):
    v=base_def.get(k,0)+fem.get(k,0)
    d=SL.get(k,{})
    if v>0 and "positive" in d: keys[d["positive"]]=v
    elif v<0 and "negative" in d: keys[d["negative"]]=-v
    elif v<0 and "positive" in d and "negative" not in d: pass
print("FACEKEYS",keys)
setkeys(keys)
w=bpy.data.worlds.new("w"); sc.world=w; w.use_nodes=True; w.node_tree.nodes["Background"].inputs[0].default_value=(0.03,0.028,0.035,1); w.node_tree.nodes["Background"].inputs[1].default_value=1
def light(name,typ,col,en,loc,rot,size=1.0):
    l=bpy.data.lights.new(name,typ); l.color=col; l.energy=en
    if typ=='AREA': l.size=size
    o=bpy.data.objects.new(name,l); sc.collection.objects.link(o); o.location=loc; o.rotation_euler=[math.radians(a) for a in rot]; return o
light("key",'AREA',(1,0.85,0.7),180,(-1.0,-1.2,1.9),(55,0,-40),1.2)
light("fill",'AREA',(0.6,0.7,1.0),25,(1.2,-0.8,1.6),(70,0,50),1.5)
light("rim",'AREA',(1,0.8,0.65),60,(0.6,0.9,1.9),(-60,0,150),0.8)
def img(n,cs='Non-Color'):
    i=bpy.data.images.load(T+n); i.colorspace_settings.name=cs; return i
I={k:img(v) for k,v in dict(var="skin_variation.png",ao="skin_ao.png",blush="blush_mask.png",thick="skin_thickness.png",brush="skin_brush.png").items()}
I["base"]=img("skin_painted_female.png",'sRGB')
def mat(P,tone):
    m=bpy.data.materials.new(f"skin_{P}_{tone}"); m.use_nodes=True; nt=m.node_tree; N=nt.nodes; L=nt.links; N.clear()
    out=N.new('ShaderNodeOutputMaterial')
    uv=N.new('ShaderNodeTexCoord')
    def tex(k,scale=None):
        t=N.new('ShaderNodeTexImage'); t.image=I[k]
        if scale:
            mp=N.new('ShaderNodeMapping'); mp.inputs['Scale'].default_value=(scale,scale,1); L.new(uv.outputs['UV'],mp.inputs[0]); L.new(mp.outputs[0],t.inputs[0])
        else: L.new(uv.outputs['UV'],t.inputs[0])
        return t.outputs[0]
    def mix(a,b,f,op='MIX'):
        n=N.new('ShaderNodeMix'); n.data_type='RGBA'; n.blend_type=op
        for s,v in ((6,a),(7,b),(0,f)):
            if isinstance(v,(float,int)): n.inputs[s].default_value=v
            elif isinstance(v,tuple): n.inputs[s].default_value=v
            else: L.new(v,n.inputs[s])
        return n.outputs[2]
    def math_(op,a,b=0.0,c=None):
        n=N.new('ShaderNodeMath'); n.operation=op
        for s,v in ((0,a),(1,b)):
            if isinstance(v,(float,int)): n.inputs[s].default_value=v
            else: L.new(v,n.inputs[s])
        if c is not None:
            if isinstance(c,(float,int)): n.inputs[2].default_value=c
            else: L.new(c,n.inputs[2])
        return n.outputs[0]
    def sep(c):
        n=N.new('ShaderNodeSeparateColor'); L.new(c,n.inputs[0]); return n.outputs
    ref=(0.87,0.69,0.55); ratio=tuple(tone[i]/ref[i] for i in range(3))+(1,)
    alb=mix(tex("base"),ratio,1.0,'MULTIPLY'); alb=mix(alb,(1.0,0.94,0.87,1),1.0,'MULTIPLY')
    v=sep(tex("var"))
    alb=mix(alb,(1.0,0.72,0.68,1),math_('MULTIPLY',v[0],0.6),'MULTIPLY')
    alb=mix(alb,(1.0,0.95,0.8,1),math_('MULTIPLY',v[1],0.6),'MULTIPLY')
    alb=mix(alb,(0.86,0.83,0.93,1),math_('MULTIPLY',v[2],0.6),'MULTIPLY')
    br=sep(tex("brush",7))[0]
    bc=math_('ADD',math_('MULTIPLY',math_('SUBTRACT',br,0.5),0.2*P),1.0)
    alb=mix(alb,(1,0.75+0.25,1,1),0,'MIX')
    alb=mix(alb,bc,1.0,'MULTIPLY') if False else mix(alb,(1,1,1,1),0,'MIX')
    # Pinsel auf Farbe (warm): albedo*(1+bc*(1,.75,.55))
    cc=N.new('ShaderNodeCombineColor'); L.new(math_('ADD',1,math_('MULTIPLY',math_('SUBTRACT',br,0.5),0.2*P)),cc.inputs[0]); L.new(math_('ADD',1,math_('MULTIPLY',math_('SUBTRACT',br,0.5),0.15*P)),cc.inputs[1]); L.new(math_('ADD',1,math_('MULTIPLY',math_('SUBTRACT',br,0.5),0.11*P)),cc.inputs[2])
    alb=mix(alb,cc.outputs[0],1.0,'MULTIPLY')
    alb=mix(alb,(0.95*1.35,0.5*1.35,0.48*1.35,1),math_('MULTIPLY',sep(tex("blush"))[0],0.4),'MULTIPLY') if False else mix(alb,mix(alb,(1.28,0.68,0.65,1),1.0,'MULTIPLY'),math_('MULTIPLY',sep(tex("blush"))[0],0.4))
    ao=sep(tex("ao"))[0]
    alb=mix(alb,mix((0.66,0.4,0.38,1),(1,1,1,1),ao),0.6,'MULTIPLY')
    # Licht: weißes Diffuse -> Shader to RGB -> Helligkeit
    d=N.new('ShaderNodeBsdfDiffuse'); d.inputs[0].default_value=(1,1,1,1)
    s2r=N.new('ShaderNodeShaderToRGB'); L.new(d.outputs[0],s2r.inputs[0])
    irr=s2r.outputs[0]
    lum=N.new('ShaderNodeRGBToBW'); L.new(irr,lum.inputs[0])
    wv=math_('POWER',math_('MINIMUM',math_('MULTIPLY',lum.outputs[0],0.9),1.0),0.75)  # wrap-ähnlich
    wv=math_('ADD',wv,math_('MULTIPLY',math_('SUBTRACT',br,0.5),0.36*P))
    wv=math_('MAXIMUM',math_('MINIMUM',wv,1.0),0.0)
    y=math_('MULTIPLY',wv,3.0); fl=math_('FLOOR',y); fr=math_('FRACT',y)
    mr=N.new('ShaderNodeMapRange'); mr.interpolation_type='SMOOTHSTEP'; L.new(fr,mr.inputs[0]); mr.inputs[1].default_value=0.3; mr.inputs[2].default_value=0.7
    stepped=math_('DIVIDE',math_('ADD',fl,mr.outputs[0]),3.0)
    lit=math_('ADD',math_('MULTIPLY',wv,1-min(0.8*P,1)),math_('MULTIPLY',stepped,min(0.8*P,1)))
    shadowcol=mix((0.6,0.52,0.66,1),(0.72,0.48,0.44,1),math_('ADD',0.5,math_('MULTIPLY',math_('SUBTRACT',br,0.5),P)))
    tonec=mix(shadowcol,(1,1,1,1),lit)
    term=math_('MAXIMUM',math_('SUBTRACT',1.0,math_('DIVIDE',math_('ABSOLUTE',math_('SUBTRACT',lit,0.5)),0.28)),0.0)
    tonec=mix(tonec,mix(tonec,(1.25,0.8,0.7,1),1.0,'MULTIPLY'),math_('MULTIPLY',term,0.6))
    col=mix(alb,tonec,1.0,'MULTIPLY')
    litc=N.new('ShaderNodeCombineColor'); [L.new(lit,litc.inputs[i]) for i in range(3)]
    col=mix(col,litc.outputs[0],1.0,'MULTIPLY')
    # Lichtfarbe (Irradiance normiert) einmischen
    col=mix(col,mix((1,1,1,1),irr,0.35,'MULTIPLY'),1.0,'MULTIPLY')
    # Umgebung + Lift
    col=mix(col,mix(alb,mix(shadowcol,(1,1,1,1),0.4),1.0,'MULTIPLY'),0.12,'ADD')
    # Gegenlicht dünne Stellen (vereinfacht: konstanter warmer Zusatz)
    col=mix(col,mix(alb,(1.0,0.4,0.3,1),1.0,'MULTIPLY'),math_('MULTIPLY',sep(tex("thick"))[0],0.15),'ADD')
    # Mitteltöne entsättigen
    hsv=N.new('ShaderNodeHueSaturation'); L.new(col,hsv.inputs['Color']); hsv.inputs['Saturation'].default_value=1-0.25*P*0.6
    col=hsv.outputs[0]
    # Randlicht
    fr_=N.new('ShaderNodeLayerWeight'); fr_.inputs[0].default_value=0.12
    col=mix(col,mix(alb,(1,0.82,0.7,1),1.0,'MULTIPLY'),math_('MULTIPLY',fr_.outputs['Facing'],0.15),'ADD')
    e=N.new('ShaderNodeEmission'); L.new(col,e.inputs[0]); e.inputs[1].default_value=1.0
    # dezentes gemaltes Glanzlicht
    g=N.new('ShaderNodeBsdfGlossy'); g.inputs['Roughness'].default_value=0.45
    ad=N.new('ShaderNodeAddShader'); L.new(e.outputs[0],ad.inputs[0])
    mxs=N.new('ShaderNodeMixShader'); mxs.inputs[0].default_value=0.04; L.new(e.outputs[0],mxs.inputs[1]); L.new(g.outputs[0],mxs.inputs[2])
    L.new(mxs.outputs[0],out.inputs[0])
    return m
cam=bpy.data.objects.new("cam",bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera=cam
cam.data.lens=85
top=max(v.co.z for v in body.data.vertices)
def shoot(path,profile):
    if profile: cam.location=(-1.45,-0.02,top-0.17); cam.rotation_euler=(math.radians(90),0,math.radians(-90))
    else: cam.location=(0,-1.45,top-0.17); cam.rotation_euler=(math.radians(90),0,0)
    sc.render.filepath=path; bpy.ops.render.render(write_still=True)
MED=(0.87,0.69,0.55); LIGHT=(0.97,0.82,0.72); DARK=(0.42,0.27,0.18)
jobs=[(0.0,MED,"p0"),(0.35,MED,"p35"),(0.7,MED,"p70"),(0.35,LIGHT,"light"),(0.35,DARK,"dark")]
for P,tone,tag in jobs:
    m=mat(P,tone); body.data.materials.clear(); body.data.materials.append(m)
    shoot(f"{OUT}/{tag}_front.png",False)
    if tag in("p0","p35","p70"): shoot(f"{OUT}/{tag}_side.png",True)
print("PREVIEW DONE")
