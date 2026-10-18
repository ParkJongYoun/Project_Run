using UnityEngine;

namespace ProjectRun.Race
{
    // Poring and Eddga share the comic page mechanics, with distinct action directions and timing.
    // This component only evaluates presentation time; the race owns its simulation hold.
    public sealed class UIItem_DemoHeroComic : MonoBehaviour
    {
        public const float Duration = 10.3f;
        public const int CutCount = 7;
        private const int chargeCut = 2, finalCut = 6;
        private static readonly float[] starts = { .30f, 1.30f, 2.55f, 3.95f, 5.10f, 6.25f, 7.55f };
        private static readonly string[] poringBeats = { "겁이 나도…", "앞으로 나아갈 거야!", "용기를 꾹 모아서!", "땅을 힘껏 박차고", "하늘을 향해, 폴짝!", "바람을 따라 날아올라", "빈틈을 뛰어넘어!" };
        private static readonly string[] eddgaBeats = { "고요 속에 감춘 힘", "목표를 꿰뚫는 눈빛", "번개를 깨우고…", "대지를 박차는 발톱", "어둠을 가르는 섬광!", "번개처럼 내달려", "맹호의 질주!" };
        private static readonly string[] poringWords = { "두근…", "꼬옥!", "꾹!", "팡!", "폴짝!", "휘이잉!", "슈우웅!" };
        private static readonly string[] eddgaWords = { "고요…", "번뜩!", "파직!", "콰직!", "번쩍!", "쐐액!", "콰지직!" };
        private static readonly Vector2[][] shapes =
        {
            new[] { new Vector2(20,20), new Vector2(360,20), new Vector2(345,405), new Vector2(20,405) },
            new[] { new Vector2(380,20), new Vector2(890,20), new Vector2(905,405), new Vector2(365,405) },
            new[] { new Vector2(910,20), new Vector2(1250,20), new Vector2(1230,405), new Vector2(925,405) },
            new[] { new Vector2(1270,20), new Vector2(1652,20), new Vector2(1652,405), new Vector2(1250,405) },
            new[] { new Vector2(20,425), new Vector2(390,425), new Vector2(415,830), new Vector2(20,830) },
            new[] { new Vector2(410,425), new Vector2(1130,425), new Vector2(1110,830), new Vector2(435,830) },
            new[] { new Vector2(1150,425), new Vector2(1652,425), new Vector2(1652,830), new Vector2(1130,830) }
        };
        private bool poring;
        private Color accent;
        private RectTransform viewport, cameraRoot, hero;
        private CanvasGroup heroOpacity;
        private readonly RectTransform[] panels = new RectTransform[CutCount];
        private readonly UIItem_ComicPanel[] pictures = new UIItem_ComicPanel[CutCount];
        private readonly UIItem_ComicPanel[] frames = new UIItem_ComicPanel[CutCount];
        private readonly RectTransform[] paperHalves = new RectTransform[2];
        private readonly CanvasGroup[] paperOpacity = new CanvasGroup[2];
        private readonly UnityEngine.UI.Image[] streaks = new UnityEngine.UI.Image[3];
        private UnityEngine.UI.Image dim, flash;
        private UnityEngine.UI.Text beat, sound;
        private UIItem_ComicBurst burst;
        public int ActiveCut { get; private set; } = -1;
        public string MonsterId { get; private set; }

        public static UIItem_DemoHeroComic Create(RectTransform parent, string monsterId, string skill)
        {
            if (monsterId != "Poring" && monsterId != "Eddga") return null;
            // Retain all four approved panels, interleaving three separately authored angles.
            var extraNames = monsterId == "Poring" ? new[] { "Side", "Release", "Rear" } : new[] { "Eyes", "Paw", "Side" };
            var sprites = new Sprite[CutCount];
            for (int i = 0; i < sprites.Length; i++)
            {
                sprites[i] = i % 2 == 0 ? DemoUI.Art.Get(monsterId + "_comic_" + i / 2)
                    : Mgr_Bundle.Load<Sprite>("UI/Demo/ExpandedComics/" + monsterId + "_" + extraNames[i / 2]);
                if (sprites[i] == null) return null;
            }
            var material = Mgr_Bundle.Load<Material>("UI/Demo/MinorousComic/Mat_ComicPanel");
            if (material == null) return null;
            var root = DemoUI.Fill(parent, "group_" + monsterId.ToLowerInvariant() + "_comic");
            var view = root.gameObject.AddComponent<UIItem_DemoHeroComic>();
            view.MonsterId = monsterId;
            view.poring = monsterId == "Poring";
            view.accent = view.poring ? new Color(1, .64f, .76f) : new Color(.39f, .79f, 1);
            view.Build(sprites, material, skill);
            view.Evaluate(0);
            return view;
        }

