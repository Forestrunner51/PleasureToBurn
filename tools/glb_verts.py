import json,struct,sys,os
sys.path.insert(0,os.path.dirname(__file__))
from glb_bounds import compose,mul,apply
def verts(path):
    d=open(path,'rb').read(); ln=struct.unpack('<I',d[12:16])[0]; j=json.loads(d[20:20+ln])
    bin_off=20+ln; bl=struct.unpack('<I',d[bin_off:bin_off+4])[0]; blob=d[bin_off+8:bin_off+8+bl]
    out=[]
    def read(acc_i):
        acc=j['accessors'][acc_i]; bv=j['bufferViews'][acc['bufferView']]
        off=bv.get('byteOffset',0)+acc.get('byteOffset',0); stride=bv.get('byteStride',12)
        return [struct.unpack_from('<fff',blob,off+i*stride) for i in range(acc['count'])]
    def walk(ni,parent):
        n=j['nodes'][ni]; m=mul(parent,compose(n))
        if 'mesh' in n:
            for prim in j['meshes'][n['mesh']]['primitives']:
                for v in read(prim['attributes']['POSITION']): out.append(apply(m,v))
        for c in n.get('children',[]): walk(c,m)
    I=[[1,0,0,0],[0,1,0,0],[0,0,1,0],[0,0,0,1]]
    for ni in j['scenes'][j.get('scene',0)]['nodes']: walk(ni,I)
    return out
mode=sys.argv[1]; path=sys.argv[2]; V=verts(path)
if mode=="facing":
    neg=[v for v in V if v[2]<-0.3]; pos=[v for v in V if v[2]>0.3]
    print(os.path.basename(path), f"verts={len(V)} max_y(z<0)={max(v[1] for v in neg):.2f} max_y(z>0)={max(v[1] for v in pos):.2f}  count(z<0)={len(neg)} count(z>0)={len(pos)}")
    # lowest vertices near the ground hint at wheel wells: print z clusters of vertices with y<0.15
    low=[round(v[2],1) for v in V if v[1]<0.12]
    from collections import Counter; print("  low-vertex z clusters:", sorted(Counter(low).items())[:20])
elif mode=="levels":
    from collections import Counter
    c=Counter(round(v[1],2) for v in V)
    print(os.path.basename(path), "y levels with many verts:", [(y,n) for y,n in sorted(c.items()) if n>=8])
