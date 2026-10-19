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
            Add(layout, "feed_store", 10, 12);
            Add(layout, "windmill", 14, 13);
            Add(layout, "waterfall", 17, 12);
            Add(layout, "race_track", 1, 5);
            Add(layout, "training", 1, 0, 2);
            Add(layout, "feeding_station", 6, 0, 3);
            Add(layout, "flower_cart", 14, 3);
            Add(layout, "fountain", 9, 8);
            Add(layout, "gazebo", 13, 5);
            Add(layout, "hot_spring", 16, 5);
            Add(layout, "broadleaf", 0, 10);
            Add(layout, "pine", 18, 8);
            Add(layout, "broadleaf", 13, 8);
            Add(layout, "pine", 16, 1);
            Add(layout, "broadleaf", 12, 1);
            Add(layout, "pine", 16, 8);
            Add(layout, "broadleaf", 18, 1);
            Add(layout, "flowerbed", 1, 9);
            Add(layout, "flowerbed", 4, 9);
            Add(layout, "flowerbed", 2, 15);
            Add(layout, "flowerbed", 6, 15);
            Add(layout, "flowerbed", 10, 15);
            Add(layout, "flowerbed", 14, 15);
            Add(layout, "flowerbed", 17, 15);
            Add(layout, "flowerbed", 14, 2);
            Add(layout, "lamp", 0, 4);
            Add(layout, "lamp", 5, 0);
            Add(layout, "lamp", 12, 3);
            Add(layout, "lamp", 12, 9);
            Add(layout, "lamp", 9, 12);
            Add(layout, "lamp", 16, 12);
            Add(layout, "lamp", 0, 12);
            Add(layout, "rock", 0, 8);
            Add(layout, "rock", 0, 9);
            Add(layout, "rock", 19, 5);
            Add(layout, "rock", 19, 7);
            Add(layout, "rock", 15, 8);
            Add(layout, "rock", 16, 15);
            Add(layout, "flowerbed", 12, 0);
            Add(layout, "flowerbed", 16, 0);
            Add(layout, "flowerbed", 18, 0);
            Add(layout, "fence", 0, 1, 1);
            Add(layout, "fence", 0, 2, 1);
            Add(layout, "fence", 0, 3, 1);
            // One connected walking network: entrance, fountain loop, facility fronts and north promenade.
            var roads = new[]
            {
                new Vector2Int(9,0), new Vector2Int(10,0), new Vector2Int(9,1), new Vector2Int(10,1), new Vector2Int(9,2), new Vector2Int(10,2), new Vector2Int(9,3), new Vector2Int(10,3),
                new Vector2Int(9,4), new Vector2Int(10,4), new Vector2Int(9,5), new Vector2Int(10,5), new Vector2Int(9,6), new Vector2Int(10,6), new Vector2Int(9,7), new Vector2Int(10,7),
                new Vector2Int(2,10), new Vector2Int(3,10), new Vector2Int(4,10), new Vector2Int(5,10), new Vector2Int(6,10), new Vector2Int(7,10), new Vector2Int(8,10), new Vector2Int(9,10),
                new Vector2Int(10,10), new Vector2Int(11,10), new Vector2Int(12,10), new Vector2Int(13,10), new Vector2Int(14,10), new Vector2Int(15,10), new Vector2Int(16,10), new Vector2Int(17,10),
                new Vector2Int(18,10), new Vector2Int(2,11), new Vector2Int(3,11), new Vector2Int(4,11), new Vector2Int(5,11), new Vector2Int(6,11), new Vector2Int(7,11), new Vector2Int(8,11),
                new Vector2Int(9,11), new Vector2Int(10,11), new Vector2Int(11,11), new Vector2Int(12,11), new Vector2Int(13,11), new Vector2Int(14,11), new Vector2Int(15,11), new Vector2Int(16,11),
                new Vector2Int(17,11), new Vector2Int(18,11), new Vector2Int(1,4), new Vector2Int(2,4), new Vector2Int(3,4), new Vector2Int(4,4), new Vector2Int(5,4), new Vector2Int(6,4),
                new Vector2Int(7,4), new Vector2Int(8,4), new Vector2Int(11,4), new Vector2Int(12,4), new Vector2Int(13,4), new Vector2Int(14,4), new Vector2Int(15,4), new Vector2Int(16,4),
                new Vector2Int(17,4), new Vector2Int(18,4), new Vector2Int(7,5), new Vector2Int(8,5), new Vector2Int(7,6), new Vector2Int(8,6), new Vector2Int(7,7), new Vector2Int(8,7),
                new Vector2Int(7,8), new Vector2Int(8,8), new Vector2Int(7,9), new Vector2Int(8,9), new Vector2Int(11,5), new Vector2Int(11,6), new Vector2Int(11,7), new Vector2Int(11,8),
                new Vector2Int(11,9), new Vector2Int(14,12), new Vector2Int(15,12), new Vector2Int(5,3), new Vector2Int(6,3), new Vector2Int(7,3), new Vector2Int(8,3), new Vector2Int(8,0),
                new Vector2Int(8,1), new Vector2Int(8,2),
            };
            foreach (var cell in roads) Add(layout, "road", cell.x, cell.y);
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
