import bpy, json
from mathutils import Vector
scene=bpy.context.scene
scene.render.fps=30
scene.frame_start=1;scene.frame_end=96
rig1=bpy.data.objects['Matyr_1_OldMotion_NewRig'];rig2=bpy.data.objects['Matyr_2_ElegantDog_NewRig']
rig1.location=(0,-.85,.0072);rig2.location=(0,.85,.001)
for rig in [rig1,rig2]:rig.hide_set(False);rig.hide_render=True;rig.show_in_front=False
for n in ['Matyr_1_Mesh','Matyr_2_Mesh']:bpy.data.objects[n].hide_set(False)
world=bpy.data.worlds.new('Independent_Run_World');scene.world=world;world.color=(.10,.12,.15)
def mat(name,color):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 node=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
 node.inputs['Base Color'].default_value=(*color,1);node.inputs['Roughness'].default_value=.75
 return m
floorMat=mat('Independent_Floor',(.045,.055,.075))
blue=mat('Reference_Run_Blue',(.065,.26,.39))
green=mat('Elegant_Run_Green',(.11,.32,.24))
white=mat('Independent_Label',(.90,.94,.99))
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.03));floor=bpy.context.object;floor.name='Comparison_Floor';floor.data.materials.append(floorMat)
for y,m in [(-.85,blue),(.85,green)]:
 bpy.ops.mesh.primitive_cube_add(size=1,location=(0,y,-.018))
 pad=bpy.context.object;pad.name='Independent_Run_Pad';pad.scale=(.40,.72,.016);pad.data.materials.append(m)
bpy.ops.object.camera_add(location=(4.6,-2.25,2.5));camera=bpy.context.object;camera.name='Independent_Comparison_Camera'
camera.rotation_euler=(Vector((0,0,.46))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=3.9;scene.camera=camera
font=bpy.data.fonts.load('C:/Windows/Fonts/malgun.ttf')
for y,text in [(-.85,'1  새 리깅 · 기존 마타 동작 참고'),(.85,'2  새 리깅 · 우아한 강아지 달리기')]:
 bpy.ops.object.text_add(location=(0,y,1.04));label=bpy.context.object;label.name='Independent_Label'
 label.data.body=text;label.data.font=font;label.data.size=.075;label.data.align_x='CENTER';label.rotation_euler=camera.rotation_euler;label.data.materials.append(white)
for xyz,power,size in [((1,-3,4),650,5),((-2,2,3),500,4)]:
 bpy.ops.object.light_add(type='AREA',location=xyz);light=bpy.context.object
 light.data.energy=power;light.data.shape='DISK';light.data.size=size
 light.rotation_euler=(Vector((0,0,.4))-light.location).to_track_quat('-Z','Y').to_euler()
scene.render.resolution_x=1600;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.frame_set(1);bpy.ops.object.select_all(action='DESELECT')
for area in bpy.context.screen.areas:
 if area.type=='VIEW_3D':
  space=area.spaces.active;space.region_3d.view_perspective='CAMERA';space.region_3d.view_camera_zoom=0
  space.shading.type='MATERIAL';space.overlay.show_overlays=False;space.show_region_ui=False
scene['tripo_rig_used']=False
scene['rig_source']='Fresh mesh-only file; 24-bone skeleton authored from visible geometry and vertex cross-sections. Fresh heat weights.'
scene['motion_1']='New analytic joint animation based on old Matyr foot trajectories; original rig is not reused.'
scene['motion_2']='Independent authored running trot: diagonal pairs, soft paw swing, stable head, slight body bounce, delayed tail.'
scene['reference_url']='https://vanat.ahc.umn.edu/gaits/trot.html'
scene['playback_help']='Space: play/pause. Left: old Matyr reference. Right: independently authored elegant dog running trot.'
out='D:/Project_Run/Run/DataBundles/검증/마타_무리깅시작_독립2종_20261020'
audit={}
for i,n in [(1,'Matyr_1_Mesh'),(2,'Matyr_2_Mesh')]:
 obj=bpy.data.objects[n];coords=[];minZ=999
 for frame in range(1,98):
  scene.frame_set(frame);bpy.context.view_layer.update()
  e=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());verts=[e.matrix_world@v.co for v in e.data.vertices]
  minZ=min(minZ,min(v.z for v in verts))
  if frame in [1,97]:coords.append(verts)
 audit[str(i)]={'unweighted_vertices':sum(sum(g.weight for g in v.groups)<.001 for v in obj.data.vertices),'loop_max_vertex_delta':max((a-b).length for a,b in zip(*coords)),'minimum_world_z':minZ,'bone_count':len(obj.parent.data.bones),'tripo_bone_names':any('tripo::' in b.name for b in obj.parent.data.bones)}
with open(out+'/작업기록/최종검사.json','w',encoding='utf-8') as f:json.dump(audit,f,ensure_ascii=False,indent=2)
scene.frame_set(1)
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=out+'/마타_무리깅부터새제작_2종달리기.blend')
print('FINAL_AUDIT',json.dumps(audit))
