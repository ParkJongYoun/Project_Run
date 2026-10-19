using UnityEngine;
using UnityEngine.UI;

namespace ProjectRun.Race
{
    // 데모 레이스 결과창(네온 스타일) 프리팹의 "부품 목록".
    // 프리팹 UIItem_DemoRaceResult 루트에 붙어 있고,
    // UIPanel_DemoRace가 이 목록을 보고 텍스트/이미지/버튼을 채운다.
    public sealed class UIItem_DemoRaceResult : MonoBehaviour
    {
        public Text Title;          // "프론테라컵 우승!"
        public Image Hero;          // 우승 몬스터 전신 일러스트
        public Text Rank;           // 이름판 왼쪽 "1위"
        public Text Name;           // 이름판 "에드가"
        public Text Summary;        // 기록 패널 (기록/레벨/전술/고유/후천)
        public Button ReplayButton; // "처음부터 다시 체험"
        public Button EndDemoButton;// "데모 종료"
    }
}
