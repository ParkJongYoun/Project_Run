using System.Collections;
using UnityEngine;

namespace ProjectRun.Race
{
    public sealed class UIItem_ArenaExhibition : MonoBehaviour
    {
        [SerializeField] private float LapSeconds = 32f;
        private static readonly string[] monsterIds = { "Minorous", "GiantPoring", "Smokie", "Savage", "Lunatic", "Edgar" };
        // Cubic control points measured on the 1672 x 941 arena illustration, from its top-left.
        // This is the dirt course, with long straights and separate end bends, not an ellipse.
        private static readonly Vector2[] course =
        {
            new Vector2(435,345), new Vector2(690,342), new Vector2(980,342), new Vector2(1225,345),
            new Vector2(1390,347), new Vector2(1515,413), new Vector2(1537,488),
            new Vector2(1570,592), new Vector2(1445,642), new Vector2(1190,648),
            new Vector2(980,655), new Vector2(730,650), new Vector2(520,643),
            new Vector2(285,637), new Vector2(111,585), new Vector2(138,491),
            new Vector2(150,411), new Vector2(294,345), new Vector2(435,345)
        };
        private const int SampleCount = 384;
        private readonly float[] distances = new float[SampleCount + 1];
        private readonly UnityEngine.UI.Image[] runners = new UnityEngine.UI.Image[6];
        private readonly Data_DirectionalRunSprites[] sets = new Data_DirectionalRunSprites[6];
        private readonly int[] directions = new int[6];
        private readonly int[] depthOrder = { 0, 1, 2, 3, 4, 5 };
        private readonly float[] depths = new float[6];
        private RectTransform courseRect;
        private float elapsed;
        private float courseLength;

        private IEnumerator Start()
        {
            courseRect=(RectTransform)transform;
            Vector2 previous=CoursePoint(0);
            for(int sample=1;sample<=SampleCount;sample++)
            {
                Vector2 point=CoursePoint((float)sample/SampleCount);
                distances[sample]=distances[sample-1]+Vector2.Distance(previous,point);previous=point;
            }
            courseLength=distances[SampleCount];
            for(int i=0;i<runners.Length;i++)
            {
                var request=Resources.LoadAsync<Data_DirectionalRunSprites>("Race2D/AutoSprite/Run_"+monsterIds[i]);
                yield return request;
                sets[i]=request.asset as Data_DirectionalRunSprites;
                if(sets[i]==null)continue;
                runners[i]=DemoUI.Image(transform,"img_runner_"+i,null,0,0,1,1);
                runners[i].gameObject.SetActive(false);
                directions[i]=-1;
            }
        }
        private void LateUpdate()
        {
            if(courseRect==null||courseLength<=0)return;
            elapsed+=Time.unscaledDeltaTime;
            for(int i=0;i<runners.Length;i++)
            {
                var image=runners[i];var set=sets[i];if(image==null||set==null)continue;
                float lap=.57f+elapsed/Mathf.Max(1,LapSeconds)-i*.009f+Mathf.Sin(elapsed*.23f+i)*.002f;
                float parameter=DistanceParameter(Mathf.Repeat(lap,1)*courseLength);
                Vector2 point=CoursePoint(parameter);
                Vector2 tangent=(CoursePoint(Mathf.Repeat(parameter+.0005f,1))-CoursePoint(Mathf.Repeat(parameter-.0005f,1))).normalized;
                float perspective=Mathf.InverseLerp(345,650,point.y);
                // Narrow lanes on the far straight become wider toward the viewer.
                point+=new Vector2(-tangent.y,tangent.x)*((i%3-1)*Mathf.Lerp(3,9,perspective));
                float heading=Mathf.Atan2(tangent.x,-tangent.y)*Mathf.Rad2Deg;
                directions[i]=set.SelectDirection(heading,directions[i],directions[i]<0);
                int count=set.FrameCount(directions[i]);
                int frame=(int)((elapsed+i*.137f)/Mathf.Max(.1f,set.ClipDuration)*count)%count;
                var sprite=set.Frame(directions[i],frame);if(sprite==null)continue;
                image.sprite=sprite;
                var rect=image.rectTransform;
                // uGUI does not apply a SpriteRenderer's ground pivot automatically.
                rect.pivot=new Vector2(sprite.pivot.x/sprite.rect.width,sprite.pivot.y/sprite.rect.height);
                rect.anchoredPosition=new Vector2((point.x/1672f-.5f)*courseRect.rect.width,(.5f-point.y/941f)*courseRect.rect.height);
                float size=Mathf.Lerp(29,48,perspective)*courseRect.rect.width/1672f;
                rect.sizeDelta=new Vector2(size,size);depths[i]=point.y;
                image.gameObject.SetActive(true);
            }
            // Only six runners: insertion sort keeps nearer feet in front without per-frame allocations.
            for(int i=1;i<depthOrder.Length;i++)
            {
                int value=depthOrder[i],j=i-1;
                while(j>=0&&depths[depthOrder[j]]>depths[value]){depthOrder[j+1]=depthOrder[j];j--;}
                depthOrder[j+1]=value;
            }
            for(int i=0;i<depthOrder.Length;i++)if(runners[depthOrder[i]]!=null)runners[depthOrder[i]].transform.SetSiblingIndex(i);
        }
        private float DistanceParameter(float distance)
        {
            int low=0,high=SampleCount;
            while(high-low>1){int middle=(low+high)/2;if(distances[middle]<distance)low=middle;else high=middle;}
            return (low+Mathf.InverseLerp(distances[low],distances[high],distance))/SampleCount;
        }
        private static Vector2 CoursePoint(float parameter)
        {
            float scaled=Mathf.Clamp01(parameter)*6;
            int segment=Mathf.Min(5,(int)scaled);float t=scaled-segment,u=1-t;int first=segment*3;
            return u*u*u*course[first]+3*u*u*t*course[first+1]+3*u*t*t*course[first+2]+t*t*t*course[first+3];
        }
    }
}
