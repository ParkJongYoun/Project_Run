using UnityEngine;

namespace ProjectRun.Race
{
    /// <summary>Fixed-scale tracking. Only the user's selection changes the followed runner or view.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class Sys_SpriteRaceCamera : MonoBehaviour
    {
        public Sys_Race Race;
        public Camera RaceCamera;
        public int FollowRunner;
        public bool Overview;
        public float Pitch = 38;
        public float Yaw = 35;
        public float FollowSize = 15;
        public float OverviewSize = 55;
        public bool FlatScene;
        public string ShotLabel => Overview ? "경기장 전체" : "주자 추적";
        public int FocusRunner => FollowRunner;
        private Vector3 focus, focusVelocity;
        private int generation = -1;
        private float manualTransition;
        private RaceDirectionalSpriteView[] views;

        public void ToggleOverview() { Overview = !Overview; manualTransition = 1.5f; }
        public void SelectRunner(int id)
        {
            FollowRunner = Mathf.Clamp(id,0,Race.Entrants.Length-1);
            Overview = false;
            manualTransition = 1.5f;
        }
        public void NextRunner() { SelectRunner((FollowRunner+1)%Race.Entrants.Length); }

        private void LateUpdate()
        {
            if(Race==null || Race.Simulation==null || RaceCamera==null || Race.Simulation.Runners.Length==0) return;
            var sim=Race.Simulation;
            bool reset=generation!=Race.Generation;
            if(reset)
            {
                generation=Race.Generation; focusVelocity=Vector3.zero;
                views=new RaceDirectionalSpriteView[sim.Runners.Length];
                foreach(var view in FindObjectsByType<RaceDirectionalSpriteView>(FindObjectsSortMode.None))
                    if(view.Race==Race && view.RunnerId>=0 && view.RunnerId<views.Length) views[view.RunnerId]=view;
            }
            if(Race.Paused && !reset && manualTransition<=0) return;
            FollowRunner=Mathf.Clamp(FollowRunner,0,sim.Runners.Length-1);
            var runner=sim.Runners[FollowRunner];
            float distance=Mathf.Lerp((float)runner.PreviousDistance,(float)runner.Distance,Mathf.Clamp01(Race.Interpolation));
            if(sim.Phase==E_RacePhase.Result) distance=(float)runner.Distance;
            Vector3 forward=sim.Forward(distance);
            Vector3 point=sim.Point(distance,Mathf.Lerp(runner.PreviousLane,runner.Lane,Mathf.Clamp01(Race.Interpolation)));
            Vector3 target=FlatScene ? Data_Race2DProjection.Project(point) : point;
            if(views[FollowRunner]!=null) target=views[FollowRunner].transform.position;
            // A stable look-ahead and vertical offset leave room for the following pack.
            target+=FlatScene ? Data_Race2DProjection.Project(forward)*5+Vector3.down*2 : forward*5+Vector3.up*1.5f;
            float size=FollowSize;
            if(Overview)
            {
                target=Vector3.zero;
                size=Mathf.Max(OverviewSize,90/Mathf.Max(.5f,RaceCamera.aspect));
            }
            float dt=Mathf.Min(Time.unscaledDeltaTime,.1f);
            if(reset) { focus=target; RaceCamera.orthographicSize=size; }
            else
            {
                focus=Vector3.SmoothDamp(focus,target,ref focusVelocity,.22f,1000,dt);
                RaceCamera.orthographicSize=Mathf.Lerp(RaceCamera.orthographicSize,size,1-Mathf.Exp(-dt*4));
            }
            if(FlatScene) RaceCamera.transform.SetPositionAndRotation(new Vector3(focus.x,focus.y,-100),Quaternion.identity);
            else
            {
                var rotation=Quaternion.Euler(Pitch,Yaw,0);
                RaceCamera.transform.SetPositionAndRotation(focus-rotation*Vector3.forward*150,rotation);
            }
            manualTransition=Mathf.Max(0,manualTransition-dt);
        }
    }
}
