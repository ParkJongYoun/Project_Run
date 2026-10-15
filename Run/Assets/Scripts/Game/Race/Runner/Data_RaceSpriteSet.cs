using System;
using UnityEngine;

namespace ProjectRun.Race
{
    public enum E_RaceSpriteAngle { Side, Rear, Top }
    public enum E_RaceArtMode { SpritesInWorld, AllSprites }

    [Serializable]
    public sealed class Data_RaceSpriteClip
    {
        public Sprite[] Frames = Array.Empty<Sprite>();
        public float[] Durations = Array.Empty<float>();
        public float Duration;
        public Texture2D[] Pages = Array.Empty<Texture2D>();
        public int FrameCount;
        public int CellSize = 800;
        public int Columns = 5;
        public int FramesPerPage = 25;
        public float PixelsPerUnit;
        public Vector2 Pivot;
        [NonSerialized] private Sprite[] generatedFrames;

        private Sprite[] GetFrames()
        {
            if (Frames != null && Frames.Length > 0) return Frames;
            if (generatedFrames != null && generatedFrames.Length == FrameCount && generatedFrames[0] != null) return generatedFrames;
            if (Pages == null || Pages.Length == 0 || FrameCount <= 0 || PixelsPerUnit <= 0) return null;
            generatedFrames = new Sprite[FrameCount];
            for (int i = 0; i < FrameCount; i++)
            {
                int pageIndex = i / FramesPerPage, cell = i % FramesPerPage;
                if (pageIndex >= Pages.Length || Pages[pageIndex] == null) continue;
                var page = Pages[pageIndex];
                var rect = new Rect(cell % Columns * CellSize, page.height - (cell / Columns + 1) * CellSize, CellSize, CellSize);
                generatedFrames[i] = Sprite.Create(page, rect, Pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
                generatedFrames[i].name = page.name + "_" + i.ToString("00");
            }
            return generatedFrames;
        }

        public void Release()
        {
            if (generatedFrames == null) return;
            foreach (var frame in generatedFrames)
            {
                if (frame == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(frame);
                else UnityEngine.Object.DestroyImmediate(frame);
            }
            generatedFrames = null;
        }

        public Sprite Sample(double clock)
        {
            var frames = GetFrames();
            if (frames == null || frames.Length == 0 || Duration <= 0) return null;
            float age = (float)(clock % Duration);
            for (int i = 0; i < frames.Length; i++)
            {
                age -= Durations != null && i < Durations.Length ? Durations[i] : Duration / frames.Length;
                if (age < 0) return frames[i];
            }
            return frames[frames.Length - 1];
        }
    }

    [CreateAssetMenu(menuName = "Project Run/Race sprite set")]
    public sealed class Data_RaceSpriteSet : ScriptableObject
    {
        public Sprite[] StillViews = new Sprite[3];
        public Data_RaceSpriteClip[] RunViews = new Data_RaceSpriteClip[3];
        public float WorldHeight = 4.2f;

        private void OnDisable()
        {
            if (RunViews == null) return;
            foreach (var clip in RunViews) clip?.Release();
        }

        public Sprite GetFrame(E_RaceSpriteAngle angle, double clock, bool running)
        {
            int index = (int)angle;
            if (running && RunViews != null && index < RunViews.Length && RunViews[index] != null)
            {
                var frame = RunViews[index].Sample(clock);
                if (frame != null) return frame;
            }
            return StillViews != null && index < StillViews.Length ? StillViews[index] : null;
        }
    }
}

