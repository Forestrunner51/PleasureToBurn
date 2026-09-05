import json,struct,sys,math,glob,os
def quat_to_mat(q):
    x,y,z,w=q
    return [[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]]
def compose(node):
    if 'matrix' in node:
        m=node['matrix']; return [[m[0],m[4],m[8],m[12]],[m[1],m[5],m[9],m[13]],[m[2],m[6],m[10],m[14]],[0,0,0,1]]
    t=node.get('translation',[0,0,0]); r=node.get('rotation',[0,0,0,1]); s=node.get('scale',[1,1,1])
    R=quat_to_mat(r)
    return [[R[0][0]*s[0],R[0][1]*s[1],R[0][2]*s[2],t[0]],[R[1][0]*s[0],R[1][1]*s[1],R[1][2]*s[2],t[1]],[R[2][0]*s[0],R[2][1]*s[1],R[2][2]*s[2],t[2]],[0,0,0,1]]
def mul(a,b): return [[sum(a[i][k]*b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]
def apply(m,v): return [m[i][0]*v[0]+m[i][1]*v[1]+m[i][2]*v[2]+m[i][3] for i in range(3)]
def bounds(path):
    d=open(path,'rb').read(); ln=struct.unpack('<I',d[12:16])[0]; j=json.loads(d[20:20+ln])
    lo=[1e9]*3; hi=[-1e9]*3
    def walk(ni,parent):
        nonlocal lo,hi
        n=j['nodes'][ni]; m=mul(parent,compose(n))
        if 'mesh' in n:
            for prim in j['meshes'][n['mesh']]['primitives']:
                acc=j['accessors'][prim['attributes']['POSITION']]
                mn,mx=acc['min'],acc['max']
                for c in [(x,y,z) for x in (mn[0],mx[0]) for y in (mn[1],mx[1]) for z in (mn[2],mx[2])]:
                    p=apply(m,c); lo=[min(lo[i],p[i]) for i in range(3)]; hi=[max(hi[i],p[i]) for i in range(3)]
        for c in n.get('children',[]): walk(c,m)
    I=[[1,0,0,0],[0,1,0,0],[0,0,1,0],[0,0,0,1]]
    for ni in j['scenes'][j.get('scene',0)]['nodes']: walk(ni,I)
    return lo,hi
if __name__=='__main__':
  for path in sys.argv[1:]:
    lo,hi=bounds(path); size=[hi[i]-lo[i] for i in range(3)]
    print(f"{os.path.basename(path):28s} size x={size[0]:.2f} y={size[1]:.2f} z={size[2]:.2f}   min=({lo[0]:.2f},{lo[1]:.2f},{lo[2]:.2f}) max=({hi[0]:.2f},{hi[1]:.2f},{hi[2]:.2f})")
