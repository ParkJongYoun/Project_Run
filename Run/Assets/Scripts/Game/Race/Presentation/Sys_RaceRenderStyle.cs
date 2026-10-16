using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectRun.Race
{
    [DefaultExecutionOrder(200)]
    public sealed class Sys_RaceRenderStyle : MonoBehaviour
    {
        public Sys_RacePresentation Presentation;
        public Volume ColorVolume;
        public VolumeProfile OriginalProfile;
        public VolumeProfile RefinedProfile;
        public Renderer[] TurfRenderers;
        public Material[] OriginalTurf;
        public Material[] RefinedTurf;
        public E_MonsterRenderStyle InitialStyle = E_MonsterRenderStyle.Outlined;
        public E_MonsterRenderStyle CurrentStyle { get; private set; }
        public event System.Action<E_MonsterRenderStyle> Changed;

        private void Start() => SetStyle((int)InitialStyle);

        public void SetStyle(int index)
        {
            CurrentStyle = (E_MonsterRenderStyle)Mathf.Clamp(index, 0, 2);
            foreach (var view in Presentation.Views)
            {
                var look = view.GetComponent<Sys_MonsterLook>();
                if (look != null) look.Apply(CurrentStyle);
            }
            bool refined = CurrentStyle == E_MonsterRenderStyle.Outlined;
            if (ColorVolume != null) ColorVolume.sharedProfile = refined ? RefinedProfile : OriginalProfile;
            for (int i = 0; i < TurfRenderers.Length; i++)
                TurfRenderers[i].sharedMaterial = refined ? RefinedTurf[i] : OriginalTurf[i];
            Changed?.Invoke(CurrentStyle);
        }
    }
}
