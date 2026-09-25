from PIL import Image, ImageDraw, ImageFont, ImageFilter
import math
S=4; W=256*S
BOLD='/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf'
def F(sz): return ImageFont.truetype(BOLD, sz*S)
def lerp(a,b,t): return tuple(int(a[i]+(b[i]-a[i])*t) for i in range(len(a)))
def bg(top,bot,glow):
    im=Image.new('RGBA',(W,W))
    d=ImageDraw.Draw(im)
    for y in range(W): d.line([(0,y),(W,y)],fill=lerp(top,bot,y/W)+(255,))
    g=Image.new('RGBA',(W,W),(0,0,0,0)); gd=ImageDraw.Draw(g)
    gd.ellipse([W*0.12,W*0.05,W*0.88,W*0.72],fill=glow+(110,))
    g=g.filter(ImageFilter.GaussianBlur(40*S))
    im=Image.alpha_composite(im,g)
    # inner border
    d=ImageDraw.Draw(im)
    d.rounded_rectangle([6*S,6*S,W-6*S,W-6*S],radius=22*S,outline=(255,255,255,60),width=3*S)
    return im
def shadow(im,box,alpha=140,blur=8):
    s=Image.new('RGBA',im.size,(0,0,0,0)); ImageDraw.Draw(s).ellipse(box,fill=(0,0,0,alpha))
    return Image.alpha_composite(im,s.filter(ImageFilter.GaussianBlur(blur*S)))
def cart(im,cx,base,w,h,depth,body_top,body_bot,outline=(20,18,28)):
    """Side/3-quarter cart: basket trapezoid with top rim depth, mesh, handle, wheels. cx=center x, base=y of wheel bottoms."""
    im=shadow(im,[cx-w*0.62,base-10*S,cx+w*0.62,base+8*S])
    lay=Image.new('RGBA',im.size,(0,0,0,0)); d=ImageDraw.Draw(lay)
    r=h*0.16; wy=base-r
    top=wy-r*0.6-h
    x0,x1=cx-w/2,cx+w/2
    bl,br=x0+w*0.10,x1-w*0.10
    # back rim (depth)
    dx,dy=depth*0.55,-depth*0.45
    d.polygon([(x0,top),(x1,top),(x1+dx,top+dy),(x0+dx,top+dy)],fill=lerp(body_top,(0,0,0),0.45)+(255,),outline=outline,width=4*S)
    # body gradient
    body=Image.new('RGBA',im.size,(0,0,0,0)); bd=ImageDraw.Draw(body)
    for y in range(int(top),int(wy-r*0.6)+1):
        t=(y-top)/max(1,(wy-r*0.6-top)); bd.line([(0,y),(W,y)],fill=lerp(body_top,body_bot,t)+(255,))
    mask=Image.new('L',im.size,0); md=ImageDraw.Draw(mask)
    poly=[(x0,top),(x1,top),(br,wy-r*0.6),(bl,wy-r*0.6)]
    md.polygon(poly,fill=255)
    lay.paste(body,(0,0),mask)
    d=ImageDraw.Draw(lay)
    # mesh lines
    n=5
    for i in range(1,n):
        t=i/n; xa=x0+(x1-x0)*t; xb=bl+(br-bl)*t
        d.line([(xa,top),(xb,wy-r*0.6)],fill=(255,255,255,70),width=3*S)
    for j in range(1,3):
        t=j/3; y=top+(wy-r*0.6-top)*t; xa=x0+(bl-x0)*t; xb=x1+(br-x1)*t
        d.line([(xa,y),(xb,y)],fill=(255,255,255,70),width=3*S)
    d.polygon(poly,outline=outline,width=5*S)
    # rim highlight
    d.line([(x0+4*S,top+4*S),(x1-4*S,top+4*S)],fill=(255,255,255,150),width=3*S)
    # handle
    hx=x0-w*0.16
    d.line([(x0,top+h*0.12),(hx,top-h*0.22)],fill=outline,width=9*S)
    d.line([(x0,top+h*0.12),(hx,top-h*0.22)],fill=(200,205,215),width=5*S)
    d.line([(hx-8*S,top-h*0.22),(hx+10*S,top-h*0.22)],fill=outline,width=11*S)
    # frame + wheels
    d.line([(bl,wy-r*0.6),(br,wy-r*0.6)],fill=outline,width=6*S)
    for wx in (bl+w*0.08,br-w*0.08):
        d.ellipse([wx-r,wy-r,wx+r,wy+r],fill=(35,35,42),outline=outline,width=4*S)
        d.ellipse([wx-r*0.42,wy-r*0.42,wx+r*0.42,wy+r*0.42],fill=(170,175,185))
    return Image.alpha_composite(im,lay),(x0,x1,top,wy-r*0.6)
