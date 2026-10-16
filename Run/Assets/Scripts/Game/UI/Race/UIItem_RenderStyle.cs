using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProjectRun.Race
{
    public sealed class UIItem_RenderStyle : MonoBehaviour
    {
        public Sys_RaceRenderStyle Styles;
        public Button[] Buttons;
        public Image[] Backgrounds;
        public Text[] Labels;
        private UnityAction[] actions;

        private void OnEnable()
        {
            if (Styles == null) Styles = FindFirstObjectByType<Sys_RaceRenderStyle>();
            if (Styles == null) return;
            actions = new UnityAction[Buttons.Length];
            for (int i = 0; i < Buttons.Length; i++)
            {
                int index = i;
                actions[i] = () => Styles.SetStyle(index);
                Buttons[i].onClick.AddListener(actions[i]);
            }
            Styles.Changed += Refresh;
            Refresh(Styles.CurrentStyle);
        }

        private void OnDisable()
        {
            if (Styles != null) Styles.Changed -= Refresh;
            if (actions != null) for (int i = 0; i < Buttons.Length; i++) Buttons[i].onClick.RemoveListener(actions[i]);
        }

        private void Refresh(E_MonsterRenderStyle style)
        {
            for (int i = 0; i < Buttons.Length; i++)
            {
                bool selected = (int)style == i;
                Backgrounds[i].color = selected ? new Color(.98f,.8f,.4f,.98f) : new Color(.06f,.15f,.18f,.92f);
                Labels[i].color = selected ? new Color(.16f,.14f,.08f) : new Color(.92f,.94f,.92f);
            }
        }
    }
}
