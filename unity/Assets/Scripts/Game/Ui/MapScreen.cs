// Level map (#screen-map): a scrolling snowy mountain drawn from the web's SVG (390×1100 design
// space, level 1 at the bottom), round ice level nodes on a winding trail with their stars and
// names, a locked sign for World 2, and Pip waving above the next level to play. Since 5 Nov 2026 (art v5b)
// it wears the play screen's palette: royal-blue → violet → pink sky with the b1 mountain band across the
// top of the scroll content, snow with blue-violet shadow blobs, the s04 trees / snowman / rocks in place of
// the drawn ones (the SVG drawing stays as the fallback for a missing slice), a cream trail with a navy edge
// and a glacier-blue dash, navy-rimmed nodes and navy-outlined name pills.
using System;
using System.Collections.Generic;
using Pip.Core;
using Pip.Game.Art2D;
using UnityEngine;
using UnityEngine.UI;

namespace Pip.Game.Ui
{
    public sealed class MapScreen : MonoBehaviour
    {
        // Node centres in the 390×1100 map (game.js NODE_POS).
        static readonly Vector2[] NodePos = { new Vector2(110, 980), new Vector2(270, 860), new Vector2(130, 730), new Vector2(260, 600), new Vector2(120, 470), new Vector2(220, 330) };
        const float MapH = 1100;

        // v5 map palette (matches the play screen's b1 sky and snow).
        const string Navy = "#1F2A4D", Snow = "#EEF6FF", Shadow = "#CFDDF7";
        static readonly Grad SkyGrad = new Grad((0, "#2E4FB5"), (0.08f, "#7B6FD0"), (0.15f, "#F4A6C8"), (0.2f, Snow), (1, Snow));
        static readonly Grad NodeGrad = new Grad((0, "#D6F4FF"), (0.3f, "#7FD0FF"), (0.7f, "#3A9BEB"), (1, "#2368C8"));

        /// <summary>Trees on the map (the web's #mtree uses): x, y of the 40×52 box's top-left, box w, h.</summary>
        static readonly float[,] Trees = { { 6, 300, 40, 52 }, { 34, 340, 32, 42 }, { 12, 560, 40, 52 }, { 44, 610, 30, 39 }, { 4, 800, 44, 57 }, { 22, 1010, 40, 52 },
            { 338, 270, 40, 52 }, { 346, 520, 36, 47 }, { 334, 720, 44, 57 }, { 316, 960, 40, 52 }, { 346, 1010, 32, 42 }, { 176, 1010, 34, 44 } };

        /// <summary>Rocks (sprite map only): bottom-centre x, y and width.</summary>
        static readonly float[,] Rocks = { { 250, 1078, 40 }, { 48, 448, 34 } };

        /// <summary>Bottom of the header band (title / back / star chip) below the safe-area top, in map units;
        /// the initial scroll keeps the top level's name tag below it when it can.</summary>
        RectTransform headerTitle;

        public event Action Back;
        public event Action<LevelDef> Pick;

        sealed class LevelNode
        {
            public LevelDef L;
            public RectTransform Root;
            public Button Button;
            public RRect Fill;
            public Image Img; // ice-kit orb (sprite swapped by state), else null
            public float Size;
            public GameObject Spot, Number, Lock;
            public Vec[] Stars;
            public Pulse Pulse;
        }

        readonly List<LevelNode> nodes = new List<LevelNode>();
        RectTransform root, content, pip;
        Text stars;

        /// <param name="safeArea">Makes a child that follows the screen's safe area.</param>
        public static MapScreen Build(RectTransform canvasRoot, Func<Transform, RectTransform> safeArea)
        {
            var root = K.Fill(canvasRoot, "Map");
            var m = root.gameObject.AddComponent<MapScreen>();
            m.root = root;
            m.BuildScroll();
            m.BuildTopBar(safeArea(root));
            return m;
        }

