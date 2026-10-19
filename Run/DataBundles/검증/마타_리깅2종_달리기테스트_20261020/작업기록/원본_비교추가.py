import bpy,json
from mathutils import Vector,Matrix
scene=bpy.context.scene
inspection=bpy.data.scenes['Matyr_Feasibility_Check']
original=bpy.data.objects['Armature.001']
source=bpy.data.objects['Armature.002']
originalMesh=bpy.data.objects['matyr_001']
rig=original.copy();rig.data=original.data.copy();rig.animation_data_clear()
rig.name='Matyr_Original_Reference_Rig';rig.location.x=0
scene.collection.objects.link(rig);rig.hide_set(False)
mesh=originalMesh.copy();mesh.data=originalMesh.data.copy();mesh.name='Matyr_Original_Reference_Mesh'
scene.collection.objects.link(mesh);mesh.parent=rig;mesh.matrix_parent_inverse=originalMesh.matrix_parent_inverse.copy();mesh.matrix_basis=originalMesh.matrix_basis.copy()
for mod in mesh.modifiers:
 if mod.type=='ARMATURE':mod.object=rig
bpy.context.window.scene=inspection
source.hide_set(False);bpy.data.collections['CHECK_run'].hide_viewport=False
qSource=source.matrix_world.to_quaternion();qRig=rig.matrix_world.to_quaternion()
oldRest={b.name:rig.matrix_world@b.matrix_local for b in rig.data.bones}
srcRest={b.name:source.matrix_world@b.matrix_local for b in source.data.bones}
samples={}
for frame in range(1,22):
 inspection.frame_set(frame);bpy.context.view_layer.update()
 samples[frame]={b.name:source.matrix_world@b.matrix for b in source.pose.bones}
bpy.context.window.scene=scene
nextBone={'bone_pelvis':'bone_spine1','bone_spine':'bone_spine1','bone_spine1':'bone_neck','bone_neck':'bone_head','bone_tail01':'bone_tail02','bone_tail02':'bone_tail03'}
for side in ['l','r']:
 for a,b in [('clavicle','Upperarm'),('Upperarm','forearm'),('forearm','hand'),('hand','finger0'),('thigh','calf'),('calf','foot'),('foot','toe0')]:nextBone[f'bone_{side}_{a}']=f'bone_{side}_{b}'
rig.animation_data_create();rig.animation_data.action=bpy.data.actions.new('Matyr_Original_Run_Reference')
inverse=rig.matrix_world.inverted()
for pb in rig.pose.bones:pb.rotation_mode='QUATERNION'
for frame in range(1,22):
 scene.frame_set(frame);pose={}
 for pb in rig.pose.bones:
  n=pb.name
  rotation=samples[frame][n].to_quaternion()@srcRest[n].to_quaternion().inverted()@oldRest[n].to_quaternion()
  if n in nextBone:
   child=nextBone[n]
   restDirection=oldRest[child].translation-oldRest[n].translation
   runDirection=samples[frame][child].translation-samples[frame][n].translation
   if restDirection.length>.0001 and runDirection.length>.0001:rotation=restDirection.rotation_difference(runDirection)@oldRest[n].to_quaternion()
  # Keep physical joint positions from the animation FBX; use original mesh bind axes.
  target=inverse@Matrix.LocRotScale(samples[frame][n].translation,rotation,Vector((.01,.01,.01)))
  args={'parent_matrix':pose[pb.parent.name],'parent_matrix_local':pb.parent.bone.matrix_local} if pb.parent else {}
  pb.matrix_basis=pb.bone.convert_local_to_pose(target,pb.bone.matrix_local,invert=True,**args)
  pb.keyframe_insert(data_path='location',frame=frame,group=n)
  pb.keyframe_insert(data_path='rotation_quaternion',frame=frame,group=n)
  pb.keyframe_insert(data_path='scale',frame=frame,group=n)
  pose[n]=target
rig.location.x=-1.45
rig.hide_render=True
rig.display_type='WIRE'
bpy.data.objects['Matyr_A_Existing_Tripo_Rig'].location.x=0
bpy.data.objects['Matyr_B_New_Rig'].location.x=1.45
pads=sorted([o for o in scene.objects if o.name.startswith('Comparison_Pad')],key=lambda o:o.location.x)
pads[0].location.x=0;pads[1].location.x=1.45
mat=bpy.data.materials.new('Comparison_Original_Green');mat.diffuse_color=(.12,.32,.19,1);mat.use_nodes=True
node=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');node.inputs['Base Color'].default_value=(.12,.32,.19,1)
bpy.ops.mesh.primitive_cube_add(size=1,location=(-1.45,.1,-.025))
pad=bpy.context.object;pad.name='Comparison_Original_Pad';pad.scale=(.65,.9,.02);pad.data.materials.append(mat)
labels=sorted([o for o in scene.objects if o.type=='FONT'],key=lambda o:o.location.x)
labels[0].location.x=0;labels[1].location.x=1.45
label=labels[0].copy();label.data=labels[0].data.copy();scene.collection.objects.link(label)
label.name='Comparison_Original_Label';label.location.x=-1.45;label.location.z=1.42;label.data.body='원본 마타 · 동작 기준'
for o in labels:o.location.z=1.42
cam=scene.camera;cam.location=(1.8,-6.8,3.25);cam.rotation_euler=(Vector((0,-.02,.65))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=4.9
for o in [*labels,label]:o.rotation_euler=cam.rotation_euler
scene.frame_set(1);bpy.context.view_layer.update()
print('THREE_WAY_READY',mesh.name, len(rig.data.bones))