def text(im,y,s,size,fill,stroke=(15,12,22)):
    d=ImageDraw.Draw(im); f=F(size)
    tw=d.textlength(s,font=f); x=(W-tw)/2
    sh=Image.new('RGBA',im.size,(0,0,0,0)); ImageDraw.Draw(sh).text((x+2*S,y+3*S),s,font=f,fill=(0,0,0,160))
    im=Image.alpha_composite(im,sh.filter(ImageFilter.GaussianBlur(2*S)))
    ImageDraw.Draw(im).text((x,y),s,font=f,fill=fill,stroke_width=3*S,stroke_fill=stroke)
    return im
def crate(d,x,y,s,col):
    d.rounded_rectangle([x,y,x+s,y+s],radius=4*S,fill=col,outline=(20,18,28),width=4*S)
    d.line([(x+s*0.2,y+s*0.5),(x+s*0.8,y+s*0.5)],fill=(255,255,255,110),width=3*S)
    d.line([(x+s*0.5,y+s*0.2),(x+s*0.5,y+s*0.8)],fill=(255,255,255,110),width=3*S)
def person(d,cx,cy,s,col):
    d.ellipse([cx-s*0.32,cy-s*0.9,cx+s*0.32,cy-s*0.26],fill=col,outline=(20,18,28),width=3*S)
    d.chord([cx-s*0.6,cy-s*0.2,cx+s*0.6,cy+s*0.9],180,360,fill=col,outline=(20,18,28),width=3*S)
def arrow(d,a,b,col,wd=7,head=16):
    wd*=S; head*=S
    ang=math.atan2(b[1]-a[1],b[0]-a[0])
    for p,q,sg in ((a,b,1),(b,a,-1)):
        pass
    d.line([a,b],fill=(20,18,28),width=wd+6*S); d.line([a,b],fill=col,width=wd)
    for tip,dirn in ((b,ang),(a,ang+math.pi)):
        l=(tip[0]-head*math.cos(dirn-0.5),tip[1]-head*math.sin(dirn-0.5))
        r=(tip[0]-head*math.cos(dirn+0.5),tip[1]-head*math.sin(dirn+0.5))
        d.polygon([tip,l,r],fill=col,outline=(20,18,28),width=3*S)
def finish(im,path):
    im=im.resize((256,256),Image.LANCZOS).convert('RGB'); im.save(path,optimize=True); print(path,im.size)

# ---------- PocketCartForAll
im=bg((20,40,70),(10,14,28),(255,170,40))
lay=Image.new('RGBA',im.size,(0,0,0,0)); d=ImageDraw.Draw(lay)
im,(x0,x1,top,bot)=cart(im,128*S+8*S,172*S,120*S,62*S,26*S,(255,196,60),(214,120,20))
lay=Image.new('RGBA',im.size,(0,0,0,0)); d=ImageDraw.Draw(lay)
# crates peeking over rim
crate(d,x0+14*S,top-26*S,30*S,(90,200,255)); crate(d,x0+48*S,top-34*S,36*S,(120,230,120)); crate(d,x0+88*S,top-22*S,26*S,(240,90,120))
# players row
for i,(px,c) in enumerate([(70,(120,200,255)),(128,(255,230,120)),(186,(150,240,160))]):
    person(d,px*S,46*S,30*S,c)
d.line([(88*S,40*S),(110*S,40*S)],fill=(255,255,255,200),width=4*S); d.line([(146*S,40*S),(168*S,40*S)],fill=(255,255,255,200),width=4*S)
im=Image.alpha_composite(im,lay)
im=text(im,186*S,'KEEP ITEMS',26,(255,214,90))
im=text(im,216*S,'FOR EVERYONE',22,(255,255,255))
finish(im,'src/PocketCartForAll/icon.png')

# ---------- WideLittleCart
im=bg((28,52,40),(10,20,16),(80,230,140))
im,(x0,x1,top,bot)=cart(im,132*S,170*S,168*S,60*S,40*S,(110,235,150),(30,150,80))
d=ImageDraw.Draw(im)
arrow(d,(x0,top-22*S),(x1,top-22*S),(255,255,255))
arrow(d,(x1+10*S,bot-4*S),(x1+30*S,top-30*S),(255,214,90),wd=6,head=13)
im=text(im,186*S,'WIDE LITTLE',26,(160,255,190))
im=text(im,216*S,'POCKET C.A.R.T.',21,(255,255,255))
finish(im,'src/WideLittleCart/icon.png')
