using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectRun.Race
{
    // UIPanel_DemoRace의 "상단 START ─── GOAL 진행바" 담당 부분.
    // 프리팹의 group_progress 안에 있는 마커 6개(marker_1~6)를
    // 각 주자의 달린 거리 비율만큼 트랙 위에서 왼쪽→오른쪽으로 움직인다.
    public sealed partial class UIPanel_DemoRace
    {
        [Header("진행바 · group_progress (비워두면 SafeArea에서 이름으로 찾음)")]
        [SerializeField] private RectTransform ProgressRoot;

        // 패널에 진행바가 없을 때 불러올 프리팹 경로 (Resources 폴더 기준)
        private const string ProgressPrefabPath = "Prefabs/UI/Slot/UIItem_RaceProgress";

        // 주자 색이 설정 안 된(흰색) 경우 쓸 기본 색 6가지
        private static readonly Color[] MarkerPalette =
        {
            new Color(.35f, .22f, .14f), // 갈색
            new Color(.25f, .55f, 1f),   // 파랑
            new Color(1f, .35f, .6f),    // 핑크
            new Color(1f, .55f, .15f),   // 주황
            new Color(.25f, .75f, .35f), // 초록
            new Color(1f, .78f, .15f),   // 노랑
        };
        private static readonly Color HeroRingColor = new Color(1f, .24f, .52f); // 내 캐릭터 테두리(핑크)

        // 마커 1개 = 흰 링 + 색 점 + 번호
        private sealed class ProgressMarker
        {
            public RectTransform Root;
            public Image Ring;
            public Image Dot;
            public Text Number;
        }

        private readonly List<ProgressMarker> progressMarkers = new List<ProgressMarker>();
        private RectTransform markerTrack;  // group_markers : 마커가 움직이는 구간 (폭 = 트랙 길이)
        private RectTransform heroArrow;    // img_hero_marker : 내 캐릭터 위 핑크 삼각형
        private Image trackFill;            // img_track_fill : 내 캐릭터 진행만큼 채워지는 라인
        private bool progressReady;
        private bool progressColored;       // 색/번호는 한 번만 칠하면 됨

        // 프리팹에서 진행바 부품을 찾아 연결한다. OnOpen에서 호출.
        private void InitProgress()
        {
            progressColored = false; // 레이스가 새로 열리면 색/번호를 다시 칠함
            if (progressReady) return;
            if (ProgressRoot == null && SafeArea != null)
                ProgressRoot = SafeArea.Find("group_progress") as RectTransform;
            if (ProgressRoot == null && SafeArea != null)
            {
                // 패널 안에 진행바가 없으면 별도 프리팹(UIItem_RaceProgress)을 불러와 SafeArea 아래에 붙임
                var prefab = Resources.Load<GameObject>(ProgressPrefabPath);
                if (prefab != null)
                {
                    var go = Instantiate(prefab, SafeArea, false); // false = 프리팹에 저장된 앵커/위치 그대로 사용
                    go.name = "group_progress";
                    ProgressRoot = go.transform as RectTransform;
                }
            }
            if (ProgressRoot == null)
            {
                Debug.LogWarning("UIPanel_DemoRace: 진행바(group_progress / " + ProgressPrefabPath + ")를 찾지 못했습니다.", this);
                return;
            }

            markerTrack = ProgressRoot.Find("group_markers") as RectTransform;
            trackFill = ProgressRoot.Find("img_track_fill")?.GetComponent<Image>();
            if (markerTrack == null) return;
            heroArrow = markerTrack.Find("img_hero_marker") as RectTransform;

            for (int i = 1; ; i++)
            {
                var root = markerTrack.Find("marker_" + i) as RectTransform;
                if (root == null) break;
                progressMarkers.Add(new ProgressMarker
                {
                    Root = root,
                    Ring = root.Find("img_ring")?.GetComponent<Image>(),
                    Dot = root.Find("img_dot")?.GetComponent<Image>(),
                    Number = root.Find("txt_num")?.GetComponent<Text>(),
                });
            }
            progressReady = progressMarkers.Count > 0;
        }

        // 마커 위치 갱신. LateUpdate에서 매 프레임 호출.
        private void UpdateProgress(Data_RaceSimulation sim)
        {
            if (!progressReady) return;
            float length = Mathf.Max(1f, sim.CourseLength); // 0으로 나누기 방지
            float width = markerTrack.rect.width;            // 트랙 길이(px)

            if (!progressColored) ColorMarkers(sim);

            for (int id = 0; id < progressMarkers.Count; id++)
            {
                var marker = progressMarkers[id];
                bool visible = id < sim.Runners.Length;
                if (marker.Root.gameObject.activeSelf != visible) marker.Root.gameObject.SetActive(visible);
                if (!visible) continue;

                // 진행률 0~1 → 트랙 위 x 좌표
                float t = Mathf.Clamp01((float)sim.Runners[id].Distance / length);
                var p = marker.Root.anchoredPosition;
                p.x = t * width;
                marker.Root.anchoredPosition = p;

                if (id == Hero)
                {
                    if (heroArrow != null)
                    {
                        var a = heroArrow.anchoredPosition;
                        a.x = p.x;
                        heroArrow.anchoredPosition = a;
                    }
                    if (trackFill != null) trackFill.fillAmount = t;
                }
            }
        }

        // 마커마다 번호와 색을 칠하고, 내 캐릭터 마커를 크게/맨 앞으로.
        private void ColorMarkers(Data_RaceSimulation sim)
        {
            for (int id = 0; id < progressMarkers.Count && id < race.Entrants.Length; id++)
            {
                var marker = progressMarkers[id];
                var setup = race.Entrants[id];
                bool hero = id == Hero;

                // 주자 색이 흰색(기본값)이면 팔레트 색 사용
                Color color = setup != null && setup.Color != Color.white ? setup.Color : MarkerPalette[id % MarkerPalette.Length];
                if (marker.Dot != null) marker.Dot.color = color;
                if (marker.Ring != null) marker.Ring.color = hero ? HeroRingColor : Color.white;
                if (marker.Number != null) marker.Number.text = (id + 1).ToString();

                marker.Root.localScale = Vector3.one * (hero ? 1.3f : 1f);
                if (hero) marker.Root.SetAsLastSibling(); // 다른 마커에 가려지지 않게 맨 위로
            }
            if (heroArrow != null) heroArrow.SetAsLastSibling();
            if (trackFill != null) trackFill.fillAmount = 0;
            progressColored = true;
        }
    }
}
