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
            Add(layout, "house", 1, 12);
            Add(layout, "shelter", 5, 12);
            Add(layout, "feeding_station", 10, 12);
            Add(layout, "windmill", 14, 13);
            Add(layout, "waterfall", 17, 12);
            Add(layout, "hot_spring", 1, 5);
            Add(layout, "gazebo", 4, 5);
            Add(layout, "race_track", 13, 5, 2);
            Add(layout, "training", 1, 0, 2);
            Add(layout, "feed_store", 6, 1);
            Add(layout, "flower_cart", 14, 2);
            Add(layout, "fountain", 9, 8);
            Add(layout, "broadleaf", 0, 10);
            Add(layout, "flowerbed", 1, 9);
            Add(layout, "flowerbed", 4, 9);
            Add(layout, "flowerbed", 2, 15);
            Add(layout, "flowerbed", 6, 15);
            Add(layout, "flowerbed", 14, 15);
            Add(layout, "flowerbed", 17, 15);
            Add(layout, "lamp", 0, 4);
            Add(layout, "lamp", 5, 0);
            Add(layout, "lamp", 12, 3);
            Add(layout, "lamp", 12, 10);
            Add(layout, "lamp", 9, 12);
            Add(layout, "lamp", 16, 12);
            Add(layout, "lamp", 0, 12);
            // Narrow connected paths retain grass around the central fountain.
            var roads = new HashSet<Vector2Int>();
            for (int y = 0; y <= 7; y++) roads.Add(new Vector2Int(9, y));
            for (int x = 1; x <= 18; x++) roads.Add(new Vector2Int(x, 4));
            for (int x = 2; x <= 18; x++) roads.Add(new Vector2Int(x, 11));
            for (int x = 8; x <= 11; x++)
            { roads.Add(new Vector2Int(x, 7)); roads.Add(new Vector2Int(x, 10)); }
            for (int y = 7; y <= 10; y++)
            { roads.Add(new Vector2Int(8, y)); roads.Add(new Vector2Int(11, y)); }
            for (int x = 6; x <= 9; x++) roads.Add(new Vector2Int(x, 0));
            roads.Add(new Vector2Int(14, 3)); roads.Add(new Vector2Int(15, 3));
            roads.Add(new Vector2Int(14, 12)); roads.Add(new Vector2Int(15, 12));
            for (int y = 0; y < 16; y++) for (int x = 0; x < 20; x++)
                if (roads.Contains(new Vector2Int(x, y))) Add(layout, "road", x, y);
            return layout;
        }

        private static void Add(Data_RanchSave layout, string id, int x, int y, int rotation = 0)
        {
            layout.Facilities.Add(new Data_RanchPlacement
            { InstanceId = "garden_" + id + "_" + x + "_" + y, FacilityId = id, X = x, Y = y, Rotation = rotation });
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
