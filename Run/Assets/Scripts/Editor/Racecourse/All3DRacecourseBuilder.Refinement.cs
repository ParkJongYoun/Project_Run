using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectRun.Editor
{
    public static partial class All3DRacecourseBuilder
    {
        private static readonly System.Collections.Generic.Dictionary<char,CharacterInfo> signGlyphs = new System.Collections.Generic.Dictionary<char,CharacterInfo>();
        private static Texture2D signAtlas;
        private static Mesh foliageMesh;

        private static void PrepareRefinedMaterials()
        {
            BakeSignGlyphs();
            PrepareFoliageMesh();
            var turf = AssetDatabase.LoadAssetAtPath<Texture2D>(artPath + "/Textures/Tex_Turf_Art.png");
            if (turf != null)
            {
                materials["TurfLight"].SetTexture("_BaseMap", turf);
                materials["TurfDark"].SetTexture("_BaseMap", turf);
                materials["Meadow"].SetTexture("_BaseMap", turf);
                materials["TurfLight"].SetColor("_BaseColor", Hex("D1E5AA"));
                materials["TurfDark"].SetColor("_BaseColor", Hex("C1D69A"));
                materials["Meadow"].SetColor("_BaseColor", Hex("B4CC97"));
            }
            Mat("Roof", "E4EEE9"); Mat("Underside", "8DAAAC");
            Mat("Stone", "CBBFA3"); Mat("GlassDeep", "235C71");
            Mat("LeafSun", "73A249"); Mat("LeafMid", "568E43"); Mat("LeafShade", "377143");
            Mat("PetalPink", "EB83AE"); Mat("PetalWhite", "FFF4DB"); Mat("PetalPurple", "9D8AC7");
            Mat("FlowerCenter", "F4C950");
            Mat("CrowdIvory", "F1EAD7"); Mat("CrowdBlue", "557CA8"); Mat("CrowdPink", "D47992");
            Mat("CrowdOrange", "DAA567"); Mat("CrowdMint", "67A599"); Mat("CrowdDark", "394D68");
            Mat("SkinLight", "EAC5A2"); Mat("SkinWarm", "BE8D66"); Mat("Hair", "3C3B38");
            var leaves=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/OldAsset/Environment/worldboss/etc/wb_bg_iys/BranchesOak001.tif");
            Mat("LeafTextured","BEDCA7",leaves,true);
            Mat("LeafTexturedShade","A5C78D",leaves,true);
            materials["Glass"].SetFloat("_Smoothness", 0.65f);
            materials["Dirt"].SetColor("_BaseColor", Hex("F4F5F3"));
            materials["GlassDeep"].SetFloat("_Smoothness", 0.7f);
            materials["Gold"].SetFloat("_Metallic", 0.5f);
            materials["Gold"].SetFloat("_Smoothness", 0.5f);
            materials["Water"].shader = Shader.Find("ProjectRun/Racecourse/GardenWater");
            materials["Water"].SetColor("_BaseColor", Hex("23878A"));
            materials["Water"].SetColor("_ShallowColor", Hex("81C9D0"));
            foreach (var material in materials.Values) EditorUtility.SetDirty(material);
        }

        private static void BuildGrandstandV2()
        {
            const string g = "10_GrandstandArchitecture";
            Geo(g,"Concrete").Box(new Vector3(0,0.6f,-113),new Vector3(186,1.2f,42));
            Geo(g,"Navy").Box(new Vector3(0,10,-128),new Vector3(181,20,3));
            Geo(g,"GlassDeep").Box(new Vector3(0,10.8f,-112.4f),new Vector3(179,3.1f,0.25f));
            Geo(g,"White").Box(new Vector3(0,9.1f,-112),new Vector3(182,0.45f,3));
            Geo(g,"White").Box(new Vector3(0,12.6f,-112),new Vector3(182,0.45f,3));
            for(int block=0;block<12;block++)
            {
                float x=-82.5f+15*block;
                for(int tier=0;tier<2;tier++)
                for(int row=0;row<(tier==0?11:8);row++)
                {
                    float y=(tier==0?1.6f:13.1f)+row*0.66f;
                    float z=(tier==0?-96.5f:-114.5f)-row*1.35f;
                    Geo(g,"Concrete").Box(new Vector3(x,y/2,z),new Vector3(13.1f,y,1.36f));
                    string seat=block%3==0?"SeatCream":block%3==1?"SeatBlue":"SeatMint";
                    for(int col=0;col<13;col++)
                    {
                        float sx=x-5.7f+col*0.95f;
                        Geo("12_Seating",seat).Box(new Vector3(sx,y+0.26f,z),new Vector3(0.7f,0.13f,0.63f));
                        Geo("12_Seating",seat).Box(new Vector3(sx,y+0.54f,z-0.28f),new Vector3(0.7f,0.5f,0.1f));
                        if(random.NextDouble()<0.68) Spectator(new Vector3(sx,y+0.32f,z),block+row+col);
                    }
                    // Broad stairs separate seating sections instead of solid blocks.
                    Geo(g,"Path").Box(new Vector3(x+7,y-0.15f,z),new Vector3(1.1f,0.3f,1.36f));
                    if(row%3==0) Geo(g,"White").Tube(new Vector3(x+7,y,z),new Vector3(x+7,y+0.9f,z),0.035f,5);
                }
                Geo(g,"White").Box(new Vector3(x+7,10.8f,-112.1f),new Vector3(0.18f,3.2f,0.3f));
                Geo(g,"White").Tube(new Vector3(x,0,-129),new Vector3(x,27,-129),0.28f,10);
                // Curved cantilever roof shell, thickness, ribs and triangular trusses.
                for(int j=0;j<20;j++)
                {
                    float t=j/20f,u=(j+1)/20f;
                    Vector3 a=new Vector3(x-7.4f,RoofHeight(t),-132+42*t);
                    Vector3 b=new Vector3(x-7.4f,RoofHeight(u),-132+42*u);
                    Vector3 c=b+Vector3.right*14.8f,d=a+Vector3.right*14.8f;
                    Geo(g,"Roof").Quad(a,b,c,d);
                    Geo(g,"Underside").Quad(d-Vector3.up*0.24f,c-Vector3.up*0.24f,b-Vector3.up*0.24f,a-Vector3.up*0.24f);
                    Geo(g,"Teal").Quad(a-Vector3.up*0.24f,b-Vector3.up*0.24f,b,a);
                    Geo(g,"Teal").Quad(d,c,c-Vector3.up*0.24f,d-Vector3.up*0.24f);
                    Geo(g,"White").Tube((a+d)*0.5f,(b+c)*0.5f,0.09f,6);
                }
                Geo(g,"Teal").Box(new Vector3(x,RoofHeight(1)-0.2f,-90),new Vector3(14.8f,0.65f,0.22f));
                Geo(g,"White").Tube(new Vector3(x,23.8f,-129),new Vector3(x,24.6f,-92),0.14f,6);
                for(int j=0;j<6;j++)
                {
                    var a=new Vector3(x,23.8f+j*0.13f,-129+j*6);
                    var b=new Vector3(x,RoofHeight((j*6+9)/42f)-0.35f,-126+j*6);
                    Geo(g,"White").Tube(a,b,0.09f,6);
                }
            }
            Geo(g,"White").Box(new Vector3(0,1.35f,-94.4f),new Vector3(184,2.7f,0.3f));
            Geo(g,"Teal").Box(new Vector3(0,1.45f,-94.2f),new Vector3(182,1.6f,0.12f));
            for(int i=-5;i<=5;i++)
                Label("Sponsor_"+i,i%2==0?"RUN PARK":"GRAND PRIX",new Vector3(i*16,1.45f,-94.08f),0.55f,Color.white,Quaternion.Euler(0,180,0));
            // Balcony rail, mullions, horizontal facade seams and end towers.
            for(int x=-88;x<=88;x+=4)
            {
                Geo(g,"White").Tube(new Vector3(x,8.8f,-111.1f),new Vector3(x,10,-111.1f),0.035f,5);
                Geo(g,"Gold").Tube(new Vector3(x,10,-111.1f),new Vector3(x+4,10,-111.1f),0.04f,6);
            }
            foreach(float x in new[]{-103f,103f})
            {
                const string tower="11_GrandstandPavilions";
                Geo(tower,"White").Box(new Vector3(x,10.6f,-114),new Vector3(20,21.2f,30));
                for(int f=0;f<5;f++)
                {
                    float y=2.7f+f*4;
                    Geo(tower,"GlassDeep").Box(new Vector3(x,y,-98.9f),new Vector3(18.4f,2.8f,0.2f));
                    Geo(tower,"Glass").Box(new Vector3(x+Mathf.Sign(x)*10.1f,y,-114),new Vector3(0.2f,2.8f,27));
                    Geo(tower,"Teal").Box(new Vector3(x,y+1.65f,-114),new Vector3(21.4f,0.28f,31));
                    for(int j=-3;j<=3;j++)Geo(tower,"White").Box(new Vector3(x+j*2.5f,y,-98.7f),new Vector3(0.12f,2.8f,0.2f));
                }
                Geo(tower,"Navy").Box(new Vector3(x,23,-114),new Vector3(16,3.6f,24));
                Geo(tower,"Roof").Box(new Vector3(x,25.2f,-114),new Vector3(23,0.5f,33));
                Geo(tower,"Gold").Box(new Vector3(x,24.7f,-97.5f),new Vector3(23,0.5f,0.25f));
                Label("PavilionTitle_"+x,"RUN PARK",new Vector3(x,23,-101.8f),1.1f,Color.white,Quaternion.Euler(0,180,0));
            }
        }

        private static float RoofHeight(float t) => 26 + 2.4f*Mathf.Sin(t*Mathf.PI*0.85f)-2*t;

        private static void Spectator(Vector3 p,int seed)
        {
            string[] shirts={"CrowdIvory","CrowdBlue","CrowdPink","CrowdOrange","CrowdMint","CrowdDark"};
            string shirt=shirts[random.Next(shirts.Length)];
            string skin=seed%4==0?"SkinWarm":"SkinLight";
            float h=0.92f+(float)random.NextDouble()*0.16f;
            Geo("13_Spectators",shirt).Ellipsoid(p+Vector3.up*0.38f,new Vector3(0.23f,h*0.36f,0.17f),6,4);
            Geo("13_Spectators",skin).Ellipsoid(p+Vector3.up*h,new Vector3(0.16f,0.2f,0.17f),6,4);
            Geo("13_Spectators","Hair").Ellipsoid(p+new Vector3(0,h+0.1f,-0.015f),new Vector3(0.166f,0.125f,0.17f),6,3);
            if(seed%4==0)
            {
                Geo("13_Spectators",shirt).Tube(p+new Vector3(-0.23f,0.52f,0),p+new Vector3(-0.4f,1.15f,0.12f),0.07f,5);
                Geo("13_Spectators",skin).Ellipsoid(p+new Vector3(-0.4f,1.2f,0.12f),Vector3.one*0.1f,5,3);
            }
        }

        private static void Bush(Vector3 p,Vector3 size,int seed,string group="25_SculptedHedges")
        {
            Geo(group,"LeafShade").Ellipsoid(p-Vector3.up*size.y*0.1f,new Vector3(size.x*0.64f,size.y*0.56f,size.z*0.64f),8,5);
            var scale=new Vector3(size.x*2/foliageMesh.bounds.size.x,size.y*2/foliageMesh.bounds.size.y,size.z*2/foliageMesh.bounds.size.z);
            var matrix=Matrix4x4.TRS(p,Quaternion.Euler(0,seed*137.5f,0),scale)*Matrix4x4.Translate(-foliageMesh.bounds.center);
            Geo(group,seed%3==0?"LeafTexturedShade":"LeafTextured").AppendMesh(foliageMesh,matrix);
        }

        private static void Flowers(Vector3 p,int count,float spread,int seed)
        {
            string[] petals={"PetalPink","PetalWhite","PetalPurple","Gold"};
            for(int i=0;i<count;i++)
            {
                float a=i*2.399f+seed,r=Mathf.Sqrt((i+0.5f)/count)*spread;
                var basePoint=p+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);
                float h=0.35f+(i%5)*0.065f;
                var head=basePoint+Vector3.up*h;
                Geo("27_FlowerGardens","LeafShade").Tube(basePoint,head,0.018f,4);
                for(int j=0;j<5;j++)
                {
                    float b=j*1.2566f;
                    Geo("27_FlowerGardens",petals[seed%4]).Ellipsoid(head+new Vector3(Mathf.Cos(b)*0.11f,0,Mathf.Sin(b)*0.11f),new Vector3(0.13f,0.055f,0.1f),5,3);
                }
                Geo("27_FlowerGardens","FlowerCenter").Ellipsoid(head+Vector3.up*0.04f,Vector3.one*0.055f,5,3);
            }
        }

        private static void Ribbon(string group,string material,Func<float,Vector3> curve,float width,int count=100)
        {
            for(int i=0;i<count;i++)
            {
                float t=i/(float)count,u=(i+1)/(float)count;
                var a=curve(t);var b=curve(u);
                var side=Vector3.Cross(Vector3.up,(b-a).normalized)*width*0.5f;
                Geo(group,material).Quad(a-side,b-side,b+side,a+side);
            }
        }

        private static void BuildGardenV2()
        {
            Ring("20_InfieldWalk", "Path", 36, 38.5f, 0.05f);
            for(int i=0;i<segments;i+=3)
            {
                var p=CoursePoint(42,i,0.7f);
                Bush(p,new Vector3(2.1f,0.85f,1.5f),i);
            }
            // Irregular reflecting lake with stone coping and a planted island.
            var lake=new Vector3(-52,0.14f,0);
            Func<float,float,Vector3> shore=(a,scale)=>lake+new Vector3(Mathf.Cos(a)*(34+4*Mathf.Cos(a*3))*scale,0,Mathf.Sin(a)*(19+3*Mathf.Cos(a*2))*scale);
            for(int i=0;i<96;i++)
            {
                float a=i*Mathf.PI*2/96,b=(i+1)*Mathf.PI*2/96;
                Geo("21_Lake","Water").Triangle(lake,shore(b,1),shore(a,1));
                Geo("21_Lake","Stone").Quad(shore(a,1)+Vector3.up*0.06f,shore(b,1)+Vector3.up*0.06f,shore(b,1.055f)+Vector3.up*0.06f,shore(a,1.055f)+Vector3.up*0.06f);
                if(i%12==0)Rock(shore(a,1.07f),0.5f+(i%3)*0.2f);
            }
            Ellipse("21_Lake","Stone",new Vector3(-61,0.21f,3),8.5f,5.5f);
            Ellipse("21_Lake","Meadow",new Vector3(-61,0.24f,3),7.7f,4.7f);
            Oak(new Vector3(-61,0.25f,3),9);
            Bush(new Vector3(-66,0.8f,3),new Vector3(2,0.8f,1.5f),4);
            Flowers(new Vector3(-58,0.25f,3),28,2.4f,0);
            // Small arched garden bridge with board joints and handrails.
            for(int i=0;i<26;i++)
            {
                float z=-29+i*0.62f,y=0.4f+Mathf.Sin(i/25f*Mathf.PI)*1.15f;
                Geo("23_GardenBridge","Trunk").Box(new Vector3(-37,y,z),new Vector3(3.4f,0.13f,0.58f));
                if(i%3==0)foreach(float x in new[]{-38.6f,-35.4f})Geo("23_GardenBridge","White").Tube(new Vector3(x,y,z),new Vector3(x,y+1.1f,z),0.055f,6);
                if(i<25)foreach(float x in new[]{-38.6f,-35.4f})Geo("23_GardenBridge","White").Tube(new Vector3(x,y+1.1f,z),new Vector3(x,0.4f+Mathf.Sin((i+1)/25f*Mathf.PI)*1.15f+1.1f,z+0.62f),0.06f,6);
            }
            Ribbon("20_InfieldWalk","Path",t=>new Vector3(-118+t*235,0.08f,-28+5*Mathf.Sin(t*Mathf.PI*3)),3);
            Ribbon("20_InfieldWalk","Path",t=>new Vector3(8+3*Mathf.Sin(t*Mathf.PI),0.08f,-36+t*72),4);
            // Central festival plaza, radial paving and low fountain.
            Ellipse("22_FountainPlaza","Stone",new Vector3(20,0.12f,5),16,16);
            Ellipse("22_FountainPlaza","Path",new Vector3(20,0.15f,5),14.8f,14.8f);
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI/8;
                Geo("22_FountainPlaza","Stone").Beam(new Vector3(20,0.17f,5)+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*6,new Vector3(20,0.17f,5)+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*14.5f,0.02f,0.12f);
            }
            Geo("22_FountainPlaza","White").Tube(new Vector3(20,0.17f,5),new Vector3(20,0.7f,5),5.3f,48);
            Ellipse("22_FountainPlaza","Water",new Vector3(20,0.72f,5),4.8f,4.8f);
            Geo("22_FountainPlaza","Stone").Tube(new Vector3(20,0.75f,5),new Vector3(20,1.5f,5),1.2f,24);
            Geo("22_FountainPlaza","Gold").Tube(new Vector3(20,1.5f,5),new Vector3(20,3,5),0.28f,12);
            Geo("22_FountainPlaza","WaterEdge").Ellipsoid(new Vector3(20,3.1f,5),new Vector3(0.6f,0.22f,0.6f),16,8);
            // Stems and petals sit in shaped beds, rather than coloured boxes.
            for(int i=0;i<22;i++)
            {
                float x=-110+i*10.2f,z=-28+5*Mathf.Sin(i/21f*Mathf.PI*3);
                var p=new Vector3(x,0.1f,z-4.2f);
                Ellipse("27_FlowerGardens","Stone",p,3.8f,1.6f,24);
                Flowers(p+Vector3.up*0.06f,26,1.5f,i);
                if(i%3==0)Bush(p+new Vector3(2,0.5f,0),new Vector3(1.1f,0.55f,1),i);
            }
            Pavilion(new Vector3(77,0,8));
            foreach(var p in new[]{new Vector3(-108,0,15),new Vector3(-95,0,-8),new Vector3(113,0,20),new Vector3(116,0,-18),new Vector3(52,0,25)})Oak(p,9+(float)random.NextDouble()*3);
            for(int i=0;i<10;i++)
            {
                var p=new Vector3(45+i%5*13,0,-5-i/5*13);
                Parasol(p,i%2==0?"PetalWhite":"Teal");
            }
        }

        private static void Pavilion(Vector3 p)
        {
            const string g="28_GardenPavilion";
            Geo(g,"Stone").Box(p+new Vector3(0,0.3f,0),new Vector3(18,0.6f,11));
            Geo(g,"White").Box(p+new Vector3(0,2.5f,-2.5f),new Vector3(16,4.4f,5));
            Geo(g,"GlassDeep").Box(p+new Vector3(0,2.6f,0.1f),new Vector3(14,3.2f,0.2f));
            Geo(g,"Teal").Box(p+new Vector3(0,5.2f,0),new Vector3(19,0.35f,12));
            for(int i=-4;i<=4;i++)
            {
                Geo(g,"Trunk").Box(p+new Vector3(i*2,5.6f,2),new Vector3(0.16f,0.35f,9));
                if(i%2==0)Geo(g,"White").Tube(p+new Vector3(i*2,0.5f,5),p+new Vector3(i*2,5.4f,5),0.11f,8);
            }
            Label("GardenCafe","GARDEN TERRACE",p+new Vector3(0,4.4f,0.3f),0.7f,Color.white,Quaternion.Euler(0,180,0));
            foreach(float x in new[]{-7f,7f})
            {
                Geo(g,"Stone").Box(p+new Vector3(x,0.6f,5),new Vector3(2,1.2f,2));
                Bush(p+new Vector3(x,1.5f,5),new Vector3(1.1f,0.8f,1.1f),(int)x);
            }
        }

        private static void Parasol(Vector3 p,string color)
        {
            const string g="29_TerraceFurniture";
            Geo(g,"White").Tube(p,p+Vector3.up*3.7f,0.045f,8);
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6,b=(i+1)*Mathf.PI/6;
                var c=p+Vector3.up*3.75f;var u=p+new Vector3(Mathf.Cos(a)*2,3.05f,Mathf.Sin(a)*2);var v=p+new Vector3(Mathf.Cos(b)*2,3.05f,Mathf.Sin(b)*2);
                Geo(g,i%2==0?color:"PetalWhite").Triangle(c,v,u);
                Geo(g,i%2==0?color:"PetalWhite").Triangle(c,u,v);
                Geo(g,"White").Tube(c,u,0.02f,4);
            }
            Geo(g,"Trunk").Tube(p+Vector3.up*0.85f,p+Vector3.up*0.95f,0.85f,16);
            for(int i=0;i<4;i++)
            {
                float a=i*Mathf.PI/2;
                var q=p+new Vector3(Mathf.Cos(a)*1.2f,0,Mathf.Sin(a)*1.2f);
                Geo(g,"White").Box(q+Vector3.up*0.45f,new Vector3(0.55f,0.1f,0.55f));
                Geo(g,"Navy").Tube(q,q+Vector3.up*0.45f,0.07f,6);
            }
        }

        private static void BuildFacilitiesV2()
        {
            const string g="30_FinishMonument";
            Geo(g,"White").Box(new Vector3(49,0.13f,-68.5f),new Vector3(0.28f,0.03f,17));
            // Two sculpted rings frame the finish without spanning the running course.
            foreach(float z in new[]{-58.1f,-79.2f})
            {
                Geo(g,"Stone").Tube(new Vector3(49,0,z),new Vector3(49,0.45f,z),1.55f,24);
                Geo(g,"White").Box(new Vector3(49,3.5f,z),new Vector3(0.8f,6.4f,0.7f));
                Geo(g,"Teal").Box(new Vector3(49,3.5f,z-0.4f),new Vector3(0.56f,5.8f,0.12f));
                for(int i=0;i<48;i++)
                {
                    float a=i*Mathf.PI*2/48,b=(i+1)*Mathf.PI*2/48;
                    Geo(g,"Gold").Tube(new Vector3(49+Mathf.Cos(a)*2.15f,8+Mathf.Sin(a)*2.15f,z),new Vector3(49+Mathf.Cos(b)*2.15f,8+Mathf.Sin(b)*2.15f,z),0.16f,8);
                    Geo(g,"White").Tube(new Vector3(49+Mathf.Cos(a)*1.86f,8+Mathf.Sin(a)*1.86f,z),new Vector3(49+Mathf.Cos(b)*1.86f,8+Mathf.Sin(b)*1.86f,z),0.07f,6);
                }
                Geo(g,"Teal").Box(new Vector3(49,8,z),new Vector3(3.1f,1.1f,0.24f));
                Label("FinishFront_"+z,"FINISH",new Vector3(49,8,z-0.14f),0.78f,Color.white,Quaternion.identity);
                Label("FinishBack_"+z,"FINISH",new Vector3(49,8,z+0.14f),0.78f,Color.white,Quaternion.Euler(0,180,0));
                foreach(float x in new[]{46.9f,51.1f})Flowers(new Vector3(x,0.05f,z),24,0.95f,0);
            }
            StartingGate(new Vector3(-68,0.12f,-68.5f));
            // Course boards use actual modelled faces and feet; labels are sector references.
            for(int i=0;i<8;i++)
            {
                var p=CoursePoint(58.1f,18+i*32);
                var outward=(p-new Vector3(Mathf.Clamp(p.x,-90,90),0,0)).normalized;
                var rot=Quaternion.LookRotation(-outward);
                Geo("33_CourseMarkers","White").Tube(p,p+Vector3.up*2.8f,0.07f,8);
                Geo("33_CourseMarkers","Coral").Tube(p+Vector3.up*0.6f,p+Vector3.up*1.15f,0.075f,8);
                Geo("33_CourseMarkers","Teal").Box(p+Vector3.up*2.7f,new Vector3(1.6f,1.1f,0.18f),rot);
                Label("Section_"+i,(i+1).ToString("00"),p+Vector3.up*2.7f-rot*Vector3.forward*0.12f,0.75f,Color.white,rot);
            }
            const string board="31_DigitalScoreboard";
            Geo(board,"Stone").Box(new Vector3(-5,0.7f,103),new Vector3(40,1.4f,6));
            foreach(float x in new[]{-18f,8f})Geo(board,"White").Box(new Vector3(x,8,103),new Vector3(1.1f,16,1.2f));
            Geo(board,"White").Box(new Vector3(-5,14,103),new Vector3(40,15,1.4f));
            Geo(board,"Navy").Box(new Vector3(-5,14,102.2f),new Vector3(38.8f,13.8f,0.15f));
            Geo(board,"Teal").Box(new Vector3(-5,19.4f,102.05f),new Vector3(38,2.6f,0.08f));
            Label("ScoreTitle","RUN PARK  /  TURF",new Vector3(-5,19.5f,101.95f),1.55f,Color.white,Quaternion.identity);
            Label("ScoreLeft","01   02   03   04   05",new Vector3(-5,15.8f,101.95f),1.2f,Hex("EEC76C"),Quaternion.identity);
            Label("ScoreRight","GRAND PRIX",new Vector3(-5,12.3f,101.95f),1.5f,Color.white,Quaternion.identity);
            Label("ScoreFoot","WELCOME TO RUN PARK",new Vector3(-5,9,101.95f),0.75f,Hex("81C7B7"),Quaternion.identity);
            Label("ScoreRear","RUN PARK",new Vector3(-5,14,103.8f),2.8f,Hex("248E91"),Quaternion.Euler(0,180,0));
            // Trussed floodlight masts: no extra real-time lights.
            for(int i=0;i<10;i++)
            {
                var p=CoursePoint(98,15+i*segments/10f);
                if(p.z<-85&&Mathf.Abs(p.x)<122)continue;
                for(int j=0;j<3;j++)
                {
                    float a=j*Mathf.PI*2/3;
                    var off=new Vector3(Mathf.Cos(a)*0.65f,0,Mathf.Sin(a)*0.65f);
                    Geo("32_TrussFloodlights","White").Tube(p+off,p+off*0.4f+Vector3.up*23,0.08f,6);
                    for(int h=0;h<11;h++)
                    {
                        var next=new Vector3(Mathf.Cos(a+Mathf.PI*2/3)*0.65f,0,Mathf.Sin(a+Mathf.PI*2/3)*0.65f);
                        Geo("32_TrussFloodlights","White").Tube(p+off+Vector3.up*(h*2),p+next+Vector3.up*(h*2+2),0.035f,4);
                    }
                }
                Geo("32_TrussFloodlights","Navy").Box(p+Vector3.up*23,new Vector3(5.2f,1.7f,0.5f));
                for(int x=0;x<6;x++)for(int y=0;y<2;y++)Geo("32_TrussFloodlights","PetalWhite").Box(p+new Vector3(-2.1f+x*0.84f,22.6f+y*0.8f,-0.28f),new Vector3(0.65f,0.58f,0.08f));
            }
        }

        private static void StartingGate(Vector3 p)
        {
            const string g="35_StartingGate";
            // Long dimension runs across the grass course, runners face +X.
            Geo(g,"Teal").Box(p+new Vector3(0,3.5f,0),new Vector3(3.5f,0.65f,16.8f));
            Geo(g,"Gold").Box(p+new Vector3(1.8f,3.8f,0),new Vector3(0.14f,0.13f,16.8f));
            for(int i=0;i<=10;i++)
            {
                float z=-8.1f+i*1.62f;
                foreach(float x in new[]{-1.6f,1.6f})Geo(g,"White").Tube(p+new Vector3(x,0.2f,z),p+new Vector3(x,3.25f,z),0.065f,8);
                Geo(g,"Teal").Box(p+new Vector3(0,1.3f,z),new Vector3(3.1f,1.8f,0.12f));
                Geo(g,"White").Tube(p+new Vector3(-1.6f,2.4f,z),p+new Vector3(1.6f,2.4f,z),0.055f,6);
                if(i<10)
                {
                    var labelPos=p+new Vector3(1.84f,3.5f,z+0.81f);
                    Label("GateNumber_"+i,(i+1).ToString(),labelPos,0.62f,Hex("F4DE76"),Quaternion.Euler(0,-90,0));
                }
            }
            foreach(float z in new[]{-8.7f,8.7f})foreach(float x in new[]{-1.4f,1.4f})
            {
                Geo(g,"Navy").Tube(p+new Vector3(x,0.43f,z-0.1f),p+new Vector3(x,0.43f,z+0.1f),0.42f,16);
                Geo(g,"Concrete").Tube(p+new Vector3(x,0.43f,z-0.12f),p+new Vector3(x,0.43f,z+0.12f),0.16f,12);
            }
        }

        private static void SculptedTree(Vector3 p,float height,int seed)
        {
            const string g="41_BroadleafTreeBelt";
            Geo(g,"Trunk").Tube(p,p+Vector3.up*height*0.63f,height*0.027f,7);
            for(int i=0;i<7;i++)
            {
                float angle=i*2.399f+seed;
                var branch=p+new Vector3(Mathf.Cos(angle)*height*0.18f,height*(0.5f+(i%3)*0.11f),Mathf.Sin(angle)*height*0.18f);
                Geo(g,"Trunk").Tube(p+Vector3.up*height*0.38f,branch,height*0.012f,5);
            }
            var size=new Vector3(height*0.82f,height*0.73f,height*0.76f);
            var scale=new Vector3(size.x/foliageMesh.bounds.size.x,size.y/foliageMesh.bounds.size.y,size.z/foliageMesh.bounds.size.z);
            var matrix=Matrix4x4.TRS(p+Vector3.up*height*0.64f,Quaternion.Euler(0,seed*137.5f,0),scale)*Matrix4x4.Translate(-foliageMesh.bounds.center);
            Geo(g,seed%3==0?"LeafTexturedShade":"LeafTextured").AppendMesh(foliageMesh,matrix);
        }

        private static void BuildSurroundingsV2()
        {
            Geo("42_DistantHills","Meadow").Landscape();
            // Layered planting is dense along the horizon but clears the grandstand facade.
            for(int i=0;i<76;i++)
            {
                var p=CoursePoint(110+(i%3)*5,i*segments/76f);
                if(p.z<-88&&Mathf.Abs(p.x)<126)continue;
                SculptedTree(p,10+(i%6)*1.3f,i);
                Bush(p+new Vector3(2,0.7f,-2),new Vector3(2.6f,1.2f,2),i,"44_BoundaryPlanting");
            }
            for(int i=0;i<42;i++)
            {
                var p=CoursePoint(143+(i%4)*4,i*segments/42f);
                SculptedTree(p,13+(i%5),i+90);
            }
            // Existing volumetric oaks frame the entrance and grandstand ends.
            foreach(float x in new[]{-126f,-110f,-85f,-60f,-35f,35f,60f,85f,110f,126f})Oak(new Vector3(x,0,-145),10+(float)random.NextDouble()*3);
            Geo("43_EntrancePromenade","Path").Box(new Vector3(0,0.03f,-144),new Vector3(245,0.1f,22));
            for(int i=-7;i<=7;i++)
            {
                float x=i*16;
                Geo("43_EntrancePromenade","Stone").Box(new Vector3(x,0.45f,-150),new Vector3(3,0.9f,4));
                Bush(new Vector3(x,1.2f,-150),new Vector3(1.6f,1.1f,2),i,"44_BoundaryPlanting");
                Geo("45_RaceBanners","White").Tube(new Vector3(x,0,-135),new Vector3(x,13,-135),0.075f,8);
                // Curved cloth strips cast real silhouettes instead of flat rigid sign cubes.
                for(int j=0;j<12;j++)
                {
                    float a=j/12f,b=(j+1)/12f;
                    var u=new Vector3(x+a*2.4f,12.7f,-135+Mathf.Sin(a*Mathf.PI*2)*0.2f);
                    var v=new Vector3(x+b*2.4f,12.7f,-135+Mathf.Sin(b*Mathf.PI*2)*0.2f);
                    Geo("45_RaceBanners",i%2==0?"Teal":"Coral").Quad(u,u-Vector3.up*4,v-Vector3.up*4,v);
                    Geo("45_RaceBanners",i%2==0?"Teal":"Coral").Quad(v,v-Vector3.up*4,u-Vector3.up*4,u);
                }
            }
            // Low planter borders and a continuous secondary hedge behind the course.
            for(int i=0;i<segments;i+=4)
            {
                var p=CoursePoint(94,i,0.6f);
                if(p.z<-87&&Mathf.Abs(p.x)<120)continue;
                Bush(p,new Vector3(2.7f,0.9f,1.8f),i,"44_BoundaryPlanting");
            }
        }

        private static void ConfigureRefinedLighting()
        {
            RenderSettings.sun.transform.rotation=Quaternion.Euler(55,145,0);
            RenderSettings.sun.intensity=1.2f;
            RenderSettings.sun.shadowStrength=0.78f;
            RenderSettings.sun.color=Hex("FFF5E4");
            RenderSettings.ambientSkyColor=new Color(0.65f,0.76f,0.87f);
            RenderSettings.ambientEquatorColor=new Color(0.5f,0.57f,0.56f);
            RenderSettings.ambientGroundColor=new Color(0.28f,0.34f,0.22f);
            var sky=materials.ContainsKey("SummerSky")?materials["SummerSky"]:AssetDatabase.LoadAssetAtPath<Material>(artPath+"/Materials/Mat_SummerSky.mat");
            if(sky==null){sky=new Material(Shader.Find("ProjectRun/Racecourse/SummerSky"));AssetDatabase.CreateAsset(sky,artPath+"/Materials/Mat_SummerSky.mat");}
            sky.SetColor("_Zenith",new Color(0.10f,0.39f,0.85f));sky.SetColor("_Horizon",new Color(0.55f,0.78f,0.96f));sky.SetColor("_CloudColor",Hex("FFFEF7"));
            sky.SetVector("_SunDirection",-RenderSettings.sun.transform.forward);
            RenderSettings.skybox=sky;
            EditorUtility.SetDirty(sky);
            RenderSettings.fogColor=Hex("B6D5D9");
            // Profile is scene-local; no project-wide quality settings are replaced.
            var volumeObject=Group("60_LightAndColor").gameObject;
            var volume=volumeObject.AddComponent<Volume>();
            volume.isGlobal=true;
            string path=artPath+"/RacecourseColor.asset";
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if(profile==null){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,path);}
            if(!profile.TryGet<ColorAdjustments>(out var color)){color=profile.Add<ColorAdjustments>();AssetDatabase.AddObjectToAsset(color,profile);}
            color.postExposure.Override(0.1f);color.saturation.Override(10);color.contrast.Override(12);
            if(!profile.TryGet<Tonemapping>(out var tone)){tone=profile.Add<Tonemapping>();AssetDatabase.AddObjectToAsset(tone,profile);}
            tone.mode.Override(TonemappingMode.ACES);
            volume.sharedProfile=profile;
            EditorUtility.SetDirty(profile);
        }

        [MenuItem("Tools/Project Run/All 3D/Camera - showcase")]
        public static void ViewShowcase() => SetCamera(new Vector3(106,13,-38),new Vector3(4,8,-104),53);

        [MenuItem("Tools/Project Run/All 3D/Camera - garden")]
        public static void ViewGarden() => SetCamera(new Vector3(-93,9,36),new Vector3(-35,1,0),52);

        private static void BakeSignGlyphs()
        {
            const string chars="ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 /";
            var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            font.RequestCharactersInTexture(chars,96,FontStyle.Bold);
            signGlyphs.Clear();
            foreach(char c in chars)if(font.GetCharacterInfo(c,out var info,96,FontStyle.Bold))signGlyphs[c]=info;
            var source=font.material.mainTexture;
            var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32);
            var atlas=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);
            try
            {
                Graphics.Blit(source,rt);RenderTexture.active=rt;
                atlas.ReadPixels(new Rect(0,0,source.width,source.height),0,0);atlas.Apply();
                string path=artPath+"/Textures/Tex_SignGlyphs.png";
                System.IO.File.WriteAllBytes(path,atlas.EncodeToPNG());AssetDatabase.ImportAsset(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;
                importer.SaveAndReimport();signAtlas=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            finally{RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(atlas);}
        }

        private static void PrepareFoliageMesh()
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/OldAsset/Environment/worldboss/etc/wb_bg_iys/oak_tree.FBX").GetComponentInChildren<MeshFilter>().sharedMesh;
            var indices=source.GetTriangles(1);var sourceVertices=source.vertices;var sourceNormals=source.normals;var sourceUv=source.uv;
            var vertices=new System.Collections.Generic.List<Vector3>();var normals=new System.Collections.Generic.List<Vector3>();
            var uv=new System.Collections.Generic.List<Vector2>();var triangles=new System.Collections.Generic.List<int>();
            var remap=new System.Collections.Generic.Dictionary<int,int>();
            // Keep pairs of triangles from the distributed leaf mesh, preserving all source files.
            for(int i=0;i+5<indices.Length;i+=24)
            for(int j=0;j<6;j++)
            {
                int old=indices[i+j];
                if(!remap.TryGetValue(old,out int current))
                {
                    current=vertices.Count;remap.Add(old,current);
                    vertices.Add(sourceVertices[old]);normals.Add(sourceNormals[old]);uv.Add(sourceUv[old]);
                }
                triangles.Add(current);
            }
            string path=artPath+"/Meshes/Mesh_FoliageClusters.asset";
            foliageMesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(foliageMesh==null){foliageMesh=new Mesh();AssetDatabase.CreateAsset(foliageMesh,path);}
            foliageMesh.Clear();foliageMesh.SetVertices(vertices);foliageMesh.SetNormals(normals);foliageMesh.SetUVs(0,uv);foliageMesh.SetTriangles(triangles,0);foliageMesh.RecalculateBounds();
            EditorUtility.SetDirty(foliageMesh);
        }

        private static void MeshLabel(string text,Vector3 position,float height,Color color,Quaternion rotation)
        {
            string key="Letter_"+ColorUtility.ToHtmlStringRGB(color);
            if(!materials.ContainsKey(key))
            {
                var mat=Mat(key,ColorUtility.ToHtmlStringRGB(color),signAtlas);
                mat.shader=Shader.Find("ProjectRun/Racecourse/SignLetters");
                mat.SetTexture("_BaseMap",signAtlas);mat.SetColor("_BaseColor",color);
                EditorUtility.SetDirty(mat);
            }
            float scale=height/70f;
            float width=text.Sum(c=>signGlyphs.TryGetValue(c,out var glyph)?glyph.advance:0)*scale;
            float cursor=-width*0.5f;
            foreach(char c in text)
            {
                if(!signGlyphs.TryGetValue(c,out var glyph))continue;
                if(c!=' ')
                {
                    float left=cursor+glyph.minX*scale,right=cursor+glyph.maxX*scale;
                    float bottom=glyph.minY*scale-height*0.5f,top=glyph.maxY*scale-height*0.5f;
                    Geo("34_PermanentSignage",key).GlyphQuad(position+rotation*new Vector3(left,bottom,0),position+rotation*new Vector3(left,top,0),
                        position+rotation*new Vector3(right,top,0),position+rotation*new Vector3(right,bottom,0),glyph);
                }
                cursor+=glyph.advance*scale;
            }
        }
    }
}
