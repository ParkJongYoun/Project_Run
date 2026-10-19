using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ProjectRun.Ranch
{
    // Decorative ranch motion is independent of race simulation and saved progression.
    public sealed class Sys_RanchResident : MonoBehaviour
    {
        public Sys_RanchPlacement Placement;
        public Animator TargetAnimator;
        public AnimationClip IdleClip;
        public AnimationClip LocomotionClip;
        public float MoveSpeed = .85f;
        public float AnimationRate = .65f;
        public float HeadingOffset;
        public float Clearance = .38f;
        public int Seed = 41;
        private static readonly List<Sys_RanchResident> residents = new List<Sys_RanchResident>();
        private readonly List<Vector3> route = new List<Vector3>();
        private System.Random random;
        private PlayableGraph graph;
        private AnimationClipPlayable idle, move;
        private AnimationMixerPlayable mixer;
        private double idleClock, moveClock;
        private float blend, pause, blockedTime, groundY;
        private Vector3 animatorPosition;
        private Quaternion animatorRotation;
        private bool dirty = true;
        private int waypoint;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            random = new System.Random(Seed); groundY = transform.position.y;
            residents.Add(this);
            if (Placement != null) Placement.Changed += InvalidateRoute;
            pause = 1 + (float)random.NextDouble()*3;
            if (TargetAnimator == null || IdleClip == null || LocomotionClip == null) return;
            animatorPosition = TargetAnimator.transform.localPosition;
            animatorRotation = TargetAnimator.transform.localRotation;
            TargetAnimator.applyRootMotion = false;
            TargetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("RanchResident_" + name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            idle = AnimationClipPlayable.Create(graph, IdleClip);
            move = AnimationClipPlayable.Create(graph, LocomotionClip);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            graph.Connect(idle,0,mixer,0); graph.Connect(move,0,mixer,1);
            AnimationPlayableOutput.Create(graph,"resident",TargetAnimator).SetSourcePlayable(mixer);
            graph.Play();
        }

        private void InvalidateRoute() { dirty = true; }

        private void Update()
        {
            if (Placement == null || Placement.Layout == null) return;
            bool walking = false;
            if (Placement.IsBuilding || Placement.InputBlocked)
            {
                dirty = true;
            }
            else
            {
                if (dirty) { route.Clear(); waypoint=0; dirty=false; pause=Mathf.Min(pause,.8f); }
                if (pause>0) pause-=Time.deltaTime;
                else if (waypoint>=route.Count)
                {
                    ChooseRoute();
                    if (route.Count==0) pause=2;
                }
                if (pause<=0 && waypoint<route.Count)
                {
                    Vector3 goal=route[waypoint]; goal.y=groundY;
                    Vector3 delta=goal-transform.position; delta.y=0;
                    if (delta.sqrMagnitude<.025f)
                    {
                        waypoint++;
                        if (waypoint>=route.Count) pause=2+(float)random.NextDouble()*4;
                    }
                    else
                    {
                        var direction=delta.normalized;
                        bool occupied=false;
                        foreach(var other in residents)
                        {
                            if(other==this || other==null) continue;
                            var separation=other.transform.position-transform.position; separation.y=0;
                            if(separation.sqrMagnitude<1.25f*1.25f && Vector3.Dot(separation,direction)>.05f) { occupied=true;break; }
                        }
                        if(occupied)
                        {
                            blockedTime+=Time.deltaTime;
                            if(blockedTime>1.2f) { route.Clear();waypoint=0;pause=.3f+(float)random.NextDouble();blockedTime=0; }
                        }
                        else
                        {
                            blockedTime=0;
                            var facing=Quaternion.LookRotation(direction)*Quaternion.Euler(0,HeadingOffset,0);
                            transform.rotation=Quaternion.RotateTowards(transform.rotation,facing,210*Time.deltaTime);
                            float turn=Mathf.Clamp01(1-Quaternion.Angle(transform.rotation,facing)/100);
                            transform.position=Vector3.MoveTowards(transform.position,goal,MoveSpeed*turn*Time.deltaTime);
                            walking=turn>.1f;
                        }
                    }
                }
            }
            if(!graph.IsValid()) return;
            blend=Mathf.MoveTowards(blend,walking?1:0,Time.deltaTime*4);
            idleClock+=Time.deltaTime;moveClock+=Time.deltaTime*AnimationRate;
            idle.SetTime(idleClock%Math.Max(.01,IdleClip.length));
            move.SetTime(moveClock%Math.Max(.01,LocomotionClip.length));
            mixer.SetInputWeight(0,1-blend);mixer.SetInputWeight(1,blend);graph.Evaluate(0);
            // Imported locomotion can contain root transform curves even with root motion disabled.
            if(TargetAnimator.transform!=transform)
            {
                TargetAnimator.transform.localPosition=animatorPosition;
                TargetAnimator.transform.localRotation=animatorRotation;
            }
        }

        private void ChooseRoute()
        {
            route.Clear();waypoint=0;
            int width=Placement.GridSize.x,height=Placement.GridSize.y,count=width*height;
            var blocked=new bool[count];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                bool road=false;
                foreach(var item in Placement.Layout.Facilities)
                {
                    var definition=Placement.Catalog.Find(item.FacilityId);if(definition==null)continue;
                    if(!definition.IsRoad)continue;
                    var size=definition.Size(item.Rotation);
                    if(x>=item.X && x<item.X+size.x && y>=item.Y && y<item.Y+size.y) { road=true;break; }
                }
                blocked[y*width+x]=!road;
            }
            int start=-1;float nearest=float.PositiveInfinity;
            for(int i=0;i<count;i++)if(!blocked[i])
            {
                float distance=(Center(i%width,i/width)-transform.position).sqrMagnitude;
                if(distance<nearest){nearest=distance;start=i;}
            }
            if(start<0)return;
            // If its road was removed during construction, return to the nearest remaining road.
            int currentX=Mathf.FloorToInt((transform.position.x-Placement.GridOrigin.x)/Placement.CellSize);
            int currentY=Mathf.FloorToInt((transform.position.z-Placement.GridOrigin.z)/Placement.CellSize);
            if(currentX<0 || currentY<0 || currentX>=width || currentY>=height || blocked[currentY*width+currentX])
                transform.position=Center(start%width,start/width);
            var previous=new int[count];for(int i=0;i<count;i++)previous[i]=-1;
            var queue=new Queue<int>();var reachable=new List<int>();previous[start]=start;queue.Enqueue(start);
            while(queue.Count>0)
            {
                int current=queue.Dequeue(),cx=current%width,cy=current/width;
                float distance=Vector3.Distance(Center(cx,cy),Center(start%width,start/width));
                if(current!=start && distance<10)reachable.Add(current);
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    if(Mathf.Abs(dx)+Mathf.Abs(dy)!=1)continue;int nx=cx+dx,ny=cy+dy;
                    if(nx<0||ny<0||nx>=width||ny>=height)continue;
                    int next=ny*width+nx;if(blocked[next]||previous[next]>=0)continue;
                    previous[next]=current;queue.Enqueue(next);
                }
            }
            if(reachable.Count==0)return;
            int end=reachable[random.Next(reachable.Count)];
            while(end!=start) { route.Add(Center(end%width,end/width));end=previous[end]; }
            route.Add(Center(start%width,start/width));route.Reverse();
        }
        private Vector3 Center(int x,int y) => new Vector3(Placement.GridOrigin.x+(x+.5f)*Placement.CellSize,
            groundY,Placement.GridOrigin.z+(y+.5f)*Placement.CellSize);
        private void OnDisable()
        {
            residents.Remove(this);
            if(Placement!=null)Placement.Changed-=InvalidateRoute;
            if(graph.IsValid())graph.Destroy();
        }
    }
}
