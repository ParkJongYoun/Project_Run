import bpy, json, math
from mathutils import Matrix, Vector, Quaternion

if bpy.context.screen.is_animation_playing:
    bpy.ops.screen.animation_cancel(restore_frame=False)
inspection = bpy.context.scene
src = next(o for o in inspection.objects if o.type == 'ARMATURE' and o.get('check_source') == 'old')
template = next(o for o in inspection.objects if o.type == 'ARMATURE' and o.get('check_source') == 'tripo')
templateMesh = next(o for o in inspection.objects if o.type == 'MESH' and o.get('check_source') == 'tripo')
src.hide_set(False)
srcQ = src.matrix_world.to_quaternion()
srcRest = {b.name: (srcQ @ b.matrix_local.to_quaternion()) for b in src.data.bones}
srcHead = {b.name: src.matrix_world @ b.head_local for b in src.data.bones}
samples = {}
for frame in range(1, 22):
    inspection.frame_set(frame)
    bpy.context.view_layer.update()
    samples[frame] = {b.name: {'q': (srcQ @ b.matrix.to_quaternion()) @ srcRest[b.name].inverted(), 'p': src.matrix_world @ b.matrix.translation} for b in src.pose.bones}
print('SOURCE_DELTA', (samples[6]['bone_l_Upperarm']['p']-samples[1]['bone_l_Upperarm']['p']).length)

scene = bpy.data.scenes.new('Matyr_Two_Rig_Run_Comparison')
bpy.context.window.scene = scene
scene.render.fps = 30
scene.frame_start = 1
scene.frame_end = 20
scene.world = bpy.data.worlds.new('Matyr_Comparison_World')
scene.world.color = (0.12, 0.12, 0.12)

rigA = template.copy()
rigA.data = template.data.copy()
rigA.name = 'Matyr_A_Existing_Tripo_Rig'
rigA.animation_data_clear()
rigA.matrix_world = Matrix.Identity(4)
scene.collection.objects.link(rigA)
rigA.hide_set(False)
for pb in rigA.pose.bones: pb.matrix_basis = Matrix.Identity(4)

def makeMesh(name, rig=None, keepWeights=False):
    obj = templateMesh.copy()
    obj.data = templateMesh.data.copy()
    obj.name = name
    obj.parent = None
    obj.matrix_parent_inverse = Matrix.Identity(4)
    obj.matrix_world = Matrix.Identity(4)
    obj.data.transform(template.matrix_world.inverted() @ templateMesh.matrix_world)
    obj.animation_data_clear()
    obj.modifiers.clear()
    if not keepWeights: obj.vertex_groups.clear()
    scene.collection.objects.link(obj)
    if rig:
        obj.parent = rig
        mod = obj.modifiers.new('Skinning', 'ARMATURE')
        mod.object = rig
    return obj

meshA = makeMesh('Matyr_A_Existing_Weights', rigA, True)
meshB = makeMesh('Matyr_B_Fresh_Skinning')
meshB['test_started_without_rig'] = True
meshB['original_vertex_groups_removed'] = True
print('B_START_GROUPS', len(meshB.vertex_groups), 'MODIFIERS', len(meshB.modifiers))

# A maps anatomically, including the misnamed right shoulder and longer tail.
mapA = {
 'tripo::Root': 'bone_root', 'tripo::Spine_0': 'bone_pelvis',
 'tripo::Spine_1': 'bone_spine', 'tripo::Spine_2': 'bone_spine1',
 'tripo::Spine_3': 'bone_neck', 'tripo::Spine_4': 'bone_r_Upperarm',
 'tripo::Head_0': 'bone_head', 'tripo::Head_1': 'bone_head',
 'tripo::Head_2': 'bone_head', 'tripo::Head_3': 'bone_head',
 'bone_19': 'bone_spine1', 'bone_20': 'bone_tail01', 'bone_21': 'bone_tail01',
 'bone_22': 'bone_tail02', 'tripo::Tail_0': 'bone_tail02',
 'tripo::Tail_1': 'bone_tail03', 'bone_25': 'bone_tail03',
 'bone_26': 'bone_tail03', 'bone_27': 'bone_tail03',
}
for side in ['Left', 'Right']:
    s = side[0].lower()
    front = ['Upperarm', 'forearm', 'hand', 'finger0', 'finger0'] if side == 'Left' else ['forearm', 'hand', 'finger0', 'finger0']
    for i, part in enumerate(front): mapA[f'tripo::0_{side}_Limb_{i}'] = f'bone_{s}_{part}'
    for i, part in enumerate(['thigh','calf','foot','toe0','toe0']): mapA[f'tripo::1_{side}_Limb_{i}'] = f'bone_{s}_{part}'

