import bpy, math, json
from mathutils import Vector, Matrix, kdtree
scene=bpy.context.scene
scene.name='Matyr_Independent_Two_Runs'
base=bpy.data.objects['Matyr_Clean_Geometry']
assert len(bpy.data.armatures)==0
assert len(base.vertex_groups)==0 and len(base.modifiers)==0
assert len(bpy.data.actions)==0

# Landmarks authored from the mesh silhouette and vertex cross-sections only.
# There are no Tripo armatures in this file or bone-data accesses to the input FBX.
points={
 'pelvis':Vector((0,.155,.435)), 'back':Vector((0,.04,.445)),
 'chest':Vector((0,-.145,.465)), 'neck':Vector((0,-.24,.515)),
 'skull':Vector((0,-.285,.635)), 'muzzle':Vector((0,-.455,.635)),
 'tail0':Vector((0,.22,.435)), 'tail1':Vector((.025,.29,.355)),
 'tail2':Vector((.095,.385,.305)), 'tail3':Vector((.215,.465,.315)),
 'tail4':Vector((.235,.45,.38))}
for sign,side in [(1,'L'),(-1,'R')]:
 points['F_'+side+'_hip']=Vector((sign*.088,-.235,.447))
 points['F_'+side+'_knee']=Vector((sign*.095,-.205,.267))
 points['F_'+side+'_ankle']=Vector((sign*.10,-.238,.066))
 points['F_'+side+'_toe']=Vector((sign*.108,-.302,.019))
 points['H_'+side+'_hip']=Vector((sign*.085,.155,.425))
 points['H_'+side+'_knee']=Vector((sign*.099,.185,.268))
 points['H_'+side+'_hock']=Vector((sign*.108,.313,.139))
 points['H_'+side+'_ankle']=Vector((sign*.111,.302,.055))
 points['H_'+side+'_toe']=Vector((sign*.118,.24,.019))

def newRig(name):
 data=bpy.data.armatures.new(name+'_Bones');rig=bpy.data.objects.new(name,data)
 scene.collection.objects.link(rig)
 bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
 bpy.ops.object.mode_set(mode='EDIT')
 def bone(n,a,b,parent=None,deform=True):
  eb=data.edit_bones.new(n);eb.head=a;eb.tail=b;eb.use_deform=deform
  if parent:eb.parent=data.edit_bones[parent]
 bone('root',(0,0,0),(0,0,.12),deform=False)
 for n,a,b,parent in [('pelvis','pelvis','back','root'),('spine','back','chest','pelvis'),('chest','chest','neck','spine'),('neck','neck','skull','chest'),('head','skull','muzzle','neck')]:bone(n,points[a],points[b],parent)
 for side in ['L','R']:
  pre='F_'+side
  bone(pre+'_upper',points[pre+'_hip'],points[pre+'_knee'],'chest')
  bone(pre+'_lower',points[pre+'_knee'],points[pre+'_ankle'],pre+'_upper')
  bone(pre+'_paw',points[pre+'_ankle'],points[pre+'_toe'],pre+'_lower')
  pre='H_'+side
  bone(pre+'_upper',points[pre+'_hip'],points[pre+'_knee'],'pelvis')
  bone(pre+'_lower',points[pre+'_knee'],points[pre+'_hock'],pre+'_upper')
  bone(pre+'_heel',points[pre+'_hock'],points[pre+'_ankle'],pre+'_lower')
  bone(pre+'_paw',points[pre+'_ankle'],points[pre+'_toe'],pre+'_heel')
 for i in range(4):bone('tail_'+str(i),points['tail'+str(i)],points['tail'+str(i+1)],'pelvis' if i==0 else 'tail_'+str(i-1))
 bpy.ops.object.mode_set(mode='OBJECT')
 return rig

rig1=newRig('Matyr_1_OldMotion_NewRig')
mesh1=base.copy();mesh1.data=base.data.copy();mesh1.name='Matyr_1_Mesh';scene.collection.objects.link(mesh1)
bpy.ops.object.select_all(action='DESELECT');mesh1.select_set(True);rig1.select_set(True);bpy.context.view_layer.objects.active=rig1
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
mod=next(m for m in mesh1.modifiers if m.type=='ARMATURE');mod.use_deform_preserve_volume=True

# Patch unweighted vertices from the newly generated heat weights only.
weighted=[v for v in mesh1.data.vertices if sum(g.weight for g in v.groups)>.001]
tree=kdtree.KDTree(len(weighted))
for i,v in enumerate(weighted):tree.insert(v.co,i)
tree.balance();patched=0
for v in mesh1.data.vertices:
 if sum(g.weight for g in v.groups)>.001:continue
 totals={}
 for co,i,d in tree.find_n(v.co,4):
  factor=1/max(d,.001)**2
  for g in weighted[i].groups:totals[g.group]=totals.get(g.group,0)+factor*g.weight
 total=sum(totals.values())
 for group,w in totals.items():mesh1.vertex_groups[group].add([v.index],w/total,'REPLACE')
 patched+=1

# Disconnected necklace and studs must not smear between the chest and head.
adj=[[] for v in mesh1.data.vertices]
for e in mesh1.data.edges:
 a,b=e.vertices;adj[a].append(b);adj[b].append(a)
seen=set()
for v in mesh1.data.vertices:
 if v.index in seen:continue
 stack=[v.index];seen.add(v.index);ids=[]
 while stack:
  index=stack.pop();ids.append(index)
  for other in adj[index]:
   if other not in seen:seen.add(other);stack.append(other)
 if len(ids)<200:
  for group in mesh1.vertex_groups:group.remove(ids)
  mesh1.vertex_groups['neck'].add(ids,1,'REPLACE')

rig2=rig1.copy();rig2.data=rig1.data.copy();rig2.name='Matyr_2_ElegantDog_NewRig';scene.collection.objects.link(rig2)
mesh2=mesh1.copy();mesh2.data=mesh1.data.copy();mesh2.name='Matyr_2_Mesh';scene.collection.objects.link(mesh2);mesh2.parent=rig2
for m in mesh2.modifiers:
 if m.type=='ARMATURE':m.object=rig2
# Both use the same newly authored skeleton design to isolate motion quality.
# Neither uses any input Tripo bone/weight, and neither rig has an action yet.
base.hide_set(True);base.hide_render=True
base['role']='Clean mesh-only audit reference; no rig or vertex groups'
rig1['motion_source']='OldAsset matyr running trajectory, independently adapted'
rig2['motion_source']='New hand-authored flying trot; no existing animation samples'
audit={'clean_armatures':0,'clean_vertex_groups':0,'clean_actions':0,'new_bones_per_rig':len(rig1.data.bones),'new_auto_weight_patches':patched,'landmark_source':'mesh silhouette and cross-sections only','tripo_rig_used':False,'points':{k:list(v) for k,v in points.items()}}
path='D:/Project_Run/Run/DataBundles/검증/마타_무리깅시작_독립2종_20261020/작업기록/새리깅_생성검증.json'
with open(path,'w',encoding='utf-8') as f:json.dump(audit,f,ensure_ascii=False,indent=2)
print(json.dumps({k:v for k,v in audit.items() if k!='points'},ensure_ascii=False))
