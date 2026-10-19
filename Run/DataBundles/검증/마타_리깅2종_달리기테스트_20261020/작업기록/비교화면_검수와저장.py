import bpy, json, math, os
from mathutils import Vector
scene=bpy.context.scene
rigA=bpy.data.objects['Matyr_A_Existing_Tripo_Rig']
rigB=bpy.data.objects['Matyr_B_New_Rig']
meshA=bpy.data.objects['Matyr_A_Existing_Weights']
meshB=bpy.data.objects['Matyr_B_Fresh_Skinning']
# Rigid necklace components follow the neck, avoiding stretched studs.
adj=[[] for v in meshB.data.vertices]
for e in meshB.data.edges:
 a,b=e.vertices;adj[a].append(b);adj[b].append(a)
seen=set();rigidCount=0
for v in meshB.data.vertices:
 if v.index in seen:continue
 stack=[v.index];seen.add(v.index);ids=[]
 while stack:
  idx=stack.pop();ids.append(idx)
  for other in adj[idx]:
   if other not in seen:seen.add(other);stack.append(other)
 if len(ids)<200:
  for group in meshB.vertex_groups:group.remove(ids)
  meshB.vertex_groups['neck'].add(ids,1,'REPLACE')
  rigidCount+=len(ids)

def vertices(obj):
 bpy.context.view_layer.update()
 e=obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
 return [e.matrix_world@v.co for v in e.data.vertices]
report={'fps':30,'loop_frames':[1,20],'loop_end_duplicate':21,'A_bones':38,'B_bones':25,'B_original_weights_removed':True,'B_necklace_rigid_vertices':rigidCount,'checks':{}}
for rig,mesh,label in [(rigA,meshA,'A'),(rigB,meshB,'B')]:
 rig.location.z=0
 allFrames=[]
 for frame in range(1,22):
  scene.frame_set(frame);allFrames.append(vertices(mesh))
 minimum=min(v.z for coords in allFrames for v in coords)
 rig.location.z=max(0,.015-minimum)
 report['checks'][label]={'unweighted_vertices':sum(sum(g.weight for g in v.groups)<.001 for v in mesh.data.vertices),'loop_max_delta':max((a-b).length for a,b in zip(allFrames[0],allFrames[-1])),'motion_max_delta':max((a-b).length for a,b in zip(allFrames[0],allFrames[10])),'vertical_clearance_offset':rig.location.z}

def material(name,color):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 bsdf=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
 bsdf.inputs['Base Color'].default_value=(*color,1)
 bsdf.inputs['Roughness'].default_value=.75
 return m
floorMat=material('Comparison_Floor',(.075,.09,.12))
blue=material('Comparison_A_Blue',(.07,.28,.46))
orange=material('Comparison_B_Orange',(.48,.22,.055))
white=material('Comparison_Labels',(.9,.93,.98))
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.04))
floor=bpy.context.object;floor.name='Comparison_Ground';floor.data.materials.append(floorMat)
for x,mat in [(-.7,blue),(.7,orange)]:
 bpy.ops.mesh.primitive_cube_add(size=1,location=(x,0,-.025))
 o=bpy.context.object;o.name='Comparison_Pad';o.scale=(.6,.84,.02);o.data.materials.append(mat)
bpy.ops.object.camera_add(location=(2.05,-4.7,2.35))
cam=bpy.context.object;cam.name='Comparison_Camera'
cam.rotation_euler=(Vector((0,-.03,.5))-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.type='ORTHO';cam.data.ortho_scale=3.05;scene.camera=cam
font=bpy.data.fonts.load('C:/Windows/Fonts/malgun.ttf')
for x,text in [(-.7,'1  기존 뼈 사용'),(.7,'2  뼈 제거 후 새 리깅')]:
 bpy.ops.object.text_add(location=(x,.03,1.18))
 o=bpy.context.object;o.name='Comparison_Label';o.data.body=text;o.data.font=font;o.data.align_x='CENTER';o.data.size=.105
 o.rotation_euler=cam.rotation_euler;o.data.materials.append(white)
for pos,energy,size in [((0,-3,5),700,5),((2,3,4),850,4)]:
 bpy.ops.object.light_add(type='AREA',location=pos)
 o=bpy.context.object;o.data.energy=energy;o.data.shape='DISK';o.data.size=size
 o.rotation_euler=(Vector((0,0,.4))-o.location).to_track_quat('-Z','Y').to_euler()
scene.render.resolution_x=1440;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.frame_start=1;scene.frame_end=20;scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT')
for area in bpy.context.screen.areas:
 if area.type=='VIEW_3D':
  space=area.spaces.active;space.region_3d.view_perspective='CAMERA'
  space.region_3d.view_camera_zoom=0
  space.shading.type='MATERIAL';space.overlay.show_overlays=False
  space.show_region_ui=False
scene['comparison_instructions']='왼쪽: 기존 트리포 뼈 38개 / 오른쪽: 뼈와 웨이트 제거 후 새 뼈 25개 및 자동 웨이트. 스페이스로 재생/정지.'
scene['quality_note']='동작 이전 비교 시험본. 접지/발 미끄러짐/어깨와 골반 웨이트는 게임 적용 전 추가 검수 필요.'
out='D:/Project_Run/Run/DataBundles/검증/마타_리깅2종_달리기테스트_20261020'
with open(out+'/작업기록/검수수치.json','w',encoding='utf-8') as f:json.dump(report,f,ensure_ascii=False,indent=2)
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=out+'/마타_기존뼈와새리깅_달리기비교.blend')
print(json.dumps(report,ensure_ascii=False))
