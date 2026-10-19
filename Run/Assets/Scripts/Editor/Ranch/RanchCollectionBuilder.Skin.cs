using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ProjectRun.Race;

namespace ProjectRun.Editor
{
    public static partial class RanchCollectionBuilder
    {
        private const string referenceRoot = artRoot + "/ReferenceMatch/";

        // Refresh only these assets: never save unrelated edits in the open lobby scene.
        [MenuItem("Tools/Project Run/Ranch/Refresh reference collection panels")]
        public static void RefreshReferencePanels()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            art = ImportArt();
            BuildPrefab(false); BuildPrefab(true);
            AssetDatabase.SaveAssets();
        }

        private static void AddReferenceArt(List<Data_DemoSprite> list)
        {
            PutArt(list,"village",ImportReference("MonsterVillage.png"));
            PutArt(list,"training_village",ImportReference("TrainingVillage.png"));
            var poring = ImportRegions("PoringIllustration.png", new[] {
                Region("Poring_body",20,136,1220,984), Region("Poring_face",20,136,1220,984) });
            var peco = ImportRegions("PecopecoIllustration.png",new[] {
                Region("Pecopeco_body",128,0,1060,1254), Region("Pecopeco_face",245,0,560,560) });
            foreach(var sprite in poring.Concat(peco)) PutArt(list,sprite.name,sprite);
            PutArt(list,"icon_speed",ImportRegions("SpeedWing.png",new[]{Region("SpeedWing",140,96,1104,1048)})[0]);
            foreach(var sprite in ImportRegions("WoodTitleFrame.png",new[] {
                new SpriteMetaData{name="title_post_left",rect=new Rect(18,230,164,364),alignment=9,pivot=new Vector2(.5f,.5f)},
                new SpriteMetaData{name="title_post_right",rect=new Rect(1844,230,176,364),alignment=9,pivot=new Vector2(.5f,.5f)} })) PutArt(list,sprite.name,sprite);
            // Rounded shapes are authored as UI geometry, not stretched painted ovals.
            PutArt(list,"ui_paper",Shape("Paper",128,96,12,Hex("FFF8E7"),Hex("F8E7C6"),Color.clear,0,false));
            PutArt(list,"ui_frame",Shape("PaperFrame",128,96,12,Color.clear,Color.clear,Hex("C9A36D"),2,true));
            PutArt(list,"ui_preview",Shape("PreviewFrame",128,96,10,Color.clear,Color.clear,Hex("F9E9BB"),4,true));
            PutArt(list,"ui_selected",Shape("SelectedFrame",128,96,12,Color.clear,Color.clear,Hex("67F500"),5,true));
            PutArt(list,"ui_plate",Shape("NameStrip",128,64,6,Hex("634326"),Hex("36251B"),Color.clear,0,false));
            PutArt(list,"ui_gauge",Shape("GaugeTrack",128,32,15,Hex("E4CEAC"),Hex("EBD7B7"),Hex("CDB491"),1.2f,false));
            string[] keys={"speed","stamina","strength","intelligence","spirit"};
            string[] tops={"14C7FF","FF7572","FFBE41","D978FF","63D65A"};
            string[] bottoms={"0095F3","FF2B32","FF8A18","B246F0","32B543"};
            for(int i=0;i<keys.Length;i++) PutArt(list,"ui_fill_"+keys[i],Shape("Gauge_"+keys[i],128,32,15,Hex(tops[i]),Hex(bottoms[i]),Color.white,1,false));
            PutArt(list,"ui_rail",Shape("ScrollRail",64,64,13,Hex("503018"),Hex("70451D"),Hex("C99855"),4,true));
            PutArt(list,"ui_thumb",Shape("ScrollThumb",64,64,12,Hex("FFD178"),Hex("E59825"),Hex("9E5D19"),3,true));
            PutArt(list,"ui_title",Shape("TitleRed",128,64,16,Hex("BA4820"),Hex("8E2C10"),Hex("D28332"),4,true));
            PutArt(list,"ui_blue",Shape("BlueHeading",128,64,12,Hex("236E9F"),Hex("174F7C"),Hex("93BDD5"),2,true));
            PutArt(list,"ui_red",Shape("RedHeading",128,64,12,Hex("CE4A50"),Hex("9C242E"),Hex("EFAD94"),2,true));
            PutArt(list,"ui_brown",Shape("BrownHeading",128,64,12,Hex("906031"),Hex("654019"),Hex("CAAD77"),2,true));
            PutArt(list,"ui_shadow",ShadowSprite());
            PutArt(list,"ui_corner",Shape("CornerRing",20,20,8,Color.clear,Color.clear,Hex("D2B483"),2,false));
            PutArt(list,"ui_rivet",Shape("Rivet",20,20,8,Hex("E6B563"),Hex("90521E"),Hex("6C3614"),2,true));
            PutArt(list,"reference_train_bg",Shape("TrainSurface",256,96,20,Hex("15B51A"),Hex("087F13"),Color.clear,0,false));
            PutArt(list,"reference_back_bg",Shape("BackSurface",192,80,30,Hex("803B0E"),Hex("421E08"),Color.clear,0,false));
            foreach(string button in new[]{"TrainFrame","BackFrame","TrainWing"})
                PutArt(list,"reference_"+button,ImportTrimmed("Assets/ArtBundles/UI/RanchCollection/ReferenceButtons/"+button+".png"));
            foreach(var monster in Mgr_Table.Demo.Monsters)
            {
                string path="Assets/ArtBundles/UI/RanchCollection/TrainingIllustrations/"+monster.Id;
                PutArt(list,monster.Id+"_training",ImportTrainingImage(path+"_Display.png"));
                PutArt(list,monster.Id+"_training_start",ImportTrainingImage(path+"_Start.png"));
            }
        }

