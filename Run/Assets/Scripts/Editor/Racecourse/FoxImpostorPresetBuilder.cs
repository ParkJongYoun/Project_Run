using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectRun.Race;
using Object=UnityEngine.Object;

namespace ProjectRun.Editor
{
    public static class FoxImpostorPresetBuilder
    {
        private const string art=FoxDirectionalImpostorBuilder.Art+"/Variants";
        private const string resources="Assets/Resources/FoxImpostor/Variants";
        private static readonly int[] azimuths={16,8,8,8,4};
        private static readonly int[] elevations={9,9,5,3,3};

        [MenuItem("Project Run/Fox Impostor/Build reduced view presets")]
        public static void Build()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building presets.");
            var test=Object.FindFirstObjectByType<Sys_FoxImpostorTest>();
            if(test==null || test.gameObject.scene.path!=FoxDirectionalImpostorBuilder.ScenePath)
                throw new InvalidOperationException("Open Scene_FoxImpostorTest first.");
            var source=AssetDatabase.LoadAssetAtPath<Data_FoxDirectionalImpostor>(FoxDirectionalImpostorBuilder.DataPath);
            if(source==null || source.ColorAtlas==null || source.DepthAtlas==null) throw new InvalidOperationException("Bake the 144-view source first.");
            if(source.AzimuthCount!=16 || source.ElevationCount!=9) throw new InvalidOperationException("This subset comparison requires a 16 x 9 source grid.");
            Directory.CreateDirectory(art);Directory.CreateDirectory(resources);
            var report=new StringBuilder("views,azimuth,elevation,tilePixels,atlasWidth,atlasHeight,rgba32ColorAndDepthMiB\n");
            BuildAtlases(source,false);BuildAtlases(source,true);
            AssetDatabase.Refresh();
            var presets=new Data_FoxDirectionalImpostor[azimuths.Length];presets[0]=source;
            for(int i=0;i<azimuths.Length;i++)
            {
                int count=azimuths[i]*elevations[i];
                report.AppendLine($"{count},{azimuths[i]},{elevations[i]},{source.TileSize},{azimuths[i]*source.TileSize},{elevations[i]*source.TileSize},{count*source.TileSize*source.TileSize*8.0/1048576:F1}");
                if(i==0) continue;
                string prefix=art+"/Fox_"+count;
                Import(prefix+"_Color.png",false);Import(prefix+"_Depth.png",true);
                string materialPath=prefix+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(material==null) {material=new Material(source.Material.shader);AssetDatabase.CreateAsset(material,materialPath);}
                var color=AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+"_Color.png");
                var depth=AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+"_Depth.png");
                material.SetTexture("_ColorAtlas",color);material.SetTexture("_DepthAtlas",depth);
                string path=resources+"/FoxImpostor_"+count+".asset";
                var preset=AssetDatabase.LoadAssetAtPath<Data_FoxDirectionalImpostor>(path);
                if(preset==null) {preset=ScriptableObject.CreateInstance<Data_FoxDirectionalImpostor>();AssetDatabase.CreateAsset(preset,path);}
                preset.ColorAtlas=color;preset.DepthAtlas=depth;preset.Material=material;
                preset.Center=source.Center;preset.Radius=source.Radius;preset.TileSize=source.TileSize;
                preset.AzimuthCount=azimuths[i];preset.ElevationCount=elevations[i];
                preset.MinimumElevation=source.MinimumElevation;preset.MaximumElevation=source.MaximumElevation;
                preset.PoseDescription=source.PoseDescription+" / exact spatial subset of the 144-view capture";
                EditorUtility.SetDirty(material);EditorUtility.SetDirty(preset);presets[i]=preset;
            }
            test.Presets=presets;
            var panel=Object.FindFirstObjectByType<UIPanel_FoxImpostorTest>();
            FoxDirectionalImpostorBuilder.InstallPresetControls(panel);
            test.AutoOrbit=false;test.SelectPreset(2);
            EditorUtility.SetDirty(test);EditorUtility.SetDirty(test.Impostor);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(test.gameObject.scene);EditorSceneManager.SaveScene(test.gameObject.scene);
            string output=FoxDirectionalImpostorBuilder.Output+"/Review/ReducedViews";Directory.CreateDirectory(output);
            File.WriteAllText(output+"/presets.csv",report.ToString());
            AddUpperHemisphere16();
            Debug.Log("Installed actual 144 / 72 / 40 / 24 / 16 / 12 view atlases. Same frozen pose and 384-pixel tiles; default comparison = 16 upper-hemisphere views.");
        }

        [MenuItem("Project Run/Fox Impostor/Add 16-view upper hemisphere")]
        public static void AddUpperHemisphere16()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            var test=Object.FindFirstObjectByType<Sys_FoxImpostorTest>();
            if(test==null || test.gameObject.scene.path!=FoxDirectionalImpostorBuilder.ScenePath)
                throw new InvalidOperationException("Open Scene_FoxImpostorTest first.");
            var source=AssetDatabase.LoadAssetAtPath<Data_FoxDirectionalImpostor>(resources+"/FoxImpostor_24.asset");
            if(source==null || source.AzimuthCount!=8 || source.ElevationCount!=3)
                throw new InvalidOperationException("The 8 x 3 / 24-view preset is required.");
            Directory.CreateDirectory(art);
            string prefix=art+"/Fox_16";
            CropUpperRows(source,false,prefix+"_Color.png");CropUpperRows(source,true,prefix+"_Depth.png");
            Import(prefix+"_Color.png",false);Import(prefix+"_Depth.png",true);
            var color=AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+"_Color.png");
            var depth=AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+"_Depth.png");
            var material=AssetDatabase.LoadAssetAtPath<Material>(prefix+".mat");
            if(material==null) {material=new Material(source.Material.shader);AssetDatabase.CreateAsset(material,prefix+".mat");}
            material.SetTexture("_ColorAtlas",color);material.SetTexture("_DepthAtlas",depth);
            string path=resources+"/FoxImpostor_16.asset";
            var preset=AssetDatabase.LoadAssetAtPath<Data_FoxDirectionalImpostor>(path);
            if(preset==null) {preset=ScriptableObject.CreateInstance<Data_FoxDirectionalImpostor>();AssetDatabase.CreateAsset(preset,path);}
            preset.ColorAtlas=color;preset.DepthAtlas=depth;preset.Material=material;
            preset.Center=source.Center;preset.Radius=source.Radius;preset.TileSize=source.TileSize;
            preset.AzimuthCount=8;preset.ElevationCount=2;preset.MinimumElevation=0;preset.MaximumElevation=90;
            preset.PoseDescription=source.PoseDescription+" / bottom (underside) row removed; captured elevation 0 to +90 degrees";
            EditorUtility.SetDirty(material);EditorUtility.SetDirty(preset);
            var counts=new[]{144,72,40,24,16,12};var presets=new Data_FoxDirectionalImpostor[counts.Length];
            for(int i=0;i<counts.Length;i++)
            {
                presets[i]=counts[i]==144?AssetDatabase.LoadAssetAtPath<Data_FoxDirectionalImpostor>(FoxDirectionalImpostorBuilder.DataPath)
                    :AssetDatabase.LoadAssetAtPath<Data_FoxDirectionalImpostor>(resources+"/FoxImpostor_"+counts[i]+".asset");
                if(presets[i]==null) throw new InvalidOperationException("Missing preset: "+counts[i]);
                if(counts[i]!=16) {presets[i].MinimumElevation=-90;presets[i].MaximumElevation=90;EditorUtility.SetDirty(presets[i]);}
            }
            test.Presets=presets;test.AutoOrbit=false;test.SelectPreset(4);
            FoxDirectionalImpostorBuilder.InstallPresetControls(Object.FindFirstObjectByType<UIPanel_FoxImpostorTest>());
            EditorUtility.SetDirty(test);EditorUtility.SetDirty(test.Impostor);AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(test.gameObject.scene);EditorSceneManager.SaveScene(test.gameObject.scene);
            Debug.Log("Added 16-view upper hemisphere: exact top two rows of Fox_24 color and depth. Camera remains free below zero to inspect failure outside the captured range.");
        }

        private static void CropUpperRows(Data_FoxDirectionalImpostor source,bool depth,string output)
        {
            var image=new Texture2D(2,2,TextureFormat.RGBA32,false,depth);
            Texture2D cropped=null;
            try
            {
                image.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(depth?source.DepthAtlas:source.ColorAtlas)));
                int width=source.AzimuthCount*source.TileSize,height=source.TileSize*2;
                if(image.width!=width || image.height!=source.TileSize*3) throw new InvalidDataException("Unexpected 24-view atlas size.");
                var pixels=new Color32[width*height];
                // Texture pixels start at the bottom: discard y=0..383, keep the horizon and top-view rows exactly.
                Array.Copy(image.GetPixels32(),width*source.TileSize,pixels,0,pixels.Length);
                cropped=new Texture2D(width,height,TextureFormat.RGBA32,false,depth);cropped.SetPixels32(pixels);cropped.Apply();
                File.WriteAllBytes(output,cropped.EncodeToPNG());
            }
            finally {Object.DestroyImmediate(image);if(cropped!=null) Object.DestroyImmediate(cropped);}
        }

        private static void BuildAtlases(Data_FoxDirectionalImpostor source,bool depth)
        {
            string sourcePath=AssetDatabase.GetAssetPath(depth?source.DepthAtlas:source.ColorAtlas);
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,depth);
            Color32[] sourcePixels;
            int sourceWidth;
            try
            {
                if(!texture.LoadImage(File.ReadAllBytes(sourcePath))) throw new InvalidDataException(sourcePath);
                sourceWidth=texture.width;sourcePixels=texture.GetPixels32();
            }
            finally {Object.DestroyImmediate(texture);}
            int tile=source.TileSize;
            for(int variant=1;variant<azimuths.Length;variant++)
            {
                int columns=azimuths[variant],rows=elevations[variant],width=columns*tile,height=rows*tile;
                var pixels=new Color32[width*height];
                for(int row=0;row<rows;row++) for(int column=0;column<columns;column++)
                {
                    int sourceColumn=column*source.AzimuthCount/columns;
                    int sourceRow=row*(source.ElevationCount-1)/(rows-1);
                    for(int y=0;y<tile;y++)
                        Array.Copy(sourcePixels,(sourceRow*tile+y)*sourceWidth+sourceColumn*tile,pixels,(row*tile+y)*width+column*tile,tile);
                }
                var atlas=new Texture2D(width,height,TextureFormat.RGBA32,false,depth);
                try
                {
                    atlas.SetPixels32(pixels);atlas.Apply();
                    File.WriteAllBytes(art+"/Fox_"+(columns*rows)+(depth?"_Depth.png":"_Color.png"),atlas.EncodeToPNG());
                }
                finally {Object.DestroyImmediate(atlas);}
            }
        }

        private static void Import(string path,bool depth)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=!depth;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=!depth;
            importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=depth?FilterMode.Point:FilterMode.Bilinear;
            importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=8192;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        }
    }
}
