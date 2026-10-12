# Entfernt die gemalte dunkle/gezackte Linie im Lippenspalt: Albedo, AO und Kavität im Mundbereich weich ausgleichen.
# Aufruf: python3 soften_mouth_slit.py lips.json <Texturordner>
import sys, json, numpy as np
from PIL import Image, ImageDraw, ImageFilter
polys=json.load(open(sys.argv[1])); D=sys.argv[2]
def region(W):
    m=Image.new('L',(W,W),0); d=ImageDraw.Draw(m)
    for p in polys: d.polygon([(u*W,(1-v)*W) for u,v in p["uv"]],fill=255)
    m=m.filter(ImageFilter.MaxFilter(int(9*W/2048)|1)).filter(ImageFilter.GaussianBlur(10*W/2048))
    return np.asarray(m).astype(float)[...,None]/255
def fix(f, chans=None):
    im=Image.open(f'{D}/{f}').convert('RGB'); W=im.size[0]; w=region(W)
    a=np.asarray(im).astype(float)
    # Dunkle Linie: Minimum-Ausreißer durch helleres, weiches Umfeld ersetzen (Max-Filter + Blur)
    b=np.asarray(im.filter(ImageFilter.MaxFilter(int(7*W/2048)|1)).filter(ImageFilter.GaussianBlur(8*W/2048))).astype(float)
    if chans is not None:
        keep=np.ones(3,bool); keep[chans]=False; b[...,keep]=a[...,keep]
    Image.fromarray(np.clip(a*(1-w)+b*w,0,255).astype(np.uint8)).save(f'{D}/{f}'); print("slit",f)
for f in ("skin_painted_female.png","skin_painted_male.png","skin_ao.png"): fix(f)
for f in ("skin_detail_female.png","skin_detail_male.png"): fix(f,[0])   # nur Kavität (R)