        void BuildScroll()
        {
            // Behind the content (seen only when the scroll overshoots): sky on top, snow below.
            K.Round(root, new Grad((0, "#2E4FB5"), (0.2f, "#7B6FD0"), (0.4f, Snow), (1, Snow)), 0).name = "sky";
            var view = K.Fill(root, "scroll");
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            content = K.Node(view, "world", new Vector2(0.5f, 1), Vector2.zero, new Vector2(390, MapH));
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.sizeDelta = new Vector2(0, MapH);
            scroll.viewport = view;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 24;

            K.Round(content, SkyGrad, 0).name = "sky";
            var band = SpriteTable.Background("b1");
            if (band != null)
            {
                // The play screen's sky band (mountains + cottages) across the top, fitted to the width; its
                // snowy foot fades into the map's snow.
                float bh = 390 * band.rect.height / band.rect.width;
                var b1 = K.Node(content, "b1", new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, bh));
                b1.anchorMin = new Vector2(0, 1);
                b1.anchorMax = new Vector2(1, 1);
                b1.sizeDelta = new Vector2(0, bh);
                var bi = b1.gameObject.AddComponent<Image>();
                bi.sprite = band;
                bi.raycastTarget = false;
                var fade = K.Node(content, "b1 fade", new Vector2(0.5f, 1), new Vector2(0, -(bh - 34)), new Vector2(0, 46));
                fade.anchorMin = new Vector2(0, 1);
                fade.anchorMax = new Vector2(1, 1);
                fade.sizeDelta = new Vector2(0, 46);
                var fg = fade.gameObject.AddComponent<RRect>();
                fg.Fill = new Grad((0, Snow + "00"), (0.7f, Snow), (1, Snow));
                fg.raycastTarget = false;
            }

            bool sprites = SpriteTable.Has("tree0") && SpriteTable.Has("tree1") && SpriteTable.Has("tree2") && SpriteTable.Has("tree3")
                && SpriteTable.Has("snowman") && SpriteTable.Has("rock0") && SpriteTable.Has("rock1");
            var art = K.Fill(content, "art").gameObject.AddComponent<Vec>();
            art.ViewBox = new Rect(0, 0, 390, MapH);
            art.raycastTarget = true; // drags anywhere scroll the map
            Art(art, band == null, !sprites);
            if (sprites) Scenery(content);

            // World 2 sign at the summit.
            var sign = K.Node(content, "world 2 sign", new Vector2(0, 1), new Vector2(195, -MapH * 0.132f), new Vector2(160, 50));
            sign.pivot = new Vector2(0.5f, 0.5f);
            // v5: the u2 wooden plaque (9-sliced); the drawn plank without it.
            RectTransform plank = sign;
            if (K.Sliced(sign, "ui.plank", 50) == null)
            {
                plank = K.Block(sign, Grad.Solid("#C98A5A"), 14, 3, 4, out var wood);
                wood.StripePeriod = 21;
                wood.StripeWidth = 3;
                wood.StripeColor = Look.Hex("#BC7D4E");
            }
            var lockIcon = K.LockIcon(plank);
            var line1 = K.Outlined(K.Node(plank, "name", new Vector2(0, 0.5f), new Vector2(0, 8), new Vector2(120, 20)), "โลก 2 · ป่าสน", 15, 1.2f, 0);
            var line2 = K.Outlined(K.Node(plank, "note", new Vector2(0, 0.5f), new Vector2(0, -9), new Vector2(120, 16)), "ยังไม่มีในต้นแบบ", 11, 1.2f, 0, TextAnchor.MiddleCenter, false);
            line1.alignment = line2.alignment = TextAnchor.MiddleLeft;
            line1.GetComponent<GameText>().Ink = line2.GetComponent<GameText>().Ink = K.Navy;
            float tw = Mathf.Max(K.Width(line1), K.Width(line2));
            float sw = 3 + 14 + 18 + 8 + tw + 14 + 3;
            sign.sizeDelta = new Vector2(sw, 50);
            K.Place(lockIcon, new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(18, 20));
            ((RectTransform)line1.transform.parent).anchoredPosition = new Vector2(14 + 18 + 8, 8);
            ((RectTransform)line2.transform.parent).anchoredPosition = new Vector2(14 + 18 + 8, -9);

