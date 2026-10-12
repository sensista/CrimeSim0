# Rastert lips.json in den G-Kanal der Detail-Maps (weich, keine Kante). Aufruf: python3 apply_lip_mask.py lips.json map1.png ...
import sys, json, numpy as np
from PIL import Image, ImageDraw, ImageFilter
polys=json.load(open(sys.argv[1]))
for f in sys.argv[2:]:
    r,g,b=Image.open(f).convert('RGB').split(); W=r.size[0]
    m=Image.new('L',(W,W),0); d=ImageDraw.Draw(m)
    for p in polys: d.polygon([(u*W,(1-v)*W) for u,v in p["uv"]],fill=int(255*p["w"]))
    m=m.filter(ImageFilter.GaussianBlur(14*W/2048))
    a=np.asarray(m).astype(float); a=a/max(a.max(),1)*0.85*255
    Image.merge('RGB',(r,Image.fromarray(a.astype(np.uint8)),b)).save(f); print("lipmask",f)
