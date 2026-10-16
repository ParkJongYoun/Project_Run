using System;
using UnityEngine;

namespace ProjectRun.Race
{
    [CreateAssetMenu(menuName = "Project Run/Fox camera sprite set")]
    public sealed class Data_FoxSpriteSet : ScriptableObject
    {
        [Serializable]
        public sealed class ShotFrames
        {
            public E_RaceShot Shot;
            public Sprite Left;
            public Sprite Right;
            public Vector3 ViewDirection;
            public float LeftPhase;
            public float RightPhase;
            public float AnchorHeight = 1.5f;
        }
        public ShotFrames[] Shots = Array.Empty<ShotFrames>();
        public Material SpriteMaterial;

        public ShotFrames Find(E_RaceShot shot)
        {
            foreach (var frames in Shots)
                if (frames.Shot == shot && frames.Left != null && frames.Right != null) return frames;
            return null;
        }
    }
}
