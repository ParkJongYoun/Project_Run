using UnityEngine;

namespace ProjectRun.Race
{
    // Presentation time only: never advances or changes the race simulation.
    public sealed class UIItem_MinorousComic : MonoBehaviour
    {
        public const float Duration = 9.2f;
        private static readonly float[] starts = { .35f, 1.70f, 3.10f, 4.20f, 5.25f, 6.45f };
        private static readonly string[] soundWords = { "휙!", "후우…", "꽉!", "파앗!", "쿵!", "콰아앙!" };
        private static readonly string[] beats = { "대열을 향해", "숨을 모으고…", "팔에 힘을!", "다리에 힘을!", "땅을 박차고!", "대열 돌파!" };
        private static readonly Vector2[][] corners =
        {
            new[] { new Vector2(27,77), new Vector2(517,77), new Vector2(469,307), new Vector2(27,307) },
            new[] { new Vector2(549,77), new Vector2(1644,77), new Vector2(1644,341), new Vector2(501,329) },
            new[] { new Vector2(27,341), new Vector2(686,341), new Vector2(637,632), new Vector2(27,632) },
            new[] { new Vector2(719,375), new Vector2(1127,375), new Vector2(1174,632), new Vector2(668,632) },
            new[] { new Vector2(1165,375), new Vector2(1644,375), new Vector2(1644,913), new Vector2(1214,913) },
            new[] { new Vector2(27,665), new Vector2(1179,665), new Vector2(1179,913), new Vector2(27,913) }
        };
        private RectTransform viewport, cameraRoot;
        private readonly RectTransform[] panels = new RectTransform[6];
        private readonly UIItem_ComicPanel[] art = new UIItem_ComicPanel[6];
        private readonly UIItem_ComicPanel[] frames = new UIItem_ComicPanel[6];
        private RectTransform breakthrough;
        private CanvasGroup breakthroughOpacity;
        private readonly RectTransform[] tornHalves = new RectTransform[2];
        private readonly CanvasGroup[] tornOpacity = new CanvasGroup[2];
        private UnityEngine.UI.Text beat, sound;
        private UnityEngine.UI.Image flash, focusShade;
        private UIItem_ComicBurst speedLines;
        private int activeCut = -1;
        public int ActiveCut => activeCut;

        public static UIItem_MinorousComic Create(RectTransform parent)
        {
            var texture = Mgr_Bundle.Load<Texture2D>("UI/Demo/MinorousComic/Minorous_Storyboard");
            var template = Mgr_Bundle.Load<Material>("UI/Demo/MinorousComic/Mat_ComicPanel");
            if (texture == null || template == null) return null;
            var root = DemoUI.Fill(parent, "group_minorous_comic");
            var view = root.gameObject.AddComponent<UIItem_MinorousComic>();
            view.Build(texture, template);
            view.Evaluate(0);
            return view;
        }