        private static Sprite ImportTrainingImage(string path)
        {
            if(!File.Exists(path))return null;
            var importer=ConfigureImporter(path);importer.spriteImportMode=SpriteImportMode.Single;
            importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Change sprite bounds only; retain the original generated PNG and its alpha unchanged.
        private static Sprite ImportTrimmed(string path)
        {
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);texture.LoadImage(File.ReadAllBytes(path));
            var pixels=texture.GetPixels32();int left=texture.width,right=0,bottom=texture.height,top=0;
            for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)
                if(pixels[y*texture.width+x].a>24){left=Mathf.Min(left,x);right=Mathf.Max(right,x);bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y);}
            var rect=new Rect(Mathf.Max(0,left-2),Mathf.Max(0,bottom-2),Mathf.Min(texture.width-left,right-left+5),Mathf.Min(texture.height-bottom,top-bottom+5));
            UnityEngine.Object.DestroyImmediate(texture);
            var importer=ConfigureImporter(path);importer.maxTextureSize=4096;importer.spriteImportMode=SpriteImportMode.Multiple;
            #pragma warning disable 618
            importer.spritesheet=new[]{new SpriteMetaData{name=Path.GetFileNameWithoutExtension(path),rect=rect,alignment=9,pivot=new Vector2(.5f,.5f)}};
            #pragma warning restore 618
            importer.SaveAndReimport();return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single();
        }

        private static Color Hex(string value) { Color color; ColorUtility.TryParseHtmlString("#"+value,out color);return color; }
        private static void PutArt(List<Data_DemoSprite> list,string key,Sprite sprite)
        { list.RemoveAll(s=>s.Key==key);list.Add(new Data_DemoSprite{Key=key,Sprite=sprite}); }
        private static SpriteMetaData Region(string name,int x,int top,int width,int height)
        { return new SpriteMetaData{name=name,rect=new Rect(x,1254-top-height,width,height),alignment=9,pivot=new Vector2(.5f,.5f)}; }

        private static TextureImporter ConfigureImporter(string path)
        {
            AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.alphaIsTransparency=true;
            importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;
            importer.spritePixelsPerUnit=100;importer.filterMode=FilterMode.Bilinear;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            return importer;
        }
        private static Sprite ImportReference(string file,Vector4 border=default)
        {
            string path=referenceRoot+file;var importer=ConfigureImporter(path);
            importer.spriteImportMode=SpriteImportMode.Single;importer.spriteBorder=border;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static Sprite[] ImportRegions(string file,SpriteMetaData[] regions)
        {
            string path=referenceRoot+file;var importer=ConfigureImporter(path);importer.spriteImportMode=SpriteImportMode.Multiple;
            #pragma warning disable 618
            importer.spritesheet=regions;
            #pragma warning restore 618
            importer.SaveAndReimport();return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        }

        private static Sprite Shape(string name,int width,int height,float radius,Color top,Color bottom,Color edge,float stroke,bool highlight)
        {
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);
            var pixels=new Color[width*height];
            for(int y=0;y<height;y++) for(int x=0;x<width;x++)
            {
                var q=new Vector2(Mathf.Abs(x+.5f-width*.5f)-width*.5f+radius+1,Mathf.Abs(y+.5f-height*.5f)-height*.5f+radius+1);
                float distance=new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-radius;
                var color=Color.Lerp(bottom,top,(float)y/(height-1));
                if(distance>-stroke) color=edge;
                if(highlight && distance<=-stroke && distance>-stroke-1.5f) color=Hex("FFF3C6");
                color.a*=Mathf.Clamp01(.5f-distance);pixels[y*width+x]=color;
            }
            texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(referenceRoot+name+".png",texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            float border=radius+3;return ImportReference(name+".png",new Vector4(border,border,border,border));
        }
        private static Sprite ShadowSprite()
        {
            var texture=new Texture2D(128,64,TextureFormat.RGBA32,false);var pixels=new Color[128*64];
            for(int y=0;y<64;y++) for(int x=0;x<128;x++)
            {float d=new Vector2((x-63.5f)/64,(y-31.5f)/32).sqrMagnitude;pixels[y*128+x]=new Color(.25f,.12f,.05f,Mathf.Pow(Mathf.Clamp01(1-d),2)*.4f);}
            texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(referenceRoot+"GroundShadow.png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            return ImportReference("GroundShadow.png");
        }
    }
}
