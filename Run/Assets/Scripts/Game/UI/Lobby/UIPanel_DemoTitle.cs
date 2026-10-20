using UnityEngine;

namespace ProjectRun.Race
{
    public sealed class UIPanel_DemoTitle : UIPanel_Base
    {
        [SerializeField] private RectTransform SafeArea;
        [SerializeField] private UnityEngine.UI.Text StartLabel;
        public void Build()
        {
            DemoUI.Clear(transform);
            var background = DemoUI.Image(transform,"bg_title","title",0,0,1280,720);
            background.gameObject.AddComponent<Canvas>();
            background.gameObject.AddComponent<UIItem_TitleArtworkMotion>();
            var fitter = background.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>(); fitter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent; fitter.aspectRatio = 1672f/941;
            DemoUI.Shade(transform,"bg_tint",new Color(.04f,.12f,.14f,.10f));
            SafeArea = DemoUI.Fill(transform,"obj_safe_area");
            var title=DemoUI.Text(SafeArea,"txt_title","MONSTER RACE",0,205,1040,100,64,DemoUI.Paper,TextAnchor.MiddleCenter,true);
            var shadow=title.gameObject.AddComponent<UnityEngine.UI.Shadow>();shadow.effectColor=new Color(.06f,.18f,.17f,.8f);shadow.effectDistance=new Vector2(2,-3);
            DemoUI.Text(SafeArea,"txt_caption","라그나로크  ·  함께 달리는 첫 번째 이야기",0,132,900,45,24,Color.white);
            var button=DemoUI.Rect(SafeArea,"btn_game_start",0,-205,420,100).gameObject.AddComponent<UnityEngine.UI.Button>();
            var hit=button.gameObject.AddComponent<UnityEngine.UI.Image>();hit.color=Color.clear;button.targetGraphic=hit;
            StartLabel=DemoUI.Text(button.transform,"txt_start","GAME START",0,0,420,90,40,Color.white,TextAnchor.MiddleCenter,true);
            var outline=StartLabel.gameObject.AddComponent<UnityEngine.UI.Shadow>();outline.effectColor=new Color(.02f,.12f,.08f,.9f);outline.effectDistance=new Vector2(2,-3);
            DemoUI.Text(SafeArea,"txt_hint","GAME START를 눌러 모험을 시작하세요",0,-270,600,35,18,Color.white);
        }
        public override void OnOpen()
        {
            if (SafeArea == null) Build();
            transform.Find("obj_safe_area/btn_game_start").GetComponent<UnityEngine.UI.Button>().onClick.AddListener(StartGame);
        }
        public override void OnClose()
        {
            var button=transform.Find("obj_safe_area/btn_game_start"); if(button!=null) button.GetComponent<UnityEngine.UI.Button>().onClick.RemoveListener(StartGame);
        }
        private void StartGame() => Mgr_Game.Instance.StartDemo();
        private void Update()
        {
            DemoUI.SafeArea(SafeArea);
            if(StartLabel!=null) StartLabel.color=new Color(1,1,1,.76f+.24f*Mathf.Sin(Time.unscaledTime*1.7f)*Mathf.Sin(Time.unscaledTime*1.7f));
        }
    }
}
