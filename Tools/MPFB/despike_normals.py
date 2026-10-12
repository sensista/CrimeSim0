# Entfernt einzelne Bake-Ausreißer (Kratzer/Flecken) aus Normal Maps: Pixel, die stark vom weichen Umfeld
# abweichen, werden weich durch das Umfeld ersetzt. Echte, weiche Formen bleiben erhalten.
import sys, numpy as np
from PIL import Image, ImageFilter
for f in sys.argv[1:]:
    im=Image.open(f).convert('RGB'); a=np.asarray(im).astype(np.float32)/255
    b=np.asarray(im.filter(ImageFilter.GaussianBlur(6))).astype(np.float32)/255
    dev=np.linalg.norm(a-b,axis=2)
    m=Image.fromarray(np.clip((dev-0.06)/0.06*255,0,255).astype(np.uint8)).filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.GaussianBlur(3))
    m=np.asarray(m).astype(np.float32)[...,None]/255
    out=a*(1-m)+b*m
    n=out*2-1; n/=np.maximum(np.linalg.norm(n,axis=2,keepdims=True),1e-4)
    Image.fromarray(np.clip((n*0.5+0.5)*255+0.5,0,255).astype(np.uint8)).save(f)
    print("despiked",f,float((m>0.5).mean()))