        private void Build(Texture texture, Material template)
        {
            DemoUI.Shade(transform, "bg_ink", new Color(.025f, .02f, .018f)).raycastTarget = true;
            viewport = DemoUI.Fill(transform, "obj_comic_viewport");
            viewport.offsetMin = new Vector2(16, 64);
            viewport.offsetMax = new Vector2(-16, -64);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            cameraRoot = DemoUI.Rect(viewport, "obj_comic_camera", 0, 0, 1672, 850);
            DemoUI.Shade(cameraRoot, "bg_paper", new Color(.84f, .81f, .72f));
            for (int i = 0; i < panels.Length; i++)
            {
                panels[i] = CreateCut(cameraRoot, "group_cut_" + i, corners[i], texture, template, out art[i], out frames[i]);
                panels[i].anchoredPosition = Center(corners[i]) - new Vector2(836, 495);
                panels[i].anchoredPosition = new Vector2(panels[i].anchoredPosition.x, -panels[i].anchoredPosition.y);
            }
            focusShade = DemoUI.Shade(viewport, "bg_focus_dim", Color.clear);
            speedLines = DemoUI.Fill(viewport, "img_focus_speed_lines").gameObject.AddComponent<UIItem_ComicBurst>();
            speedLines.raycastTarget = false;
            var focusLayer = DemoUI.Fill(viewport, "group_breakthrough");
            breakthrough = CreateCut(focusLayer, "group_torn_cut", TornOutline(), texture, template, out var picture, out var frame);
            picture.SetLight(1, 1);
            frame.color = new Color(1, .94f, .76f);
            breakthroughOpacity = breakthrough.gameObject.AddComponent<CanvasGroup>();
            breakthroughOpacity.blocksRaycasts = breakthroughOpacity.interactable = false;
            var tearLayer = DemoUI.Fill(viewport, "group_center_tear");
            var page = new[] { new Vector2(0,70), new Vector2(1672,70), new Vector2(1672,920), new Vector2(0,920) };
            for (int i = 0; i < 2; i++)
            {
                var tear = DemoUI.Rect(tearLayer, i == 0 ? "img_torn_left" : "img_torn_right", 0, 0, 0, 0).gameObject.AddComponent<UIItem_ComicPanel>();
                tear.Configure(texture, page, template, false);
                tear.SetLight(0, .24f);
                tear.SetTear(i == 0 ? -1 : 1);
                tornHalves[i] = tear.rectTransform;
                tornOpacity[i] = tear.gameObject.AddComponent<CanvasGroup>();
                tornOpacity[i].blocksRaycasts = tornOpacity[i].interactable = false;
            }
            sound = DemoUI.Text(viewport, "txt_impact", "", 0, 0, 440, 130, 76, DemoUI.Paper, TextAnchor.MiddleCenter, true);
            var outline = sound.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(.04f, .025f, .01f); outline.effectDistance = new Vector2(4, -4);
            flash = DemoUI.Shade(viewport, "img_contact_flash", Color.clear);
            var title = DemoUI.Text(transform, "txt_skill", "대열 돌파", 235, -31, 390, 54, 38, DemoUI.Paper, TextAnchor.MiddleLeft, true);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0, 1);
            var name = DemoUI.Text(transform, "txt_monster", "마이너 우로스", -213, -32, 340, 44, 21, DemoUI.Gold, TextAnchor.MiddleRight);
            name.rectTransform.anchorMin = name.rectTransform.anchorMax = new Vector2(1, 1);
            beat = DemoUI.Text(transform, "txt_beat", "", -80, 31, 650, 48, 29, DemoUI.Paper, TextAnchor.MiddleCenter, true);
            beat.rectTransform.anchorMin = beat.rectTransform.anchorMax = new Vector2(.5f, 0);
        }

        private static RectTransform CreateCut(Transform parent, string name, Vector2[] shape, Texture texture, Material template,
            out UIItem_ComicPanel picture, out UIItem_ComicPanel frame)
        {
            var root = DemoUI.Rect(parent, name, 0, 0, 0, 0);
            picture = DemoUI.Rect(root, "img_art", 0, 0, 0, 0).gameObject.AddComponent<UIItem_ComicPanel>();
            picture.Configure(texture, shape, template, false);
            root.sizeDelta = picture.rectTransform.sizeDelta;
            picture.rectTransform.anchoredPosition = Vector2.zero;
            frame = DemoUI.Rect(root, "img_frame", 0, 0, 0, 0).gameObject.AddComponent<UIItem_ComicPanel>();
            frame.Configure(null, shape, null, true);
            frame.rectTransform.anchoredPosition = Vector2.zero;
            return root;
        }

        private static Vector2 Center(Vector2[] shape)
        {
            Vector2 minimum = shape[0], maximum = shape[0];
            foreach (var point in shape) { minimum = Vector2.Min(minimum, point); maximum = Vector2.Max(maximum, point); }
            return (minimum + maximum) * .5f;
        }

