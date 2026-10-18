using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProjectRun.Race
{
    public static class DemoUI
    {
        public static readonly Color Ink = new Color(.13f, .23f, .19f);
        public static readonly Color Green = new Color(.09f, .29f, .22f);
        public static readonly Color Gold = new Color(.75f, .58f, .29f);
        public static readonly Color Paper = new Color(1, .975f, .91f);
        public static Data_DemoArt Art => Mgr_Bundle.Load<Data_DemoArt>("UI/Demo/Data_DemoArt");

        public static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(width, height); rect.anchoredPosition = new Vector2(x, y);
            return rect;
        }
        public static RectTransform Fill(Transform parent, string name)
        {
            var rect = Rect(parent, name, 0, 0, 0, 0);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
        public static UnityEngine.UI.Image Image(Transform parent, string name, string key, float x, float y, float width, float height, bool preserve = false)
        {
            var image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = string.IsNullOrEmpty(key) ? null : Art.Get(key);
            image.raycastTarget = false; image.preserveAspect = preserve;
            if (image.sprite != null && image.sprite.border.sqrMagnitude > 0) image.type = UnityEngine.UI.Image.Type.Sliced;
            return image;
        }
        public static UnityEngine.UI.Image Shade(Transform parent, string name, Color color)
        {
            var image = Fill(parent, name).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color; image.raycastTarget = false; return image;
        }
        public static UnityEngine.UI.Text Text(Transform parent, string name, string value, float x, float y, float width, float height,
            int size = 22, Color? color = null, TextAnchor alignment = TextAnchor.MiddleCenter, bool title = false)
        {
            var label = Rect(parent, name, x, y, width, height).gameObject.AddComponent<UnityEngine.UI.Text>();
            label.font = title ? Art.TitleFont : Art.BodyFont; label.text = value; label.fontSize = size;
            label.color = color ?? Ink; label.alignment = alignment; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            label.supportRichText = true; return label;
        }
        public static UnityEngine.UI.Button Button(Transform parent, string name, string value, float x, float y, float width, float height, UnityAction action, bool primary = true)
        {
            var bg = Image(parent, name, "button_bg", x, y, width, height);
            bg.color = primary ? Color.white : new Color(.77f, .85f, .78f); bg.raycastTarget = true;
            var button = bg.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = bg;
            var colors = button.colors; colors.highlightedColor = new Color(1,.94f,.76f); colors.pressedColor = new Color(.72f,.79f,.62f); button.colors = colors;
            Image(bg.transform, "img_frame", "button_frame", 0, 0, width, height);
            Text(bg.transform, "txt_label", value, 0, 0, width - 20, height - 6, 22, Paper);
            if (action != null) button.onClick.AddListener(action);
            return button;
        }
        public static RectTransform Panel(Transform parent, string name, float width = 1120, float height = 554)
        {
            var rect = Rect(parent, name, 0, 5, width, height);
            Image(rect, "bg_paper", "panel_bg", 0, 0, width, height);
            Image(rect, "img_frame", "panel_frame", 0, 0, width, height);
            return rect;
        }
        public static Canvas Canvas(Transform parent, string name, int order)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280,720); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            return canvas;
        }
        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject; child.SetActive(false);
                if (Application.isPlaying) Object.Destroy(child); else Object.DestroyImmediate(child);
            }
        }
        public static void SafeArea(RectTransform rect)
        {
            if (rect == null || Screen.width <= 0 || Screen.height <= 0) return;
            var safe = Screen.safeArea;
            rect.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rect.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
