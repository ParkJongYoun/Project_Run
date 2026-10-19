using UnityEngine;
using UnityEngine.UI;

namespace ProjectRun.Race
{
    // UIPanel_DemoRace의 "하단바 순위 글자(2위 / 6)" 담당 부분.
    // 글자 크기·색·폰트를 코드에 박아두지 않고 Inspector에서 바로 조절할 수 있게 뺐다.
    // 위치: UIPanel_DemoRace 프리팹 루트 선택 → Inspector → "하단 순위 글자" 항목
    // 글자 오브젝트: obj_safe_area > bg_footer > txt_rank (위치/칸 크기는 여기서 조절)
    public sealed partial class UIPanel_DemoRace
    {
        [Header("하단 순위 글자 · bg_footer > txt_rank")]
        [SerializeField, Tooltip("비워두면 진행바 START 글자와 같은 폰트(Black Han Sans)를 자동 사용")]
        private Font RankFont;
        [SerializeField, Tooltip("순위 숫자 \"2\" 크기")] private int RankNumberSize = 60;
        [SerializeField, Tooltip("\"위\" 글자 크기")] private int RankUnitSize = 44;
        [SerializeField, Tooltip("\" / 6\" 크기")] private int RankTotalSize = 30;
        [SerializeField, Tooltip("\"2위\" 색")] private Color RankColor = new Color(.31f, .9f, 1f);   // 하늘색
        [SerializeField, Tooltip("\" / 6\" 색")] private Color RankTotalColor = Color.white;
        [SerializeField, Tooltip("기울임체 사용")] private bool RankItalic = true;
        [SerializeField, Tooltip("글자 테두리 색 (투명이면 테두리 없음)")] private Color RankOutlineColor = new Color(.02f, .08f, .3f, 1f);
        [SerializeField, Tooltip("글자 테두리 두께(px)")] private float RankOutlineSize = 2.5f;

        private string rankLabelCache;  // 마지막으로 쓴 글자 (같으면 다시 안 씀)
        private bool rankFontApplied;

        // "2위 / 6" 글자를 만들어 txt_rank에 넣는다. LateUpdate에서 호출.
        private void UpdateRankLabel(int rank, int total)
        {
            if (RankLabel == null) return;
            if (!rankFontApplied)
            {
                // 폰트: Inspector 지정값 → 없으면 진행바 START 글자 폰트(Black Han Sans) 빌려 쓰기
                var font = RankFont;
                if (font == null && ProgressRoot != null)
                {
                    var start = ProgressRoot.Find("txt_start");
                    if (start != null && start.TryGetComponent<Text>(out var startText)) font = startText.font;
                }
                if (font != null) RankLabel.font = font;

                // 테두리: txt_rank에 Outline 컴포넌트가 없으면 붙이고 색/두께 적용
                var outline = RankLabel.GetComponent<Outline>();
                if (outline == null) outline = RankLabel.gameObject.AddComponent<Outline>();
                outline.effectColor = RankOutlineColor;
                outline.effectDistance = new Vector2(RankOutlineSize, -RankOutlineSize);
                outline.enabled = RankOutlineColor.a > 0;
                RankLabel.supportRichText = true; // <size>, <color> 태그 사용
                RankLabel.horizontalOverflow = HorizontalWrapMode.Overflow; // 커진 글자가 잘리지 않게
                RankLabel.verticalOverflow = VerticalWrapMode.Overflow;
                rankFontApplied = true;
            }

            string main = "#" + ColorUtility.ToHtmlStringRGB(RankColor);
            string sub = "#" + ColorUtility.ToHtmlStringRGB(RankTotalColor);
            string text =
                "<size=" + RankNumberSize + "><color=" + main + ">" + rank + "</color></size>"   // 2
                + "<size=" + RankUnitSize + "><color=" + main + ">위</color></size>"             // 위
                + "<size=" + RankTotalSize + "><color=" + sub + "> / " + total + "</color></size>"; //  / 6
            if (RankItalic) text = "<i>" + text + "</i>";

            if (text != rankLabelCache) { rankLabelCache = text; RankLabel.text = text; }
        }
    }
}
