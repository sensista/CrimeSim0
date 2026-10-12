# Erzeugt eine weiche Lippen-Tönungsmaske (G-Kanal der Detail-Map) aus der echten Lippengeometrie:
# Lippen-Polygone (um die Mundmitte, vorne) -> UV-Polygone als JSON; danach in PIL rastern + stark weichzeichnen.
# Aufruf: blender -b PlayerBase.blend --python lip_tint_mask.py -- out.json
import bpy, json, sys, numpy as np
out=sys.argv[-1]
me=bpy.data.objects["Body"].data
co=np.array([v.co[:] for v in me.vertices])
U,L=co[474],co[523]; M=(U+L)/2
uv=me.uv_layers.active.data
polys=[]
for p in me.polygons:
    c=np.mean([co[v] for v in p.vertices],axis=0)
    dx=abs(c[0]-M[0])/0.025; dz=(c[2]-M[2])
    if c[1]>M[1]+0.012: continue          # nur Vorderseite
    # Lippenkörper: oben bis ~9 mm, unten bis ~10 mm, nach außen schmaler
    lim_up=0.009*(1-0.55*dx*dx); lim_dn=0.010*(1-0.5*dx*dx)
    if dx>1 or dz>lim_up or dz<-lim_dn: continue
    w=max(0.0,1-dx**2)
    polys.append({"uv":[tuple(uv[li].uv) for li in p.loop_indices],"w":w})
json.dump(polys,open(out,"w")); print("LIPPOLYS",len(polys))
