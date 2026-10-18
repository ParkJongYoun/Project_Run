using System;
using UnityEngine;

namespace ProjectRun.Race
{
    [CreateAssetMenu(menuName = "Project Run/Directional run sprites")]
    public sealed class Data_DirectionalRunSprites : ScriptableObject
    {
        public string CharacterName;
        public string SourcePrefabPath;
        public int DirectionCount = 16;
        public int FramesPerDirection = 12;
        public int[] DirectionFrameCounts = Array.Empty<int>();
        public float ClipDuration;
        public float CapturePitch = 38;
        public float CaptureYaw = 35;
        [Tooltip("Directions in screen space, clockwise from north: N, NE, E, SE, S, SW, W, NW.")]
        public bool ScreenSpaceDirections;
        [Tooltip("Optional clockwise screen bearings. Isometric diagonals are projected using the art's capture pitch.")]
        public float[] DirectionAngles = Array.Empty<float>();
        public float WorldSize;
        public Vector2 GroundPivot;
        public Material SpriteMaterial;
        public Sprite[] Frames = Array.Empty<Sprite>();

        public Sprite Frame(int direction, int frame)
        {
            direction = (direction % DirectionCount + DirectionCount) % DirectionCount;
            int count = FrameCount(direction);
            int index = direction * FramesPerDirection + (frame % count + count) % count;
            return index < Frames.Length ? Frames[index] : null;
        }

        public int FrameCount(int direction)
        {
            return DirectionFrameCounts != null && direction >= 0 && direction < DirectionFrameCounts.Length
                && DirectionFrameCounts[direction] > 0 ? DirectionFrameCounts[direction] : FramesPerDirection;
        }

        public float DirectionAngle(int direction)
        {
            return DirectionAngles != null && DirectionAngles.Length == DirectionCount
                ? DirectionAngles[direction] : direction * (360f / DirectionCount);
        }

        public int SelectDirection(float heading, int previousDirection, bool reset)
        {
            int closest = 0;
            float smallest = float.MaxValue;
            for (int direction = 0; direction < DirectionCount; direction++)
            {
                float error = Mathf.Abs(Mathf.DeltaAngle(DirectionAngle(direction), heading));
                if (error < smallest) { smallest = error; closest = direction; }
            }
            if (!reset && previousDirection >= 0 && previousDirection < DirectionCount)
            {
                float previousError = Mathf.Abs(Mathf.DeltaAngle(DirectionAngle(previousDirection), heading));
                // A two-degree boundary margin also works with unequal isometric sectors.
                float margin = DirectionAngles != null && DirectionAngles.Length == DirectionCount ? 4f : 8f;
                if (previousError <= smallest + margin) return previousDirection;
            }
            return closest;
        }
    }
}
