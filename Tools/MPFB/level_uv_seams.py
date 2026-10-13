# v7.4: Farbsprünge an UV-Nähten im Nacken/oberen Rücken (sichtbares "Rechteck" + Linie am Haaransatz) beheben.
# Ursache: Kopf-Insel und Rumpf-Inseln von skin_painted_female.png haben an den Nähten unterschiedliche Grundtöne.
# Lösung: Pro Rumpf-Insel eine weiche, niederfrequente Farbkorrektur, die an der Naht exakt auf die
# Nachbarseite ausgleicht (Kopfseite bleibt unverändert = Gesicht unangetastet) und ins Inselinnere abklingt.
# Aufruf: blender -b --python level_uv_seams.py -- <textur.png>
import bpy, bmesh, numpy as np, sys
from PIL import Image, ImageFilter
TEX=sys.argv[-1]
bpy.ops.wm.open_mainfile(filepath="/workspace/mpfb_player/out/PlayerBase.blend")
ob=bpy.data.objects["Body"]; me=ob.data
bm=bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table(); bm.verts.ensure_lookup_table(); uvl=bm.loops.layers.uv.active
img=Image.open(TEX).convert("RGB"); W,H=img.size
A=np.asarray(img).astype(np.float32)/255
Ab=np.asarray(img.filter(ImageFilter.GaussianBlur(6))).astype(np.float32)/255
def samp(uv):
    x=min(max(int(uv[0]*W),0),W-1); y=min(max(int((1-uv[1])*H),0),H-1); return Ab[y,x]
# UV-Inseln (Union-Find über Kanten mit gleichen UVs)
par=list(range(len(bm.faces)))
def f(i):
    while par[i]!=i: par[i]=par[par[i]]; i=par[i]
    return i
seam=[]
for e in bm.edges:
    if len(e.link_faces)!=2: continue
    a,b=e.link_faces
    def uvs(fc): return {l.vert.index:np.array(l[uvl].uv) for l in fc.loops if l.vert in e.verts}
    ua,ub=uvs(a),uvs(b)
    if all(np.linalg.norm(ua[k]-ub[k])<1e-5 for k in ua): par[f(a.index)]=f(b.index)
    else: seam.append((e,a,b,ua,ub))
isl=np.array([f(i) for i in range(len(bm.faces))])
co=np.array([v.co[:] for v in bm.verts])
# Kopf-Insel = Insel des Mundvertex 474
head=isl[bm.verts[474].link_faces[0].index]
# Korrekturwerte an Nahtpunkten, je (Insel, Vertex)
fix={}
for e,a,b,ua,ub in seam:
    z=co[[v.index for v in e.verts],2].mean()
    if not (1.10<z<1.50): continue
    ia,ib=isl[a.index],isl[b.index]
    for t in (0.0,0.5,1.0):
        vs=[v.index for v in e.verts]
        pa=ua[vs[0]]*(1-t)+ua[vs[1]]*t; pb=ub[vs[0]]*(1-t)+ub[vs[1]]*t
        ca,cb=samp(pa),samp(pb)
        tgt_a = cb if ib==head else (ca if ia==head else (ca+cb)/2)
        tgt_b = ca if ia==head else (cb if ib==head else (ca+cb)/2)
        for vi in vs:
            w=1-t if vi==vs[0] else t
            if w<0.25: continue
            if ia!=head: fix.setdefault((ia,vi),[]).append(tgt_a-ca)
            if ib!=head: fix.setdefault((ib,vi),[]).append(tgt_b-cb)
fix={k:np.mean(v,0) for k,v in fix.items()}
print("SEAMPTS",len(fix),"max",max(np.abs(v).max() for v in fix.values()))
# Ausbreiten ins Inselinnere (Abklingen über ~6 Kantenringe)
adj={}
for fc in bm.faces:
    i=isl[fc.index]
    if i==head: continue
    vs=[v.index for v in fc.verts]
    for k in range(len(vs)):
        a=(i,vs[k]); b=(i,vs[(k+1)%len(vs)])
        adj.setdefault(a,set()).add(b); adj.setdefault(b,set()).add(a)
corr={k:np.zeros(3) for k in adj}
for k,v in fix.items():
    if k in corr: corr[k]=v
for _ in range(40):
    new={}
    for k,nb in adj.items():
        if k in fix: new[k]=fix[k]; continue
        new[k]=0.93*np.mean([corr[n] for n in nb],0)
    corr=new
# Rastern
D=np.zeros((H,W,3),np.float32); Wt=np.zeros((H,W),np.float32)
for fc in bm.faces:
    i=isl[fc.index]
    if i==head: continue
    cs=[corr.get((i,l.vert.index),np.zeros(3)) for l in fc.loops]
    if max(np.abs(c).max() for c in cs)<1e-4: continue
    P=[np.array(l[uvl].uv)*[W,H] for l in fc.loops]
    for k in range(1,len(P)-1):
        tri=[P[0],P[k],P[k+1]]; tc=[cs[0],cs[k],cs[k+1]]
        xs=[p[0] for p in tri]; ys=[H-p[1] for p in tri]
        x0,x1=int(max(min(xs)-2,0)),int(min(max(xs)+2,W-1)); y0,y1=int(max(min(ys)-2,0)),int(min(max(ys)+2,H-1))
        if x1<x0 or y1<y0: continue
        gx,gy=np.meshgrid(np.arange(x0,x1+1)+0.5,np.arange(y0,y1+1)+0.5)
        (ax,ay),(bx,by),(cx,cy)=[(p[0],H-p[1]) for p in tri]
        den=(by-cy)*(ax-cx)+(cx-bx)*(ay-cy)
        if abs(den)<1e-9: continue
        l1=((by-cy)*(gx-cx)+(cx-bx)*(gy-cy))/den; l2=((cy-ay)*(gx-cx)+(ax-cx)*(gy-cy))/den; l3=1-l1-l2
        m=(l1>-0.02)&(l2>-0.02)&(l3>-0.02)
        val=l1[...,None]*tc[0]+l2[...,None]*tc[1]+l3[...,None]*tc[2]
        D[y0:y1+1,x0:x1+1][m]=val[m]; Wt[y0:y1+1,x0:x1+1][m]=1
# Dilatation über Inselränder (Padding), damit Mip/Bilinear an Nähten passt
from scipy import ndimage
idx=ndimage.distance_transform_edt(Wt==0,return_distances=False,return_indices=True)
Dd=D[idx[0],idx[1]]; near=ndimage.distance_transform_edt(Wt==0)<8
Dd[~near]=0
# nur Pixel der Rumpf-Inseln (bzw. ihr Padding) ändern, Kopfinsel nie
out=np.clip(A+Dd,0,1)
Image.fromarray((out*255+0.5).astype(np.uint8)).save(TEX)
print("LEVEL DONE")
