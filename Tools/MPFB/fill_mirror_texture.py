# v7.4: helles "Rechteck" auf Nacken/oberem Rücken (skin_painted_*.png).
# Ursache: Eine Rücken-UV-Insel liegt großteils auf ungemaltem Füllbereich (konstante Farbe, heller, ohne
# Hautvariation) -> harte Kanten an Wirbelsäule und Schulter. Die gespiegelte Insel der anderen Körperseite
# ist korrekt bemalt.
# Lösung: Betroffene Inseln werden komplett aus der Spiegel-Insel übernommen (pro Dreieck gleiche Baryzentrik,
# gespiegelte Vertices) -> anatomisch symmetrische, bemalte Haut; Kopf-/Gesichtsinsel unverändert.
# Danach level_uv_seams.py für die Nahtangleichung.  Aufruf: blender -b --python fill_mirror_texture.py -- <textur.png>
import bpy, bmesh, numpy as np, sys
from PIL import Image
from mathutils import kdtree
TEX=sys.argv[-1]
bpy.ops.wm.open_mainfile(filepath="/workspace/mpfb_player/out/PlayerBase.blend")
me=bpy.data.objects["Body"].data
bm=bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table(); bm.verts.ensure_lookup_table(); uvl=bm.loops.layers.uv.active
co=np.array([v.co[:] for v in bm.verts]); n=len(co)
kd=kdtree.KDTree(n)
for i,c in enumerate(co): kd.insert(c,i)
kd.balance()
MIR=np.array([kd.find((-c[0],c[1],c[2]))[1] for c in co])
img=np.asarray(Image.open(TEX).convert("RGB")).astype(np.float32); H,W,_=img.shape
# UV-Inseln
par=list(range(len(bm.faces)))
def f_(i):
    while par[i]!=i: par[i]=par[par[i]]; i=par[i]
    return i
for e in bm.edges:
    if len(e.link_faces)!=2: continue
    a,b=e.link_faces
    ua={l.vert.index:tuple(np.round(l[uvl].uv,6)) for l in a.loops if l.vert in e.verts}
    ub={l.vert.index:tuple(np.round(l[uvl].uv,6)) for l in b.loops if l.vert in e.verts}
    if ua==ub: par[f_(a.index)]=f_(b.index)
isl=np.array([f_(i) for i in range(len(bm.faces))])
fkey={tuple(sorted(v.index for v in f.verts)):f for f in bm.faces}
def fuv(f): return {l.vert.index:np.array(l[uvl].uv)*[W,H] for l in f.loops}
def tris(f):
    U=fuv(f); vs=[v.index for v in f.verts]
    for k in range(1,len(vs)-1): yield (vs[0],vs[k],vs[k+1]),[U[vs[0]],U[vs[k]],U[vs[k+1]]]
def raster(T):
    A=np.array([[T[1][0]-T[0][0],T[2][0]-T[0][0]],[T[1][1]-T[0][1],T[2][1]-T[0][1]]])
    if abs(np.linalg.det(A))<1e-9: return None
    xs=[p[0] for p in T]; ys=[p[1] for p in T]
    x0,x1=int(max(min(xs)-2,0)),int(min(max(xs)+2,W-1)); y0,y1=int(max(min(ys)-2,0)),int(min(max(ys)+2,H-1))
    gx,gy=np.meshgrid(np.arange(x0,x1+1)+0.5,np.arange(y0,y1+1)+0.5)
    rel=np.stack([gx-T[0][0],gy-T[0][1]],-1)@np.linalg.inv(A).T
    b1,b2=rel[...,0],rel[...,1]; m=(1-b1-b2>-0.04)&(b1>-0.04)&(b2>-0.04)
    return gx[m],gy[m],b1[m],b2[m]
# Füllfarbe = häufigster exakt konstanter Wert; Anteil je Insel
flatv=img.reshape(-1,3).astype(np.uint8); vals,cnt=np.unique(flatv,axis=0,return_counts=True); FILL=vals[np.argmax(cnt)].astype(np.float32)
const=np.abs(img-FILL).max(2)<=1.0
print("FILL colour",FILL)
def ffrac(f):
    c=t=0
    for _,T in tris(f):
        r=raster(T)
        if r is None: continue
        px=r[0].astype(int).clip(0,W-1); py=(H-r[1]).astype(int).clip(0,H-1); c+=const[py,px].sum(); t+=len(px)
    return c/max(t,1)
FF=np.array([ffrac(f) for f in bm.faces])
# Insel betroffen, wenn ein deutlicher Teil auf Füllfarbe liegt; kopiert wird jede Fläche mit Füllanteil, deren Spiegel bemalt ist
bad=set()
out=img.copy(); nf=0
for f in bm.faces:
    i=isl[f.index]
    c=co[[v.index for v in f.verts]].mean(0)
    if c[2]<0.9 or FF[f.index]<0.003: continue
    mf=fkey.get(tuple(sorted(MIR[v.index] for v in f.verts)))
    if mf is None or FF[mf.index]>=FF[f.index]*0.3: continue
    MU=fuv(mf)
    for (a,b,c_),T in tris(f):
        S=[MU[MIR[a]],MU[MIR[b]],MU[MIR[c_]]]
        r=raster(T)
        if r is None: continue
        gx,gy,b1,b2=r
        sx=S[0][0]+b1*(S[1][0]-S[0][0])+b2*(S[2][0]-S[0][0]); sy=S[0][1]+b1*(S[1][1]-S[0][1])+b2*(S[2][1]-S[0][1])
        out[(H-gy).astype(int).clip(0,H-1),gx.astype(int).clip(0,W-1)]=img[(H-sy).astype(int).clip(0,H-1),sx.astype(int).clip(0,W-1)]
    nf+=1
print("faces mirrored",nf,"fill faces",int((FF>0.05).sum()))
Image.fromarray(np.clip(out+0.5,0,255).astype(np.uint8)).save(TEX)
