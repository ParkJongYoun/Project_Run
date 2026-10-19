using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectRun.Ranch
{
    [Serializable]
    public sealed class Data_RanchFacility
    {
        public string Id;
        public string Name;
        [TextArea] public string Description;
        public Vector2Int Footprint = Vector2Int.one;
        public GameObject Prefab;
        public Sprite Portrait;
        public string Category;
        public bool IsRoad;
        public Vector2Int Size(int rotation) => (rotation & 1) == 0 ? Footprint : new Vector2Int(Footprint.y, Footprint.x);
    }

    public sealed class Data_RanchCatalog : ScriptableObject
    {
        public Data_RanchFacility[] Facilities;
        public Data_RanchFacility Find(string id) => Array.Find(Facilities, item => item.Id == id);
    }

    [Serializable]
    public sealed class Data_RanchPlacement
    {
        public string InstanceId;
        public string FacilityId;
        public int X;
        public int Y;
        public int Rotation;
        public Data_RanchPlacement Copy() => (Data_RanchPlacement)MemberwiseClone();
    }

    [Serializable]
    public sealed class Data_RanchSave
    {
        public int Version = 1;
        public List<Data_RanchPlacement> Facilities = new List<Data_RanchPlacement>();
    }

    public static class RanchDemoLayout
    {
        public static Data_RanchSave Create()
        {
            var layout = new Data_RanchSave();
            for (int x=0;x<20;x++) layout.Facilities.Add(new Data_RanchPlacement
                { InstanceId="initial_road_"+x, FacilityId="road", X=x, Y=7 });
            layout.Facilities.Add(new Data_RanchPlacement { InstanceId="initial_shelter", FacilityId="shelter", X=3, Y=9 });
            layout.Facilities.Add(new Data_RanchPlacement { InstanceId="initial_feed_store", FacilityId="feed_store", X=12, Y=9 });
            return layout;
        }
    }

    // Grid rules have no dependency on camera, frame rate or preview rendering.
    public static class RanchGrid
    {
        public static bool CanPlace(Data_RanchCatalog catalog, IList<Data_RanchPlacement> placed,
            Data_RanchPlacement candidate, Vector2Int bounds, out string reason)
        {
            reason = "";
            if (candidate == null || candidate.Rotation < 0 || candidate.Rotation > 3)
            { reason = "배치 정보를 확인해 주세요"; return false; }
            var definition = catalog.Find(candidate.FacilityId);
            if (definition == null) { reason = "시설 정보를 찾을 수 없어요"; return false; }
            var size = definition.Size(candidate.Rotation);
            if (candidate.X < 0 || candidate.Y < 0 || candidate.X > bounds.x - size.x || candidate.Y > bounds.y - size.y)
            { reason = "잠긴 구역에는 배치할 수 없어요"; return false; }
            foreach (var other in placed)
            {
                if (other.InstanceId == candidate.InstanceId) continue;
                var otherDefinition = catalog.Find(other.FacilityId);
                if (otherDefinition == null) continue;
                var otherSize = otherDefinition.Size(other.Rotation);
                if (candidate.X < other.X + otherSize.x && candidate.X + size.x > other.X &&
                    candidate.Y < other.Y + otherSize.y && candidate.Y + size.y > other.Y)
                { reason = "다른 시설과 겹쳐요"; return false; }
            }
            return true;
        }

        public static bool Validate(Data_RanchCatalog catalog, Data_RanchSave save, Vector2Int bounds)
        {
            if (save == null || save.Version != 1 || save.Facilities == null || save.Facilities.Count > 256) return false;
            var ids = new HashSet<string>();
            foreach (var entry in save.Facilities)
                if (entry == null || string.IsNullOrWhiteSpace(entry.InstanceId) || !ids.Add(entry.InstanceId) ||
                    !CanPlace(catalog, save.Facilities, entry, bounds, out _)) return false;
            return true;
        }
    }
}