        private void Build(Sprite[] sprites, Material material, string skill)
        {
            DemoUI.Shade(transform, "bg_ink", poring ? new Color(.065f, .018f, .035f) : new Color(.01f, .025f, .055f)).raycastTarget = true;
            viewport = DemoUI.Fill(transform, "obj_comic_viewport");
            viewport.offsetMin = new Vector2(16, 64); viewport.offsetMax = new Vector2(-16, -64);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            cameraRoot = DemoUI.Rect(viewport, "obj_comic_camera", 0, 0, 1672, 850);
            DemoUI.Shade(cameraRoot, "bg_paper", poring ? new Color(.88f, .80f, .77f) : new Color(.14f, .22f, .31f));
            var focalPoints = poring
                ? new[] { new Vector2(.53f,.55f), new Vector2(.38f,.50f), new Vector2(.50f,.60f), new Vector2(.70f,.52f), new Vector2(.54f,.43f), new Vector2(.55f,.50f), new Vector2(.53f,.42f) }
                : new[] { new Vector2(.57f,.49f), new Vector2(.50f,.50f), new Vector2(.52f,.53f), new Vector2(.62f,.55f), new Vector2(.50f,.50f), new Vector2(.50f,.50f), new Vector2(.58f,.55f) };
            for (int i = 0; i < panels.Length; i++)
            {
                panels[i] = CreateCut(cameraRoot, "group_cut_" + i, shapes[i], sprites[i], focalPoints[i], material, out pictures[i], out frames[i]);
                panels[i].anchoredPosition = PagePosition(shapes[i]);
            }
            dim = DemoUI.Shade(viewport, "bg_final_dim", Color.clear);
            burst = DemoUI.Fill(viewport, "img_speed_lines").gameObject.AddComponent<UIItem_ComicBurst>();
            burst.raycastTarget = false;
            hero = CreateCut(viewport, "group_torn_final", TornShape(poring ? 480 : 550), sprites[finalCut],
                poring ? new Vector2(.53f,.44f) : new Vector2(.55f,.58f), material, out var heroArt, out var heroFrame);
            heroArt.SetLight(1, 1); heroFrame.color = Color.Lerp(accent, Color.white, .65f);
            heroOpacity = hero.gameObject.AddComponent<CanvasGroup>();
            heroOpacity.blocksRaycasts = heroOpacity.interactable = false;
            var paper = DemoUI.Fill(viewport, "group_tearing_paper");
            var pageShape = new[] { Vector2.zero, new Vector2(1672,0), new Vector2(1672,850), new Vector2(0,850) };
            for (int i = 0; i < paperHalves.Length; i++)
            {
                var piece = DemoUI.Rect(paper, "img_paper_" + i, 0, 0, 0, 0).gameObject.AddComponent<UIItem_ComicPanel>();
                piece.ConfigureRegion(sprites[0].texture, pageShape, new Rect(0,0,1,1), material, false);
                piece.SetLight(0, .20f);
                piece.SetTear(i == 0 ? -1 : 1, poring ? Vector2.up : new Vector2(1,.75f));
                paperHalves[i] = piece.rectTransform;
                paperOpacity[i] = piece.gameObject.AddComponent<CanvasGroup>();
                paperOpacity[i].blocksRaycasts = paperOpacity[i].interactable = false;
            }
            for (int i = 0; i < streaks.Length; i++)
            {
                var rect = DemoUI.Rect(viewport, "img_action_streak_" + i, 0, 0, 1800, i == 1 ? 7 : 2);
                streaks[i] = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); streaks[i].raycastTarget = false;
            }
            sound = DemoUI.Text(viewport, "txt_impact", "", 0, 0, 500, 125, 78, accent, TextAnchor.MiddleCenter, true);
            var outline = sound.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(.025f,.015f,.035f); outline.effectDistance = new Vector2(4,-4);
            flash = DemoUI.Shade(viewport, "img_contact_flash", Color.clear);
            var title = DemoUI.Text(transform, "txt_skill", skill, 295, -31, 510, 54, 38, DemoUI.Paper, TextAnchor.MiddleLeft, true);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0,1);
            var name = DemoUI.Text(transform, "txt_monster", poring ? "포링" : "에드가", -213, -32, 340, 44, 21, accent, TextAnchor.MiddleRight);
            name.rectTransform.anchorMin = name.rectTransform.anchorMax = new Vector2(1,1);
            beat = DemoUI.Text(transform, "txt_beat", "", -80, 31, 650, 48, 29, DemoUI.Paper, TextAnchor.MiddleCenter, true);
            beat.rectTransform.anchorMin = beat.rectTransform.anchorMax = new Vector2(.5f,0);
        }

        private static Vector2 Center(Vector2[] shape)
        {
            Vector2 min = shape[0], max = shape[0];
            foreach (var point in shape) { min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
            return (min + max) * .5f;
        }
        private static Vector2 PagePosition(Vector2[] shape)
        {
            var center = Center(shape); return new Vector2(center.x - 836, 425 - center.y);
        }
        private static RectTransform CreateCut(Transform parent, string name, Vector2[] shape, Sprite sprite, Vector2 focal,
            Material material, out UIItem_ComicPanel picture, out UIItem_ComicPanel frame)
        {
            var root = DemoUI.Rect(parent, name, 0, 0, 0, 0);
            picture = DemoUI.Rect(root, "img_art", 0, 0, 0, 0).gameObject.AddComponent<UIItem_ComicPanel>();
            picture.Configure(sprite.texture, shape, material, false);
            root.sizeDelta = picture.rectTransform.sizeDelta;
            var region = sprite.rect;
            float width = Mathf.Min(region.width, region.height * root.sizeDelta.x / root.sizeDelta.y);
            float height = Mathf.Min(region.height, region.width * root.sizeDelta.y / root.sizeDelta.x);
            float left = Mathf.Clamp(region.width * focal.x - width * .5f, 0, region.width - width);
            float top = Mathf.Clamp(region.height * focal.y - height * .5f, 0, region.height - height);
            var sample = new Rect((region.x + left) / sprite.texture.width,
                1 - region.yMax / sprite.texture.height + top / sprite.texture.height,
                width / sprite.texture.width, height / sprite.texture.height);
            // Reuse the already-owned material rather than allocating another instance.
            picture.ConfigureRegion(sprite.texture, shape, sample, null, false);
            picture.rectTransform.anchoredPosition = Vector2.zero;
            frame = DemoUI.Rect(root, "img_frame", 0, 0, 0, 0).gameObject.AddComponent<UIItem_ComicPanel>();
            frame.Configure(null, shape, null, true); frame.rectTransform.anchoredPosition = Vector2.zero;
            return root;
        }

        private static Vector2[] TornShape(float height)
        {
            var corners = new[] { new Vector2(0,14), new Vector2(760,0), new Vector2(748,height), new Vector2(18,height - 14) };
            var center = new Vector2(380,height * .5f);
            var depth = new[] { .018f,.078f,.009f,.043f,.015f,.105f,.012f,.052f,.024f,.004f,.070f,.016f };
            var points = new Vector2[192];
            for (int edge = 0; edge < 4; edge++) for (int tooth = 0; tooth < 12; tooth++)
            {
                var a = Vector2.Lerp(corners[edge], corners[(edge + 1) % 4], tooth / 12f);
                var b = Vector2.Lerp(corners[edge], corners[(edge + 1) % 4], (tooth + 1) / 12f);
                a = Vector2.Lerp(a, center, depth[(tooth + edge * 5) % 12]);
                b = Vector2.Lerp(b, center, depth[(tooth + 1 + edge * 5) % 12]);
                for (int fiber = 0; fiber < 4; fiber++)
                    points[(edge * 12 + tooth) * 4 + fiber] = Vector2.Lerp(Vector2.Lerp(a,b,fiber / 4f), center,
                        fiber == 0 ? 0 : depth[(tooth * 3 + fiber * 5) % 12] * .10f);
            }
            return points;
        }

        public void Evaluate(float elapsed)
        {
            int current = -1;
            for (int i = 0; i < starts.Length; i++) if (elapsed >= starts[i]) current = i;
            ActiveCut = current;
            float local = current < 0 ? 0 : elapsed - starts[current];
            for (int i = 0; i < panels.Length; i++)
            {
                pictures[i].SetLight(i == current ? 1 : i < current ? .12f : 0, i == current ? 1 : .43f);
                frames[i].color = i == current ? accent : new Color(.08f,.07f,.09f);
                panels[i].localScale = Vector3.one;
                var offset = Vector2.zero;
                if (i == 4 && current == 4)
                    offset = (poring ? new Vector2(-75,-135) : new Vector2(-220,90)) * (1 - Smooth(0,.28f,local));
                panels[i].anchoredPosition = PagePosition(shapes[i]) + offset;
            }
            AnimateCamera(elapsed);
            AnimateFinal(elapsed);
            float impact = current == 3 || current == 4 ? Pulse(local - .22f,.25f) : current == finalCut ? Pulse(local - .23f,.32f) : 0;
            dim.color = new Color(.01f,.01f,.025f, Smooth(starts[finalCut] - .28f,starts[finalCut],elapsed) * .94f);
            burst.color = new Color(accent.r,accent.g,accent.b,current == finalCut ? .28f : impact * .27f);
            burst.Animate(elapsed * (poring ? .65f : 2));
            flash.color = new Color(1,poring ? .88f : .96f,poring ? .93f : 1,impact * .32f);
            AnimateStreaks(elapsed);
            var beats = poring ? poringBeats : eddgaBeats;
            beat.text = current < 0 ? "" : beats[current];
            AnimateSound(current, local);
        }

        private void AnimateCamera(float elapsed)
        {
            float fit = Mathf.Min(viewport.rect.width / 1672, viewport.rect.height / 850) / 1.04f;
            float local = elapsed - starts[chargeCut];
            float pullBack = 1 - Smooth(starts[3] - .50f,starts[3],elapsed);
            float push = Smooth(.12f,.75f,local) * pullBack;
            float scale = fit * (1 + (poring ? .72f : .88f) * push);
            var target = PagePosition(shapes[chargeCut]) * Smooth(0,.60f,local) * pullBack;
            float maxX = Mathf.Max(0,836 - viewport.rect.width * .5f / (fit * (poring ? 1.72f : 1.88f)));
            float maxY = Mathf.Max(0,425 - viewport.rect.height * .5f / (fit * (poring ? 1.72f : 1.88f)));
            target.x = Mathf.Clamp(target.x,-maxX,maxX); target.y = Mathf.Clamp(target.y,-maxY,maxY);
            cameraRoot.localScale = Vector3.one * scale;
            cameraRoot.anchoredPosition = -target * scale;
        }

        private void AnimateFinal(float elapsed)
        {
            float local = elapsed - starts[finalCut];
            hero.gameObject.SetActive(local >= 0);
            float fit = Mathf.Min(viewport.rect.width * .90f / hero.rect.width, viewport.rect.height * .86f / hero.rect.height);
            hero.localScale = Vector3.one * fit;
            var arrival = poring ? new Vector2(-30,-155) : new Vector2(-210,85);
            hero.anchoredPosition = arrival * (1 - Smooth(.02f,.50f,local));
            heroOpacity.alpha = Smooth(0,.07f,local);
            float opening = Smooth(.03f,.64f,local);
            float opacity = Smooth(starts[finalCut] - .28f,starts[finalCut] - .08f,elapsed) * (1 - Smooth(.55f,.82f,local));
            float cover = Mathf.Max(viewport.rect.width / 1672,viewport.rect.height / 850) * 1.05f;
            for (int i = 0; i < paperHalves.Length; i++)
            {
                float side = i == 0 ? -1 : 1;
                paperHalves[i].gameObject.SetActive(opacity > .001f);
                paperHalves[i].localScale = Vector3.one * cover;
                paperHalves[i].anchoredPosition = (poring ? new Vector2(side * 65,side * viewport.rect.height * 1.1f)
                    : new Vector2(side * viewport.rect.width * .9f,side * viewport.rect.height * .48f)) * opening;
                paperHalves[i].localRotation = Quaternion.Euler(0,0,side * (poring ? 7 : -14) * opening);
                paperOpacity[i].alpha = opacity;
            }
        }

        private void AnimateStreaks(float elapsed)
        {
            float age = elapsed - starts[finalCut];
            float sweep = Smooth(-.08f,.34f,age);
            float alpha = Smooth(-.09f,.02f,age) * (1 - Smooth(.17f,.40f,age));
            for (int i = 0; i < streaks.Length; i++)
            {
                streaks[i].color = new Color(accent.r,accent.g,accent.b,alpha * (i == 1 ? .9f : .6f));
                streaks[i].rectTransform.localRotation = Quaternion.Euler(0,0,poring ? 62 : -28);
                streaks[i].rectTransform.anchoredPosition = new Vector2((i - 1) * 27,Mathf.Lerp(-240,210,sweep));
            }
        }

        private void AnimateSound(int current, float local)
        {
            float age = local - (current == chargeCut ? .55f : .28f);
            bool visible = current >= 0 && age >= 0 && age < (current == finalCut ? 1.8f : .60f);
            sound.gameObject.SetActive(visible); if (!visible) return;
            var words = poring ? poringWords : eddgaWords;
            sound.text = words[current]; sound.fontSize = current == finalCut ? 80 : 60;
            sound.rectTransform.anchoredPosition = new Vector2(viewport.rect.width * (current == chargeCut ? -.29f : .28f),-viewport.rect.height * .29f);
            sound.rectTransform.localRotation = Quaternion.Euler(0,0,poring ? 8 : -9);
            sound.color = current == finalCut ? accent : DemoUI.Paper;
        }

        private static float Smooth(float start, float end, float value) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,end,value));
        private static float Pulse(float age, float length) => age < 0 ? 0 : 1 - Smooth(0,length,age);
    }
}
