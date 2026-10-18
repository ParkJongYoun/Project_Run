using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace ProjectRun.Race.Editor
{
    public static class DemoBuilder
    {
        private const string Report="DataBundles/기획/심사데모_제작자료";
        [Serializable] private class ArtManifest { public ArtEntry[] Images; }
        [Serializable] private class ArtEntry { public string Key; public string Path; }
        [Serializable] private class ClipManifest { public string SourceVideoSha256; public float[] IllustrationRect; public int[] CellSize; public ClipPage[] Pages; public ClipFrame[] Frames; }
        [Serializable] private class ClipPage { public string File; public int Width; public int Height; }
        [Serializable] private class ClipFrame { public int Index;public int Page;public int X;public int Y;public float Duration; }
        private static readonly string[] lobbyFolders={"WildRose_Modular_v01","Minorous_Modular_v01","Matyr_Modular_v01"};
        private static readonly string[] lobbyIds={"WildRose","Minorous","Matyr"};
        [MenuItem("Tools/Project Run/Demo/1 Import UI Art")]
        public static void ImportArt()
        {
            AssetDatabase.Refresh();
            var manifest=JsonUtility.FromJson<ArtManifest>(File.ReadAllText(Report+"/아트연결목록.json"));
            var list=new List<Data_DemoSprite>();
            foreach(var entry in manifest.Images)
            {
                AssetDatabase.ImportAsset(entry.Path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(entry.Path);Configure(importer,2048);
                importer.spriteImportMode=SpriteImportMode.Single;
                bool sliced=entry.Key.EndsWith("_frame")||entry.Key.EndsWith("_bg")||entry.Key=="card_selected";
                // Complete illustrations and book paper must retain their original composition.
                if(entry.Key=="training_bg"||entry.Key=="book_bg")sliced=false;
                if(sliced)
                {
                    importer.GetSourceTextureWidthAndHeight(out int width,out int height);
                    int border=Mathf.Min(40,Mathf.Min(width,height)/4);
                    importer.spriteBorder=new Vector4(border,border,border,border);
                }
                importer.SaveAndReimport();list.Add(new Data_DemoSprite{Key=entry.Key,Sprite=AssetDatabase.LoadAssetAtPath<Sprite>(entry.Path)});
            }
            Directory.CreateDirectory("Assets/Resources/UI/Demo");AssetDatabase.Refresh();
            string path="Assets/Resources/UI/Demo/Data_DemoArt.asset";
            var art=AssetDatabase.LoadAssetAtPath<Data_DemoArt>(path);
            if(art==null){art=ScriptableObject.CreateInstance<Data_DemoArt>();AssetDatabase.CreateAsset(art,path);}
            art.BodyFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/ArtBundles/UI/RaceFantasy/Fonts/NanumGothic-Bold.ttf");
            art.TitleFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/ArtBundles/UI/SGLReference/Fonts/NanumMyeongjo-Bold.ttf");
            art.Images=list.ToArray();EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();Debug.Log("DEMO_ART_OK "+list.Count);
        }
        private static void Configure(TextureImporter importer,int maximum=4096)
        {
            importer.textureType=TextureImporterType.Sprite;importer.spritePixelsPerUnit=100;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.npotScale=TextureImporterNPOTScale.None;
            importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=maximum;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.isReadable=false;
            importer.ClearPlatformTextureSettings("WebGL");importer.ClearPlatformTextureSettings("Standalone");
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
        }
        public static void ImportComics()
        {
            var art=AssetDatabase.LoadAssetAtPath<Data_DemoArt>("Assets/Resources/UI/Demo/Data_DemoArt.asset");
            var entries=art.Images.Where(x=>!x.Key.Contains("_comic_")).ToList();
            string[] ids={"Poring","Minorous","Eddga"},names={"포링","미노타우로스","에드가"};
            for(int i=0;i<3;i++)
            {
                string target="Assets/ArtBundles/UI/Demo/"+ids[i]+"_comic.png";
                File.Copy("DataBundles/아트/심사데모_스킬연출/"+names[i]+"_만화4컷.png",target,true);
                AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(target);Configure(importer,2048);
                importer.GetSourceTextureWidthAndHeight(out int width,out int height);
                importer.spriteImportMode=SpriteImportMode.Multiple;
#pragma warning disable CS0618
                importer.spritesheet=Enumerable.Range(0,4).Select(n=>new SpriteMetaData{name="cut_"+n,rect=new Rect((n%2)*width/2f+5,(1-n/2)*height/2f+5,width/2f-10,height/2f-10),alignment=(int)SpriteAlignment.Center,pivot=new Vector2(.5f,.5f)}).ToArray();
#pragma warning restore CS0618
                importer.SaveAndReimport();
                foreach(var sprite in AssetDatabase.LoadAllAssetsAtPath(target).OfType<Sprite>())entries.Add(new Data_DemoSprite{Key=ids[i]+"_comic_"+sprite.name.Substring(4),Sprite=sprite});
            }
            art.Images=entries.ToArray();EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();
        }
        public static void ImportLobby(int index)
        {
            string source="DataBundles/LobbyAnimation/"+lobbyFolders[index];string id=lobbyIds[index];
            string target="Assets/ArtBundles/Lobby/Demo/"+id;Directory.CreateDirectory(target);
            string[] keys=Directory.GetDirectories(source+"/jobs").Select(Path.GetFileName).Where(k=>k!="butler_idle"&&File.Exists(source+"/jobs/"+k+"/final/clip.json")).ToArray();
            var clips=new Dictionary<string,Data_LobbyAnimation>();
            foreach(string key in keys)clips[key]=ImportClip(source+"/jobs/"+key+"/final",target+"/"+key,"Assets/ArtBundles/Lobby/Demo/"+id+"/"+key+".asset");
            var root=new GameObject("P_Lobby_"+id,typeof(RectTransform),typeof(CanvasGroup));root.SetActive(false);
            var rect=(RectTransform)root.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var fit=root.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;fit.aspectRatio=1672f/941;
            Action<string,string> plate=(filename,name)=>
            {
                string file=source+"/"+filename;if(!File.Exists(file))return;
                string path=target+"/"+filename;File.Copy(file,path,true);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);Configure(importer);importer.spriteImportMode=SpriteImportMode.Single;importer.SaveAndReimport();
                var image=DemoUI.Fill(root.transform,name).gameObject.AddComponent<UnityEngine.UI.Image>();image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);image.raycastTarget=false;
            };
            Action<string,string> layer=(key,special)=>
            {
                if(!clips.ContainsKey(key))return;
                var image=DemoUI.Fill(root.transform,"img_"+key).gameObject.AddComponent<UnityEngine.UI.Image>();image.raycastTarget=false;
                var player=image.gameObject.AddComponent<UIItem_LobbyAnimation>();player.UseIllustrationLayout=true;
                player.ConfigureStates(clips[key],special!=null&&clips.ContainsKey(special)?clips[special]:null,3,6,special==null?.13f:0);
                image.sprite=clips[key].Frames[0];
            };
            plate("background.png","bg_scene");plate("contact_shadow.png","img_contact_shadow");
            if(index==0){layer("curtain_left_idle",null);layer("curtain_right_idle",null);ConfigureStillButler(DemoUI.Fill(root.transform,"img_butler").gameObject.AddComponent<Image>());plate("foreground.png","img_foreground");layer("maid_cat_idle","maid_cat_cheer");}
            if(index==1){layer("leaves_idle",null);layer("lantern_idle",null);layer("villager_idle",null);plate("foreground_occlusion.png","img_foreground");layer("minorous_idle","minorous_cheer");}
            if(index==2){layer("leaves_idle",null);layer("banner_idle",null);layer("runners_idle",null);layer("matyr_idle","matyr_cheer");}
            Directory.CreateDirectory("Assets/Resources/Prefabs/Lobby");root.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/Prefabs/Lobby/P_Lobby_"+id+".prefab");UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();Debug.Log("DEMO_LOBBY_OK "+id+" "+clips.Count);
        }
        public static void ConfigureStillButler(Image image)
        {
            const string path="Assets/ArtBundles/Lobby/Demo/WildRose/butler_still.png";
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(sprite==null)
            {
                File.Copy("DataBundles/아트/로비_집사_고정/집사_고정.png",path,true);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);Configure(importer,2048);
                importer.spriteImportMode=SpriteImportMode.Single;importer.SaveAndReimport();
                sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            var player=image.GetComponent<UIItem_LobbyAnimation>();
            if(player!=null)UnityEngine.Object.DestroyImmediate(player);
            image.name="img_butler";image.sprite=sprite;image.raycastTarget=false;image.preserveAspect=false;
            // Original full-frame placement: 576x1024 at 1.03125 illustration pixels per pixel.
            var rect=image.rectTransform;
            rect.anchorMin=new Vector2(310f/1672,1-954f/941);
            rect.anchorMax=new Vector2(904f/1672,1+102f/941);
            rect.offsetMin=rect.offsetMax=Vector2.zero;
        }
        public static Data_LobbyAnimation ImportClip(string source,string target,string clipPath)
        {
            var data=JsonUtility.FromJson<ClipManifest>(File.ReadAllText(source+"/clip.json"));Directory.CreateDirectory(target);
            var sprites=new Sprite[data.Frames.Length];
            for(int p=0;p<data.Pages.Length;p++)
            {
                var page=data.Pages[p];string path=target+"/"+page.File;File.Copy(source+"/"+page.File,path,true);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);Configure(importer);importer.spriteImportMode=SpriteImportMode.Multiple;
#pragma warning disable CS0618
                importer.spritesheet=data.Frames.Where(f=>f.Page==p).Select(f=>new SpriteMetaData{name="frame_"+f.Index.ToString("D4"),rect=new Rect(f.X,page.Height-f.Y-data.CellSize[1],data.CellSize[0],data.CellSize[1]),alignment=(int)SpriteAlignment.Center,pivot=new Vector2(.5f,.5f)}).ToArray();
#pragma warning restore CS0618
                importer.SaveAndReimport();var loaded=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s=>s.name);
                foreach(var frame in data.Frames.Where(f=>f.Page==p))sprites[frame.Index]=loaded["frame_"+frame.Index.ToString("D4")];
            }
            Directory.CreateDirectory(Path.GetDirectoryName(clipPath));AssetDatabase.Refresh();
            var clip=AssetDatabase.LoadAssetAtPath<Data_LobbyAnimation>(clipPath);
            if(clip==null){clip=ScriptableObject.CreateInstance<Data_LobbyAnimation>();AssetDatabase.CreateAsset(clip,clipPath);}
            clip.Frames=sprites;clip.Durations=data.Frames.Select(f=>f.Duration).ToArray();var r=data.IllustrationRect;
            clip.IllustrationRect=new Rect(r[0],r[1],r[2],r[3]);clip.SourceVideoSha256=data.SourceVideoSha256;
            EditorUtility.SetDirty(clip);AssetDatabase.SaveAssetIfDirty(clip);return clip;
        }
        [MenuItem("Tools/Project Run/Demo/3 Install Title and Lobby")]
        public static void InstallScenes()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop play mode first.");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Preserve unsaved scene edits before installing.");
            InstallScene(Define_Scene.Title,true);InstallScene(Define_Scene.Lobby,false);
            var racePanel=new GameObject("UIPanel_DemoRace",typeof(RectTransform));racePanel.SetActive(false);racePanel.AddComponent<UIPanel_DemoRace>();
            PrefabUtility.SaveAsPrefabAsset(racePanel,"Assets/Resources/Prefabs/UI/Panel/UIPanel_DemoRace.prefab");UnityEngine.Object.DestroyImmediate(racePanel);
            var scenes=EditorBuildSettings.scenes.ToList();
            foreach(string name in new[]{Define_Scene.Title,Define_Scene.Lobby,Define_Scene.OvalToon,Define_Scene.Sprite2D})
            {
                string path=Define_Scene.Path(name);int index=scenes.FindIndex(s=>s.path==path);
                if(index>=0)scenes[index]=new EditorBuildSettingsScene(path,true);else scenes.Add(new EditorBuildSettingsScene(path,true));
            }
            EditorBuildSettings.scenes=scenes.OrderBy(s=>s.path==Define_Scene.Path(Define_Scene.Title)?0:1).ToArray();
            EditorSceneManager.OpenScene(Define_Scene.Path(Define_Scene.Title));AssetDatabase.SaveAssets();Debug.Log("DEMO_SCENES_OK");
        }
        private static void InstallScene(string name,bool title)
        {
            var scene=EditorSceneManager.OpenScene(Define_Scene.Path(name));
            // The user requested replacement of these two screens. Keep cameras, lighting and managers.
            foreach(var root in scene.GetRootGameObjects())if(root.name=="UICanvas_Root")UnityEngine.Object.DestroyImmediate(root);
            var ui=new GameObject("UICanvas_Root",typeof(RectTransform));
            var canvas=DemoUI.Canvas(ui.transform,"Canvas_Panel",(int)E_CanvasLayer.Panel);
            var panel=DemoUI.Fill(canvas.transform,title?"UIPanel_DemoTitle":"UIPanel_DemoLobby");panel.gameObject.SetActive(false);
            if(title)panel.gameObject.AddComponent<UIPanel_DemoTitle>().Build();else panel.gameObject.AddComponent<UIPanel_DemoLobby>().Build();
            string path="Assets/Resources/Prefabs/UI/Panel/"+(title?"UIPanel_DemoTitle":"UIPanel_DemoLobby")+".prefab";
            PrefabUtility.SaveAsPrefabAsset(panel.gameObject,path);
            // Save the enabled scene instance. Runtime OnOpen reconnects non-persistent callbacks.
            panel.gameObject.SetActive(true);
            if(UnityEngine.Object.FindFirstObjectByType<EventSystem>()==null)new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            var managers=GameObject.Find("[Managers]")??new GameObject("[Managers]");if(managers.GetComponent<Mgr_UI>()==null)managers.AddComponent<Mgr_UI>();
            if(GameObject.Find("[Systems]")==null)new GameObject("[Systems]");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
    }
}
