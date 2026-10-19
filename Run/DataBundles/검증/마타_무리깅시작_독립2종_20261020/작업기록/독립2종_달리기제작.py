import bpy, math, json
from mathutils import Vector, Matrix, Quaternion, Euler
scene=bpy.context.scene
rig1=bpy.data.objects['Matyr_1_OldMotion_NewRig']
rig2=bpy.data.objects['Matyr_2_ElegantDog_NewRig']

# The skeletons above have already been built from geometry before reading this reference.
refScene=bpy.data.scenes.new('Old_Matyr_Motion_Reference_Only')
bpy.context.window.scene=refScene
before=set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath='D:/Project_Run/Run/Assets/OldAsset/Monsters/resource/ani_fbx_001/morocc_mob/matyr/animation/matyr@matyr_run_01.FBX')
added=set(bpy.data.objects)-before
source=next(o for o in added if o.type=='ARMATURE')
reference=[]
for frame in range(1,22):
 refScene.frame_set(frame);bpy.context.view_layer.update()
 item={}
 for side,old in [('L','l'),('R','r')]:
  item['F_'+side]=list(source.matrix_world@source.pose.bones['bone_'+old+'_finger0'].head)
  item['H_'+side]=list(source.matrix_world@source.pose.bones['bone_'+old+'_toe0'].head)
 item['pelvis']=list(source.matrix_world@source.pose.bones['bone_pelvis'].head)
 item['neck']=list(source.matrix_world@source.pose.bones['bone_neck'].head)
 reference.append(item)
bpy.context.window.scene=scene
# Remove imported reference objects: they never become a target skeleton.
for obj in added:bpy.data.objects.remove(obj,do_unlink=True)
bpy.data.scenes.remove(refScene)
with open('D:/Project_Run/Run/DataBundles/검증/마타_무리깅시작_독립2종_20261020/작업기록/기존마타_발궤적참고.json','w',encoding='utf-8') as f:json.dump(reference,f,indent=2)

tau=2*math.pi
rest={b.name:b.matrix_local.copy() for b in rig1.data.bones}
heads={b.name:b.head_local.copy() for b in rig1.data.bones}
tails={b.name:b.tail_local.copy() for b in rig1.data.bones}
ranges={}
for pre in ['F_L','F_R','H_L','H_R']:
 ranges[pre]=[(min(x[pre][axis] for x in reference),max(x[pre][axis] for x in reference)) for axis in range(3)]
pelvisZ=[r['pelvis'][2] for r in reference]

def referenceSample(phase,key,axis):
 value=phase*20;i=int(value);fraction=value-i
 return reference[i][key][axis]*(1-fraction)+reference[(i+1)%20][key][axis]*fraction

def solveTwo(hip,end,length1,length2,pole):
 delta=end-hip;distance=delta.length
 distance=max(abs(length1-length2)+.0001,min(distance,(length1+length2)*.996))
 direction=delta.normalized();end=hip+direction*distance
 a=(length1*length1-length2*length2+distance*distance)/(2*distance)
 height=math.sqrt(max(0,length1*length1-a*a))
 bend=Vector(pole)-direction*Vector(pole).dot(direction)
 if bend.length<.0001:bend=Vector((0,1,0))
 bend.normalize()
 return hip+direction*a+bend*height,end

def newDogFoot(phase,pre):
 # Running trot: diagonal pairs, brief flight, soft swing and level paws at contact.
 offset=0 if pre in ['F_L','H_R'] else .5
 u=(phase+offset)%1
 stance=.43;reach=.125 if pre.startswith('F') else .135
 height=.10 if pre.startswith('F') else .085
 toe=tails[pre+'_paw'].copy()
 if u<stance:
  t=u/stance
  toe.y+=-reach+2*reach*t
  toe.z=.019
  pitch=0.0
 else:
  t=(u-stance)/(1-stance)
  ease=t*t*(3-2*t)
  toe.y+=reach-2*reach*ease
  toe.z=.019+height*math.sin(math.pi*t)**1.35
  pitch=.24*math.sin(tau*t)
 return toe,pitch,u<stance

def oldMotionFoot(phase,pre):
 # Transfer the source footfall timing, not its rig or target bind pose.
 y=referenceSample(phase,pre,1);z=referenceSample(phase,pre,2)
 loY,hiY=ranges[pre][1];loZ,hiZ=ranges[pre][2]
 toe=tails[pre+'_paw'].copy()
 toe.y+=(2*(y-loY)/max(hiY-loY,.001)-1)*(.145 if pre.startswith('F') else .155)
 toe.z=.019+(z-loZ)/max(hiZ-loZ,.001)*(.145 if pre.startswith('F') else .13)
 return toe,0.12*math.sin(tau*phase),toe.z<.037

