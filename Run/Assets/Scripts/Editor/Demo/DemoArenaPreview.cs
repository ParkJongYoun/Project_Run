using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ProjectRun.Race.Editor
{
    // Deterministic capture of one complete exhibition lap for art/path review.
    public static class DemoArenaPreview
    {
        private const string Folder="DataBundles/검증/로비_안내와경기장_보정_20261018";
        public static void Capture() => Mgr_Game.Instance.StartCoroutine(Frames());
        private static IEnumerator Frames()
        {
            var lobby=Object.FindFirstObjectByType<UIPanel_DemoLobby>();
            lobby.ShowArena();lobby.DismissGuide();
            if(GameObject.Find("obj_menu_highlight")!=null)
            {
                lobby.ShowNotebook();
                GameObject.Find("btn_close_notebook").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            }
            var exhibition=Object.FindFirstObjectByType<UIItem_ArenaExhibition>();
            float timeout=Time.realtimeSinceStartup+20;
            while(exhibition.transform.childCount<6&&Time.realtimeSinceStartup<timeout)yield return null;
            if(exhibition.transform.childCount!=6)throw new System.InvalidOperationException("Exhibition sprites did not load.");
            yield return null;
            Directory.CreateDirectory(Folder+"/작업_주행프레임");
            var clock=typeof(UIItem_ArenaExhibition).GetField("elapsed",BindingFlags.NonPublic|BindingFlags.Instance);
            var tick=typeof(UIItem_ArenaExhibition).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance);
            float original=(float)clock.GetValue(exhibition);
            var positions=new StringBuilder("frame,runner,x,y,sprite\n");
            exhibition.enabled=false;
            try
            {
                for(int frame=0;frame<384;frame++)
                {
                    clock.SetValue(exhibition,frame/12f-Time.unscaledDeltaTime);tick.Invoke(exhibition,null);
                    yield return new WaitForEndOfFrame();
                    var screen=ScreenCapture.CaptureScreenshotAsTexture();
                    try
                    {
                        File.WriteAllBytes(Folder+"/작업_주행프레임/"+frame.ToString("D4")+".jpg",screen.EncodeToJPG(90));
                    }
                    finally {Object.Destroy(screen);}
                    var rect=(RectTransform)exhibition.transform;
                    foreach(var image in exhibition.GetComponentsInChildren<UnityEngine.UI.Image>())
                    {
                        var point=image.rectTransform.anchoredPosition;
                        positions.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2:F2},{3:F2},{4}\n",frame,image.name,(point.x/rect.rect.width+.5f)*1672,(.5f-point.y/rect.rect.height)*941,image.sprite.name);
                    }
                }
                File.WriteAllText(Folder+"/한바퀴_발위치.csv",positions.ToString());
                File.WriteAllText(Folder+"/주행캡처_상태.txt","COMPLETED 384 frames, 12 fps, 32 seconds, 6 runners.");
            }
            finally
            {
                if(exhibition!=null){clock.SetValue(exhibition,original);exhibition.enabled=true;}
            }
        }
    }
}
