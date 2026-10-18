using System;
using UnityEditor;
using UnityEngine;

namespace ProjectRun.Editor
{
    public static partial class PronteraWorldRaceBuilder
    {
        private static void RefinedMaterials()
        {
            Mat("Cliff", "BBC5A7", source+"mjolnir/mjolnir_cliff001a.tga");
            Mat("CliffLight", "CED2AC", source+"mjolnir/mjolnir_cliff001a.tga");
            Mat("CliffDark", "A7B697", source+"mjolnir/mjolnir_cliff001a.tga");
            Mat("Strata", "D9B991", source+"desert/moroc_Rock001.tga"); Mat("StrataLight", "E5C7A0", source+"desert/moroc_Rock001.tga"); Mat("StrataDark", "BC9D77", source+"desert/moroc_Rock001.tga");
            Mat("PaleStone", "E2D7B7", source+"prontera_city/pr_map_01.png");
            Mat("Plaster", "F4E4C1"); Mat("FlowerWhite", "FFF3C3"); Mat("FlowerGold", "F4C154"); Mat("FlowerPink", "D9869E");
            Mat("DeepLeaf", "365F40"); Mat("Shrub", "5F904E"); Mat("MeadowLeaf", "83A94E"); Mat("Foam", "BBF1E5");
            Mat("WoodDeck", "AD8556");
            mats["Sandstone"].SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture>(source+"prontera_city/pr_map_01.png"));
            mats["Water"].shader=Shader.Find("ProjectRun/Racecourse/WorldWater");
            var sea=Mat("DistantWater","406D7F");sea.shader=mats["Water"].shader;sea.SetColor("_BaseColor",ColorHtml("315D73"));sea.SetColor("_ShallowColor",ColorHtml("6D9EAE"));
            mats["WorldGround"].SetTexture("_CliffMap",AssetDatabase.LoadAssetAtPath<Texture>(source+"mjolnir/mjolnir_cliff001a.tga"));
            foreach(string name in new[]{"Cliff","CliffLight","CliffDark","Strata","StrataLight","StrataDark"})
            {mats[name].shader=Shader.Find("ProjectRun/Racecourse/WorldRock");mats[name].SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture>(source+"mjolnir/mjolnir_cliff001a.tga"));}
        }

        private static float IslandRadius(float angle)
        { return 1 + .045f*Mathf.Sin(angle*5+.8f)+.035f*Mathf.Sin(angle*9); }

        private static float SculptedHeight(float x,float z)
        {
            var nearest=ClosestRoadPoint(new Vector3(x,0,z));
            float distance=Vector2.Distance(new Vector2(x,z),new Vector2(nearest.x,nearest.z));
            float y=Height(x,z)-.35f;
            bool city=Mathf.Abs(x)<90 && z>25 && z<185;
            if(city)y=41.7f;
            else
            {
                float noise=Mathf.PerlinNoise(x*.021f+20,z*.022f+20);
                y+=Mathf.SmoothStep(0,1,Mathf.InverseLerp(12,42,distance))*(noise*17-2);
                // A river gorge separates the forest switchback from the citadel and desert shelf.
                float valley=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(42,92,Mathf.Abs(x+16)))) *
                    (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-8,30,z))) * Mathf.SmoothStep(0,1,Mathf.InverseLerp(-132,-95,z));
                y=Mathf.Lerp(y,-7+noise*3,valley);
            }
            // A real gap beneath the timber bridge, with a natural stream bed.
            if(x< -85 && x> -245)y-=18*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(5,15,Mathf.Abs(z+89))));
            float pool=Mathf.Pow((x+12)/64,2)+Mathf.Pow((z+67)/41,2);
            if(pool<1.45f)y=Mathf.Lerp(y,-8,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.97f,1.45f,pool)));
            float oasis=Mathf.Pow((x-157)/25,2)+Mathf.Pow((z+4)/24,2);
            if(oasis<3)y=Mathf.Lerp(y,32,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.6f,3,oasis)));
            if(oasis<1.5f)y=Mathf.Lerp(y,29,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.96f,1.5f,oasis)));
            float cut=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(11,19,distance));
            return Mathf.Lerp(y,Mathf.Min(y,nearest.y-.38f),cut);
        }

        private static void SculptedLandscape()
        {
            Geo("distant_water","DistantWater").Quad(new Vector3(-2000,-64,-2000),new Vector3(-2000,-64,2000),new Vector3(2000,-64,2000),new Vector3(2000,-64,-2000));
            const float step=4;
            for(float x=-292;x<292;x+=step)for(float z=-215;z<258;z+=step)
            {
                float nx=(x+step*.5f)/280,nz=(z+step*.5f-18)/228;
                if(new Vector2(nx,nz).magnitude>IslandRadius(Mathf.Atan2(nx,nz)))continue;
                Geo("terrain","WorldGround").Quad(new Vector3(x,TerrainY(x,z),z),new Vector3(x,TerrainY(x,z+step),z+step),new Vector3(x+step,TerrainY(x+step,z+step),z+step),new Vector3(x+step,TerrainY(x+step,z),z));
            }
            // Tall irregular perimeter faces replace the rectangular ground plane.
            for(int i=0;i<180;i++)
            {
                float a=i*Mathf.PI/90,b=(i+1)*Mathf.PI/90;
                var p=new Vector3(Mathf.Sin(a)*280*IslandRadius(a),0,18+Mathf.Cos(a)*228*IslandRadius(a));
                var q=new Vector3(Mathf.Sin(b)*280*IslandRadius(b),0,18+Mathf.Cos(b)*228*IslandRadius(b));
                float py=TerrainY(p.x,p.z),qy=TerrainY(q.x,q.z);
                for(int j=0;j<6;j++)
                {
                    float t=j/6f,u=(j+1)/6f;
                    var p0=p+Vector3.up*Mathf.Lerp(-62,py,t);var q0=q+Vector3.up*Mathf.Lerp(-62,qy,t);
                    var p1=p+Vector3.up*Mathf.Lerp(-62,py,u);var q1=q+Vector3.up*Mathf.Lerp(-62,qy,u);
                    Geo("island_cliffs",p.x>100?(j%2==0?"StrataLight":"Strata"):(j%2==0?"CliffLight":"Cliff")).Quad(p0,q0,q1,p1,.08f);
                }
                var pin=p*.975f;pin.y=TerrainY(pin.x,pin.z);var qin=q*.975f;qin.y=TerrainY(qin.x,qin.z);
                Geo("cliff_rim","WorldGround").Quad(p+Vector3.up*py,q+Vector3.up*qy,qin,pin);
            }
            // Nested rock shelves form the forest uplands and the city's exposed foundation.
            for(int i=0;i<18;i++)
            {
                float x=-237+(i%5)*23,z=40+(i/5)*48;
                Mesa(new Vector3(x,TerrainY(x,z)-7,z),Rand(17,26),Rand(16,25),Rand(13,24),false,i*31);
            }
            for(int i=0;i<11;i++)
            {
                float x=-80+i*16;
                Mesa(new Vector3(x,0,24),Rand(10,16),Rand(9,14),42,false,i*47);
            }
            // The central valley is a lake fed by stepped cascades, instead of strips ending in grass.
            Disc("river_pool","Water",new Vector3(-12,-3.6f,-67),64,41);
            Stream(new Vector3(-232,-6,-89),new Vector3(-81,-6,-89),8,10);
            Stream(new Vector3(-81,-6,-89),new Vector3(-42,-3.5f,-65),9,12);
            Mesa(new Vector3(-25,-6,-4),20,24,16,false,43);
            Mesa(new Vector3(-64,0,24),15,14,36,false,29);
            Stream(new Vector3(-24,10.1f,8),new Vector3(-18,10.1f,-18),7,7);
            Waterfall(new Vector3(-24,10,8),new Vector3(-18,-3.5f,-32),11);
            Waterfall(new Vector3(-64,36,24),new Vector3(-57,10,2),8);
            Waterfall(new Vector3(-57,10,2),new Vector3(-38,-3.5f,-35),7);
            for(int i=0;i<30;i++)
            {
                float a=i*Mathf.PI*2/30;var p=new Vector3(-12+Mathf.Sin(a)*63,-5,-67+Mathf.Cos(a)*39);
                Rock(p,Rand(2,6),false);
            }
            Disc("oasis","Water",new Vector3(157,30.8f,-4),23,22);
            // Distant ridges keep the world from ending against a blank horizon during the race.
            for(float x=-680;x<680;x+=14)for(float z=260;z<680;z+=14)
            {
                var a=new Vector3(x,MountainHeight(x,z),z);var b=new Vector3(x,MountainHeight(x,z+14),z+14);
                var c=new Vector3(x+14,MountainHeight(x+14,z+14),z+14);var d=new Vector3(x+14,MountainHeight(x+14,z),z);
                Geo("distant_ridges",x>130?"Strata":"Cliff").Quad(a,b,c,d);
            }
        }
        private static float MountainHeight(float x,float z)
        {
            float n=Mathf.PerlinNoise(x*.006f+50,z*.012f+30);
            float detail=Mathf.PerlinNoise(x*.024f+20,z*.02f+40);
            return -62+Mathf.SmoothStep(0,1,Mathf.InverseLerp(260,365,z))*(30+n*n*175+detail*28);
        }

        private static void Mesa(Vector3 p,float rx,float rz,float h,bool sand,int seed,bool mountain=false)
        {
            if(!mountain)
            {
                float clearance=RoadDistance(p)-13;
                if(clearance<7)return;
                float fit=Mathf.Min(1,clearance/(Mathf.Max(rx,rz)*1.33f));rx*=fit;rz*=fit;
            }
            const int sides=19;
            for(int level=0;level<5;level++)
            {
                float t=level/5f,u=(level+1)/5f;
                float bottom=1-t*.32f+.05f*Mathf.Sin(t*14+seed),top=1-u*.32f+.05f*Mathf.Sin(u*14+seed);
                if(mountain){bottom=1-t*.96f;top=1-u*.96f;}
                for(int i=0;i<sides;i++)
                {
                    float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                    float ra=1+.22f*Mathf.Sin(i*12.73f+seed)+.08f*Mathf.Cos(i*5),rb=1+.22f*Mathf.Sin(((i+1)%sides)*12.73f+seed)+.08f*Mathf.Cos(((i+1)%sides)*5);
                    float ha=2*Mathf.Sin(a*4+seed),hb=2*Mathf.Sin(b*4+seed);
                    var v0=p+new Vector3(Mathf.Sin(a)*rx*ra*bottom,h*t+ha*Mathf.Sin(t*Mathf.PI),Mathf.Cos(a)*rz*ra*bottom);
                    var v1=p+new Vector3(Mathf.Sin(b)*rx*rb*bottom,h*t+hb*Mathf.Sin(t*Mathf.PI),Mathf.Cos(b)*rz*rb*bottom);
                    var v2=p+new Vector3(Mathf.Sin(b)*rx*rb*top,h*u+hb*Mathf.Sin(u*Mathf.PI),Mathf.Cos(b)*rz*rb*top);
                    var v3=p+new Vector3(Mathf.Sin(a)*rx*ra*top,h*u+ha*Mathf.Sin(u*Mathf.PI),Mathf.Cos(a)*rz*ra*top);
                    string material=sand?(level%2==0?"Strata":"StrataLight"):(level%2==0?"Cliff":"CliffLight");
                    Geo("rock_shelves",material).Quad(v0,v1,v2,v3,.035f);
                    if(level==4)Geo("shelf_tops",sand?"Sand":"ForestFloor").Triangle(p+Vector3.up*h,v3,v2);
                }
            }
            if(!mountain&&!sand&&rx>9)
            {
                for(int i=0;i<3;i++)
                {float a=i*2.399f;Tree(p+new Vector3(Mathf.Sin(a)*rx*.32f,h,Mathf.Cos(a)*rz*.32f),Rand(10,17));}
            }
        }

        private static void Stream(Vector3 a,Vector3 b,float ra,float rb)
        {
            var right=Vector3.Cross(Vector3.up,(b-a).normalized).normalized;
            Geo("streams","Water").Quad(a-right*ra,b-right*rb,b+right*rb,a+right*ra);
        }
        private static void Waterfall(Vector3 a,Vector3 b,float width)
        {
            var right=Vector3.Cross(Vector3.up,(b-a).normalized).normalized;
            for(int i=0;i<9;i++)
            {
                float t=i/9f,u=(i+1)/9f;
                var p=Vector3.Lerp(a,b,t);var q=Vector3.Lerp(a,b,u);
                p.y=Mathf.Lerp(a.y,b.y,t*t);q.y=Mathf.Lerp(a.y,b.y,u*u);
                Geo("cascades","Water").Quad(p-right*width*.5f,q-right*width*.5f,q+right*width*.5f,p+right*width*.5f);
                for(int k=0;k<5;k++)Beam("cascade_foam","Foam",p+right*(k-2)*width*.18f,q+right*(k-2)*width*.18f,Rand(.12f,.32f));
            }
            Disc("splash","Foam",b+Vector3.up*.08f,width*.65f,2.5f);
        }

        private static void RefinedRoad()
        {
            float bridge=NearestDistance(new Vector3(-139,0,-89));
            float bridgeEnd=bridge+16,bridgeStart=bridge-16;
            for(float s=0;s<course.Length;s+=1.5f)
            {
                float e=Mathf.Min(s+1.5f,course.Length);var p=course.Point(s);
                bool deck=s>=bridgeStart&&s<=bridgeEnd;
                string material=deck?"Wood":Mathf.Abs(p.x)<85&&p.z>40?"Cobble":p.x>90&&p.z>-116?"DesertRoad":"Road";
                Geo("road",material).Quad(course.Point(s,-9),course.Point(e,-9),course.Point(e,9),course.Point(s,9),.22f);
                if(deck)
                {
                    var q=Quaternion.LookRotation(course.Forward(s));
                    Box("bridge_planks","WoodDeck",course.Point(s)-Vector3.up*.12f,new Vector3(18.8f,.35f,1.35f),q);
                    for(int side=-1;side<=1;side+=2)
                    {Beam("bridge_truss","Wood",course.Point(s,side*8.8f)-Vector3.up*2,course.Point(e,side*8.8f)-Vector3.up*2,.7f);}
                }
                else for(int side=-1;side<=1;side+=2)
                {
                    var q=Quaternion.LookRotation(course.Forward(s));
                    Box("course_curb",p.x>100?"StrataLight":"PaleStone",course.Point(s,side*9.35f)-Vector3.up*.08f,new Vector3(.7f,.3f,1.42f),q);
                }
            }
            for(float s=0;s<course.Length;s+=5)
            {
                var p=course.Point(s);bool city=Mathf.Abs(p.x)<87&&p.z>40;
                for(int side=-1;side<=1;side+=2)
                {
                    var a=course.Point(s,side*10.4f);var b=course.Point(Mathf.Min(s+5,course.Length),side*10.4f);
                    Box("barrier_posts",city?"PaleStone":"Wood",a+Vector3.up*.9f,new Vector3(.65f,1.8f,.65f));
                    Beam("barrier_rails",city?"PaleStone":"Wood",a+Vector3.up*1.3f,b+Vector3.up*1.3f,.32f);
                    if(!city)Beam("barrier_low","Wood",a+Vector3.up*.5f,b+Vector3.up*.5f,.23f);
                    for(int k=0;k<3;k++)
                    {
                        var l=Vector3.Lerp(a,b,(k+.1f)/3)+Vector3.up*1.35f;var r=Vector3.Lerp(a,b,(k+.85f)/3)+Vector3.up*1.35f;
                        Geo("festival_flags",k%3==0?"Blue":k%3==1?"Ivory":"Red").Triangle(l,(l+r)*.5f-Vector3.up*.8f,r);
                    }
                }
            }
            for(float s=bridgeStart;s<bridgeEnd;s+=8)
            {
                for(int side=-1;side<=1;side+=2)
                {
                    var p=course.Point(s,side*8);float bottom=-7;
                    Box("bridge_piers","Wood",new Vector3(p.x,(p.y+bottom)/2,p.z),new Vector3(.9f,p.y-bottom,.9f));
                    Beam("bridge_braces","Wood",p-Vector3.up*.8f,course.Point(s+8,side*8)-Vector3.up*8,.55f);
                }
            }
            for(float s=20;s<course.Length;s+=38)
            {
                var f=course.Forward(s);var r=Vector3.Cross(Vector3.up,f).normalized;var p=course.Point(s)+Vector3.up*.045f;
                for(int k=0;k<2;k++)
                {Beam("direction_chevrons","Ivory",p-r*1.4f-f*(k*2+1),p-f*k*2+f*.5f,.32f);Beam("direction_chevrons","Ivory",p+r*1.4f-f*(k*2+1),p-f*k*2+f*.5f,.32f);}
            }
        }

        private static void Rock(Vector3 p,float h,bool sand)
        {
            var t=Model("mjolnir/mj_rock001.FBX",sand?"obj_sandstone_boulder":"obj_mossy_boulder",p,h,Rand(0,360),sand?desert:forest);
            foreach(var r in t.GetComponentsInChildren<Renderer>())r.sharedMaterial=mats[sand?"StrataLight":"Cliff"];
        }
        private static void Tree(Vector3 p,float h)
        {Model("worldboss/etc/wb_bg_iys/oak_tree.FBX","obj_forest_canopy",p,h,Rand(0,360),forest);}
        private static void DenseForest()
        {
            // Deliberate clusters leave the entire racing corridor readable.
            for(int i=0;i<430;i++)
            {
                float x=Rand(-267,-77),z=Rand(-165,216),h=Rand(15,29);var p=new Vector3(x,TerrainY(x,z),z);
                float d=RoadDistance(p);
                if(d<13+h*.45f || Mathf.Abs(z+89)<12 || p.y< -5)continue;
                if(new Vector2(x/270,(z-18)/225).magnitude>1)continue;
                Tree(p,h);
            }
            for(int i=0;i<90;i++)
            {
                float x=Rand(-93,88),z=Rand(-153,15);var p=new Vector3(x,TerrainY(x,z),z);
                if(RoadDistance(p)<23 || p.y<0)continue;
                Tree(p,Rand(9,20));
            }
            // Roadside rocks, fern-like tufts, wildflowers, and bushes fill the near camera field.
            for(float s=0;s<course.Length;s+=2.3f)
            {
                var center=course.Point(s);if(center.x>90||Mathf.Abs(center.x)<86&&center.z>30)continue;
                for(int side=-1;side<=1;side+=2)
                {
                    var p=course.Point(s,side*Rand(12,20));p.y=TerrainY(p.x,p.z);
                    if(p.y< -4 || Mathf.Abs(p.z+89)<8&&p.x< -85)continue;
                    Shrub(p,Rand(.7f,1.7f));
                    if(((int)s)%5==0)Rock(p,Rand(1,3.2f),false);
                    for(int k=0;k<4;k++)Flowers(p+new Vector3(Rand(-2,2),0,Rand(-2,2)),Rand(.35f,.75f),k%3);
                }
            }
            Model("payon_city/payon_to_house001.FBX","obj_payon_teahouse",new Vector3(-199,TerrainY(-199,44),44),19,110,forest);
            Model("payon_city/payon_to_gate001_a001.FBX","obj_payon_gate",new Vector3(-185,TerrainY(-185,-28),-28),15,80,forest,"payon_to_gate001_a001");
            Model("payon_city/payon_to_house001.FBX","obj_payon_bridge_pavilion",new Vector3(-183,TerrainY(-183,-116),-116),13,15,forest);
        }

        private static void Shrub(Vector3 p,float size)
        {
            for(int i=0;i<3;i++)
            {
                var c=p+new Vector3((i-1)*size*.6f,size*.35f,0);
                FoliageBall(c,new Vector3(size*.65f,size*.55f,size*.65f),i%2==0?"Shrub":"MeadowLeaf");
            }
        }
        private static void FoliageBall(Vector3 center,Vector3 scale,string material)
        {
            for(int j=0;j<5;j++)for(int i=0;i<9;i++)
            {
                float a=i*Mathf.PI*2/9,b=(i+1)*Mathf.PI*2/9,v=-Mathf.PI/2+j*Mathf.PI/5,w=v+Mathf.PI/5;
                var p0=center+Vector3.Scale(scale,new Vector3(Mathf.Sin(a)*Mathf.Cos(v),Mathf.Sin(v),Mathf.Cos(a)*Mathf.Cos(v)));
                var p1=center+Vector3.Scale(scale,new Vector3(Mathf.Sin(b)*Mathf.Cos(v),Mathf.Sin(v),Mathf.Cos(b)*Mathf.Cos(v)));
                var p2=center+Vector3.Scale(scale,new Vector3(Mathf.Sin(b)*Mathf.Cos(w),Mathf.Sin(w),Mathf.Cos(b)*Mathf.Cos(w)));
                var p3=center+Vector3.Scale(scale,new Vector3(Mathf.Sin(a)*Mathf.Cos(w),Mathf.Sin(w),Mathf.Cos(a)*Mathf.Cos(w)));
                Geo("garden_foliage",material).Quad(p0,p1,p2,p3);
            }
        }
        private static void Flowers(Vector3 p,float size,int color)
        {
            p.y=TerrainY(p.x,p.z)+.06f;
            for(int i=0;i<4;i++)
            {
                var q=p+new Vector3(Rand(-.65f,.65f),0,Rand(-.65f,.65f));
                Beam("flower_stems","DeepLeaf",q,q+Vector3.up*size,.045f);
                Disc("wildflowers",color==0?"FlowerWhite":color==1?"FlowerGold":"FlowerPink",q+Vector3.up*size,size*.32f,size*.32f);
            }
        }

        private static void Cylinder(string name,string material,Vector3 p,float bottom,float top,float h,int sides=24)
        {
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                var pa=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));var pb=new Vector3(Mathf.Sin(b),0,Mathf.Cos(b));
                Geo(name,material).Quad(p+pa*bottom,p+pb*bottom,p+pb*top+Vector3.up*h,p+pa*top+Vector3.up*h);
                Geo(name,material).Triangle(p+Vector3.up*h,p+pa*top+Vector3.up*h,p+pb*top+Vector3.up*h);
            }
        }
        private static void Ring(string name,string material,Vector3 p,float inner,float outer,float h,int sides=64)
        {
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                var pa=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));var pb=new Vector3(Mathf.Sin(b),0,Mathf.Cos(b));
                Geo(name,material).Quad(p+pa*outer+Vector3.up*h,p+pb*outer+Vector3.up*h,p+pb*inner+Vector3.up*h,p+pa*inner+Vector3.up*h);
                Geo(name,material).Quad(p+pa*outer,p+pb*outer,p+pb*outer+Vector3.up*h,p+pa*outer+Vector3.up*h);
                Geo(name,material).Quad(p+pb*inner,p+pa*inner,p+pa*inner+Vector3.up*h,p+pb*inner+Vector3.up*h);
            }
        }

        private static void CitadelTown()
        {
            Box("citadel_platform","PaleStone",new Vector3(0,39,102),new Vector3(174,6,157));
            Geo("town_paving","Cobble").Quad(new Vector3(-86,42.02f,27),new Vector3(-86,42.02f,178),new Vector3(86,42.02f,178),new Vector3(86,42.02f,27),.22f);
            // A true round fountain becomes the clear focal point of the half-circle drive.
            var fountain=new Vector3(0,42.05f,87);
            Ring("fountain_steps","PaleStone",fountain,0,13,.35f);
            Ring("fountain_basin","PaleStone",fountain+Vector3.up*.35f,9.8f,11.4f,1.2f);
            Disc("fountain_water","Water",fountain+Vector3.up*1.25f,9.9f,9.9f);
            Cylinder("fountain_column","PaleStone",fountain,2.4f,1.5f,5);
            Ring("fountain_tier","PaleStone",fountain+Vector3.up*4,0,5.2f,.6f);
            Disc("fountain_upper_water","Water",fountain+Vector3.up*4.65f,4.9f,4.9f);
            Cylinder("fountain_spire","PaleStone",fountain+Vector3.up*4.5f,1.2f,.4f,4);
            Cylinder("fountain_jet","Foam",fountain+Vector3.up*8.5f,.45f,.08f,2.2f,12);
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4;var dir=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));
                Waterfall(fountain+dir*4.8f+Vector3.up*4.7f,fountain+dir*7+Vector3.up*1.3f,.5f);
            }
            Ring("plaza_pattern","PaleStone",fountain+Vector3.up*.015f,17,18,.035f);
            Model("prontera_city/prontera_to_01_cathedral.FBX","obj_prontera_cathedral",new Vector3(46,42,143),43,180,town);
            Model("prontera_city/prontera_to_01_castle.FBX","obj_prontera_castle",new Vector3(-36,42,158),40,180,town);
            // Small footprint houses at their proper architectural scale create real streets.
            int index=0;
            for(float z=36;z<=172;z+=19)for(float x=-72;x<=72;x+=22)
            {
                var p=new Vector3(x+Rand(-2,2),42,z+Rand(-1,1));
                if(RoadDistance(p)<21)continue;
                if(Vector2.Distance(new Vector2(p.x,p.z),new Vector2(0,87))<25)continue;
                if(z>120&&(x>20||x<8))continue;
                float h=Rand(15,19);
                string asset=index%3==0?"pronterafield/pronterafield_house_03.FBX":"pronterafield/pronterafield_house_01.FBX";
                string part=index%3==0?"pronterafield_house_03_01":null;
                var house=Model(asset,"obj_prontera_residence_"+index,p,h,z<80?180:0,town,part);
                if(index%4==0)house.localScale=new Vector3(house.localScale.x*.8f,house.localScale.y,house.localScale.z*.85f);
                index++;
            }
            // An outer residential district beyond the race boulevard gives a layered skyline.
            for(int i=0;i<12;i++)
            {
                float x=-72+(i%6)*27,z=195+(i/6)*22;
                Model("pronterafield/pronterafield_house_01.FBX","obj_outer_roofline",new Vector3(x,TerrainY(x,z),z),Rand(17,24),i%2==0?180:0,town);
            }
            for(float x=-84;x<84;x+=10.5f)
            {CitadelWall(new Vector3(x+5.25f,42,26),0,10.5f);CitadelWall(new Vector3(x+5.25f,42,180),0,10.5f);}
            for(float z=31;z<180;z+=10)
            {if(Mathf.Abs(z-96)<17)continue;CitadelWall(new Vector3(-86,42,z),90,10);CitadelWall(new Vector3(86,42,z),90,10);}
            foreach(float x in new[]{-86f,86f})foreach(float z in new[]{26f,180f})CitadelTower(new Vector3(x,42,z),15);
            foreach(float x in new[]{-86f,86f})
            {float s=NearestDistance(new Vector3(x,42,96));var p=course.Point(s);p.y=42;ArchGate(p,Quaternion.LookRotation(course.Forward(s)).eulerAngles.y,false);}
            var gardens=new[]{new Vector3(-68,42,127),new Vector3(-8,42,131),new Vector3(68,42,119),new Vector3(25,42,108),new Vector3(-26,42,111),new Vector3(-68,42,57),new Vector3(66,42,55)};
            foreach(var p in gardens)
            {Ring("town_gardens","PaleStone",p,4,4.5f,.6f,24);Tree(p+Vector3.up*.4f,12);for(int k=0;k<6;k++){float a=k*Mathf.PI/3;Shrub(p+new Vector3(Mathf.Cos(a)*3,.5f,Mathf.Sin(a)*3),1.2f);}}
            for(int i=0;i<10;i++)
            {
                float a=i*Mathf.PI*2/10;var p=fountain+new Vector3(Mathf.Sin(a)*22,0,Mathf.Cos(a)*22);
                if(RoadDistance(p)<12)continue;
                Ring("planters","PaleStone",p,2.5f,3,.6f,16);Shrub(p+Vector3.up*.5f,2.2f);
                Lamp(p+new Vector3(3,0,0));
            }
            for(float s=0;s<course.Length;s+=16)
            {
                var p=course.Point(s);if(Mathf.Abs(p.x)>80||p.z<40)continue;
                for(int side=-1;side<=1;side+=2)Lamp(course.Point(s,side*12.2f));
            }
            for(int i=0;i<9;i++)
            {
                float x=-66+i*17;
                Market(new Vector3(x,42,35),i%2==0?"Red":"Blue",0);
            }
        }

        private static void CitadelWall(Vector3 p,float yaw,float width)
        {
            var q=Quaternion.Euler(0,yaw,0);
            Box("ramparts","PaleStone",p+Vector3.up*4.5f,new Vector3(width,9,3.2f),q);
            Box("wall_cornice","Stone",p+Vector3.up*8.5f,new Vector3(width,1,3.8f),q);
            for(float x=-width*.5f+1;x<width*.5f;x+=2.5f)Box("wall_battlements","PaleStone",p+q*new Vector3(x,10,0),new Vector3(1.4f,2,3.5f),q);
            Box("wall_buttresses","Stone",p+Vector3.up*3,new Vector3(1.6f,6,4.8f),q);
        }
        private static void CitadelTower(Vector3 p,float h)
        {
            Cylinder("citadel_towers","PaleStone",p,5.5f,5,h,16);
            Cylinder("tower_cornice","Stone",p+Vector3.up*(h-1.3f),5.2f,6,1.3f,16);
            Ring("tower_roof","Cobble",p+Vector3.up*h,0,5.7f,.2f,16);
            for(int i=0;i<12;i++)
            {
                var q=Quaternion.Euler(0,i*30,0);
                Box("tower_crenels","PaleStone",p+q*new Vector3(0,h+1,5.2f),new Vector3(1.6f,2,1.6f),q);
                if(i%3==0)Box("tower_windows","Dark",p+q*new Vector3(0,h-5,5.05f),new Vector3(.55f,2,.12f),q);
            }
        }
        private static void ArchGate(Vector3 p,float yaw,bool sand)
        {
            var q=Quaternion.Euler(0,yaw,0);string material=sand?"StrataLight":"PaleStone";
            if(!sand){CitadelTower(p+q*new Vector3(-17,0,0),22);CitadelTower(p+q*new Vector3(17,0,0),22);}
            for(int side=-1;side<=1;side+=2)
            {Box("arch_piers",material,p+q*new Vector3(side*13,4,0),new Vector3(4,8,5),q);}
            for(int i=0;i<18;i++)
            {
                float a=i*Mathf.PI/18,b=(i+1)*Mathf.PI/18;
                Vector3 pa=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0),pb=new Vector3(Mathf.Cos(b),Mathf.Sin(b),0);
                var ca=p+Vector3.up*8;
                var a0=ca+q*(pa*11+Vector3.back*2.5f);var b0=ca+q*(pb*11+Vector3.back*2.5f);
                var a1=ca+q*(pa*14+Vector3.back*2.5f);var b1=ca+q*(pb*14+Vector3.back*2.5f);
                var depth=q*Vector3.forward*5;
                Geo("gate_voussoirs",material).Quad(a0,b0,b1,a1);
                Geo("gate_voussoirs",material).Quad(b0+depth,a0+depth,a1+depth,b1+depth);
                Geo("gate_intrados",material).Quad(b0,a0,a0+depth,b0+depth);
                Geo("gate_voussoirs",material).Quad(a1,b1,b1+depth,a1+depth);
            }
            if(!sand)
            {
                Box("gate_crown",material,p+Vector3.up*22,new Vector3(27,2,5),q);
                for(int i=-5;i<=5;i++)Box("gate_crenels",material,p+q*new Vector3(i*2.4f,24,0),new Vector3(1.4f,2,5),q);
                for(int side=-1;side<=1;side+=2)Banner(p+q*new Vector3(side*17,15,-5.7f),q,4,7);
            }
        }
        private static void Banner(Vector3 p,Quaternion q,float w,float h)
        {
            Box("banner_poles","Gold",p+Vector3.up*.3f,new Vector3(w+.5f,.15f,.15f),q);
            Geo("royal_banners","Blue").Quad(p+q*new Vector3(-w/2,0,0),p+q*new Vector3(-w/2,-h+.8f,0),p+q*new Vector3(w/2,-h+.8f,0),p+q*new Vector3(w/2,0,0));
            Geo("royal_banners","Blue").Triangle(p+q*new Vector3(-w/2,-h+.8f,0),p+Vector3.down*h,p+q*new Vector3(w/2,-h+.8f,0));
            Beam("banner_crest","Ivory",p+q*new Vector3(0,-1.6f,-.04f),p+q*new Vector3(0,-h+1.5f,-.04f),w*.1f);
            Beam("banner_crest","Ivory",p+q*new Vector3(-w*.25f,-h*.48f,-.04f),p+q*new Vector3(w*.25f,-h*.48f,-.04f),w*.1f);
        }
        private static void Lamp(Vector3 p)
        {
            Cylinder("lamp_bases","PaleStone",p,.65f,.4f,.8f,8);
            Box("lampposts","Dark",p+Vector3.up*2.7f,new Vector3(.18f,4,.18f));
            Box("lamp_glass","FlowerGold",p+Vector3.up*4.9f,new Vector3(.7f,.8f,.7f));
            Cylinder("lamp_hats","Dark",p+Vector3.up*5.3f,.7f,0,.6f,4);
        }
        private static void Market(Vector3 p,string color,float yaw)
        {
            var q=Quaternion.Euler(0,yaw,0);
            Box("market_counters","Wood",p+Vector3.up*.7f,new Vector3(5,1.4f,2.4f),q);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Box("stall_posts","Wood",p+q*new Vector3(x*2.5f,2,z*1.4f),new Vector3(.15f,4,.15f),q);
            for(int i=0;i<8;i++)
            {
                float x=-3+i*.75f;
                Geo("striped_awning",i%2==0?color:"Ivory").Quad(p+q*new Vector3(x,3.5f,-2),p+q*new Vector3(x,4.4f,0),p+q*new Vector3(x+.75f,4.4f,0),p+q*new Vector3(x+.75f,3.5f,-2));
                Geo("striped_awning",i%2==0?color:"Ivory").Quad(p+q*new Vector3(x,4.4f,0),p+q*new Vector3(x,3.5f,2),p+q*new Vector3(x+.75f,3.5f,2),p+q*new Vector3(x+.75f,4.4f,0));
            }
            for(int i=0;i<4;i++)Cylinder("market_produce",i%2==0?"Red":"FlowerGold",p+q*new Vector3(-1.8f+i*1.2f,1.4f,0),.45f,.4f,.5f,7);
        }

        private static void SandstoneDesert()
        {
            // Large mesas create a canyon skyline; small rubble is reserved for the track edge.
            Vector3[] sites={new Vector3(218,28,40),new Vector3(227,27,-25),new Vector3(222,11,-90),new Vector3(238,42,112),new Vector3(197,42,173),new Vector3(119,42,193),new Vector3(92,0,-38)};
            for(int i=0;i<sites.Length;i++)Mesa(sites[i],i==6?18:Rand(22,34),Rand(20,32),i==6?29:Rand(24,49),true,i*53);
            for(int i=0;i<60;i++)
            {
                float x=Rand(100,247),z=Rand(-130,160),h=Rand(2,6);var p=new Vector3(x,TerrainY(x,z)-.4f,z);
                if(RoadDistance(p)<12+h*1.5f||Vector2.Distance(new Vector2(x,z),new Vector2(157,-4))<26)continue;
                Rock(p,h,true);
            }
            for(int i=0;i<36;i++)
            {
                float a=i*2.39996f;float radius=Rand(25,40);var p=new Vector3(157+Mathf.Cos(a)*radius,0,-4+Mathf.Sin(a)*radius);
                if(RoadDistance(p)<18)continue;p.y=TerrainY(p.x,p.z);
                Model("housing_outside_town/town_morocc/housing_outside_town_morocc_tree01.FBX","obj_oasis_palm",p,Rand(12,19),Rand(0,360),desert);
                Shrub(p,2);
            }
            for(int i=0;i<16;i++)
            {
                var p=new Vector3(Rand(98,224),0,Rand(-120,120));if(RoadDistance(p)<14||Vector2.Distance(new Vector2(p.x,p.z),new Vector2(157,-4))<27)continue;p.y=TerrainY(p.x,p.z);
                float h=Rand(3,5);Cylinder("cactus","DeepLeaf",p,.45f,.35f,h,9);
                for(int side=-1;side<=1;side+=2)
                {Beam("cactus_arms","DeepLeaf",p+Vector3.up*h*.4f,p+new Vector3(side*.9f,h*.4f,0),.5f);Cylinder("cactus","DeepLeaf",p+new Vector3(side*.9f,h*.4f,0),.3f,.22f,h*.35f,8);}
            }
            // Ruined avenue and gateway stand beside the desert's upper sweeper.
            for(int i=0;i<8;i++)
            {
                float x=181+(i%2)*17,z=70+(i/2)*22;
                var p=new Vector3(x,TerrainY(x,z),z);
                Model("pyramid_dn/prmd_dn_brickpillar001.FBX","obj_ruin_obelisk",p,Rand(13,21),0,desert,"prmd_dn_brickpillar001");
                Box("ruin_bases","StrataLight",p+Vector3.up*.6f,new Vector3(7,1.2f,7));
                if(i%2==0)Box("ruin_lintels","Strata",p+new Vector3(8.5f,17,0),new Vector3(24,3.5f,5));
            }
            for(int i=0;i<9;i++)
            {var p=new Vector3(168+i*7,42,145);Model("desert_city2/morocc_wall.FBX","obj_temple_ruin",p,10,180,desert);}
            SteppedPyramid(new Vector3(220,42,185),66,51);
            SteppedPyramid(new Vector3(153,42,217),39,30);
            float archDistance=NearestDistance(new Vector3(161,0,67));
            ArchGate(course.Point(archDistance)-Vector3.up*.16f,Quaternion.LookRotation(course.Forward(archDistance)).eulerAngles.y,true);
            Model("desert_city2/morocc_city_smithy.FBX","obj_oasis_smithy",new Vector3(198,TerrainY(198,25),25),16,-90,desert);
            Market(new Vector3(185,TerrainY(185,-24),-24),"Red",-45);
            Model("desert_city2/ushib_tent001_city.FBX","obj_caravan_tent",new Vector3(207,TerrainY(207,-4),-4),10,-90,desert);
            for(float s=0;s<course.Length;s+=9)
            {
                var p=course.Point(s);if(p.x<100||p.z< -112)continue;
                for(int side=-1;side<=1;side+=2)
                {var a=course.Point(s,side*12.4f);a.y=TerrainY(a.x,a.z);if(Vector2.Distance(new Vector2(a.x,a.z),new Vector2(157,-4))>25)Rock(a,Rand(1,2.8f),true);}
            }
        }
        private static void SteppedPyramid(Vector3 p,float width,float h)
        {
            for(int i=0;i<24;i++)
            {
                float t=i/24f;float size=width*(1-t);
                Box("pyramid_courses",i%3==0?"StrataLight":"Sandstone",p+Vector3.up*(h*(t+1/48f)),new Vector3(size,h/24,size));
            }
            Box("pyramid_portal","Dark",p+new Vector3(0,3.5f,-width*.5f-.03f),new Vector3(5,7,.2f));
        }
        private static void FestivalDetails()
        {
            var p=course.Point(0);var f=course.Forward(0);var q=Quaternion.LookRotation(f);var r=q*Vector3.right;
            for(int side=-1;side<=1;side+=2)
            {
                Cylinder("start_columns","PaleStone",p+r*side*12,1.2f,.9f,9,8);
                Cylinder("start_finials","Gold",p+r*side*12+Vector3.up*9,1.1f,0,2,8);
                Banner(p+r*side*12+Vector3.up*7-f*1.25f,q,2.4f,4);
            }
            Box("start_sign_back","Wood",p+Vector3.up*9,new Vector3(25,2.8f,.65f),q);
            Box("start_sign_face","Ivory",p+Vector3.up*9-f*.36f,new Vector3(22,2.1f,.08f),q);
            Sign("START / FINISH",p+Vector3.up*9-f*.43f,q,.95f,root);
            var label=root.Find("txt_START / FINISH");if(label!=null)label.GetComponent<TextMesh>().color=ColorHtml("5B4028");
            for(int x=0;x<18;x++)for(int z=0;z<2;z++)Box("finish_checkers",(x+z)%2==0?"Ivory":"Dark",p+r*(x-8.5f)+f*(z-.5f)+Vector3.up*.045f,new Vector3(1,.04f,1),q);
            for(int i=0;i<70;i++)
            {
                float x=Rand(-180,160),z=Rand(-201,-158);var t=new Vector3(x,TerrainY(x,z),z);
                if(new Vector2(x/270,(z-18)/225).magnitude>.95f)continue;
                Tree(t,Rand(10,22));
            }
        }
    }
}
