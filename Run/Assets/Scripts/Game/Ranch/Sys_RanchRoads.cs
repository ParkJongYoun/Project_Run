using UnityEngine;

namespace ProjectRun.Ranch
{
    // The same road placements drive the painted ground and resident navigation.
    [ExecuteAlways]
    public sealed class Sys_RanchRoads : MonoBehaviour
    {
        public Sys_RanchPlacement Placement;
        private Sys_RanchPlacement subscribed;
        private Texture2D mask;
        private int layoutHash=int.MinValue;

        private void OnEnable() => Initialize();
        private void Start() => Initialize();
        public void Initialize()
        {
            if(subscribed!=Placement)
            {
                if(subscribed!=null)subscribed.Changed-=Refresh;
                subscribed=Placement;
                if(subscribed!=null)subscribed.Changed+=Refresh;
            }
            layoutHash=int.MinValue;Refresh();
        }
        public void Refresh()
        {
            if(Placement==null || Placement.Catalog==null)return;
            var layout=Application.isPlaying&&Placement.Layout!=null?Placement.Layout:Placement.InitialLayout;
            if(layout==null)return;
            int hash=17;
            unchecked
            {
                hash=hash*31+Placement.GridSize.GetHashCode();
                hash=hash*31+Placement.GridOrigin.GetHashCode();
                hash=hash*31+Placement.CellSize.GetHashCode();
                foreach(var item in layout.Facilities)
                {
                    var definition=Placement.Catalog.Find(item.FacilityId);
                    if(definition==null || !definition.IsRoad || IsMoving(item))continue;
                    hash=hash*31+item.X;hash=hash*31+item.Y;hash=hash*31+item.Rotation;
                }
            }
            if(mask!=null && hash==layoutHash)return;
            layoutHash=hash;
            int width=Placement.GridSize.x,height=Placement.GridSize.y;
            if(mask==null || mask.width!=width || mask.height!=height)
            {
                Release();
                mask=new Texture2D(width,height,TextureFormat.RGBA32,false,true)
                    { name="RanchRoadMask",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.DontSave };
            }
            var pixels=new Color32[width*height];
            foreach(var item in layout.Facilities)
            {
                var definition=Placement.Catalog.Find(item.FacilityId);
                if(definition==null || !definition.IsRoad || IsMoving(item))continue;
                var size=definition.Size(item.Rotation);
                for(int y=Mathf.Max(0,item.Y);y<Mathf.Min(height,item.Y+size.y);y++)
                    for(int x=Mathf.Max(0,item.X);x<Mathf.Min(width,item.X+size.x);x++)pixels[y*width+x]=new Color32(255,255,255,255);
            }
            mask.SetPixels32(pixels);mask.Apply(false,false);
            Shader.SetGlobalTexture("_RanchRoadMask",mask);
            Shader.SetGlobalVector("_RanchRoadGrid",new Vector4(Placement.GridOrigin.x,Placement.GridOrigin.z,Placement.CellSize,0));
            Shader.SetGlobalVector("_RanchRoadSize",new Vector4(width,height,0,0));
        }
        private bool IsMoving(Data_RanchPlacement item) => Application.isPlaying && Placement.IsMoving && Placement.Preview.InstanceId==item.InstanceId;
        private void Release()
        {
            if(mask==null)return;
            if(Application.isPlaying)Destroy(mask);else DestroyImmediate(mask);
            mask=null;
        }
        private void OnDisable()
        {
            if(subscribed!=null)subscribed.Changed-=Refresh;
            subscribed=null;
            Shader.SetGlobalTexture("_RanchRoadMask",Texture2D.blackTexture);
            Shader.SetGlobalVector("_RanchRoadGrid",Vector4.zero);
            Release();
        }
    }
}
