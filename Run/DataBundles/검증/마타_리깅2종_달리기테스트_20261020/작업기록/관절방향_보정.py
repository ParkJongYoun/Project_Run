import bpy, math, json
from mathutils import Matrix, Vector, Quaternion, kdtree
scene=bpy.context.scene
rigA=bpy.data.objects['Matyr_A_Existing_Tripo_Rig']
rigB=bpy.data.objects['Matyr_B_New_Rig']
meshB=bpy.data.objects['Matyr_B_Fresh_Skinning']
# Fill islands from freshly computed nearby weights, never from Tripo's original groups.
weighted=[v for v in meshB.data.vertices if sum(g.weight for g in v.groups)>.001]
tree=kdtree.KDTree(len(weighted))
for i,v in enumerate(weighted):tree.insert(v.co,i)
tree.balance()
fixed=0
for v in meshB.data.vertices:
 if sum(g.weight for g in v.groups)>.001:continue
 hits=tree.find_n(v.co,4); totals={}; total=0
 for co,index,dist in hits:
  factor=1/max(dist,.001)**2
  for g in weighted[index].groups:totals[g.group]=totals.get(g.group,0)+g.weight*factor
  total+=factor
 denom=sum(totals.values())
 for index,w in totals.items():meshB.vertex_groups[index].add([v.index],w/denom,'REPLACE')
 fixed+=1
print('FRESH_WEIGHT_ISLANDS_FIXED',fixed)
inspection=bpy.data.scenes['Matyr_Feasibility_Check']
bpy.context.window.scene=inspection
bpy.data.collections['CHECK_run'].hide_viewport=False
source=next(o for o in inspection.objects if o.type=='ARMATURE' and o.get('check_source')=='run')
source.hide_set(False)
srcQ=source.matrix_world.to_quaternion()
srcRest={b.name:srcQ@b.matrix_local.to_quaternion() for b in source.data.bones}
bindSource=next(o for o in inspection.objects if o.type=='ARMATURE' and o.get('check_source')=='old')
srcHead={b.name:bindSource.matrix_world@b.head_local for b in bindSource.data.bones}
samples={}
for frame in range(1,22):
 inspection.frame_set(frame);bpy.context.view_layer.update()
 samples[frame]={b.name:{'q':(srcQ@b.matrix.to_quaternion())@srcRest[b.name].inverted(),'p':source.matrix_world@b.matrix.translation} for b in source.pose.bones}
bpy.context.window.scene=scene
mapA={'tripo::Root':'bone_root','tripo::Spine_0':'bone_pelvis','tripo::Spine_1':'bone_spine','tripo::Spine_2':'bone_spine1','tripo::Spine_3':'bone_neck','bone_19':'bone_spine1'}
for i in range(4):mapA[f'tripo::Head_{i}']='bone_head'
for i,n in enumerate(['bone_20','bone_21','bone_22','tripo::Tail_0','tripo::Tail_1','bone_25','bone_26','bone_27']):mapA[n]='bone_tail0'+str(min(3,1+i//3))
mapB={'root':'bone_root','pelvis':'bone_pelvis','spine':'bone_spine','chest':'bone_spine1','neck':'bone_neck','head':'bone_head','tail_1':'bone_tail01','tail_2':'bone_tail02','tail_3':'bone_tail03'}
directionA={};directionB={}
for side in ['Left','Right']:
 s=side[0].lower()
 front=[f'tripo::0_Left_Limb_{i}' for i in range(5)] if side=='Left' else ['tripo::Spine_4']+[f'tripo::0_Right_Limb_{i}' for i in range(4)]
 hind=[f'tripo::1_{side}_Limb_{i}' for i in range(5)]
 for parts,names,prefix in [(['Upperarm','forearm','hand','finger0'],front,'front'),(['thigh','calf','foot','toe0'],hind,'hind')]:
  for i in range(4):
   n=names[i];src=f'bone_{s}_{parts[i]}';nB=f'{prefix}_{s}_{parts[i]}'
   mapA[n]=src;mapB[nB]=src
   # Distal toe segment repeats the last physical foot direction.
   start=f'bone_{s}_{parts[min(i,2)]}'
   end=f'bone_{s}_{parts[min(i+1,3)]}'
   directionA[n]=(start,end,names[i+1])
   directionB[nB]=(start,end,None)
  mapA[names[4]]=f'bone_{s}_{parts[3]}'

def bake(rig,mapping,directions):
 rig.animation_data_clear();rig.animation_data_create()
 rig.animation_data.action=bpy.data.actions.new('Run_Corrected_'+rig.name)
 rest={b.name:b.matrix_local.copy() for b in rig.data.bones}
 for pb in rig.pose.bones:pb.rotation_mode='QUATERNION'
 previous={}
 for frame in range(1,22):
  scene.frame_set(frame)
  pose={}
  for pb in rig.pose.bones:
   n=pb.name;sourceName=mapping.get(n)
   delta=samples[frame][sourceName]['q'] if sourceName else Quaternion()
   if 'tail' in n.lower() or n in ['bone_20','bone_21','bone_22','bone_25','bone_26','bone_27']:
    delta=Quaternion().slerp(delta,.28)
   q=delta@rest[n].to_quaternion()
   if n in directions:
    start,end,nextBone=directions[n]
    desired=samples[frame][end]['p']-samples[frame][start]['p']
    current=(rest[nextBone].translation-rest[n].translation) if nextBone else pb.bone.tail_local-pb.bone.head_local
    if desired.length>.00001 and current.length>.00001:
     q=current.rotation_difference(desired)@rest[n].to_quaternion()
   if pb.parent:pos=pose[pb.parent.name]@(rest[pb.parent.name].inverted()@rest[n]).translation
   else:pos=rest[n].translation.copy()
   if n in ['tripo::Spine_0','pelvis']:
    pos.z+=(samples[frame]['bone_pelvis']['p'].z-srcHead['bone_pelvis'].z)*.82
   desiredMatrix=Matrix.LocRotScale(pos,q,Vector((1,1,1)))
   args={'parent_matrix':pose[pb.parent.name],'parent_matrix_local':rest[pb.parent.name]} if pb.parent else {}
   basis=pb.bone.convert_local_to_pose(desiredMatrix,rest[n],invert=True,**args)
   pb.matrix_basis=basis
   if n in previous and pb.rotation_quaternion.dot(previous[n])<0:pb.rotation_quaternion.negate()
   previous[n]=pb.rotation_quaternion.copy()
   pb.keyframe_insert(data_path='location',frame=frame,group=n)
   pb.keyframe_insert(data_path='rotation_quaternion',frame=frame,group=n)
   pose[n]=desiredMatrix
 print('CORRECTED',rig.name)
bake(rigA,mapA,directionA)
bake(rigB,mapB,directionB)
scene.frame_set(1)
bpy.context.view_layer.update()
print('B_UNWEIGHTED',sum(sum(g.weight for g in v.groups)<.001 for v in meshB.data.vertices))
