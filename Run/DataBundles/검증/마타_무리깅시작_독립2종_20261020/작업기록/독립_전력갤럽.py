import bpy, math, json
from mathutils import Vector,Matrix,Quaternion,Euler
scene=bpy.context.scene
rig=bpy.data.objects['Matyr_2_ElegantDog_NewRig']
mesh=bpy.data.objects['Matyr_2_Mesh']
if bpy.context.screen.is_animation_playing:bpy.ops.screen.animation_cancel(restore_frame=False)
rest={b.name:b.matrix_local.copy() for b in rig.data.bones}
heads={b.name:b.head_local.copy() for b in rig.data.bones}
tails={b.name:b.tail_local.copy() for b in rig.data.bones}
tau=math.tau

def solve(hip,goal,l1,l2,pole):
 d=goal-hip;direction=d.normalized()
 length=max(abs(l1-l2)+.0001,min(d.length,(l1+l2)*.995))
 end=hip+direction*length
 a=(l1*l1-l2*l2+length*length)/(2*length)
 h=math.sqrt(max(0,l1*l1-a*a))
 bend=Vector(pole)-direction*Vector(pole).dot(direction);bend.normalize()
 return hip+direction*a+bend*h,end

def foot(phase,pre):
 # Rotary-gallop footfalls: RH, LH, extended flight, LF, RF, collected flight.
 touchdown={'H_R':0.0,'H_L':.07,'F_L':.43,'F_R':.50}[pre]
 u=(phase-touchdown)%1
 stance=.18 if pre[0]=='H' else .20
 reach=.185 if pre[0]=='H' else .175
 toe=tails[pre+'_paw'].copy()
 if u<stance:
  t=u/stance
  toe.y+=-reach+2*reach*t
  toe.z=.019
  pitch=.10*max(0,(t-.65)/.35)
 else:
  t=(u-stance)/(1-stance)
  ease=t*t*(3-2*t)
  toe.y+=reach-2*reach*ease
  toe.z=.019+(.205 if pre[0]=='H' else .19)*math.sin(math.pi*t)**1.05
  pitch=.38*math.sin(tau*t)
 return toe,pitch,u<stance

def pose(phase):
 # Two flight peaks per stride; the longer body extension occurs after hind push-off.
 extended=math.cos(tau*(phase-.30))
 bounce=-.025+.024*math.cos(2*tau*(phase-.30))
 pitch=.055*math.sin(tau*(phase-.36))
 bodyQ=Euler((pitch,0,.008*math.sin(tau*phase)),'XYZ').to_quaternion()
 body=Matrix.LocRotScale(Vector((0,0,bounce)),bodyQ,Vector((1,1,1)))
 joints={n:body@heads[n] for n in ['pelvis','spine','chest','neck','head']}
 joints['pelvis'].y+=.038*extended
 joints['spine'].y+=.012*extended;joints['spine'].z-=.012*extended
 joints['chest'].y-=.022*extended
 joints['neck'].y-=.025*extended;joints['neck'].z-=.023
 joints['head'].y-=.03*extended;joints['head'].z-=.055
 poses={'root':Matrix.Translation((0,0,bounce))}
 for n,nextName in [('pelvis','spine'),('spine','chest'),('chest','neck'),('neck','head')]:
  direction=joints[nextName]-joints[n]
  delta=(tails[n]-heads[n]).rotation_difference(direction)
  poses[n]=Matrix.LocRotScale(joints[n],delta@rest[n].to_quaternion(),Vector((1,1,1)))
 headQ=Euler((.12+.025*math.sin(tau*phase-.4),0,0),'XYZ').to_quaternion()@rest['head'].to_quaternion()
 poses['head']=Matrix.LocRotScale(joints['head'],headQ,Vector((1,1,1)))
 feet={}
 for side in ['L','R']:
  for kind in ['F','H']:
   pre=kind+'_'+side
   toe,pawPitch,contact=foot(phase,pre)
   pawVector=Quaternion((1,0,0),pawPitch)@(tails[pre+'_paw']-heads[pre+'_paw'])
   ankle=toe-pawVector
   attach='chest' if kind=='F' else 'pelvis'
   hip=poses[attach]@(rest[attach].inverted()@rest[pre+'_upper']).translation
   l1=(tails[pre+'_upper']-heads[pre+'_upper']).length
   l2=(tails[pre+'_lower']-heads[pre+'_lower']).length
   if kind=='F':
    knee,ankle=solve(hip,ankle,l1,l2,(0,1,0))
    chain=[(pre+'_upper',hip,knee),(pre+'_lower',knee,ankle),(pre+'_paw',ankle,ankle+pawVector)]
   else:
    heel=Quaternion((1,0,0),pawPitch*.5)@(tails[pre+'_heel']-heads[pre+'_heel'])
    hock=ankle-heel
    knee,hock=solve(hip,hock,l1,l2,(0,-1,0));ankle=hock+heel
    chain=[(pre+'_upper',hip,knee),(pre+'_lower',knee,hock),(pre+'_heel',hock,ankle),(pre+'_paw',ankle,ankle+pawVector)]
   for n,a,b in chain:
    q=(tails[n]-heads[n]).rotation_difference(b-a)@rest[n].to_quaternion()
    poses[n]=Matrix.LocRotScale(a,q,Vector((1,1,1)))
   feet[pre]={'toe':list(chain[-1][2]),'contact':contact}
 parent='pelvis'
 for i in range(4):
  n='tail_'+str(i)
  pos=poses[parent]@(rest[parent].inverted()@rest[n]).translation
  q=bodyQ@Euler((.035*math.sin(tau*phase-i*.55),0,.045*math.sin(tau*phase-i*.5)),'XYZ').to_quaternion()@rest[n].to_quaternion()
  poses[n]=Matrix.LocRotScale(pos,q,Vector((1,1,1)));parent=n
 return poses,feet

