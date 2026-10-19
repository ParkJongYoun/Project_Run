using UnityEngine;
using UnityEngine.UI;

namespace ProjectRun.Race
{
    // UIPanel_DemoRace의 "결과창 스킨" 담당 부분.
    // 별도 프리팹 UIItem_DemoRaceResult(네온 스타일)를 불러와서
    // 기존 크림색 카드(group_result_card) 대신 보여준다.
    public sealed partial class UIPanel_DemoRace
    {
        // 결과창 프리팹 경로 (Resources 폴더 기준)
        private const string ResultPrefabPath = "Prefabs/UI/Slot/UIItem_DemoRaceResult";

        [Header("결과창 스킨 · 새 결과창 이름판의 순위 텍스트")]
        [SerializeField] private Text ResultRank;

        private bool resultSkinReady; // 새 결과창으로 교체가 끝났는지
        private int resultRankNumberSize = 132; // "1위"의 숫자 크기 (프리팹 txt_rank에 적힌 <size=N> 값을 그대로 따라감)

        // 새 결과창 프리팹으로 교체. 버튼 연결(BindButtons)보다 먼저 호출해야 함.
        private void InitResultSkin()
        {
            if (resultSkinReady || ResultRoot == null) return;
            var prefab = Resources.Load<GameObject>(ResultPrefabPath);
            if (prefab == null) return; // 프리팹이 없으면 기존 카드 그대로 사용

            var go = Instantiate(prefab, ResultRoot, false); // group_result 아래에 붙임 (어두운 막 bg_result_dim 위)
            go.name = "group_result_neon";
            var item = go.GetComponent<UIItem_DemoRaceResult>();
            if (item == null)
            {
                Debug.LogWarning("UIPanel_DemoRace: " + ResultPrefabPath + "에 UIItem_DemoRaceResult가 없습니다.", this);
                Destroy(go);
                return;
            }

            // 기존 크림색 카드 숨기기 (기존 제목 텍스트의 부모 = group_result_card)
            if (ResultTitle != null) ResultTitle.transform.parent.gameObject.SetActive(false);

            // 코드가 쓰는 참조를 새 결과창 부품으로 바꿔 끼움
            ResultTitle = item.Title;
            ResultHero = item.Hero;
            ResultRank = item.Rank;
            // 프리팹에서 직접 바꾼 숫자 크기(<size=N>)를 읽어와서 실행 중에도 같은 크기로 표시
            if (ResultRank != null)
            {
                var m = System.Text.RegularExpressions.Regex.Match(ResultRank.text, @"<size=(\d+)>");
                if (m.Success) resultRankNumberSize = int.Parse(m.Groups[1].Value);
            }
            ResultName = item.Name;
            ResultSummary = item.Summary;
            ReplayButton = item.ReplayButton;
            EndDemoButton = item.EndDemoButton;
            resultSkinReady = true;
        }

        // 결과 텍스트 채우기 (ShowResult에서 호출)
        private void FillResultTexts(int rank, string finishTime, string speed)
        {
            const string Gold = "#FFC928";   // 강조 금색 (우승!) — 모크업 색
            const string Cyan = "#4FE6FF";   // 강조 하늘색 (기록 라벨)
            string result = rank == 1 ? "우승!" : "완주!";

            if (resultSkinReady)
            {
                // 새 네온 결과창: 모크업처럼 색 강조
                ResultTitle.text = "프론테라컵 <color=" + Gold + ">" + result + "</color>";
                // 숫자는 프리팹에 적힌 크기 그대로, "위"는 텍스트 기본 크기
                if (ResultRank != null) ResultRank.text = "<size=" + resultRankNumberSize + ">" + rank + "</size>위";
                ResultSummary.text =
                    "<color=" + Cyan + ">" + rank + "위</color>   ·  " + finishTime + "초\n"
                    + "<color=" + Cyan + ">Lv.5</color>   ·  스피드 " + speed + "\n"
                    + "<size=10> </size>\n" // 위(기록)와 아래(능력) 사이 간격
                    + "전술  " + Monster.Tactic + "\n고유  " + Monster.Skill + "\n후천  지치지 않는 발";
            }
            else
            {
                // 기존 크림색 카드 (예전과 동일)
                ResultTitle.text = "프론테라컵 " + result;
                ResultSummary.text = rank + "위  ·  " + finishTime + "초\nLv.5  ·  스피드 " + speed
                    + "\n전술  " + Monster.Tactic + "\n고유  " + Monster.Skill + "\n후천  지치지 않는 발";
            }
        }
    }
}
