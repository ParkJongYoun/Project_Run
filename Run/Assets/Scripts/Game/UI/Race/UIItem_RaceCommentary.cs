using System.Collections.Generic;
using UnityEngine;

namespace ProjectRun.Race
{
    /// <summary>Three reusable rows, oldest above newest. Overflow retires before recycling a row.</summary>
    public sealed class UIItem_RaceCommentary : MonoBehaviour
    {
        public Sys_Race Race;
        public UnityEngine.UI.Text[] Lines;
        [SerializeField, Min(1)] private float HoldSeconds = 8;
        [SerializeField, Min(.1f)] private float FadeSeconds = 1.8f;
        [SerializeField, Min(.1f)] private float OverflowFadeSeconds = .45f;
        private const float LineSpacing = 24;
        private sealed class Row
        {
            public UnityEngine.UI.Text Label;
            public Color Tint;
            public float Age, Retirement, Alpha;
            public bool Retiring;
        }
        private readonly List<Row> visible = new List<Row>(3);
        private readonly Queue<string> pending = new Queue<string>(8);
        private Row[] pool;
        private string lastMessage;
        private int generation = -1;
        private double nextContext;
        private float insertCooldown;

        public static UIItem_RaceCommentary Create(Sys_Race race, UnityEngine.UI.Text template)
        {
            var source = template.rectTransform;
            var root = new GameObject("group_commentary_lines", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.SetParent(source.parent, false);
            rect.anchorMin = source.anchorMin; rect.anchorMax = source.anchorMax; rect.pivot = source.pivot;
            rect.anchoredPosition = source.anchoredPosition; rect.sizeDelta = new Vector2(source.rect.width, 72);
            var lines = new UnityEngine.UI.Text[3];
            for (int i = 0; i < lines.Length; i++)
            {
                // Keep the authored template in place: reparenting an inherited prefab child is not persistent.
                var label = Instantiate(template, rect);
                label.name = "txt_commentary_" + i;
                label.enabled = true;
                label.fontSize = 18; label.resizeTextForBestFit = false; label.raycastTarget = false;
                label.alignment = TextAnchor.MiddleLeft;
                label.horizontalOverflow = HorizontalWrapMode.Overflow; label.verticalOverflow = VerticalWrapMode.Truncate;
                label.text = "";
                var line = label.rectTransform;
                line.anchorMin = line.anchorMax = new Vector2(.5f, 0);
                line.pivot = new Vector2(.5f, 0); line.sizeDelta = new Vector2(rect.rect.width, LineSpacing);
                line.anchoredPosition = new Vector2(0, i * LineSpacing);
                lines[i] = label;
            }
            template.enabled = false;
            var feed = root.AddComponent<UIItem_RaceCommentary>();
            feed.Race = race; feed.Lines = lines;
            return feed;
        }
        private void OnEnable() { Mgr_Event.RaceChanged += Handle; Mgr_Event.Reset += ResetFeed; }
        private void OnDisable() { Mgr_Event.RaceChanged -= Handle; Mgr_Event.Reset -= ResetFeed; ResetFeed(); }
        private void ResetFeed()
        {
            visible.Clear(); pending.Clear(); lastMessage = null; nextContext = 5; generation = -1; insertCooldown = 0;
            if (Lines != null) foreach (var line in Lines) if (line != null) line.text = "";
        }
        private void EnsureGeneration()
        {
            if (generation == Race.Generation) return;
            ResetFeed(); generation = Race.Generation;
            if (Race.Simulation.Phase == E_RacePhase.Preparing && !Race.IsSkipping) Enqueue("잠시 후 출발! 여섯 몬스터가 준비합니다");
        }
        public void Enqueue(string message)
        {
            if (string.IsNullOrEmpty(message) || message == lastMessage || Race == null || Race.IsSkipping) return;
            if (pending.Count == 8) pending.Dequeue();
            pending.Enqueue(message); lastMessage = message;
        }
        private void Handle(Data_RaceEvent value)
        {
            if (Race == null || Race.IsSkipping || Race.Simulation == null) return;
            EnsureGeneration();
            var runner = Race.Entrants[value.RunnerId];
            switch (value.Kind)
            {
                case E_RaceEvent.Start: Enqueue("출발! 첫 번째 코너를 향해 달립니다"); break;
                case E_RaceEvent.Skill: Enqueue(runner.Name + " · " + runner.SkillName + " 발동!"); break;
                case E_RaceEvent.Overtake: Enqueue(runner.Name + " · " + Race.Simulation.Runners[value.RunnerId].Rank + "위로 추월!"); break;
                case E_RaceEvent.Finish:
                    if (Race.Simulation.Order[0] == value.RunnerId) Enqueue(runner.Name + " · 선두로 결승선 통과!");
                    break;
                case E_RaceEvent.Result: ResetFeed(); generation = Race.Generation; break;
            }
        }
        private void Update()
        {
            if (Race == null || Race.Simulation == null || Lines == null || Lines.Length != 3) return;
            if (pool == null)
            {
                pool = new Row[Lines.Length];
                for (int i = 0; i < pool.Length; i++) pool[i] = new Row { Label = Lines[i], Tint = Lines[i].color };
            }
            EnsureGeneration();
            var sim = Race.Simulation;
            if (Race.IsSkipping || sim.Phase == E_RacePhase.Result) return;
            float delta = Race.Paused || Race.PresentationHeld ? 0 : Mathf.Min(Time.unscaledDeltaTime, .1f) * Race.PlaybackSpeed;
            insertCooldown = Mathf.Max(0, insertCooldown - delta);
            if (sim.Phase == E_RacePhase.Running && sim.Time >= nextContext)
            {
                nextContext = sim.Time + 5;
                var leader = sim.Runners[sim.Order[0]];
                int blocker = -1;
                foreach (var runner in sim.Runners) if (runner.BlockedBy >= 0) { blocker = runner.Id; break; }
                if (blocker >= 0) Enqueue(Race.Entrants[blocker].Name + " · 앞 주자를 피해 빈틈 탐색!");
                else if (sim.Order.Length > 1)
                {
                    var second = sim.Runners[sim.Order[1]];
                    Enqueue(Race.Entrants[second.Id].Name + " · 선두와 " + Mathf.Max(0, (float)(leader.Distance - second.Distance)).ToString("0.0") + "m 차로 추격!");
                }
            }
            for (int i = visible.Count - 1; i >= 0; i--)
            {
                var row = visible[i]; row.Age += delta;
                if (row.Retiring) row.Retirement += delta;
                if (row.Age >= HoldSeconds + FadeSeconds || row.Retiring && row.Retirement >= OverflowFadeSeconds)
                { row.Label.text = ""; visible.RemoveAt(i); }
            }
            if (pending.Count > 0 && delta > 0 && insertCooldown == 0)
            {
                if (visible.Count == 3) visible[0].Retiring = true;
                else foreach (var row in pool)
                {
                    if (visible.Contains(row)) continue;
                    row.Age = row.Retirement = row.Alpha = 0; row.Retiring = false;
                    row.Label.text = pending.Dequeue(); row.Label.rectTransform.anchoredPosition = Vector2.zero;
                    visible.Add(row); insertCooldown = .3f; break;
                }
            }
            for (int i = 0; i < visible.Count; i++)
            {
                var row = visible[i]; int depth = visible.Count - 1 - i;
                float ageFade = 1 - Mathf.SmoothStep(0, 1, (row.Age - HoldSeconds) / FadeSeconds);
                float retirementFade = row.Retiring ? 1 - Mathf.SmoothStep(0, 1, row.Retirement / OverflowFadeSeconds) : 1;
                float freshness = depth == 0 ? 1 : depth == 1 ? .74f : .46f;
                float targetAlpha = freshness * Mathf.SmoothStep(0, 1, row.Age / .18f) * ageFade * retirementFade;
                // Retirement reaches transparent before the label is reused, even during a burst of events.
                row.Alpha = row.Retiring || row.Age > HoldSeconds ? Mathf.Min(row.Alpha, targetAlpha) : Mathf.MoveTowards(row.Alpha, targetAlpha, delta * 5);
                var tint = row.Tint; tint.a *= row.Alpha; row.Label.color = tint;
                var point = row.Label.rectTransform.anchoredPosition;
                point.y = Mathf.Lerp(point.y, depth * LineSpacing, 1 - Mathf.Exp(-14 * delta));
                row.Label.rectTransform.anchoredPosition = point;
            }
        }
    }
}