rig.animation_data_clear();rig.animation_data_create();rig.animation_data.action=bpy.data.actions.new('Independent_Dog_Rotary_Gallop_18frames')
previous={};contacts=[]
for frame in range(1,74):
 poses,feet=pose(((frame-1)%18)/18)
 contacts.append({'frame':frame,'feet':feet})
 for pb in rig.pose.bones:
  n=pb.name;pb.rotation_mode='QUATERNION'
  args={'parent_matrix':poses[pb.parent.name],'parent_matrix_local':rest[pb.parent.name]} if pb.parent else {}
  pb.matrix_basis=pb.bone.convert_local_to_pose(poses[n],rest[n],invert=True,**args)
  if n in previous and pb.rotation_quaternion.dot(previous[n])<0:pb.rotation_quaternion.negate()
  previous[n]=pb.rotation_quaternion.copy()
  pb.keyframe_insert(data_path='location',frame=frame,group=n)
  pb.keyframe_insert(data_path='rotation_quaternion',frame=frame,group=n)
rig.location.z=0
minimum=999;maxMinimum=-999;ends=[]
for frame in range(1,74):
 scene.frame_set(frame);bpy.context.view_layer.update()
 e=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get());coords=[e.matrix_world@v.co for v in e.data.vertices]
 z=min(v.z for v in coords);minimum=min(minimum,z);maxMinimum=max(maxMinimum,z)
 if frame in [1,73]:ends.append(coords)
rig.location.z=max(0,.001-minimum)
scene.frame_start=1;scene.frame_end=72;scene.frame_set(1)
for obj in scene.objects:
 if obj.type=='FONT' and obj.data.body.startswith('2 '):obj.data.body='2  새로 만든 전력 달리기 · 갤럽'
rig['motion_source']='Independently authored rotary gallop; no existing animation data'
scene['motion_2']='Rotary gallop: RH, LH, extended flight, LF, RF, collected flight. 18 frames at 30fps.'
scene['reference_url']='https://vanat.ahc.umn.edu/gaits/rotGallop.html'
out='D:/Project_Run/Run/DataBundles/검증/마타_무리깅시작_독립2종_20261020'
report={'period_frames':18,'fps':30,'loop_mesh_error':max((a-b).length for a,b in zip(*ends)),'minimum_mesh_z_before_clearance':minimum,'clearance_offset':rig.location.z,'maximum_flight_clearance':maxMinimum+rig.location.z,'motion_reference':'https://vanat.ahc.umn.edu/gaits/rotGallop.html','footfall_samples':contacts}
with open(out+'/작업기록/전력갤럽_검증.json','w',encoding='utf-8') as f:json.dump(report,f,ensure_ascii=False,indent=2)
print(json.dumps({k:v for k,v in report.items() if k!='footfall_samples'}))