        private static Vector2[] TornOutline()
        {
            // Unequal paper teeth on all four edges. Radial notches keep the mesh star-shaped.
            var edges = new[] { new Vector2(480,665), new Vector2(1005,671), new Vector2(980,910), new Vector2(490,913) };
            var center = new Vector2(742, 788);
            var depth = new[] { .025f, .082f, .009f, .048f, .018f, .105f, .012f, .063f, .027f, .004f, .075f, .016f, .045f };
            var outline = new Vector2[depth.Length * edges.Length];
            for (int edge = 0; edge < edges.Length; edge++)
                for (int tooth = 0; tooth < depth.Length; tooth++)
                {
                    var point = Vector2.Lerp(edges[edge], edges[(edge + 1) % edges.Length], tooth / (float)depth.Length);
                    float indent = depth[(tooth + edge * 5) % depth.Length];
                    outline[edge * depth.Length + tooth] = Vector2.Lerp(point, center, indent);
                }
            // Small splinters break up the large notches so the edge reads as ripped paper.
            var splinters = new Vector2[outline.Length * 4];
            var steps = new[] { 0f, .21f, .48f, .79f };
            for (int i = 0; i < outline.Length; i++)
                for (int part = 0; part < steps.Length; part++)
                {
                    var point = Vector2.Lerp(outline[i], outline[(i + 1) % outline.Length], steps[part]);
                    float fiber = part == 0 ? 0 : depth[(i * 3 + part * 5) % depth.Length] * .11f;
                    splinters[i * steps.Length + part] = Vector2.Lerp(point, center, fiber);
                }
            return splinters;
        }

        public void Evaluate(float elapsed)
        {
            int current = -1;
            for (int i = 0; i < starts.Length; i++) if (elapsed >= starts[i]) current = i;
            activeCut = current;
            float local = current < 0 ? 0 : elapsed - starts[current];
            float fit = Mathf.Min(viewport.rect.width / 1672, viewport.rect.height / 850) / 1.04f;
            for (int i = 0; i < panels.Length; i++)
            {
                bool selected = current == i;
                art[i].SetLight(selected ? 1 : i < current ? .2f : 0, selected ? 1 : .46f);
                frames[i].color = selected ? DemoUI.Gold : new Color(.10f, .09f, .08f);
                AnimatePanelEntry(i, elapsed);
            }
            AnimateCamera(elapsed, fit);
            focusShade.color = new Color(.015f, .01f, .005f, Smooth(6.15f, 6.42f, elapsed) * .92f);
            float impact = current == 4 ? Pulse(local - .64f, .28f) : current == 5 ? Pulse(local - .12f, .45f) : 0;
            float entryLines = current < 0 ? 0 : Pulse(local, .45f);
            speedLines.color = new Color(1, .82f, .46f, current == 5 ? .28f : .12f * entryLines + .25f * impact);
            speedLines.Animate(elapsed * 1.7f);
            AnimateBreakthrough(elapsed);
            AnimateTear(elapsed);
            flash.color = new Color(1, .91f, .68f, impact * (current == 5 ? .38f : .19f));
            beat.text = current < 0 ? "" : beats[current];
            AnimateSound(current, local);
        }

        private void AnimatePanelEntry(int index, float elapsed)
        {
            float local = elapsed - starts[index];
            var center = Center(corners[index]);
            var position = new Vector2(center.x - 836, 495 - center.y);
            // Brief lateral arrivals retain the panel's printed size and place on the page.
            if (local >= 0 && local < .28f && (index == 0 || index == 2))
                position += new Vector2(index == 0 ? -170 : 120, index == 0 ? 12 : -8) * (1 - BackOut(local / .28f));
            panels[index].anchoredPosition = position;
            panels[index].localScale = Vector3.one;
        }

