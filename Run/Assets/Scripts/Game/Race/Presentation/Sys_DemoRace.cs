using UnityEngine;

namespace ProjectRun.Race
{
    [DefaultExecutionOrder(180)]
    public sealed class Sys_DemoRace : MonoBehaviour
    {
        private Sys_Race race;
        private UIPanel_DemoRace panel;
        private Sys_SpriteRaceCamera spriteCamera;
        private Sys_RaceCamera camera3D;
        private Bounds spriteBounds;
        private bool hasSpriteBounds;
        private bool recorded;
        private int generation=-1;
        /// <summary>Approximate demo race duration in seconds; runner speed = course length / RaceSeconds.</summary>
        public const float RaceSeconds = 70f;
        public static void ConfigureRace(Sys_Race target)
        {
            if(Mgr_Game.Instance==null)return;
            // Prepare before Sys_Race creates its simulation, including direct Play from either demo arena.
            Mgr_Game.Instance.PrepareDirectDemoRace(target.gameObject.scene.name);
            if(!Mgr_Game.Instance.IsDemoRace)return;
            int selected=Mgr_User.Data.SelectedMonster;
            if(selected<0)return;
            ConfigureSetups(target,selected);
        }
        public static void ConfigureSetups(Sys_Race target,int selected)
        {
            if (target.gameObject.scene.name == Define_Scene.OvalToon)
                target.Entrants = Mgr_Bundle.Load<Data_CartoonRaceRoster>("Tables/Data_CartoonRaceRoster").CreateEntrants();
            var monster=Mgr_Table.Demo.Monsters[selected];
            var entries=new Data_RunnerSetup[target.Entrants.Length];
            float courseLength=target.Course!=null?target.Course.Length:Data_RaceSimulation.Length;
            float speed=courseLength/RaceSeconds;
            var skillTable=Table_RaceSkills.Load();
            float[] early={.7f,.2f,-.5f,1.7f,-.2f,.8f};
            for(int i=0;i<entries.Length;i++)
            {
                var entry=target.Entrants[i].Copy();entries[i]=entry;
                entry.BaseSpeed=speed*(.965f+(i%3)*.009f);
                entry.EarlyPower=early[i%early.Length]*speed/20;
                entry.LatePower=speed*.045f;
                var skills=skillTable.Runners[i];
                entry.RaceSkills=skills;
                entry.SurgeTime=999;entry.ConditionalSkill=true;entry.UseStamina=true;
                entry.SkillProgress=skills.UniqueProgress;
                entry.SurgePower=speed*skills.UniquePower;
                entry.SurgeDuration=skills.UniqueDuration;
                entry.DemoSkillType=i<3?i+1:4;
                entry.SkillProgressGrace=.025f;
                entry.SkillEventSpacing=1.6f;
                entry.UniqueEventSpacing=5;
                entry.InnerRailPadding=1.5f;
                entry.OuterSprint=i==2;
                entry.SkillAcceleration=i==2?4.2f:2.4f;
                if(i==2)entry.LatePower=speed*.08f;
                entry.SkillActivationLimit=1;
                entry.StaminaDrainMultiplier=1;entry.Lane=(i-2.5f)*2.7f+.75f;
                entry.SkillName=skills.Unique;
            }
            foreach(var entry in Mgr_Table.Demo.Monsters)
                if(entry.RunnerIndex<entries.Length){entries[entry.RunnerIndex].Name=entry.Name;entries[entry.RunnerIndex].SkillName=entry.Skill;}
            var hero=entries[monster.RunnerIndex];
            float trainedSpeed=monster.Stats[0]+Mgr_User.Data.Monsters[selected].SpeedBonus;
            hero.BaseSpeed=speed*(.949f+Mathf.Clamp(trainedSpeed-240,0,80)*.0002f);
            hero.EarlyPower=speed*(selected==2?-.025f:monster.TacticIndex==1?.035f:-.035f);
            hero.LatePower=speed*(selected==2?.08f:.055f);
            // Selected runners keep their species' timing, power and duration.
            hero.ConditionalSkill=true;hero.DemoSkillType=selected+1;
            hero.SkillActivationLimit=1;
            hero.StaminaDrainMultiplier=Mgr_User.Data.Monsters[selected].EndurancePassive?.8f:1;
            target.Entrants=entries;target.LapCount=1;target.CountdownSeconds=3;target.EnableLaneContests=true;
        }
        public static void ConfigureRaceVariation(Sys_Race target, int seed)
        {
            if (target.gameObject.scene.name != Define_Scene.OvalToon || Mgr_Game.Instance == null ||
                !Mgr_Game.Instance.IsDemoRace) return;
            ConfigureRaceVariation(target.Entrants,Mgr_Game.Instance.HeroRunner,seed);
        }
        public static void ConfigureRaceVariation(Data_RunnerSetup[] entries,int hero,int seed)
        {
            var random = new System.Random(seed);
            bool comeback = random.NextDouble() < Table_RaceSkills.Load().ComebackChance;
            for (int i=0;i<entries.Length;i++)
            {
                var entry=entries[i];
                entry.DemoComeback=i==hero&&comeback;
                entry.SkillProgress=entry.RaceSkills.UniqueProgress+((float)random.NextDouble()-.5f)*.024f;
                entry.SkillScheduleSeed=seed;
            }
        }
        private void Start()
        {
            race=FindFirstObjectByType<Sys_Race>();if(race==null){enabled=false;return;}
            foreach(var old in FindObjectsByType<UIPanel_RaceHUD>(FindObjectsSortMode.None))old.gameObject.SetActive(false);
            foreach(var old in FindObjectsByType<UIPanel_SpriteRaceHUD>(FindObjectsSortMode.None))old.gameObject.SetActive(false);
            foreach(var old in FindObjectsByType<UIPanel_PlayMode>(FindObjectsSortMode.None))old.gameObject.SetActive(false);
            var uiRoot=GameObject.Find("UICanvas_Root")??new GameObject("UICanvas_Root",typeof(RectTransform));
            var canvas=DemoUI.Canvas(uiRoot.transform,"Canvas_Cutscene_Demo",(int)E_CanvasLayer.Cutscene);
            var prefab=Mgr_Bundle.Load<GameObject>("Prefabs/UI/Panel/UIPanel_DemoRace");
            var root=Instantiate(prefab,canvas.transform).GetComponent<RectTransform>();root.name="UIPanel_DemoRace";
            root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
            panel=root.GetComponent<UIPanel_DemoRace>();panel.Configure(race);root.gameObject.SetActive(true);
            var manager=FindFirstObjectByType<Mgr_UI>();if(manager!=null)manager.Register(E_PanelType.DemoRace,panel);
            spriteCamera=FindFirstObjectByType<Sys_SpriteRaceCamera>();if(spriteCamera!=null)spriteCamera.SelectRunner(Mgr_Game.Instance.HeroRunner);
            if(spriteCamera!=null&&spriteCamera.FlatScene)
            {
                var ground=GameObject.Find("img_meadow_course");
                var renderer=ground==null?null:ground.GetComponent<SpriteRenderer>();
                if(renderer!=null){spriteBounds=renderer.bounds;hasSpriteBounds=true;}
            }
            camera3D=FindFirstObjectByType<Sys_RaceCamera>();if(camera3D!=null){camera3D.LandscapeComposition=true;camera3D.CameraDistanceScale=.95f;}
            var presentation=FindFirstObjectByType<Sys_RacePresentation>();
            if(presentation!=null)presentation.Cutscenes=false;
            var skillVideo=FindFirstObjectByType<Sys_RaceSkillVideo>();if(skillVideo!=null)skillVideo.enabled=false;
            var demoVideo=gameObject.AddComponent<Sys_DemoSkillVideo>();
            demoVideo.Configure(race,panel,Mgr_Table.Demo.Monsters[Mgr_User.Data.SelectedMonster].Id);
            gameObject.AddComponent<Sys_DemoSkillEffects>();
            // Optional six-runner purchased FX; removing this hook restores the legacy presentation.
            if (gameObject.scene.name == Define_Scene.OvalToon)
                gameObject.AddComponent<Sys_RaceSkillEffects>();
        }
        private void Update()
        {
            if(race==null)return;
            if(generation!=race.Generation){generation=race.Generation;recorded=false;}
            if(spriteCamera!=null && !race.Paused)
            {
                float t=(float)race.Simulation.Time;
                spriteCamera.Overview=race.Countdown>1.5f;
                float cycle=Mathf.Repeat(t,13);
                spriteCamera.FollowSize=cycle<5?19:cycle<9?15.5f:23;
                if(race.Simulation.Runners[Mgr_Game.Instance.HeroRunner].SkillAge>=0&&race.Simulation.Runners[Mgr_Game.Instance.HeroRunner].SkillAge<4)spriteCamera.FollowSize=13.5f;
            }
            if(race.Simulation.Phase==E_RacePhase.Result&&!recorded)
            {
                recorded=true;Mgr_User.Data.RaceInProgress=false;Mgr_User.Data.RaceCount++;Mgr_User.Save();
            }
        }
        private void LateUpdate()
        {
            if(!hasSpriteBounds||spriteCamera==null||spriteCamera.RaceCamera==null)return;
            // Keep cinematic zooms inside the painted ground at every viewport aspect.
            var camera=spriteCamera.RaceCamera;
            float aspect=Mathf.Max(.1f,camera.aspect);
            float halfHeight=Mathf.Min(camera.orthographicSize,spriteBounds.extents.y-.02f,(spriteBounds.extents.x-.02f)/aspect);
            camera.orthographicSize=halfHeight;
            float halfWidth=halfHeight*aspect;
            var point=camera.transform.position;
            point.x=Mathf.Clamp(point.x,spriteBounds.min.x+halfWidth,spriteBounds.max.x-halfWidth);
            point.y=Mathf.Clamp(point.y,spriteBounds.min.y+halfHeight,spriteBounds.max.y-halfHeight);
            camera.transform.position=point;
        }
    }
}
