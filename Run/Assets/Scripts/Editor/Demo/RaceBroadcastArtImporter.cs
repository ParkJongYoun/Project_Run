using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectRun.Race.Editor
{
    // Imports authored PNG pixels; this does not paint, rasterize, or generate UI artwork.
    public static class RaceBroadcastArtImporter
    {
        private const string Folder = "Assets/ArtBundles/UI/RaceBroadcast/";
        public static Sprite Frame { get; private set; }
        public static Sprite Background { get; private set; }
        public static Sprite ListIcon { get; private set; }

        public static void Import()
        {
            Frame=ImportSprite("SkillFrame",new Rect(0,724-560,2172,442),new Vector4(220,55,220,55),36);
            Background=ImportSprite("SkillBackground",new Rect(28,724-534,2120,382),new Vector4(200,50,200,50),36);
            ListIcon=ImportSprite("StandingsIcon",new Rect(144,1254-1012,968,726),Vector4.zero,26);
        }

        private static Sprite ImportSprite(string name,Rect rect,Vector4 border,float displayHeight)
        {
            string path=Folder+name+".png";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            if(importer==null)throw new InvalidOperationException("Missing authored UI image: "+path);
            importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Multiple;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.maxTextureSize=4096;importer.npotScale=TextureImporterNPOTScale.None;
            importer.spritePixelsPerUnit=rect.height/displayHeight*100;
            importer.filterMode=FilterMode.Bilinear;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
#pragma warning disable 618
            importer.spritesheet=new[]{new SpriteMetaData{name=name,rect=rect,border=border,pivot=new Vector2(.5f,.5f),alignment=9}};
#pragma warning restore 618
            importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single();
        }
    }
}
