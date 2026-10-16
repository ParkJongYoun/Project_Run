using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using ProjectRun.Race;
using Object=UnityEngine.Object;

namespace ProjectRun.Editor
{
    public static class FoxDirectionalImpostorValidation
    {
        public static string Capture(float yaw,float elevation,string label,string outputGroup=null)
        {
            var test=Object.FindFirstObjectByType<Sys_FoxImpostorTest>();
            if(test==null) throw new InvalidOperationException("Open Scene_FoxImpostorTest.");
            var camera=test.OrbitCamera;
            float oldYaw=test.Yaw,oldElevation=test.Elevation,oldDistance=test.DistanceRatio;
            bool oldOriginal=test.ShowOriginal,oldAuto=test.AutoOrbit;
            var oldRect=camera.rect; var oldBackground=camera.backgroundColor;
            var grid=GameObject.Find("obj_floor_grid");
            string output=FoxDirectionalImpostorBuilder.Output+"/Review";
            if(!string.IsNullOrEmpty(outputGroup)) output=Path.Combine(output,outputGroup);
            Directory.CreateDirectory(output);
            Texture2D original=null,impostor=null,pair=null;
            try
            {
                test.AutoOrbit=false;test.Yaw=yaw;test.Elevation=elevation;test.DistanceRatio=3.2f;
                if(grid!=null) grid.SetActive(false);
                camera.rect=new Rect(0,0,1,1);camera.aspect=1;camera.backgroundColor=Color.clear;
                test.ShowOriginal=true;test.ApplyView();
                Object.DestroyImmediate(FoxSpriteCapture.Render(camera,512,512));
                original=FoxSpriteCapture.Render(camera,512,512);
                test.ShowOriginal=false;test.ApplyView();
                Object.DestroyImmediate(FoxSpriteCapture.Render(camera,512,512));
                impostor=FoxSpriteCapture.Render(camera,512,512);
                var a=original.GetPixels32();var b=impostor.GetPixels32();
                int intersection=0,union=0,originalCount=0,impostorCount=0;double difference=0;
                for(int i=0;i<a.Length;i++)
                {
                    bool left=a[i].a>127,right=b[i].a>127;
                    if(left) originalCount++;if(right) impostorCount++;
                    if(left||right) union++;
                    if(left&&right) {intersection++;difference+=Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b);}
                }
                pair=new Texture2D(1024,512,TextureFormat.RGBA32,false);
                pair.SetPixels32(0,0,512,512,a);pair.SetPixels32(512,0,512,512,b);pair.Apply();
                File.WriteAllBytes(output+"/"+label+"_3D_vs_2D.png",pair.EncodeToPNG());
                var report=$"{label},{yaw},{elevation},{originalCount},{impostorCount},{(double)intersection/Math.Max(1,union):F4},{difference/Math.Max(1,intersection)/765:F4}";
                string path=output+"/comparison.csv";
                if(!File.Exists(path)) File.WriteAllText(path,"view,yaw,elevation,originalPixels,impostorPixels,silhouetteIoU,meanColorError\n");
                File.AppendAllText(path,report+"\n");return report;
            }
            finally
            {
                if(original!=null) Object.DestroyImmediate(original);if(impostor!=null) Object.DestroyImmediate(impostor);if(pair!=null) Object.DestroyImmediate(pair);
                camera.rect=oldRect;camera.backgroundColor=oldBackground;camera.ResetAspect();
                if(grid!=null) grid.SetActive(true);
                test.Yaw=oldYaw;test.Elevation=oldElevation;test.DistanceRatio=oldDistance;test.ShowOriginal=oldOriginal;test.AutoOrbit=oldAuto;test.ApplyView();
            }
        }
    }
}
