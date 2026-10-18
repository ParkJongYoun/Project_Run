using UnityEngine;

namespace ProjectRun.Race
{
    public sealed class Sys_SpriteRacePresentation : MonoBehaviour
    {
        public Sys_Race Race;
        public int SkillRunner { get; private set; } = -1;
        public string Announcement { get; private set; } = "출발을 준비합니다";
        public float SkillAge => SkillRunner < 0 || Race == null || Race.Simulation == null ? -1 : (float)Race.Simulation.Time - skillTime;
        private float skillTime;
        private float announcementUntil;
        private int announcedLap = 1;

        private void OnEnable() { Mgr_Event.RaceChanged += OnRaceEvent; Mgr_Event.Reset += ResetPresentation; }
        private void OnDisable() { Mgr_Event.RaceChanged -= OnRaceEvent; Mgr_Event.Reset -= ResetPresentation; }
        private void ResetPresentation() { SkillRunner = -1; skillTime = -100; announcementUntil = 0; announcedLap = 1; Announcement = "출발을 준비합니다"; }
        private void OnRaceEvent(Data_RaceEvent value)
        {
            if (Race == null || Race.IsSkipping || value.RunnerId < 0 || value.RunnerId >= Race.Entrants.Length) return;
            string name = Race.Entrants[value.RunnerId].Name;
            if (value.Kind == E_RaceEvent.Skill)
            {
                SkillRunner = value.RunnerId; skillTime = value.Time;
                Announcement = name + " · " + Race.Entrants[value.RunnerId].SkillName;
                announcementUntil = value.Time + 2;
            }
            else if (value.Kind == E_RaceEvent.Overtake && value.Time > announcementUntil)
            { Announcement = name + " 추월 성공!"; announcementUntil = value.Time + 2; }
            else if (value.Kind == E_RaceEvent.Start) { Announcement = "출발! 안쪽 자리를 향한 경합"; announcementUntil = 3; }
            else if (value.Kind == E_RaceEvent.Result) Announcement = name + " 우승!";
        }
        private void Update()
        {
            if (Race == null || Race.Simulation == null || Race.Simulation.Time < announcementUntil || Race.Simulation.Phase == E_RacePhase.Result) return;
            var sim = Race.Simulation;
            int lap = sim.CurrentLap(sim.Runners[sim.Order[0]].Distance);
            if (lap > announcedLap)
            {
                announcedLap = lap;
                Announcement = lap == sim.LapCount ? "마지막 바퀴! 선두 자리를 향한 경합" : lap + "바퀴째 진입!";
                announcementUntil = (float)sim.Time + 2.5f; return;
            }
            foreach (int id in sim.Order)
                if (sim.Runners[id].BlockedBy >= 0)
                { Announcement = Race.Entrants[id].Name + " · 앞 주자를 피해 빈틈 탐색"; return; }
            Announcement = sim.Runners[sim.Order[0]].Distance > sim.CourseLength * .72
                ? "결승선을 향한 마지막 스퍼트!" : "선두 " + Race.Entrants[sim.Order[0]].Name + " · 추격 대열 경합";
        }
    }
}
