# Gezielte, glatte anatomische Form-Details (kein Rauschen) für den MPFB-Körper.
# Wird benutzt von: add_anatomy_morph.py (Mesh-Morph: Schlüsselbeine, Grube darüber, Drosselgrube)
#                   bake_detail_maps.py   (Normal-/Detail-Map: Gesicht, Halssehnen, Schlüsselbeine ...)
# Alle Angaben in Metern, Blender-Koordinaten (vorne = -Y, oben = +Z).
import numpy as np
from mathutils import Vector
from mathutils.kdtree import KDTree

class Surf:
    def __init__(self, co, nr):
        self.co=co; self.nr=nr
        self.kd=KDTree(len(co))
        for i,p in enumerate(co): self.kd.insert(Vector(p),i)
        self.kd.balance()
    def snap(self, p, front=True):
        # nächster Oberflächenpunkt; bei front nur nach vorne/außen zeigende Punkte
        best=None
        for (c,i,d) in self.kd.find_n(Vector(p),24):
            if front and self.nr[i][1]>0.2: continue
            best=i; break
        if best is None: best=self.kd.find(Vector(p))[1]
        return self.co[best], self.nr[best]

def polyline(S, pts, n=12, front=True):
    pts=[np.array(p,float) for p in pts]; out=[]; nrm=[]
    for a,b in zip(pts[:-1],pts[1:]):
        for t in np.linspace(0,1,n,endpoint=False): q,nn=S.snap(a+(b-a)*t,front); out.append(q); nrm.append(nn)
    q,nn=S.snap(pts[-1],front); out.append(q); nrm.append(nn)
    return np.array(out),np.array(nrm)

def line_field(co, nr, P, PN, amp, width, along=None):
    # Gauß-Profil um eine Oberflächen-Polylinie; nur auf gleich orientierter Oberfläche
    best=np.full(len(co),1e9); bn=np.zeros((len(co),3)); bt=np.zeros(len(co))
    for k in range(len(P)-1):
        a,b=P[k],P[k+1]; ab=b-a; L2=max(ab@ab,1e-12)
        t=np.clip(((co-a)@ab)/L2,0,1); q=a+t[:,None]*ab
        d=np.linalg.norm(co-q,axis=1); m=d<best
        best[m]=d[m]; bn[m]=PN[k]; bt[m]=(k+t[m])/(len(P)-1)
    facing=np.clip((np.einsum('ij,ij->i',nr,bn)-0.3)/0.5,0,1)
    w=np.exp(-(best/width)**2)*facing
    if along is not None: w*=along(bt)
    return amp*w

def point_field(co, nr, p, pn, amp, width):
    d=np.linalg.norm(co-p,axis=1); facing=np.clip((nr@pn-0.3)/0.5,0,1)
    return amp*np.exp(-(d/width)**2)*facing

def taper(t): return np.sin(np.clip(t,0,1)*np.pi)**0.6   # Enden weich auslaufen

def clavicle_landmarks(rig):
    mw=rig.matrix_world; lm={}
    for s in ("l","r"):
        b=rig.data.bones[f"clavicle_{s}"]
        lm[f"clav_{s}"]=(np.array(mw@b.head_local),np.array(mw@b.tail_local))
    return lm

def body_offsets(co, nr, lm, S, scale=1.0, wscale=1.0):
    """Schlüsselbein-Grat, Grube darüber, Drosselgrube (für Mesh-Morph und Bake)."""
    d=np.zeros(len(co)); heads=[]
    for s in ("l","r"):
        h,t=lm[f"clav_{s}"]; heads.append(h)
        sgn=np.sign(t[0]-h[0]) or 1.0
        # Schlüsselbein: leichte S-Kurve vom Brustbein zur Schulter, etwas vor dem Knochen
        a=h+np.array([sgn*0.012,-0.06,-0.005]); b=h+(t-h)*0.5+np.array([0,-0.06,0.006]); c=t+np.array([-sgn*0.012,-0.04,0.004])
        P,PN=polyline(S,[a,b,c])
        d+=line_field(co,nr,P,PN,0.0032*scale,0.0075*wscale,taper)
        # Grube über dem Schlüsselbein (supraklavikulär)
        P2,PN2=polyline(S,[a+np.array([sgn*0.012,0.01,0.022]),b+np.array([0,0.015,0.024]),c+np.array([-sgn*0.02,0.02,0.02])])
        d+=line_field(co,nr,P2,PN2,-0.0026*scale,0.011*wscale,taper)
    mid=(heads[0]+heads[1])/2
    p,pn=S.snap(mid+np.array([0,-0.08,0.012]))
    d+=point_field(co,nr,p,pn,-0.0038*scale,0.011*wscale)
    return d, p