        private void AnimateCamera(float elapsed, float fit)
        {
            // Move the viewpoint across one continuous page. A cut never lifts toward the viewer.
            var target = Vector2.zero;
            float zoom = 1;
            if (elapsed >= starts[1] && elapsed < 3.52f)
            {
                float local = elapsed - starts[1];
                float pullBack = Smooth(2.78f, 3.52f, elapsed);
                var face = FrameTarget(new Vector2(364, 276), fit * 2.15f);
                target = face * Smooth(0, .57f, local) * (1 - pullBack);
                zoom += 1.15f * Smooth(.12f, .70f, local) * (1 - pullBack);
            }
            else if (elapsed >= starts[4])
            {
                float local = elapsed - starts[4];
                var hoof = FrameTarget(new Vector2(584, -235), fit * 2.35f);
                target = hoof * Smooth(0, .50f, local);
                zoom += 1.35f * Smooth(.13f, .60f, local);
            }
            float scale = fit * zoom;
            cameraRoot.localScale = Vector3.one * scale;
            cameraRoot.anchoredPosition = -target * scale;
            float stomp = Pulse(elapsed - (starts[4] + .64f), .28f);
            cameraRoot.anchoredPosition += new Vector2(Mathf.Sin(elapsed * 73), Mathf.Cos(elapsed * 59)) * stomp * 7;
        }

        private Vector2 FrameTarget(Vector2 target, float scale)
        {
            // Keep the focused subject and the edge of the original page inside the framing.
            float maxX = Mathf.Max(0, 836 - viewport.rect.width * .5f / scale);
            float maxY = Mathf.Max(0, 425 - viewport.rect.height * .5f / scale);
            return new Vector2(Mathf.Clamp(target.x, -maxX, maxX), Mathf.Clamp(target.y, -maxY, maxY));
        }

        private void AnimateBreakthrough(float elapsed)
        {
            float local = elapsed - starts[5];
            breakthrough.gameObject.SetActive(local >= 0);
            if (local < 0) return;
            float fit = Mathf.Min(viewport.rect.width * .90f / breakthrough.rect.width,
                viewport.rect.height * .86f / breakthrough.rect.height);
            // The torn paper reveals a composed image at a fixed size; no card-pop scaling.
            breakthrough.localScale = Vector3.one * fit;
            float impact = Pulse(local - .12f, .40f);
            breakthrough.anchoredPosition = new Vector2(0, Mathf.Lerp(22, 0, Smooth(0, .52f, local)))
                + new Vector2(Mathf.Sin(local * 67), Mathf.Cos(local * 51)) * impact * 10;
            breakthroughOpacity.alpha = Smooth(0, .06f, local);
        }

        private void AnimateTear(float elapsed)
        {
            float local = elapsed - 6.45f;
            float opening = Smooth(.03f, .62f, local);
            float opacity = Smooth(6.15f, 6.34f, elapsed) * (1 - Smooth(.55f, .82f, local));
            float cover = Mathf.Max(viewport.rect.width / 1672, viewport.rect.height / 850) * 1.05f;
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1 : 1;
                tornHalves[i].gameObject.SetActive(opacity > .001f);
                tornHalves[i].localScale = Vector3.one * cover;
                tornHalves[i].anchoredPosition = new Vector2(side * viewport.rect.width * .82f * opening, side * 46 * opening);
                tornHalves[i].localRotation = Quaternion.Euler(0, 0, -side * 13 * opening);
                tornOpacity[i].alpha = opacity;
            }
        }

        private void AnimateSound(int current, float local)
        {
            float delay = current == 4 ? .64f : current == 5 ? .25f : .28f;
            float age = local - delay;
            bool visible = current >= 0 && age > 0 && age < (current == 5 ? 1.8f : .55f);
            sound.gameObject.SetActive(visible);
            if (!visible) return;
            sound.text = soundWords[current];
            sound.fontSize = current >= 4 ? 84 : 62;
            float side = current == 4 ? -1 : 1;
            sound.rectTransform.anchoredPosition = new Vector2(viewport.rect.width * .29f * side, -viewport.rect.height * .30f);
            sound.rectTransform.localRotation = Quaternion.Euler(0, 0, current == 4 ? 9 : -8);
            sound.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1, Smooth(0, .15f, age));
            sound.color = current >= 4 ? new Color(1, .85f, .43f) : DemoUI.Paper;
        }

        private static float Pulse(float age, float length) => age < 0 ? 0 : 1 - Smooth(0, length, age);
        private static float BackOut(float value)
        {
            float t = Mathf.Clamp01(value) - 1;
            return 1 + 2.25f * t * t * t + 1.25f * t * t;
        }
        private static float Smooth(float start, float end, float value) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, end, value));
    }
}
