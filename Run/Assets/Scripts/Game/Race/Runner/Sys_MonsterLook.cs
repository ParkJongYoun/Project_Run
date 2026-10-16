using UnityEngine;

namespace ProjectRun.Race
{
    public enum E_MonsterRenderStyle { Original, Toon, Outlined }

    /// <summary>Only swaps presentation resources. Race and animation state are left untouched.</summary>
    [DisallowMultipleComponent]
    public sealed class Sys_MonsterLook : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Surface
        {
            public Renderer Renderer;
            public Material[] Original;
            public Material[] Toon;
            public Material[] Outlined;
        }

        public Surface[] Surfaces;
        public Renderer[] Outlines;
        public E_MonsterRenderStyle CurrentStyle { get; private set; }

        public void Apply(E_MonsterRenderStyle style)
        {
            CurrentStyle = style;
            foreach (var surface in Surfaces)
                surface.Renderer.sharedMaterials = style == E_MonsterRenderStyle.Original ? surface.Original
                    : style == E_MonsterRenderStyle.Toon ? surface.Toon : surface.Outlined;
            foreach (var outline in Outlines) outline.enabled = style == E_MonsterRenderStyle.Outlined;
        }
    }
}
