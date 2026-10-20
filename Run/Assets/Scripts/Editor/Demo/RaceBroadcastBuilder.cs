using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace ProjectRun.Race.Editor
{
    public static class RaceBroadcastBuilder
    {
        private const string portraitFolder="Assets/ArtBundles/UI/RaceBroadcast";
        private const string reportFolder="DataBundles/검증/경기스킬과HUD_20261020";

        [MenuItem("Tools/Project Run/Demo/Apply Race Broadcast HUD")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");
            CreatePortraits();
            ApplyLayout();
        }

        [MenuItem("Tools/Project Run/Demo/Apply Race Broadcast Layout")]
        public static void ApplyLayout()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");
            RaceBroadcastArtImporter.Import();
            var root=PrefabUtility.LoadPrefabContents(DemoRacePrefabBuilder.PrefabPath);
            try
            {
                var safe=root.transform.Find("obj_safe_area");
                var footer=safe.Find("bg_footer");
                var cutscene=safe.Find("btn_cutscene");
                if(cutscene!=null)Object.DestroyImmediate(cutscene.gameObject);
                var lobby=safe.Find("btn_lobby");
                if(lobby!=null)lobby.gameObject.SetActive(false);
                var hero=footer.Find("img_hero").GetComponent<Image>();
                hero.rectTransform.localScale=Vector3.one;
                hero.rectTransform.anchoredPosition=new Vector2(-467,8);
                hero.rectTransform.sizeDelta=new Vector2(122,122);
                hero.preserveAspect=true;
                var feed=footer.Find("group_commentary_lines").GetComponent<UIItem_RaceCommentary>();
                var feedRect=(RectTransform)feed.transform;
                feedRect.localScale=Vector3.one;
                feedRect.anchoredPosition=new Vector2(153,17);
                feedRect.sizeDelta=new Vector2(286,63);
                if(feed.GetComponent<RectMask2D>()==null)feed.gameObject.AddComponent<RectMask2D>();
                var feedSettings=new SerializedObject(feed);
                feedSettings.FindProperty("LineSpacing").floatValue=20;
                feedSettings.ApplyModifiedPropertiesWithoutUndo();
                for(int i=0;i<feed.Lines.Length;i++)
                {
                    var line=feed.Lines[i];
                    line.fontSize=15;line.resizeTextForBestFit=false;
                    line.horizontalOverflow=HorizontalWrapMode.Wrap;
                    line.verticalOverflow=VerticalWrapMode.Truncate;
                    line.rectTransform.sizeDelta=new Vector2(280,21);
                    line.rectTransform.anchoredPosition=new Vector2(0,i*20);
                    line.text="";
                }
                var fallback=footer.Find("txt_commentary").GetComponent<Text>();
                fallback.fontSize=15;fallback.rectTransform.anchoredPosition=feedRect.anchoredPosition;
                fallback.rectTransform.sizeDelta=feedRect.sizeDelta;fallback.horizontalOverflow=HorizontalWrapMode.Wrap;
                foreach(var face in safe.Find("group_standings").GetComponentsInChildren<Image>(true))
                    if(face.name=="img_face"){face.preserveAspect=true;face.rectTransform.localScale=Vector3.one;face.rectTransform.sizeDelta=new Vector2(48,48);}
                EnsureBroadcast(safe);
                PrefabUtility.SaveAsPrefabAsset(root,DemoRacePrefabBuilder.PrefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
            Debug.Log("RACE_BROADCAST_HUD_APPLIED");
        }

        private static void EnsureBroadcast(Transform safe)
        {
            if(safe.Find("group_skill_feed")==null)
            {
                var feed=DemoUI.Rect(safe,"group_skill_feed",-184,-246,320,244);
                Anchor(feed,1,1);
                for(int i=0;i<4;i++)
                {
                    var row=DemoUI.Rect(feed,"obj_skill_"+i,0,0,320,55);
                    row.anchorMin=row.anchorMax=new Vector2(.5f,0);
                    var item=row.gameObject.AddComponent<UIItem_RaceSkillRibbon>();
                    item.Opacity=row.gameObject.AddComponent<CanvasGroup>();
                    item.Opacity.blocksRaycasts=false;item.Opacity.interactable=false;
                    item.Face=DemoUI.Image(row,"img_face",null,-115,0,48,48,true);
                    item.Runner=DemoUI.Text(row,"txt_runner","",7,13,185,20,12,new Color(.7f,.8f,.9f),TextAnchor.MiddleLeft);
                    item.Skill=DemoUI.Text(row,"txt_skill","",28,-9,228,25,19,Color.white,TextAnchor.MiddleLeft,true);
                    row.gameObject.SetActive(false);
                }
            }
            var feedRect=(RectTransform)safe.Find("group_skill_feed");
            Anchor(feedRect,1,1);feedRect.pivot=Vector2.one;
            feedRect.anchoredPosition=new Vector2(-24,-112);feedRect.sizeDelta=new Vector2(260,160);
            foreach(var ribbon in feedRect.GetComponentsInChildren<UIItem_RaceSkillRibbon>(true))
            {
                var row=(RectTransform)ribbon.transform;
                row.anchorMin=row.anchorMax=new Vector2(1,1);row.pivot=new Vector2(1,.5f);
                row.sizeDelta=new Vector2(UIItem_RaceSkillRibbon.WidthFor(E_RaceSkillRarity.Normal),UIItem_RaceSkillRibbon.RowHeight);
                var background=AuthoredImage(row,"bg_ribbon",RaceBroadcastArtImporter.Background,true);
                Stretch(background.rectTransform,3);
                background.transform.SetAsFirstSibling();
                ribbon.Frame=AuthoredImage(row,"img_frame",RaceBroadcastArtImporter.Frame,true);
                Stretch(ribbon.Frame.rectTransform,0);ribbon.Frame.transform.SetSiblingIndex(1);
                foreach(string obsolete in new[]{"img_accent","img_shine"})
                {
                    var child=row.Find(obsolete);if(child!=null)Object.DestroyImmediate(child.gameObject);
                }
                ribbon.UniquePoint=AuthoredImage(row,"img_unique_point",RaceSprite("race_dot"),false);
                Anchor(ribbon.UniquePoint.rectTransform,1,.5f);
                ribbon.UniquePoint.rectTransform.anchoredPosition=new Vector2(-14,0);
                ribbon.UniquePoint.rectTransform.sizeDelta=new Vector2(5,5);
                ribbon.UniquePoint.gameObject.SetActive(false);
                var rarity=row.Find("txt_rarity");
                if(rarity!=null)Object.DestroyImmediate(rarity.gameObject);
                PlaceLeft(ribbon.Face.rectTransform,28,0,28,28);
                PlaceText(ribbon.Runner.rectTransform,49,8,23,14);ribbon.Runner.fontSize=10;
                PlaceText(ribbon.Skill.rectTransform,49,-7,23,19);
                ribbon.Skill.font=DemoUI.Art.BodyFont;ribbon.Skill.fontStyle=FontStyle.Bold;ribbon.Skill.fontSize=15;
                ribbon.Skill.horizontalOverflow=HorizontalWrapMode.Wrap;ribbon.Skill.verticalOverflow=VerticalWrapMode.Truncate;
                row.gameObject.SetActive(false);
            }
            if(safe.Find("btn_standings")==null)
            {
                var buttonRoot=DemoUI.Rect(safe,"btn_standings",53,-109,54,39);Anchor(buttonRoot,0,1);
                buttonRoot.gameObject.AddComponent<Button>();
            }
            var toggle=safe.Find("btn_standings");
            var buttonBackground=AuthoredImage(toggle,"bg_button",RaceBroadcastArtImporter.Background,true);
            Stretch(buttonBackground.rectTransform,2);buttonBackground.raycastTarget=true;
            toggle.GetComponent<Button>().targetGraphic=buttonBackground;
            var buttonFrame=AuthoredImage(toggle,"img_frame",RaceBroadcastArtImporter.Frame,true);
            Stretch(buttonFrame.rectTransform,0);buttonFrame.color=new Color(.05f,.85f,1);
            var icon=AuthoredImage(toggle,"img_list",RaceBroadcastArtImporter.ListIcon,false);
            icon.rectTransform.anchoredPosition=Vector2.zero;icon.rectTransform.sizeDelta=new Vector2(30,24);icon.preserveAspect=true;
            var marker=AuthoredImage(safe,"img_player_marker",RaceSprite("race_rank_marker_pink"),false);
            marker.rectTransform.sizeDelta=new Vector2(36,39);marker.preserveAspect=true;
        }
        private static Sprite RaceSprite(string name)=>
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ArtBundles/UI/Demo/RaceHUD/"+name+".png");
        private static Image AuthoredImage(Transform parent,string name,Sprite sprite,bool sliced)
        {
            if(sprite==null)throw new InvalidOperationException("Missing image sprite for "+name);
            var rect=parent.Find(name) as RectTransform;
            if(rect==null)rect=DemoUI.Rect(parent,name,0,0,36,36);
            foreach(var old in rect.GetComponents<Graphic>())
                if(!(old is Image))Object.DestroyImmediate(old);
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(rect.gameObject);
            var image=rect.GetComponent<Image>()??rect.gameObject.AddComponent<Image>();
            image.sprite=sprite;image.type=sliced?Image.Type.Sliced:Image.Type.Simple;
            image.color=Color.white;image.raycastTarget=false;image.preserveAspect=false;
            return image;
        }
        private static void Stretch(RectTransform rect,float inset)
        {
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
            rect.offsetMin=Vector2.one*inset;rect.offsetMax=-Vector2.one*inset;
        }
        private static void Anchor(RectTransform rect,float x,float y){rect.anchorMin=rect.anchorMax=new Vector2(x,y);}
        private static void PlaceLeft(RectTransform rect,float x,float y,float width,float height)
        {Anchor(rect,0,.5f);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(width,height);}
        private static void PlaceText(RectTransform rect,float left,float y,float right,float height)
        {
            rect.anchorMin=new Vector2(0,.5f);rect.anchorMax=new Vector2(1,.5f);rect.pivot=new Vector2(.5f,.5f);
            rect.offsetMin=new Vector2(left,y-height*.5f);rect.offsetMax=new Vector2(-right,y+height*.5f);
        }

        public static void CreatePortraits()
        {
            Directory.CreateDirectory(portraitFolder);Directory.CreateDirectory(reportFolder);
            var roster=AssetDatabase.LoadAssetAtPath<Data_CartoonRaceRoster>("Assets/Resources/Tables/Data_CartoonRaceRoster.asset");
            var entries=roster.Playable.Concat(roster.Opponents).ToArray();
            string[] ids={"Poring","Minorous","Pecopeco","Lunatic","BaphometJr","Alpaca"};
            for(int i=0;i<entries.Length;i++)
            {
                var root=Object.Instantiate(entries[i].Prefab);
                var cameraObject=new GameObject("Camera_RaceFaceCapture");
                var camera=cameraObject.AddComponent<Camera>();
                var target=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32);
                var image=new Texture2D(512,512,TextureFormat.RGBA32,false);
                var previous=RenderTexture.active;
                try
                {
                    root.SetActive(true);
                    root.transform.position=Vector3.zero;
                    foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
                    var view=root.GetComponent<RaceRunnerView>();
                    if(view.IdleClip!=null&&view.MountAnimator!=null)view.IdleClip.SampleAnimation(view.MountAnimator.gameObject,.15f);
                    var renderers=root.GetComponentsInChildren<Renderer>().Where(r=>r.enabled&&!(r is ParticleSystemRenderer)).ToArray();
                    var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                    Vector3 focus=bounds.center;
                    float size=bounds.extents.y;
                    if(i==0){size=Mathf.Max(bounds.extents.x,bounds.extents.y)*1.14f;}
                    else if(i==3){focus.y=bounds.min.y+bounds.size.y*.58f;size=bounds.size.y*.48f;}
                    else
                    {
                        focus=view.MonsterFaceTarget!=null?view.MonsterFaceTarget.position:bounds.center+Vector3.up*bounds.extents.y*.45f;
                        size=bounds.size.y*(i==1?.23f:i==5?.25f:.31f);
                        focus.y-=bounds.size.y*.025f;
                        if(i>=4)
                        {
                            var socket=root.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="socket_head_mid");
                            if(socket!=null)focus=socket.position;
                        }
                    }
                    camera.orthographic=true;camera.orthographicSize=size;
                    camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
                    camera.transform.position=focus+new Vector3(0,bounds.size.y*.05f,Mathf.Max(20,bounds.size.z*4));
                    camera.transform.LookAt(focus);camera.targetTexture=target;
                    camera.Render();RenderTexture.active=target;
                    image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();
                    var bytes=image.EncodeToPNG();
                    string path=portraitFolder+"/Face_"+ids[i]+".png";
                    File.WriteAllBytes(path,bytes);File.WriteAllBytes(reportFolder+"/"+entries[i].Name+"_얼굴원본.png",bytes);
                    AssetDatabase.ImportAsset(path);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType=TextureImporterType.Sprite;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
                    importer.maxTextureSize=512;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
                    entries[i].FacePortrait=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
                finally
                {
                    RenderTexture.active=previous;camera.targetTexture=null;target.Release();
                    Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(root);Object.DestroyImmediate(target);Object.DestroyImmediate(image);
                }
            }
            EditorUtility.SetDirty(roster);
        }
    }
}
