using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ProjectRun.Race;
using UnityEngine;
using UnityEditor;

namespace ProjectRun.Editor
{
    public static class PronteraWorldRaceValidation
    {
        public static string Validate()
        {
            var race=UnityEngine.Object.FindFirstObjectByType<Sys_Race>();
            var course=race.Course;
            if(course==null || !course.IsValid)throw new Exception("Missing race course.");
            var issues=new List<string>();
            if(Vector3.Distance(course.Points[0],course.Points[course.Points.Length-1])>.001f)issues.Add("Open loop");
            for(int i=1;i<course.Distances.Length;i++)if(course.Distances[i]<=course.Distances[i-1])issues.Add("Non-monotonic distance at "+i);
            if(Vector3.Dot(course.Forward(.05f),course.Forward(course.Length-.05f))<.999f)issues.Add("Tangent discontinuity");
            float metricMin=float.MaxValue,metricMax=0;
            for(float s=0;s<course.Length;s+=1)
            foreach(var entry in race.Entrants)
            {
                float m=course.Metric(s,entry.Lane);
                metricMin=Mathf.Min(metricMin,m);metricMax=Mathf.Max(metricMax,m);
                if(float.IsNaN(m)||m<.35f||m>1.8f)issues.Add("Unsafe lane curvature at "+s);
                if(Mathf.Abs(entry.Lane)+.8f>course.RoadWidth/2)issues.Add("Runner lane outside road");
            }
            // Non-adjacent parts of the closed road must not cross or overlap the driving surface.
            float separatedMin=float.MaxValue;
            for(float a=0;a<course.Length;a+=5)
            for(float b=a+45;b<course.Length;b+=5)
            {
                if(course.Length-b+a<45)continue;
                separatedMin=Mathf.Min(separatedMin,Vector3.Distance(course.Center(a),course.Center(b)));
            }
            if(separatedMin<course.RoadWidth)issues.Add("Overlapping road sections");
            var uninterrupted=new Data_RaceSimulation(race.Entrants,course);
            var batched=new Data_RaceSimulation(race.Entrants,course);
            uninterrupted.Begin();batched.Begin();
            int steps=0;
            while(uninterrupted.Phase!=E_RacePhase.Result && steps<18000){uninterrupted.Step();steps++;}
            for(int i=0;i<steps;){int batch=Math.Min(137,steps-i);for(int j=0;j<batch;j++)batched.Step();i+=batch;}
            if(uninterrupted.Phase!=E_RacePhase.Result || uninterrupted.Runners.Any(r=>!r.Finished))issues.Add("Race did not finish");
            for(int i=0;i<uninterrupted.Runners.Length;i++)
                if(uninterrupted.Runners[i].FinishTime!=batched.Runners[i].FinishTime || uninterrupted.Runners[i].Rank!=batched.Runners[i].Rank)issues.Add("Step batching changed outcome");
            var legacy=new Data_RaceSimulation(race.Entrants);
            if(legacy.CourseLength!=Data_RaceSimulation.Length || legacy.Point(123,4)!=Data_RaceSimulation.CoursePoint(123,4))issues.Add("Legacy oval changed");
            var renderers=GameObject.Find("P_Racecourse_PronteraWorld").GetComponentsInChildren<Renderer>();
            int missing=renderers.Count(r=>r.sharedMaterials.Any(m=>m==null||m.shader==null||m.shader.name.Contains("InternalError")));
            if(missing>0)issues.Add("Missing material or shader: "+missing);
            long triangles=GameObject.Find("P_Racecourse_PronteraWorld").GetComponentsInChildren<MeshFilter>().Where(m=>m.sharedMesh!=null).Sum(m=>(long)m.sharedMesh.triangles.Length/3);
            string result="Scene: "+UnityEngine.SceneManagement.SceneManager.GetActiveScene().path+"\nCourse length: "+course.Length.ToString("F2")+" m\nRoad width: "+course.RoadWidth+" m\nSamples: "+course.Points.Length+
                "\nLane metric: "+metricMin.ToString("F3")+".."+metricMax.ToString("F3")+"\nNonadjacent centerline clearance: "+separatedMin.ToString("F2")+" m\nSimulation steps: "+steps+
                "\nFinish times: "+string.Join(", ",uninterrupted.Order.Select(i=>race.Entrants[i].Name+"="+uninterrupted.Runners[i].FinishTime.ToString("F3")))+
                "\nBatched stepping and legacy oval compatibility checked.\nRenderers: "+renderers.Length+"; instanced triangles: "+triangles+"\nIssues: "+(issues.Count==0?"NONE":string.Join("; ",issues.Distinct()));
            Directory.CreateDirectory("DataBundles/검증/프론테라월드");File.WriteAllText("DataBundles/검증/프론테라월드/validation.txt",result);
            if(issues.Count>0)throw new Exception(result);
            return result;
        }
    }
}
