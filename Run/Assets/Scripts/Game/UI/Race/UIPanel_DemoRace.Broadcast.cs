using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectRun.Race
{
    public sealed partial class UIPanel_DemoRace
    {
        private UIItem_RaceSkillRibbon[] ribbonPool;
        private readonly List<UIItem_RaceSkillRibbon> visibleRibbons=new List<UIItem_RaceSkillRibbon>(4);
        private readonly Queue<Data_RaceEvent> pendingRibbons=new Queue<Data_RaceEvent>();
        private Button standingsToggle;
        private RectTransform playerMarker;
        private Sys_RacePresentation racePresentation;
        private Camera markerCamera;
        private bool standingsExpanded=true;
        private float ribbonCooldown;

        private void InitBroadcast()
        {
            var feed=SafeArea.Find("group_skill_feed");
            ribbonPool=feed.GetComponentsInChildren<UIItem_RaceSkillRibbon>(true);
            standingsToggle=SafeArea.Find("btn_standings").GetComponent<Button>();
            standingsToggle.onClick.AddListener(ToggleStandings);
            playerMarker=SafeArea.Find("img_player_marker") as RectTransform;
            racePresentation=FindFirstObjectByType<Sys_RacePresentation>();
            markerCamera=Camera.main;
            ResetBroadcast();
            Mgr_Event.Reset+=ResetBroadcast;
        }
        private void CloseBroadcast()
        {
            Mgr_Event.Reset-=ResetBroadcast;
            if(standingsToggle!=null)standingsToggle.onClick.RemoveListener(ToggleStandings);
            ResetBroadcast();
        }
        private void ResetBroadcast()
        {
            pendingRibbons.Clear();visibleRibbons.Clear();ribbonCooldown=0;
            if(ribbonPool!=null)foreach(var ribbon in ribbonPool)ribbon.Clear();
            SetStandings(race!=null&&race.Simulation!=null&&race.Simulation.Phase==E_RacePhase.Preparing);
            if(playerMarker!=null)playerMarker.gameObject.SetActive(false);
        }
        private void ToggleStandings()=>SetStandings(!standingsExpanded);
        private void SetStandings(bool expanded)
        {
            standingsExpanded=expanded;
            if(StandingsRoot!=null)StandingsRoot.gameObject.SetActive(expanded);
            if(standingsToggle!=null)
            {
                var colors=standingsToggle.colors;
                colors.normalColor=expanded?new Color(.32f,.85f,1):Color.white;
                standingsToggle.colors=colors;
            }
        }
        private void HandleBroadcast(Data_RaceEvent value)
        {
            if(value.Kind==E_RaceEvent.Start)SetStandings(false);
            if(value.Kind==E_RaceEvent.Result){ResetBroadcast();return;}
            if(value.Kind!=E_RaceEvent.Skill&&value.Kind!=E_RaceEvent.MinorSkill)return;
            if(race.Entrants[value.RunnerId].RaceSkills==null)return;
            pendingRibbons.Enqueue(value);
        }
        private void UpdateBroadcast()
        {
            float delta=race.Paused||race.PresentationHeld?0:Mathf.Min(Time.unscaledDeltaTime,.1f);
            ribbonCooldown=Mathf.Max(0,ribbonCooldown-delta);
            for(int i=visibleRibbons.Count-1;i>=0;i--)
            {
                visibleRibbons[i].Tick(delta,i);
                if(!visibleRibbons[i].Active)visibleRibbons.RemoveAt(i);
            }
            if(pendingRibbons.Count>0&&visibleRibbons.Count==ribbonPool.Length)visibleRibbons[0].RetireSoon();
            // Every activation gets a readable slot, including bursts at 2x.
            if(pendingRibbons.Count>0&&ribbonCooldown<=0&&delta>0&&visibleRibbons.Count<ribbonPool.Length)
                foreach(var ribbon in ribbonPool)
                {
                    if(ribbon.Active)continue;
                    var value=pendingRibbons.Dequeue();
                    ribbon.Show(race.Entrants[value.RunnerId],value.RunnerId,value.Rarity,value.RunnerId==Hero,visibleRibbons.Count);
                    visibleRibbons.Add(ribbon);ribbonCooldown=.3f;break;
                }
            UpdatePlayerMarker();
        }
        private void UpdatePlayerMarker()
        {
            bool show=race.Simulation.Phase==E_RacePhase.Running&&!race.PresentationHeld&&
                !race.Simulation.Runners[Hero].Finished&&racePresentation!=null&&racePresentation.Views!=null&&
                Hero<racePresentation.Views.Length&&markerCamera!=null&&!CameraRig.RaceStartIntro.Playing;
            if(!show){playerMarker.gameObject.SetActive(false);return;}
            var runner=racePresentation.Views[Hero];
            Vector3 point=runner.MonsterFaceTarget!=null?runner.MonsterFaceTarget.position:runner.Focus;
            point.y+=Mathf.Max(.7f,runner.CameraFocusHeight*.32f);
            Vector3 screen=markerCamera.WorldToScreenPoint(point);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(SafeArea,screen,null,out local);
            Rect bounds=SafeArea.rect;
            bool onScreen=screen.z>0&&local.x>bounds.xMin+24&&local.x<bounds.xMax-24&&
                local.y>bounds.yMin+135&&local.y<bounds.yMax-100;
            playerMarker.gameObject.SetActive(onScreen);
            if(onScreen)playerMarker.anchoredPosition=local+Vector2.up*(20+Mathf.Sin((float)race.Simulation.Time*3)*3);
        }
    }
}