# Build a new independent anatomical skeleton; the mesh carries no copied weights.
points = {b.name: b.head_local.copy() for b in rigA.data.bones}
def p(name): return points[name].copy()
armData = bpy.data.armatures.new('Matyr_B_New_Anatomical_Skeleton')
rigB = bpy.data.objects.new('Matyr_B_New_Rig', armData)
scene.collection.objects.link(rigB)
bpy.ops.object.select_all(action='DESELECT')
rigB.select_set(True)
bpy.context.view_layer.objects.active = rigB
bpy.ops.object.mode_set(mode='EDIT')
specs = []
def bone(name, head, tail, parent=None, deform=True, source=None):
    b = armData.edit_bones.new(name)
    b.head, b.tail = head, tail
    if (b.tail-b.head).length < .005: b.tail = b.head + Vector((0,0,.02))
    if parent: b.parent = armData.edit_bones[parent]
    b.use_deform = deform
    specs.append((name, source))
    return b
bone('root', Vector((0,0,0)), Vector((0,0,.15)), deform=False, source='bone_root')
bone('pelvis', p('tripo::Spine_0'), p('tripo::Spine_1'), 'root', source='bone_pelvis')
bone('spine', p('tripo::Spine_1'), p('tripo::Spine_2'), 'pelvis', source='bone_spine')
bone('chest', p('tripo::Spine_2'), p('tripo::Spine_3'), 'spine', source='bone_spine1')
bone('neck', p('tripo::Spine_3'), p('tripo::Head_1'), 'chest', source='bone_neck')
bone('head', p('tripo::Head_1'), p('tripo::Head_3'), 'neck', source='bone_head')
for side in ['Left','Right']:
    s = side[0].lower()
    frontNames = [f'tripo::0_Left_Limb_{i}' for i in range(5)] if side=='Left' else ['tripo::Spine_4'] + [f'tripo::0_Right_Limb_{i}' for i in range(4)]
    parent='chest'
    for i,part in enumerate(['Upperarm','forearm','hand','finger0']):
        n = f'front_{s}_{part}'
        bone(n,p(frontNames[i]),p(frontNames[i+1]),parent,source=f'bone_{s}_{part}')
        parent=n
    parent='pelvis'
    for i,part in enumerate(['thigh','calf','foot','toe0']):
        n=f'hind_{s}_{part}'
        bone(n,p(f'tripo::1_{side}_Limb_{i}'),p(f'tripo::1_{side}_Limb_{i+1}'),parent,source=f'bone_{s}_{part}')
        parent=n
tailNames=['bone_20','bone_22','tripo::Tail_1','bone_26']
parent='pelvis'
for i in range(3):
    n=f'tail_{i+1}'
    bone(n,p(tailNames[i]),p(tailNames[i+1]),parent,source=f'bone_tail0{i+1}')
    parent=n
bpy.ops.object.mode_set(mode='OBJECT')
mapB = dict(specs)
bpy.ops.object.select_all(action='DESELECT')
meshB.select_set(True)
rigB.select_set(True)
bpy.context.view_layer.objects.active = rigB
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
print('B_AUTO_WEIGHT',len(meshB.vertex_groups),'UNWEIGHTED',sum(not v.groups for v in meshB.data.vertices))

def bake(rig, mapping, label):
    rig.animation_data_create()
    rig.animation_data.action = bpy.data.actions.new(label)
    rest = {b.name:b.matrix_local.copy() for b in rig.data.bones}
    for pb in rig.pose.bones: pb.rotation_mode = 'QUATERNION'
    # Pose matrices are built from parent transforms and rest offsets, preserving segment lengths.
    for frame in range(1,22):
        scene.frame_set(frame)
        pose = {}
        for pb in rig.pose.bones:
            name=pb.name; source=mapping.get(name)
            delta=samples[frame][source]['q'] if source else Quaternion()
            q = delta @ rest[name].to_quaternion()
            if pb.parent:
                pos = pose[pb.parent.name] @ (rest[pb.parent.name].inverted() @ rest[name]).translation
            else: pos=rest[name].translation.copy()
            # Transfer body lift while leaving horizontal travel in place.
            if name in ['tripo::Spine_0','pelvis']:
                pos.z += (samples[frame]['bone_pelvis']['p'].z-srcHead['bone_pelvis'].z)*.82
            desired = Matrix.LocRotScale(pos,q,Vector((1,1,1)))
            if pb.parent:
                basis=pb.bone.convert_local_to_pose(desired,rest[name],parent_matrix=pose[pb.parent.name],parent_matrix_local=rest[pb.parent.name],invert=True)
            else: basis=pb.bone.convert_local_to_pose(desired,rest[name],invert=True)
            pb.matrix_basis = basis
            pb.keyframe_insert(data_path='location',frame=frame,group=name)
            pb.keyframe_insert(data_path='rotation_quaternion',frame=frame,group=name)
            pose[name]=desired
    print('BAKED',label,len(rig.data.bones))

bake(rigA,mapA,'Matyr_Run_A_Tripo_Retarget')
bake(rigB,mapB,'Matyr_Run_B_Fresh_Rig')
rigA.location.x=-.7
rigB.location.x=.7
for rig in [rigA,rigB]:
    rig.show_in_front=False
    rig.display_type='WIRE'
    rig.hide_render=True
scene.frame_set(1)
bpy.context.view_layer.update()
print('COMPLETE',scene.name)
