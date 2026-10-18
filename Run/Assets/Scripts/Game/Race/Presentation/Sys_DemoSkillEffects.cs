using UnityEngine;

namespace ProjectRun.Race
{
    // Presentation reads the same skill clock as the deterministic simulation.
    [DefaultExecutionOrder(110)]
    public sealed class Sys_DemoSkillEffects : MonoBehaviour
    {
        private Sys_Race race;
        private Transform actor;
        private Transform origin;
        private Camera viewCamera;
        private bool flat;
        private int hero;
        private Vector3 previousLift;
        private readonly LineRenderer[] streaks=new LineRenderer[12];
        private readonly LineRenderer[] rings=new LineRenderer[2];
        private void Start()
        {
            race=FindFirstObjectByType<Sys_Race>();hero=Mgr_Game.Instance.HeroRunner;viewCamera=Camera.main;
            foreach(var view in FindObjectsByType<RaceDirectionalSpriteView>(FindObjectsSortMode.None))
                if(view.RunnerId==hero){actor=view.Display.transform;origin=view.transform;flat=true;}
            if(actor==null)foreach(var view in FindObjectsByType<RaceRunnerView>(FindObjectsSortMode.None))
                if(view.RunnerId==hero){actor=view.MountRoot;origin=view.transform;}
            if(actor==null||race==null){enabled=false;return;}
            var material=Resources.Load<Material>("UI/Demo/Mat_DemoSkill");
            for(int i=0;i<streaks.Length;i++)streaks[i]=Line("obj_speed_streak_"+i,material,2);
            for(int i=0;i<rings.Length;i++){rings[i]=Line("obj_shockwave_"+i,material,65);rings[i].loop=false;}
        }
        private LineRenderer Line(string name,Material material,int count)
        {
            var root=new GameObject(name);root.transform.SetParent(transform,false);
            var line=root.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;
            line.positionCount=count;line.numCapVertices=2;line.enabled=false;line.sortingOrder=20000;
            return line;
        }
        private void Update()
        {
            if(actor!=null){actor.position-=previousLift;previousLift=Vector3.zero;}
        }
        private void LateUpdate()
        {
            if(actor==null||race.Simulation==null||viewCamera==null)return;
            var sim=race.Simulation;var runner=sim.Runners[hero];float age=runner.SkillAge;
            bool active=age>=0&&age<race.Entrants[hero].SurgeDuration&&!runner.Finished&&sim.Phase!=E_RacePhase.Result;
            if(sim.IsAirborne(hero))
            {
                previousLift=Vector3.up*Mathf.Sin((age-.7f)/1.5f*Mathf.PI)*(flat?2.2f:3.4f);
                actor.position+=previousLift;
            }
            Color tint=Mgr_User.Data.SelectedMonster==0?new Color(1,.36f,.7f):Mgr_User.Data.SelectedMonster==1?new Color(1,.65f,.15f):new Color(.24f,.8f,1);
            float fade=active?Mathf.Min(Mathf.Clamp01(age/.22f),Mathf.Clamp01((race.Entrants[hero].SurgeDuration-age)/.7f)):0;
            Vector3 center=origin.position+Vector3.up*(flat?.3f:1.1f);
            Vector3 forward=flat?Data_Race2DProjection.Project(sim.Forward((float)runner.Distance)).normalized:sim.Forward((float)runner.Distance);
            Vector3 right=viewCamera.transform.right,up=viewCamera.transform.up;
            for(int i=0;i<streaks.Length;i++)
            {
                var line=streaks[i];line.enabled=active;if(!active)continue;
                float phase=Mathf.Repeat(age*2.2f+i*.618f,1),angle=i*2.4f;
                Vector3 offset=(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*(.8f+(i%3)*.32f);
                Vector3 point=center+offset-forward*(phase*4);
                line.SetPosition(0,point);line.SetPosition(1,point-forward*(.8f+phase*2.4f));
                line.startWidth=.065f*(1-phase);line.endWidth=.012f;
                line.startColor=new Color(tint.r,tint.g,tint.b,fade*(1-phase)*.8f);line.endColor=new Color(1,1,1,0);
            }
            for(int i=0;i<rings.Length;i++)
            {
                var line=rings[i];line.enabled=active;if(!active)continue;
                float phase=Mathf.Repeat(age*.9f+i*.5f,1),radius=.8f+phase*5;
                for(int j=0;j<65;j++)
                {
                    float a=j*Mathf.PI*2/64;
                    Vector3 offset=flat?new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius*.36f,0):new Vector3(Mathf.Cos(a)*radius,.12f,Mathf.Sin(a)*radius);
                    line.SetPosition(j,origin.position+offset);
                }
                line.startWidth=line.endWidth=.12f*(1-phase);
                line.startColor=line.endColor=new Color(tint.r,tint.g,tint.b,fade*(1-phase)*.7f);
            }
        }
        private void OnDisable(){if(actor!=null)actor.position-=previousLift;previousLift=Vector3.zero;}
    }
}