            for (int i = 0; i < Levels.World1.Length; i++) nodes.Add(BuildNode(Levels.World1[i], i < NodePos.Length ? NodePos[i] : new Vector2(195, 980 - i * 130)));

            // Pip above the next level: a 60×74 window whose bottom sits just over the node.
            pip = K.Node(content, "pip", new Vector2(0, 1), Vector2.zero, new Vector2(60, 74));
            pip.pivot = new Vector2(0.5f, 0.5f);
            MapPip.Create2D(pip);
        }

        LevelNode BuildNode(LevelDef L, Vector2 at)
        {
            var n = new LevelNode { L = L };
            float size = L.Rival ? 84 : 66;
            n.Root = K.Node(content, "level " + L.Id, new Vector2(0, 1), new Vector2(at.x, -at.y), new Vector2(size, size));
            n.Root.pivot = new Vector2(0.5f, 0.5f);
            n.Size = size;
            RectTransform face;
            bool ice = SpriteTable.Has("ui.node") && SpriteTable.Has("ui.nodeBoss") && SpriteTable.Has("ui.nodeLock");
            if (ice)
            {
                // v6 ice kit (7 Nov 2026): glossy ice orb with a snow cap, gold number, gold padlock.
                face = K.Fill(n.Root, "face");
                var orb = new GameObject("orb", typeof(RectTransform));
                orb.transform.SetParent(face, false);
                n.Img = orb.AddComponent<Image>();
                FitOrb(n.Img, L.Rival ? "ui.nodeBoss" : "ui.node", size);
            }
            else
            {
                face = K.Block(n.Root, NodeGrad, size / 2, 4, 5, out n.Fill, 6, "#104E964D");
                // Navy rim and drop (the v5 kit's outline colour) and a crisp glare across the top.
                foreach (var part in new[] { n.Root.Find("drop"), face.Find("border") })
                    if (part) part.GetComponent<RRect>().color = Look.Hex(Navy);
                var glare = K.Round(face, new Color(1, 1, 1, 0.7f), size * 0.2f, size * 0.2f, size * 0.56f, size * 0.2f, size * 0.1f);
                glare.name = "glare";
                var spot = K.Round(face, Color.white, 6);
                spot.rectTransform.anchorMin = spot.rectTransform.anchorMax = new Vector2(0.34f, 0.72f);
                spot.rectTransform.sizeDelta = new Vector2(size * 0.17f, size * 0.17f);
                spot.rectTransform.anchoredPosition = Vector2.zero;
                spot.color = new Color(1, 1, 1, 0.95f);
                n.Spot = spot.gameObject;
            }
            var num = K.Gtext(face, L.Id.ToString(), 30, 3, 0);
            if (ice)
            {
                num.GetComponent<GameText>().SetGold();
                K.CentreBody(num);
            }
            else num.rectTransform.offsetMin = new Vector2(0, 2);
            n.Number = num.gameObject;
            var lk = K.LockIcon(face);
            if (ice)
            {
                lk.Shapes[0].Fill = null;
                lk.Shapes[0].FillGrad = K.Gold;
                lk.SetVerticesDirty();
            }
            K.Place(lk, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 29));
            n.Lock = lk.gameObject;

            // Three stars under the node, the middle one a little lower.
            // Ice orbs hang icicles ~11 units under the node: the stars sit 8 lower so the tips stay clear.
            var row = K.Node(n.Root, "stars", new Vector2(0.5f, 0), new Vector2(0, ice ? -4 : 4), new Vector2(60, 24));
            row.pivot = new Vector2(0.5f, 1);
            n.Stars = new Vec[3];
            for (int i = 0; i < 3; i++)
            {
                n.Stars[i] = K.StarIcon(row);
                K.Place(n.Stars[i], new Vector2(0.5f, 1), new Vector2((i - 1) * 20, i == 1 ? -4 : 0), new Vector2(20, 20));
            }

