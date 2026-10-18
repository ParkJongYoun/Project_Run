using System;
using UnityEngine;

namespace ProjectRun.Race
{
    public sealed class Data_DemoArt : ScriptableObject
    {
        public Font BodyFont;
        public Font TitleFont;
        public Data_DemoSprite[] Images;
        public Sprite Get(string key)
        {
            if (Images != null) foreach (var entry in Images) if (entry.Key == key) return entry.Sprite;
            return null;
        }
    }
    [Serializable] public sealed class Data_DemoSprite { public string Key; public Sprite Sprite; }
}