def face_offsets(co, nr, S, lip_u, lip_l, eyes, notch, scale=1.0):
    """Philtrum, Nasenflügel, Nasolabialfalte, Lidfalte, Halssehnen (SCM)."""
    d=np.zeros(len(co)); M=(co[lip_u]+co[lip_l])/2
    sel=(np.abs(co[:,0])<0.004)&(co[:,2]>M[2]+0.01)&(co[:,2]<M[2]+0.06)
    tip=co[sel][np.argmin(co[sel][:,1])]
    sel2=(np.abs(co[:,0])<0.003)&(co[:,2]>M[2]+0.006)&(co[:,2]<tip[2]-0.006)&(co[:,1]<tip[1]+0.03)
    sn=co[sel2][np.argmax(co[sel2][:,1])] if sel2.any() else tip+np.array([0,0.015,-0.015])
    top=co[lip_u]+np.array([0,0,0.004])
    # Philtrum: Rinne in der Mitte, zwei weiche Säulen
    P,PN=polyline(S,[sn,top],8); d+=line_field(co,nr,P,PN,-0.0007*scale,0.0028)
    for sx in (-1,1):
        P,PN=polyline(S,[sn+np.array([sx*0.004,0,0]),top+np.array([sx*0.0055,0,0])],8); d+=line_field(co,nr,P,PN,0.0005*scale,0.002)
        # Nasenflügel-Falte (Bogen um den Flügel)
        alar=[sn+np.array([sx*0.008,0.003,-0.001]),sn+np.array([sx*0.0165,0.006,0.004]),sn+np.array([sx*0.018,0.004,0.012]),sn+np.array([sx*0.013,-0.002,0.018])]
        P,PN=polyline(S,alar,6); d+=line_field(co,nr,P,PN,-0.0009*scale,0.0018)
        # Nasolabial: weiche Linie Nasenflügel -> neben Mundwinkel
        P,PN=polyline(S,[sn+np.array([sx*0.020,0.004,0.004]),M+np.array([sx*0.027,0.0,0.008]),M+np.array([sx*0.031,0.0,-0.01])],8)
        d+=line_field(co,nr,P,PN,-0.0005*scale,0.0045,taper)
    # Lidfalten
    for E in eyes:
        arc=[E+np.array([dx,-0.012,0.0105-28*dx*dx]) for dx in np.linspace(-0.012,0.013,7)]
        P,PN=polyline(S,arc,4); d+=line_field(co,nr,P,PN,-0.0006*scale,0.0016,taper)
    # Halssehnen (M. sternocleidomastoideus): hinter dem Ohr -> Brustbein neben der Drosselgrube
    for sx in (-1,1):
        E=eyes[0] if np.sign(eyes[0][0])==sx else eyes[1]
        mast=np.array([sx*0.058,E[1]+0.085,E[2]-0.045]); st=notch+np.array([sx*0.012,0.0,0.006])
        midp=(mast+st)/2+np.array([sx*0.006,-0.03,0])
        P,PN=polyline(S,[mast,midp,st],10,front=False)
        d+=line_field(co,nr,P,PN,0.0016*scale,0.008,taper)
    return d

def face_zones(co, nr, lip_u, lip_l, eyes):
    """Glatte Zonen-Masken (0..1) für Glanz und gemalte Farbzonen. Nur große, weiche Gaußverläufe."""
    M=(co[lip_u]+co[lip_l])/2
    sel=(np.abs(co[:,0])<0.004)&(co[:,2]>M[2]+0.01)&(co[:,2]<M[2]+0.06)
    tip=co[sel][np.argmin(co[sel][:,1])]
    E=(eyes[0]+eyes[1])/2
    front=np.clip((-nr[:,1]-0.1)/0.4,0,1)
    def g(p,sx,sy,sz):
        r=(co-p)/np.array([sx,sy,sz]); return np.exp(-(r*r).sum(1))
    fore=g(E+np.array([0,-0.01,0.05]),0.05,0.06,0.035)*front
    nose=np.maximum(g(tip,0.012,0.02,0.02),g((tip+E)/2,0.008,0.03,0.022))
    lips=g(M,0.025,0.02,0.012)
    cheekL=g(np.array([eyes[0][0]-0.008,eyes[0][1]-0.005,eyes[0][2]-0.035]),0.03,0.04,0.026)
    cheekR=g(np.array([eyes[1][0]+0.008,eyes[1][1]-0.005,eyes[1][2]-0.035]),0.03,0.04,0.026)
    cheeks=np.maximum(cheekL,cheekR)
    under=np.maximum(g(eyes[0]+np.array([0,-0.012,-0.014]),0.014,0.02,0.007),g(eyes[1]+np.array([0,-0.012,-0.014]),0.014,0.02,0.007))
    nostr=np.maximum(g(tip+np.array([0.011,0.008,-0.006]),0.009,0.015,0.008),g(tip+np.array([-0.011,0.008,-0.006]),0.009,0.015,0.008))
    jaw=np.clip((M[2]-0.02-co[:,2])/0.03,0,1)*np.clip((co[:,2]-(M[2]-0.2))/0.03,0,1)   # Kiefer/Hals
    # Glanz: Stirn, Nase, Lippen hoch; Wangen/Kinn/Hals matt
    gloss=np.clip(0.35+0.55*np.maximum(np.maximum(fore,nose),lips)-0.2*cheeks-0.15*jaw,0,1)
    red=np.clip(0.9*cheeks+0.8*g(tip,0.012,0.02,0.014)+0.7*nostr,0,1)
    warm=np.clip(0.8*under,0,1)
    cool=np.clip(0.5*jaw,0,1)
    return gloss, red, warm, cool
