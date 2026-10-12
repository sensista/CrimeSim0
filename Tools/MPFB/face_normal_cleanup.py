import numpy as np, subprocess, io
from PIL import Image, ImageFilter
def ss(e0,e1,x):
    t=np.clip((x-e0)/(e1-e0),0,1);return t*t*(3-2*t)
orig=Image.open(io.BytesIO(subprocess.run("cd /workspace/CrimeSim0 && git show lookdev-v6:Assets/_Project/Art/Textures/Character/skin_painted_female.png | git lfs smudge",shell=True,capture_output=True).stdout)).convert('L')
W=2048
hp=np.abs(np.asarray(orig).astype(float)-np.asarray(orig.filter(ImageFilter.GaussianBlur(10))).astype(float))
feat=Image.fromarray(np.clip((hp-8)*20,0,255).astype(np.uint8)).filter(ImageFilter.MaxFilter(41)).filter(ImageFilter.GaussianBlur(16))
feat=np.asarray(feat).astype(float)/255
X,Y=np.meshgrid(np.arange(W)/W,np.arange(W)/W)
import json
from PIL import ImageDraw
polys=json.load(open('/tmp/an/uv.json'))
hm=Image.new('L',(W,W),0); dr=ImageDraw.Draw(hm)
for p in polys:
    us=[u for u,v in p]; vs=[1-v for u,v in p]
    if min(us)>0.6 and min(vs)>0.08 and max(vs)<0.92: dr.polygon([(u*W,(1-v)*W) for u,v in p],fill=255)
head=np.asarray(hm.filter(ImageFilter.MaxFilter(15)).filter(ImageFilter.GaussianBlur(4))).astype(float)/255
neutralize=(head*(1-feat*0.6*(1-ss(0.70,0.74,X))))[...,None]  # Gesicht komplett glatt, Ohren behalten Form
for v in ('female','male'):
    f=f'/workspace/CrimeSim0/Assets/_Project/Art/Textures/Character/skin_normal_{v}.png'
    im=Image.open(f).convert('RGB')
    a=np.asarray(im.filter(ImageFilter.GaussianBlur(3))).astype(float)/255   # Riffel glätten
    flat=np.array([0.5,0.5,1.0])
    k=0.95*neutralize
    out=a*(1-k)+flat*k
    n=out*2-1; n/=np.maximum(np.linalg.norm(n,axis=2,keepdims=True),1e-4)
    Image.fromarray(np.clip((n*0.5+0.5)*255+0.5,0,255).astype(np.uint8)).save(f)
print("FACENRM OK")
