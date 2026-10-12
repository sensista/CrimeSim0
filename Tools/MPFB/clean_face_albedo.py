# Glättet feine Kratzer/Striche im Gesichts-Albedo; nur Augen, Nasenlöcher, Lippen (starke Kontraste) bleiben scharf.
import sys, numpy as np
from PIL import Image, ImageFilter
def ss(e0,e1,x):
    t=np.clip((x-e0)/(e1-e0),0,1); return t*t*(3-2*t)
for f in sys.argv[1:]:
    im=Image.open(f).convert('RGB'); W=im.size[0]
    a=np.asarray(im).astype(float)
    L=im.convert('L'); hp=np.abs(np.asarray(L).astype(float)-np.asarray(L.filter(ImageFilter.GaussianBlur(10))).astype(float))
    strong=Image.fromarray(np.clip((hp-22)*25,0,255).astype(np.uint8)).filter(ImageFilter.MaxFilter(21)).filter(ImageFilter.GaussianBlur(6))
    keep=np.asarray(strong).astype(float)/255
    X,Y=np.meshgrid(np.arange(W)/W,np.arange(W)/W)
    region=ss(0.6,0.63,X)*ss(0.08,0.12,Y)*(1-ss(0.88,0.92,Y))
    b=np.asarray(im.filter(ImageFilter.GaussianBlur(5))).astype(float)
    w=(region*(1-keep))[...,None]
    Image.fromarray(np.clip(a*(1-w)+b*w,0,255).astype(np.uint8)).save(f); print("clean",f)
