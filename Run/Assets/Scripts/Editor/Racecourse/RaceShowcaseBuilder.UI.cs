using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using ProjectRun.Race;
namespace ProjectRun.Editor
{
    public static partial class RaceShowcaseBuilder
    {
        private static void BuildHUD(Sys_Race race, Sys_RacePresentation presentation, Sys_RaceCamera director) => BuildFantasyHUD(race, presentation, director);
        private static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
        {
            var child = parent.Find(name);
            var rect = child != null ? child.GetComponent<RectTransform>() : new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max; rect.pivot = new Vector2(.5f,.5f);
            rect.sizeDelta = size; rect.anchoredPosition = position; rect.localScale = Vector3.one; rect.gameObject.SetActive(true); return rect;
        }
        private static UnityEngine.UI.Image Image(Transform parent, string name, Vector2 min, Vector2 max, Vector2 pos, Vector2 size, Color color)
        {
            var rect = Rect(parent,name,min,max,pos,size);var image=Get<UnityEngine.UI.Image>(rect.gameObject);image.color=color;image.raycastTarget=false;return image;
        }
        private static UnityEngine.UI.Text Text(Transform parent,string name,string value,Vector2 min,Vector2 max,Vector2 pos,Vector2 size,int fontSize,Color color,TextAnchor alignment)
        {
            var rect=Rect(parent,name,min,max,pos,size);var text=Get<UnityEngine.UI.Text>(rect.gameObject);
            text.font=font;text.text=value;text.fontSize=fontSize;text.color=color;text.alignment=alignment;text.supportRichText=true;text.fontStyle=fontSize>=24 ? FontStyle.Bold : FontStyle.Normal;
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;text.raycastTarget=false;return text;
        }
    }
}

