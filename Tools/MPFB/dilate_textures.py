# Blutet Texturen über die UV-Inselränder hinaus aus (gegen dunkle/helle Nähte durch Filterung/Mipmaps).
# Aufruf: python3 dilate_textures.py <uv.json aus uv_coverage.py> <textur.png> [...]
import sys, json, numpy as np
from PIL import Image, ImageDraw, ImageFilter
polys=json.load(open(sys.argv[1]))
for f in sys.argv[2:]:
    im=Image.open(f); mode=im.mode; a=np.asarray(im.convert('RGB')).astype(np.float32); H,W=a.shape[:2]
    m=Image.new("L",(W,H),0); d=ImageDraw.Draw(m)
    for p in polys: d.polygon([(u*W,(1-v)*H) for u,v in p],fill=255)
    inuv=np.asarray(m)>127
    # Nur echte Hintergrundpixel (gleich der Ecke oben links) außerhalb der UV-Inseln füllen; nie Inhalt verändern
    bg=a[2,2]; isbg=np.all(np.abs(a-bg)<1.5,axis=2)
    valid=(~((~inuv)&isbg)).astype(np.float32)
    col=a*valid[...,None]; w=valid.copy()
    known=valid>0
    for it in range(int(max(W,H)/32)):
        s=np.zeros_like(col); ws=np.zeros_like(w)
        for dy in (-1,0,1):
            for dx in (-1,0,1):
                s+=np.roll(np.roll(col,dy,0),dx,1); ws+=np.roll(np.roll(w,dy,0),dx,1)
        new=(ws>0)&(~known)
        col[new]=s[new]/ws[new][:,None]; w[new]=1; known|=new
    out=np.where(valid[...,None]>0,a,col)
    Image.fromarray(np.clip(out+0.5,0,255).astype(np.uint8)).convert(mode if mode in("RGB","L","RGBA") else "RGB").save(f)
    print("dilated",f)