            // Name tag beside the node, away from the trail.
            bool right = at.x < 195;
            var tag = K.Node(n.Root, "name", new Vector2(right ? 1 : 0, 0.5f), new Vector2(right ? 10 : -10, 0), new Vector2(100, 24));
            tag.pivot = new Vector2(right ? 0 : 1, 0.5f);
            var tagFace = K.IceSlice(tag, "ui.iceTag", 26, out _);
            if (tagFace == null)
            {
                tagFace = tag;
                K.Round(tag, Look.Hex(Navy), 12);
                K.Round(tag, Color.white, 9.5f, 2.5f, 2.5f, 2.5f, 2.5f);
            }
            var name = K.CentreBody(K.Label(tagFace, L.Name, 13, Look.Hex(Navy), TextAnchor.MiddleCenter, true));
            tag.sizeDelta = new Vector2(K.Width(name) + 25, tagFace == tag ? 24 : 26);

            n.Button = K.Button(n.Root, face, n.Fill, () => Pick?.Invoke(L));
            if (n.Img) n.Button.targetGraphic = n.Img;
            n.Pulse = n.Root.gameObject.AddComponent<Pulse>();
            n.Pulse.Period = 1.3f;
            n.Pulse.Scale = 1.09f;
            return n;
        }

        /// <summary>Ice-kit orbs: x = the ice circle's centre from the sprite's top, y = its diameter over the sprite
        /// width (measured on the art, 7 Nov 2026; the locked orb carries a soft frost halo).</summary>
        static readonly Dictionary<string, Vector2> OrbFit = new Dictionary<string, Vector2>
        {
            { "ui.node", new Vector2(0.472f, 0.989f) }, { "ui.nodeBoss", new Vector2(0.497f, 0.989f) }, { "ui.nodeLock", new Vector2(0.491f, 0.865f) },
        };

        /// <summary>Sizes the orb so its ice circle spans the node and centres the circle on it; the snow cap and
        /// icicles reach past the node's square.</summary>
        static void FitOrb(Image img, string role, float size)
        {
            var sp = SpriteTable.Ui(role);
            var f = OrbFit[role];
            img.sprite = sp;
            img.raycastTarget = true;
            float w = size / f.y, h = w * sp.rect.height / sp.rect.width;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 1 - f.x);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>The ice-kit back button: the circle (centre 0.558 of the sprite height from its top, 0.99 of its
        /// width across) 50 units wide like the u2 one, the white chevron baked in.</summary>
        static void IceBack(RectTransform back, Action onClick)
        {
            var face = K.Fill(back, "face");
            var go = new GameObject("ice back", typeof(RectTransform));
            go.transform.SetParent(face, false);
            var img = go.AddComponent<Image>();
            img.sprite = SpriteTable.Ui("ui.iceBack");
            float w = 50 / 0.99f, h = w * img.sprite.rect.height / img.sprite.rect.width;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 1 - 0.558f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(w, h);
            var b = K.Button(back, face, null, onClick);
            b.targetGraphic = img;
            var p = back.GetComponent<Press>();
            p.Depth = 0;
            p.Scale = 0.9f;
        }

        void BuildTopBar(RectTransform bar)
        {
            // Sky-blue band behind the bar that fades into the map (reaches up under the notch).
            var band = K.Node(bar, "band", new Vector2(0.5f, 1), new Vector2(0, 120), new Vector2(0, 120 + 12 + 44 + 22));
            band.anchorMin = new Vector2(0, 1);
            band.anchorMax = new Vector2(1, 1);
            band.sizeDelta = new Vector2(0, band.sizeDelta.y);
            var g = band.gameObject.AddComponent<RRect>();
            g.raycastTarget = false;
            g.Fill = new Grad((0, "#2E4FB5F2"), (0.78f, "#3A55BCE0"), (1, "#3A55BC00"));

            var back = K.Node(bar, "back", new Vector2(0, 1), new Vector2(14, -12), new Vector2(44, 44));
            if (SpriteTable.Has("ui.iceBack")) IceBack(back, () => Back?.Invoke());
            else
            {
                K.RoundButton(back, () => Back?.Invoke(), out var bc);
                K.Place(K.BackIcon(bc), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22));
            }

            var title = K.Node(bar, "title", new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(220, 44));
            var titleText = K.Gtext(title, "โลก 1 · ทุ่งหิมะ", 23, 3, 0);
            if (SpriteTable.Has("ui.node")) titleText.GetComponent<GameText>().SetGold(); // v6: gold like the title sign
            headerTitle = title;

            var chip = K.Node(bar, "stars", new Vector2(1, 1), new Vector2(-14, -14), new Vector2(92, 40));
            var chipFace = SpriteTable.Has("ui.star") ? K.IceSlice(chip, "ui.icePill", 40, out _) : null;
            if (chipFace != null)
            {
                // v6: the blue ice pill, the gold star and the count on its face under the snow cap.
                K.Sprite(K.Node(chipFace, "star", new Vector2(0, 0.5f), new Vector2(-4, 0), new Vector2(30, 30)), "ui.star");
                stars = K.CentreBody(K.Outlined(K.Node(chipFace, "n", new Vector2(0, 0.5f), new Vector2(31, 0), new Vector2(60, 28)), "0/18", 18, 2.5f, 0, TextAnchor.MiddleLeft));
                stars.GetComponent<GameText>().Ink = K.Navy;
            }
            else if (K.Sliced(chip, "ui.pill", 40) != null && SpriteTable.Has("ui.star"))
            {
                // v5: the navy u2 pill with the gold u3 star over its left end (it counts stars, not coins).
                K.Sprite(K.Node(chip, "star", new Vector2(0, 0.5f), new Vector2(-4, 1), new Vector2(34, 34)), "ui.star");
                stars = K.Outlined(K.Node(chip, "n", new Vector2(0, 0.5f), new Vector2(33, 1), new Vector2(60, 34)), "0/18", 18, 2.5f, 0, TextAnchor.MiddleLeft);
                stars.GetComponent<GameText>().Ink = K.Navy;
            }
            else
            {
                var face = K.Block(chip, Grad.Solid("#E4F6FF"), 20, 3, 4, out _);
                var star = K.StarIcon(face);
                K.Place(star, new Vector2(0, 0.5f), new Vector2(6, 0), new Vector2(24, 24));
                stars = K.Label(K.Node(face, "n", new Vector2(0, 0.5f), new Vector2(34, 0), new Vector2(60, 34)), "0/18", 18, K.Ink, TextAnchor.MiddleLeft, true);
            }
        }

        /// <summary>Refreshes locks, stars and the current level, then scrolls it into view.</summary>
        public void Show(Progress p)
        {
            gameObject.SetActive(true);
            var cur = p.NextPlayable();
            LevelNode curNode = null;
            foreach (var n in nodes)
            {
                bool ok = p.Unlocked(n.L.Id);
                int s = p.StarsOf(n.L.Id);
                n.Button.interactable = ok;
                if (n.Img) FitOrb(n.Img, !ok ? "ui.nodeLock" : n.L.Rival ? "ui.nodeBoss" : "ui.node", n.Size);
                else n.Fill.Set(!ok ? K.LockedGrad : n.L.Rival ? K.BossGrad : NodeGrad, n.Fill.Radius);
                if (n.Spot) n.Spot.SetActive(ok);
                n.Number.SetActive(ok);
                n.Lock.SetActive(!ok);
                for (int i = 0; i < 3; i++)
                {
                    var on = i < s;
                    var shape = n.Stars[i].Shapes[0];
                    if (on) { shape.Fill = null; shape.FillGrad = K.Gold; shape.Stroke = K.Ink; }
                    else { shape.FillGrad = null; shape.Fill = Look.Hex("#D6E2EE"); shape.Stroke = new Color(K.Ink.r, K.Ink.g, K.Ink.b, 0.55f); }
                    n.Stars[i].SetVerticesDirty();
                }
                bool isCur = ok && n.L == cur;
                n.Pulse.enabled = isCur;
                if (isCur) curNode = n;
            }
            stars.text = p.TotalStars + "/" + Progress.MaxStars;
            pip.gameObject.SetActive(curNode != null);
            if (curNode != null) pip.anchoredPosition = curNode.Root.anchoredPosition + new Vector2(0, 79);

            // Scroll so the current node sits a little below the middle of the map area under the header, but no
            // further than keeps the top level's name tag clear of the header (the map is barely taller than
            // the screen), unless that would push the current node and its stars off the bottom.
            Canvas.ForceUpdateCanvases();
            var focus = curNode ?? nodes[0];
            float viewH = root.rect.height, nodeTop = -focus.Root.anchoredPosition.y - focus.Root.sizeDelta.y / 2;
            float header = HeaderBottom() + 10;
            float want = nodeTop - (header + (viewH - header) * 0.55f);
            var top = nodes[nodes.Count - 1];
            float topTag = -top.Root.anchoredPosition.y - 12;
            float focusBottom = -focus.Root.anchoredPosition.y + focus.Root.sizeDelta.y / 2 + 30;
            float cap = Mathf.Max(topTag - header, focusBottom - viewH + 8);
            content.anchoredPosition = new Vector2(0, Mathf.Clamp(Mathf.Min(want, cap), 0, Mathf.Max(0, MapH - viewH)));
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>The header title's bottom edge, measured from the top of the screen in map units.</summary>
        float HeaderBottom()
        {
            if (headerTitle == null) return 70;
            var c = new Vector3[4];
            headerTitle.GetWorldCorners(c);
            var local = root.InverseTransformPoint(c[0]);
            return root.rect.yMax - local.y;
        }

        /// <summary>The s04 trees, snowman and rocks where the drawn ones stand, bottoms on the snow.</summary>
        void Scenery(RectTransform parent)
        {
            for (int i = 0; i < Trees.GetLength(0); i++)
            {
                float w = Trees[i, 2], h = Trees[i, 3];
                Prop(parent, "tree" + (i % 4), Trees[i, 0] + w / 2, Trees[i, 1] + h, h * 1.3f, true);
            }
            Prop(parent, "snowman", 330, 822, 62, true);
            for (int i = 0; i < Rocks.GetLength(0); i++) Prop(parent, "rock" + i, Rocks[i, 0], Rocks[i, 1], Rocks[i, 2], false);
        }

        static void Prop(RectTransform parent, string role, float x, float bottom, float size, bool byHeight)
        {
            var sp = SpriteTable.Get(role).S;
            float aspect = sp.rect.width / sp.rect.height;
            var sz = byHeight ? new Vector2(size * aspect, size) : new Vector2(size, size / aspect);
            // The slices carry a little transparent padding under the feet: sink them by 4 % of the height.
            var rt = K.Node(parent, role, new Vector2(0, 1), new Vector2(x, -bottom - sz.y * 0.04f), sz);
            rt.pivot = new Vector2(0.5f, 0);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sp;
            img.preserveAspect = true;
            img.raycastTarget = false;
        }

        // ---------- map art (index.html .map-bg) ----------

        const string InkHex = "#2B2140";

        /// <param name="mountains">Draw the far mountains and snow hill (no b1 sprite).</param>
        /// <param name="drawnScenery">Draw the trees and the snowman (no s04 slices).</param>
        static void Art(Vec v, bool mountains, bool drawnScenery)
        {
            if (mountains) DrawnMountains(v);
            // Blue-violet shadow blobs in the snow, then a few sparkles.
            v.Add(Shape.Ellipse(82, 905, 64, 18).WithFill(Shadow));
            v.Add(Shape.Ellipse(318, 650, 60, 16).WithFill(Shadow));
            v.Add(Shape.Ellipse(70, 515, 46, 12).WithFill(Shadow));
            v.Add(Shape.Ellipse(330, 1065, 52, 13).WithFill(Shadow));
            v.Add(Shape.Ellipse(205, 790, 40, 10).WithFill(new Color(0.81f, 0.87f, 0.97f, 0.7f)));
            float[,] sparkles = { { 40, 470, 5 }, { 350, 600, 6 }, { 190, 1040, 5 }, { 360, 860, 4 }, { 30, 700, 4 }, { 300, 330, 5 }, { 80, 1080, 4 } };
            for (int i = 0; i < sparkles.GetLength(0); i++)
            {
                float x = sparkles[i, 0], y = sparkles[i, 1], r = sparkles[i, 2], q = r * 0.28f;
                v.Add(Shape.Path(FormattableString.Invariant($"M{x} {y - r} L{x + q} {y - q} L{x + r} {y} L{x + q} {y + q} L{x} {y + r} L{x - q} {y + q} L{x - r} {y} L{x - q} {y - q} Z"))
                    .WithFill("#FFFFFF").WithStroke("#B7C8EE", 1));
            }
            // The frozen pond.
            v.Add(Shape.Ellipse(300, 424, 60, 22).WithFill(Shadow));
            v.Add(Shape.Ellipse(300, 420, 58, 22).WithFill("#BFE6FF").WithStroke(Navy, 3));
            v.Add(Shape.Path("M268 414 L284 428 M300 410 L318 426").WithStroke("#FFFFFF", 3));
            // The trail: a soft shadow, navy edge, pale ice inner edge, cream snow, glacier-blue dashes.
            const string trail = "M110 1100 L110 980 C110 920 270 920 270 860 C270 800 130 800 130 730 C130 660 260 670 260 600 C260 530 120 540 120 470 C120 400 220 400 220 330 C220 262 195 236 195 178";
            v.Add(Shape.Path(trail, 1, 0, 6).WithStroke("#B4C2EA", 46));
            v.Add(Shape.Path(trail).WithStroke(Navy, 38));
            v.Add(Shape.Path(trail).WithStroke("#CDE6F8", 30));
            v.Add(Shape.Path(trail).WithStroke("#FFF9EE", 24));
            v.Add(Shape.Path(trail).WithStroke("#4FA3F7", 3.5f).WithDash(10, 12));
            if (!drawnScenery)
            {
                // Soft shadows under the sprite trees, snowman and rocks (Scenery places the sprites).
                for (int i = 0; i < Trees.GetLength(0); i++)
                {
                    float w = Trees[i, 2], h = Trees[i, 3];
                    v.Add(Shape.Ellipse(Trees[i, 0] + w / 2, Trees[i, 1] + h, w * 0.55f, w * 0.14f).WithFill(new Color(0.62f, 0.69f, 0.88f, 0.55f)));
                }
                v.Add(Shape.Ellipse(330, 822, 22, 6).WithFill(new Color(0.62f, 0.69f, 0.88f, 0.55f)));
                for (int i = 0; i < Rocks.GetLength(0); i++)
                    v.Add(Shape.Ellipse(Rocks[i, 0], Rocks[i, 1], Rocks[i, 2] * 0.55f, Rocks[i, 2] * 0.14f).WithFill(new Color(0.62f, 0.69f, 0.88f, 0.55f)));
            }
            else DrawnScenery(v);
            // Flags either side of the last level.
            v.Add(Shape.Rect(168, 262, 3, 40).WithFill(Navy).WithStroke(Navy, 2));
            v.Add(Shape.Path("M171 264 H192 L186 271 L192 278 H171 Z").WithFill("#FF5C93").WithStroke(Navy, 2));
            v.Add(Shape.Rect(268, 270, 3, 40).WithFill(Navy).WithStroke(Navy, 2));
            v.Add(Shape.Path("M271 272 H292 L286 279 L292 286 H271 Z").WithFill("#FFC93C").WithStroke(Navy, 2));
        }

        /// <summary>Fallback without the b1 sprite: the web's far mountains with snowcaps and the snow hill.</summary>
        static void DrawnMountains(Vec v)
        {
            v.Add(Shape.Path("M0 230 L0 120 L60 62 L112 112 L172 28 L242 118 L302 48 L390 140 L390 230 Z").WithFill("#AFC2F6"));
            v.Add(Shape.Path("M172 28 L190 52 Q182 48 178 55 Q172 48 166 55 Q160 49 154 52 Z M302 48 L318 68 Q312 65 308 71 Q302 65 296 71 Q291 66 286 68 Z M60 62 L74 80 Q69 77 65 83 Q60 77 55 83 Q50 78 46 80 Z").WithFill("#FFFFFF"));
            v.Add(Shape.Path("M172 28 L242 118 L186 118 Z M302 48 L390 140 L318 140 Z").WithFill(new Color(1, 1, 1, 0.25f)));
            v.Add(Shape.Path("M0 250 Q100 180 200 212 Q300 244 390 190 L390 300 L0 300 Z").WithFill("#FFFFFF"));
            v.Add(Shape.Path("M0 262 Q100 196 200 226 Q300 256 390 204").WithStroke("#DCE8FF", 6));
        }

        /// <summary>Fallback without the s04 slices: the web's drawn trees and snowman.</summary>
        static void DrawnScenery(Vec v)
        {
            // Trees (symbol #mtree, 40×52).
            var trees = Trees;
            for (int i = 0; i < trees.GetLength(0); i++)
            {
                float w = trees[i, 2], h = trees[i, 3], s = Mathf.Min(w / 40, h / 52);
                float x = trees[i, 0] + (w - 40 * s) / 2, y = trees[i, 1] + (h - 52 * s) / 2;
                v.Add(Shape.Rect(17, 40, 6, 10, 2).WithFill("#9A5B2E").WithStroke(InkHex, 2.2f).At(s, x, y));
                v.Add(Shape.Path("M20 14 L37 42 Q20 47 3 42 Z").WithFill("#1AA56A").WithStroke(InkHex, 2.2f).At(s, x, y));
                v.Add(Shape.Path("M20 2 L32 26 Q20 30 8 26 Z").WithFill("#28C17F").WithStroke(InkHex, 2.2f).At(s, x, y));
                v.Add(Shape.Path("M20 3 L26 13 Q23 11 21 14 Q20 11 18 14 Q16 11 14 13 Z").WithFill("#FFFFFF").At(s, x, y));
                v.Add(Shape.Path("M8 40 Q14 36 20 40 Q26 36 32 40 L35 42 Q20 47 5 42 Z").WithFill("#FFFFFF").At(s, x, y));
            }
            // Snowman.
            v.Add(Shape.Circle(0, 16, 15).WithFill("#FFFFFF").WithStroke(InkHex, 2.2f).At(1, 330, 790));
            v.Add(Shape.Circle(0, -6, 10).WithFill("#FFFFFF").WithStroke(InkHex, 2.2f).At(1, 330, 790));
            v.Add(Shape.Rect(-8, -24, 16, 9, 2).WithFill(InkHex).WithStroke(InkHex, 2.2f).At(1, 330, 790));
            v.Add(Shape.Path("M0 -5 L9 -3 L0 -1 Z").WithFill("#FF9A1F").At(1, 330, 790));
        }
    }
}
