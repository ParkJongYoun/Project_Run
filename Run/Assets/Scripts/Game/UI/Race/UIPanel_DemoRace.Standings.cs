using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectRun.Race
{
    // UIPanel_DemoRace의 "왼쪽 순위 리스트(6줄)" 담당 부분.
    // 프리팹의 group_standings > row_1 ~ row_6 를 찾아서
    // 매 프레임 현재 순위대로 이름 / 초상 / 내 캐릭터 강조를 갱신한다.
    public sealed partial class UIPanel_DemoRace
    {
        [Header("순위 리스트 · group_standings (비워두면 SafeArea에서 이름으로 찾음)")]
        [SerializeField] private RectTransform StandingsRoot;

        // 한 줄(row_N)에 들어있는 UI 부품 묶음
        private sealed class StandingRow
        {
            public Image Background;   // img_bg   : 줄 배경 (기본 / 선택 글로우)
            public Image Face;         // img_face : 초상 아이콘
            public Text Name;          // txt_name : 이름
            public int RunnerId = -1;  // 지금 이 줄에 표시 중인 주자 번호 (변경 감지용)
            public bool IsHero;        // 지금 이 줄이 내 캐릭터 강조 상태인지
        }

        // 줄 배경의 "모양 정보" (스프라이트 + 위치 + 크기) 한 세트
        private struct RowLook
        {
            public Sprite Sprite;
            public Vector2 Position;
            public Vector2 Size;
            public static RowLook From(Image image) => new RowLook
            {
                Sprite = image.sprite,
                Position = image.rectTransform.anchoredPosition,
                Size = image.rectTransform.sizeDelta,
            };
            public void ApplyTo(Image image)
            {
                image.sprite = Sprite;
                image.rectTransform.anchoredPosition = Position;
                image.rectTransform.sizeDelta = Size;
            }
        }

        private readonly List<StandingRow> standingRows = new List<StandingRow>();
        private readonly List<Sprite> fallbackFaces = new List<Sprite>(); // 엔트런트에 초상이 없을 때 쓸 프리팹 기본 초상
        private RowLook normalLook;    // 일반 줄 배경 (프리팹 row_1 기준)
        private RowLook selectedLook;  // 내 캐릭터 줄 배경 (프리팹 row_2 기준)
        private bool standingsReady;

        // 프리팹에서 줄 6개를 찾아 연결한다. OnOpen에서 한 번 호출.
        private void InitStandings()
        {
            if (standingsReady) return; // 이미 연결했으면 다시 하지 않음 (재오픈 대비)
            if (StandingsRoot == null && SafeArea != null)
                StandingsRoot = SafeArea.Find("group_standings") as RectTransform;
            if (StandingsRoot == null)
            {
                Debug.LogWarning("UIPanel_DemoRace: group_standings를 찾지 못해 기존 텍스트 순위를 사용합니다.", this);
                // 꺼둔 예전 순위 패널(bg_standings)을 다시 켜서 텍스트 순위라도 보이게 함
                if (StandingsLabel != null) StandingsLabel.transform.parent.gameObject.SetActive(true);
                return;
            }

            for (int i = 1; ; i++)
            {
                var row = StandingsRoot.Find("row_" + i);
                if (row == null) break; // row_1, row_2 ... 없는 번호가 나오면 종료
                var item = new StandingRow
                {
                    Background = row.Find("img_bg")?.GetComponent<Image>(),
                    Face = row.Find("img_face")?.GetComponent<Image>(),
                    Name = row.Find("txt_name")?.GetComponent<Text>(),
                };
                if (item.Background == null || item.Face == null || item.Name == null)
                {
                    Debug.LogWarning("UIPanel_DemoRace: row_" + i + " 의 img_bg / img_face / txt_name 중 빠진 것이 있습니다.", this);
                    continue;
                }
                standingRows.Add(item);
                // 프리팹 초상은 "주자 번호 순서"로 넣어둔 기본값으로 보고 대체용으로 저장
                fallbackFaces.Add(item.Face.sprite);
            }
            if (standingRows.Count == 0) return;

            // 배경 모양 2종 기억: row_1 = 일반, row_2 = 선택(글로우). row_2가 없으면 일반만 사용.
            normalLook = RowLook.From(standingRows[0].Background);
            selectedLook = standingRows.Count > 1 ? RowLook.From(standingRows[1].Background) : normalLook;
            // 시작은 모든 줄을 일반 배경으로 맞춤 (강조는 UpdateStandings에서 내 캐릭터 줄에만 적용)
            foreach (var item in standingRows) normalLook.ApplyTo(item.Background);
            standingsReady = true;
        }

        // 현재 순위로 줄 내용을 갱신한다. LateUpdate에서 매 프레임 호출.
        private void UpdateStandings(Data_RaceSimulation sim)
        {
            if (!standingsReady) return;
            for (int i = 0; i < standingRows.Count; i++)
            {
                var row = standingRows[i];
                bool visible = i < sim.Order.Length; // 주자가 6명보다 적으면 남는 줄은 숨김
                if (row.Background.transform.parent.gameObject.activeSelf != visible)
                    row.Background.transform.parent.gameObject.SetActive(visible);
                if (!visible) continue;

                int id = sim.Order[i];    // i번째 순위에 있는 주자 번호
                bool hero = id == Hero;   // 내 캐릭터인지

                // 주자가 바뀐 줄만 이름/초상 교체 (매 프레임 같은 값을 다시 넣지 않도록)
                if (row.RunnerId != id)
                {
                    row.RunnerId = id;
                    var setup = race.Entrants[id];
                    row.Name.text = setup.Name;
                    var face = setup.FacePortrait != null ? setup.FacePortrait : setup.Portrait;
                    if (face == null && id < fallbackFaces.Count) face = fallbackFaces[id];
                    if (face != null) row.Face.sprite = face;
                }

                // 내 캐릭터 강조가 바뀐 줄만 배경 교체
                if (row.IsHero != hero)
                {
                    row.IsHero = hero;
                    (hero ? selectedLook : normalLook).ApplyTo(row.Background);
                }
            }
        }
    }
}