def makePose(phase,version):
 if version==1:
  bounce=-.035+.028*math.sin(tau*phase+.4)
  pitch=.045*math.sin(tau*phase-.7)
  roll=.006*math.sin(tau*phase)
 else:
  bounce=-.037+.008*math.cos(2*tau*phase+.7)
  pitch=.009*math.sin(2*tau*phase)
  roll=.005*math.sin(tau*phase)
 bodyQ=Euler((pitch,0,roll),'XYZ').to_quaternion()
 body=Matrix.LocRotScale(Vector((0,0,bounce)),bodyQ,Vector((1,1,1)))
 poses={};positions={}
 poses['root']=Matrix.Translation(Vector((0,0,bounce)))
 for n in ['pelvis','spine','chest']:
  poses[n]=body@rest[n]
 for n in ['neck','head']:
  # Hold a proud head with small compensating motion, not full trunk pitching.
  q=Euler((pitch*.25+.014*math.sin(tau*phase-.3),0,roll*.3),'XYZ').to_quaternion()@rest[n].to_quaternion()
  poses[n]=Matrix.LocRotScale(body@heads[n],q,Vector((1,1,1)))
 for side in ['L','R']:
  for kind in ['F','H']:
   pre=kind+'_'+side
   toe,pawPitch,contact=oldMotionFoot(phase,pre) if version==1 else newDogFoot(phase,pre)
   pawDelta=Quaternion((1,0,0),pawPitch)
   pawVector=pawDelta@(tails[pre+'_paw']-heads[pre+'_paw'])
   ankle=toe-pawVector
   hip=body@heads[pre+'_upper']
   if kind=='F':
    length1=(tails[pre+'_upper']-heads[pre+'_upper']).length
    length2=(tails[pre+'_lower']-heads[pre+'_lower']).length
    knee,ankle=solveTwo(hip,ankle,length1,length2,(0,1,0))
    chain=[(pre+'_upper',hip,knee),(pre+'_lower',knee,ankle),(pre+'_paw',ankle,ankle+pawVector)]
   else:
    heelVector=tails[pre+'_heel']-heads[pre+'_heel']
    # Hock stays behind the grounded paw; foot folds gently during swing.
    heelVector=Quaternion((1,0,0),pawPitch*.5)@heelVector
    hock=ankle-heelVector
    length1=(tails[pre+'_upper']-heads[pre+'_upper']).length
    length2=(tails[pre+'_lower']-heads[pre+'_lower']).length
    knee,hock=solveTwo(hip,hock,length1,length2,(0,-1,0))
    ankle=hock+heelVector
    chain=[(pre+'_upper',hip,knee),(pre+'_lower',knee,hock),(pre+'_heel',hock,ankle),(pre+'_paw',ankle,ankle+pawVector)]
   for n,a,b in chain:
    direction=b-a;restDirection=tails[n]-heads[n]
    q=restDirection.rotation_difference(direction)@rest[n].to_quaternion()
    poses[n]=Matrix.LocRotScale(a,q,Vector((1,1,1)))
   positions[pre]={'toe':list(chain[-1][2]),'contact':contact}
 parent='pelvis'
 for i in range(4):
  n='tail_'+str(i)
  # Preserve authored tail silhouette and add small delayed lateral follow-through.
  follow=Quaternion((0,0,1),.035*math.sin(tau*phase-i*.45))
  parentRest=rest[parent]
  pos=poses[parent]@(parentRest.inverted()@rest[n]).translation
  q=bodyQ@follow@rest[n].to_quaternion()
  poses[n]=Matrix.LocRotScale(pos,q,Vector((1,1,1)))
  parent=n
 return poses,positions

records={}
for rig,version,period in [(rig1,1,24),(rig2,2,32)]:
 rig.animation_data_create();rig.animation_data.action=bpy.data.actions.new('OldMatyr_Inspired_NewMotion' if version==1 else 'Elegant_Dog_Original_Trot')
 for pb in rig.pose.bones:pb.rotation_mode='QUATERNION'
 previous={};records[str(version)]=[]
 for frame in range(1,98):
  phase=((frame-1)%period)/period
  poses,feet=makePose(phase,version)
  records[str(version)].append({'frame':frame,'phase':phase,'feet':feet})
  for pb in rig.pose.bones:
   n=pb.name
   args={'parent_matrix':poses[pb.parent.name],'parent_matrix_local':rest[pb.parent.name]} if pb.parent else {}
   pb.matrix_basis=pb.bone.convert_local_to_pose(poses[n],rest[n],invert=True,**args)
   if n in previous and pb.rotation_quaternion.dot(previous[n])<0:pb.rotation_quaternion.negate()
   previous[n]=pb.rotation_quaternion.copy()
   pb.keyframe_insert(data_path='location',frame=frame,group=n)
   pb.keyframe_insert(data_path='rotation_quaternion',frame=frame,group=n)
 print('AUTHORED',rig.name,'period',period)
rig1.location.x=-.72;rig2.location.x=.72
scene.render.fps=30;scene.frame_start=1;scene.frame_end=96;scene.frame_set(1)
with open('D:/Project_Run/Run/DataBundles/검증/마타_무리깅시작_독립2종_20261020/작업기록/발궤적_키프레임기록.json','w',encoding='utf-8') as f:json.dump(records,f,ensure_ascii=False,indent=2)
print('OLD_IMPORTED_RIG_OBJECTS_REMOVED',all(o.type!='ARMATURE' or o in [rig1,rig2] for o in bpy.data.objects))
