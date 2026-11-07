// Builders for the toy UI (index.html's .toy / .big-btn / .ice-btn / .chip-btn / .gpanel / .gtext):
// thick ink outlines, an ink drop under everything, glossy ice gradients; plus the icons, copied
// path-for-path from the web's inline SVGs, and the little looping animations. Since the v5 art pass
// (5 Nov 2026) the buttons, panels, ribbons, pills and plaques are the glossy toy-3D slices of the u2 / u3
// sheets (SpriteTable ui.* roles, 9-sliced where they stretch); each builder falls back to the procedural
// widget when its slice is missing. Text has no drop shadow (user, 5 Nov 2026), only the navy outline.
using System;
using System.Collections.Generic;
using Pip.Game.Art2D;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Pip.Game.Ui
{
    public static class K
    {
        public static readonly Color Ink = Look.Hex("#2B2140"), InkSoft = Look.Hex("#4A3F6B");
        /// <summary>The navy of the v5 UI kit's outlines (lettering on the sprite widgets).</summary>
        public static readonly Color Navy = Look.Hex("#1D2B5C");

        // Gradients from the web stylesheet.
        public static readonly Grad BigBtn = new Grad((0, "#E6FAFF"), (0.22f, "#A4E4FF"), (0.62f, "#52BDF2"), (1, "#2A8FDA"));
        public static readonly Grad BlueBtn = new Grad((0, "#DDEBFF"), (0.22f, "#93C2FF"), (0.62f, "#4F8DEB"), (1, "#2F5FC6"));
        public static readonly Grad IceBtn = new Grad((0, "#FFFFFF"), (0.45f, "#DDF5FF"), (1, "#A9E0F8"));
        public static readonly Grad Toy = new Grad((0, "#FFFFFF"), (1, "#DDF4FF"));
        public static readonly Grad ChipOn = new Grad((0, "#9BE0FF"), (1, "#2A8FDA"));
        public static readonly Grad Gold = new Grad((0, "#FFF6B0"), (0.5f, "#FFD43B"), (1, "#FF9F1C"));
        public static readonly Grad NodeGrad = new Grad((0, "#E6FAFF"), (0.3f, "#A4E4FF"), (0.7f, "#4DB8F0"), (1, "#2A8FDA"));
        public static readonly Grad BossGrad = new Grad((0, "#FFF3C4"), (0.4f, "#FFD84A"), (1, "#FFA61F"));
        public static readonly Grad LockedGrad = new Grad((0, "#EEF3F7"), (1, "#BFD0DD"));
        public static readonly Grad Badge = new Grad((0, "#7FD3FA"), (1, "#2A8FDA"));

        static Font body, game;

        /// <summary>Body text (Mitr in the web). OS Thai font until the bundled Kanit/Mitr land.</summary>
        public static Font Body => body ? body : body = Font.CreateDynamicFontFromOSFont(new[] { "Sukhumvit Set", "Thonburi", "Noto Sans Thai", "Leelawadee UI", "Arial Unicode MS", "Helvetica Neue", "Arial" }, 32);

        /// <summary>Game lettering (Fredoka + Kanit in the web): rounded Latin and digits, Thai from the fallbacks.</summary>
        public static Font Game => game ? game : game = Font.CreateDynamicFontFromOSFont(new[] { "Arial Rounded MT Bold", "Sukhumvit Set", "Thonburi", "Noto Sans Thai", "Leelawadee UI", "Arial Unicode MS", "Arial" }, 32);

        // ---------- layout ----------

        public static RectTransform Node(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Fills the parent, inset by left / bottom / right / top.</summary>
        public static RectTransform Fill(Transform parent, string name, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        public static void NoLayout(Component c) => (c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>()).ignoreLayout = true;

        /// <summary>Fixed sizes never shrink; a flexible item (flexW > 0) takes whatever width is left.</summary>
        public static LayoutElement Sized(Component c, float w = -1, float h = -1, float flexW = -1)
        {
            var le = c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = flexW > 0 && w < 0 ? 1 : w;
            le.minWidth = w;
            le.preferredHeight = h;
            le.minHeight = h;
            le.flexibleWidth = flexW;
            return le;
        }

        // ---------- shapes ----------

        public static RRect Round(Transform parent, Grad fill, float radius, float l = 0, float b = 0, float r = 0, float t = 0, bool raycast = false)
        {
            var rt = Fill(parent, "shape", l, b, r, t);
            var g = rt.gameObject.AddComponent<RRect>();
            g.Fill = fill;
            g.Radius = radius;
            g.raycastTarget = raycast;
            return g;
        }

        public static RRect Round(Transform parent, Color c, float radius, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            var g = Round(parent, null, radius, l, b, r, t);
            g.color = c;
            return g;
        }

        /// <summary>The toy block every button and chip is made of: an ink drop below, then an ink
        /// border around a gradient face. Returns the face, the parent for the content.</summary>
        public static RectTransform Block(RectTransform root, Grad fill, float radius, float border, float drop, out RRect face, float insetBottom = 0, string insetHex = null)
        {
            Round(root, Ink, radius, 0, -drop, 0, drop).name = "drop";
            var lift = Fill(root, "face");
            Round(lift, Ink, radius).name = "border";
            face = Round(lift, fill, Mathf.Max(0, radius - border), border, border, border, border, true);
            face.name = "fill";
            if (insetBottom > 0) { face.InsetBottom = insetBottom; face.InsetColor = Look.Hex(insetHex); }
            return lift;
        }

        // ---------- sprite widgets (u2 / u3) ----------

        /// <summary>A ui.* slice as a plain Image filling the parent (inset l / b / r / t), or null when the
        /// slice is missing.</summary>
        public static Image Sprite(Transform parent, string role, float l = 0, float b = 0, float r = 0, float t = 0, bool preserveAspect = true)
        {
            var sp = SpriteTable.Ui(role);
            if (sp == null) return null;
            var img = Fill(parent, role, l, b, r, t).gameObject.AddComponent<Image>();
            img.sprite = sp;
            img.preserveAspect = preserveAspect;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>A 9-sliced ui.* slice filling the parent; the whole slice is scaled so its height maps to
        /// refHeight units (its borders keep their proportions), or null when the slice is missing.</summary>
        public static Image Sliced(Transform parent, string role, float refHeight, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            var sp = SpriteTable.Ui(role);
            if (sp == null) return null;
            var img = Fill(parent, role, l, b, r, t).gameObject.AddComponent<Image>();
            img.sprite = sp;
            img.type = Image.Type.Sliced;
            img.fillCenter = true;
            img.pixelsPerUnitMultiplier = sp.rect.height / Mathf.Max(1, refHeight) * (100f / sp.pixelsPerUnit);
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Ice-kit slices (7 Nov 2026): x = the snow cap's lower edge, y = the body's bottom (above the
        /// icicles), as fractions of the slice height from its top, measured on the art.</summary>
        public static readonly Dictionary<string, Vector2> IceFace = new Dictionary<string, Vector2>
        {
            { "ui.btnGreen", new Vector2(0.270f, 0.823f) }, { "ui.btnBlue", new Vector2(0.232f, 0.786f) },
            { "ui.btnIce", new Vector2(0.259f, 0.790f) }, { "ui.tagline", new Vector2(0.263f, 0.795f) },
            { "ui.icePill", new Vector2(0.280f, 0.983f) }, { "ui.iceTag", new Vector2(0.294f, 0.983f) },
            { "ui.chipOn", new Vector2(0.336f, 0.942f) }, { "ui.chipOff", new Vector2(0.290f, 0.984f) },
        };

        /// <summary>An ice-kit slice laid so its snow top to body bottom fills the parent's height h (the icicles
        /// hang below it), 9-sliced sideways. Returns the face under the snow cap, where the content goes, or null
        /// when the slice is missing.</summary>
        public static RectTransform IceSlice(Transform parent, string role, float h, out Image img)
        {
            img = null;
            if (!IceFace.TryGetValue(role, out var f) || !SpriteTable.Has(role)) return null;
            float full = h / f.y;
            img = Sliced(parent, role, full, 0, h - full, 0, 0);
            return Fill(parent, "face", 0, 0, 0, f.x * full);
        }

        /// <summary>A pill button from a u3 slice: the face sinks a little and shrinks while held.</summary>
        static Button SpriteButton(RectTransform root, string role, Action onClick, out RectTransform content)
        {
            float h = root.sizeDelta.y;
            var face = Fill(root, "face");
            if (IceFace.ContainsKey(role))
            {
                // Ice kit: snow cap and body fill the rect, the icicles hang below; content sits on the face.
                var area = IceSlice(face, role, h, out var ice);
                ice.raycastTarget = true;
                content = Fill(area, "content", 6, 0, 6, 0);
                var ib = Button(root, face, null, onClick);
                ib.targetGraphic = ice;
                var ip = root.GetComponent<Press>();
                ip.Depth = 2;
                ip.Scale = 0.95f;
                return ib;
            }
            var img = Sliced(face, role, h);
            img.raycastTarget = true;
            // A near-square button (the map / retry buttons, 60 × 60): the pill's whole stretchable middle
            // would be squeezed into a few units, and the high mip level that picks blurred the outline into a
            // pale streak down the centre. Lower the multiplier so the borders come out 2 % wider (and taller)
            // than the rect: Image.GetAdjustedBorders then scales them back to fit exactly, the caps meet with
            // no middle quad, and the same Image draws a clean round button.
            var sp = img.sprite;
            float unit = 100f / sp.pixelsPerUnit / img.pixelsPerUnitMultiplier;
            float caps = (sp.border.x + sp.border.z) * unit, middle = (sp.rect.width - sp.border.x - sp.border.z) * unit;
            float w = root.sizeDelta.x;
            if (w > 0 && w - caps < middle * 0.5f)
                img.pixelsPerUnitMultiplier *= caps / (w * 1.02f);
            // The slice carries its 3D lip and soft shadow under the face: lift the content to the centre of
            // the face (measured 7 Nov 2026 on the green / blue / ice pills: the face sits 0.035 h above the
            // old content centre, i.e. 0.095 h above the rect centre).
            content = Fill(face, "content", 6, 6 + h * 0.13f, 6, 6);
            var b = Button(root, face, null, onClick);
            b.targetGraphic = img;
            var p = root.GetComponent<Press>();
            p.Depth = 2;
            p.Scale = 0.95f;
            return b;
        }

        // ---------- text ----------

        /// <summary>One line of text; paragraphs set horizontalOverflow back to Wrap.</summary>
        public static Text Label(Transform parent, string s, int size, Color c, TextAnchor align = TextAnchor.MiddleCenter, bool game = false)
        {
            var rt = Fill(parent, "label");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = game ? Game : Body;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.color = c;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = s;
            return t;
        }

        /// <summary>Game lettering: thick ink outline, white / ice two-tone fill (no drop unless asked).</summary>
        public static Text Gtext(Transform parent, string s, int size, float stroke = 3.5f, float drop = 0, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var t = Label(parent, s, size, Color.white, align, true);
            var g = t.gameObject.AddComponent<GameText>();
            g.Stroke = stroke;
            g.Drop = drop;
            return t;
        }

        /// <summary>White text with the web's .outline: a 2px ink edge (no drop unless asked).</summary>
        public static Text Outlined(Transform parent, string s, int size, float stroke = 2, float drop = 0, TextAnchor align = TextAnchor.MiddleCenter, bool game = true)
        {
            var t = Label(parent, s, size, Color.white, align, game);
            var g = t.gameObject.AddComponent<GameText>();
            g.Stroke = stroke;
            g.Drop = drop;
            g.TwoTone = false;
            g.Dirs = 12;
            return t;
        }

        public static float Width(Text t) => Mathf.Ceil(t.preferredWidth);

        /// <summary>Centres the label's letters (their body, not the font's line box) in its rect, offset units
        /// higher; adds a plain GameText (no outline) when the label has none.</summary>
        public static Text CentreBody(Text t, float offset = 0)
        {
            var g = t.GetComponent<GameText>();
            if (!g)
            {
                g = t.gameObject.AddComponent<GameText>();
                g.Stroke = 0;
                g.Drop = 0;
                g.TwoTone = false;
                g.Dirs = 0;
            }
            g.CentreBody = true;
            g.CentreOffset = offset;
            t.SetVerticesDirty();
            return t;
        }

        static TextGenerator inkGen;

        /// <summary>Horizontal extent of the drawn glyphs (min / max x, units) when the text is laid out
        /// MiddleLeft from x = 0: the layout width (preferredWidth) also counts the side bearings and the last
        /// advance, so centring by it leaves the glyphs off-centre. Mesh effects (GameText's outline) are
        /// not included. (0, Width) for an empty string.</summary>
        public static Vector2 InkSpan(Text t)
        {
            inkGen ??= new TextGenerator();
            var s = t.GetGenerationSettings(new Vector2(4096, 512));
            s.textAnchor = TextAnchor.MiddleLeft;
            s.pivot = new Vector2(0, 0.5f);
            s.horizontalOverflow = HorizontalWrapMode.Overflow;
            s.generateOutOfBounds = true;
            s.resizeTextForBestFit = false;
            inkGen.Populate(t.text, s);
            float lo = float.MaxValue, hi = float.MinValue, unit = 1f / Mathf.Max(0.0001f, t.pixelsPerUnit);
            var v = inkGen.verts;
            for (int i = 0; i + 3 < v.Count; i += 4)
            {
                float a = Mathf.Min(v[i].position.x, v[i + 2].position.x), b = Mathf.Max(v[i].position.x, v[i + 2].position.x);
                if (b - a < 0.01f) continue; // spaces / empty quads
                lo = Mathf.Min(lo, a); hi = Mathf.Max(hi, b);
            }
            return hi < lo ? new Vector2(0, Width(t)) : new Vector2(lo * unit, hi * unit);
        }

        // ---------- buttons ----------

        public static Button Button(RectTransform root, RectTransform face, RRect hit, Action onClick)
        {
            var b = root.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            if (hit) b.targetGraphic = hit;
            b.onClick.AddListener(() => onClick?.Invoke());
            var p = root.gameObject.AddComponent<Press>();
            p.Face = face;
            p.Target = b;
            return b;
        }

        /// <summary>.big-btn: tall glossy ice capsule; blue = the second-tier button.</summary>
        public static Button BigButton(RectTransform root, bool blue, Action onClick, out RectTransform content)
        {
            // v5: green = the primary action (play, resume, next level, retry), blue = the second tier.
            string role = blue ? "ui.btnBlue" : "ui.btnGreen";
            if (SpriteTable.Has(role)) return SpriteButton(root, role, onClick, out content);
            float h = root.sizeDelta.y;
            var face = Block(root, blue ? BlueBtn : BigBtn, h / 2, 4, 6, out var fill, 6, "#104E9652");
            content = Fill(face, "content", 4, 4, 4, 4);
            // Frost glare along the top (.big-btn::before).
            Round(content, new Color(1, 1, 1, 0.6f), 10, 18, h - 8 - 5 - 12, 18, 5).name = "gloss";
            if (!blue) Shimmer.AddTo(fill);
            return Button(root, face, fill, onClick);
        }

        /// <summary>.ice-btn: white-to-ice rounded square, ink label.</summary>
        public static Button IceButton(RectTransform root, Action onClick, out RectTransform content, float radius = 20)
        {
            // Near-square ice buttons (the result screen's map / retry) keep the plain u3 pill: the ice kit's snow
            // cap and icicles do not survive being squeezed into a square.
            bool square = root.sizeDelta.x < root.sizeDelta.y * 1.6f;
            string iceRole = square && SpriteTable.Has("ui.btnIceSq") ? "ui.btnIceSq" : "ui.btnIce";
            if (SpriteTable.Has(iceRole)) return SpriteButton(root, iceRole, onClick, out content);
            var face = Block(root, IceBtn, radius, 4, 5, out var fill, 5, "#2A8FDA40");
            content = Fill(face, "content", 4, 4, 4, 4);
            return Button(root, face, fill, onClick);
        }

        /// <summary>.round.toy: the 44px round icon button. v5: the navy ui.back / ui.pause slice, which
        /// carries its own white glyph, so the content (where callers put the vector icon) is hidden.</summary>
        public static Button RoundButton(RectTransform root, Action onClick, out RectTransform content, string role = "ui.back")
        {
            if (SpriteTable.Has(role))
            {
                var rface = Fill(root, "face");
                var img = Sprite(rface, role, -3, -3, -3, -3);
                img.raycastTarget = true;
                content = Fill(rface, "content");
                content.gameObject.SetActive(false);
                var b = Button(root, rface, null, onClick);
                b.targetGraphic = img;
                var p = root.GetComponent<Press>();
                p.Depth = 0;
                p.Scale = 0.9f;
                return b;
            }
            var face = Block(root, Toy, root.sizeDelta.x / 2, 3, 4, out var fill);
            content = Fill(face, "content", 3, 3, 3, 3);
            return Button(root, face, fill, onClick);
        }

        /// <summary>A row of icon + label (or label + icon) centred in a button face.</summary>
        public static Text IconLabel(RectTransform content, Vec icon, float iconSize, string label, int size, Color c, float gap = 10, bool outlined = false, bool iconAfter = false)
        {
            var row = Node(content, "row", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10, iconSize));
            var t = outlined ? Outlined(row, label, size) : Label(row, label, size, c, TextAnchor.MiddleLeft, true);
            t.alignment = TextAnchor.MiddleLeft;
            CentreBody(t);
            float tw = Width(t), iw = icon ? iconSize + gap : 0;
            row.sizeDelta = new Vector2(iw + tw, Mathf.Max(iconSize, size * 1.4f));
            if (icon)
            {
                var irt = icon.rectTransform;
                irt.SetParent(row, false);
                irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(iconAfter ? 1 : 0, 0.5f);
                irt.anchoredPosition = Vector2.zero;
                irt.sizeDelta = new Vector2(iconSize, iconSize);
            }
            var lrt = t.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(iconAfter ? 0 : iw, 0);
            lrt.offsetMax = new Vector2(iconAfter ? -iw : 0, 0);
            return t;
        }

        // ---------- icons (paths from index.html) ----------

        const string Ice = "#2A8FDA", InkHex = "#2B2140";
        public const string StarPath = "M12 2.5l2.9 6 6.6.9-4.8 4.6 1.2 6.5L12 17.4 6.1 20.5l1.2-6.5L2.5 9.4l6.6-.9z";

        public static Vec Icon(Transform parent, string name, Rect viewBox = default)
        {
            var rt = Node(parent, name, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24, 24));
            var v = rt.gameObject.AddComponent<Vec>();
            v.raycastTarget = false;
            v.ViewBox = viewBox.width > 0 ? viewBox : new Rect(0, 0, 24, 24);
            return v;
        }

        public static Vec PlayIcon(Transform p, string fill = "#FFFFFF") =>
            Icon(p, "play").Add(Shape.Path("M7 4.5v15l12-7.5z").WithFill(fill).WithStroke(InkHex, 2));

        public static Vec MapIcon(Transform p) => Icon(p, "map")
            .Add(Shape.Path("M3 6.5l5.5-2.5 7 2.5L21 4v13.5l-5.5 2.5-7-2.5L3 20z").WithFill(Ice).WithStroke(InkHex, 1.8f))
            .Add(Shape.Path("M8.5 4v13.5M15.5 6.5V20").WithStroke(InkHex, 1.6f));

        public static Vec RetryIcon(Transform p, string tint = Ice) => Icon(p, "retry")
            .Add(Shape.Path("M18.6 8.4A7.2 7.2 0 1 0 19.2 14").WithStroke(InkHex, 6))
            .Add(Shape.Path("M18.6 8.4A7.2 7.2 0 1 0 19.2 14").WithStroke(tint, 2.8f))
            .Add(Shape.Path("M21.6 3.6l-.4 7.2-6.9-2.3z").WithFill(tint).WithStroke(InkHex, 1.6f));

        public static Vec HomeIcon(Transform p) => Icon(p, "home")
            .Add(Shape.Path("M3.5 11.5 12 4l8.5 7.5h-2.2V20h-4.6v-5h-3.4v5H5.7v-8.5z").WithFill(Ice).WithStroke(InkHex, 1.8f));

        public static Vec BackIcon(Transform p) => Icon(p, "back")
            .Add(Shape.Path("M15 5l-7 7 7 7").WithStroke(InkHex, 2.6f));

        public static Vec PauseIcon(Transform p) => Icon(p, "pause")
            .Add(Shape.Rect(5.5f, 4, 4.5f, 16, 2).WithFill(InkHex))
            .Add(Shape.Rect(14, 4, 4.5f, 16, 2).WithFill(InkHex));

        public static Vec LockIcon(Transform p) => Icon(p, "lock", new Rect(0, 0, 18, 20))
            .Add(Shape.Rect(2, 8, 14, 11, 3).WithFill("#8C84A8").WithStroke(InkHex, 2))
            .Add(Shape.Path("M5 8V6a4 4 0 0 1 8 0v2").WithStroke(InkHex, 2.4f));

        public static Vec StarIcon(Transform p, bool on = true)
        {
            var s = Shape.Path(StarPath);
            if (on) s.WithGrad(Gold).WithStroke(InkHex, 2);
            else { s.WithFill("#D6E2EE"); s.Stroke = new Color(Ink.r, Ink.g, Ink.b, 0.55f); s.StrokeWidth = 2; }
            return Icon(p, "star").Add(s);
        }

        public static Vec FlakeIcon(Transform p) => Icon(p, "flake")
            .Add(Shape.Circle(12, 12, 9.5f).WithFill("#FFC93C").WithStroke(InkHex, 2))
            .Add(Shape.Path("M12 7v10M7.7 9.5l8.6 5M7.7 14.5l8.6-5").WithStroke("#FFFFFF", 2));

        public static Vec CoinIcon(Transform p) => Icon(p, "coin")
            .Add(Shape.Circle(12, 12, 10.5f).WithFill("#FFC93C").WithStroke(InkHex, 1.8f))
            .Add(Shape.Path("M12 6.5v11M7.2 9.2l9.6 5.6M7.2 14.8l9.6-5.6").WithStroke("#FFFFFF", 2));

        public const string HeartPath = "M12 21s-7.5-4.6-9.5-9.2C1 8.4 3.1 5 6.6 5c2 0 3.6 1.1 5.4 3 1.8-1.9 3.4-3 5.4-3 3.5 0 5.6 3.4 4.1 6.8C19.5 16.4 12 21 12 21z";

        public static Vec FlagIcon(Transform p) => Icon(p, "flag", new Rect(0, 0, 22, 26))
            .Add(Shape.Rect(3, 2, 3, 23, 1.5f).WithFill(InkHex))
            .Add(Shape.Path("M6 3h14l-3.5 4.5L20 12H6z").WithFill("#FF5C93").WithStroke(InkHex, 1.6f));

        public static void Place(Graphic g, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = g.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        // ---------- panels ----------

        public sealed class PanelParts
        {
            public RectTransform Root, Body;
            public Text Title;
            public RRect Band;
            public Vec Tails;
            /// <summary>v5: the red u3 ribbon header (Band / Tails are null then).</summary>
            public Image Ribbon;
            Color tail, fold;

            /// <summary>Blue ribbon for a win, slate for a fall (.gpanel.fail). The v5 sprite ribbon stays red.</summary>
            public void SetFail(bool fail)
            {
                if (Band == null) return;
                Band.Set(fail ? new Grad((0, "#DCE8F2"), (0.45f, "#9DB8D2"), (1, "#6787A8")) : new Grad((0, "#C6F1FF"), (0.45f, "#6CCBF7"), (1, "#2A8FDA")), Band.Radius);
                var t = Look.Hex(fail ? "#6F8FB0" : "#2A8FDA");
                var f = Look.Hex(fail ? "#4A6584" : "#175C9E");
                if (t == tail && f == fold) return;
                tail = t; fold = f;
                Tails.Shapes[0].Fill = Tails.Shapes[1].Fill = t;
                Tails.Shapes[2].Fill = Tails.Shapes[3].Fill = f;
                Tails.SetVerticesDirty();
            }
        }

        /// <summary>.gpanel: a frosted ice block with an ink frame, a snow-capped ribbon title and
        /// icicles underneath. Body is a vertical layout (12 apart) that sizes the panel.</summary>
        public static PanelParts Panel(Transform parent, string title, float width)
        {
            if (SpriteTable.Has("ui.panel") && SpriteTable.Has("ui.ribbon")) return SpritePanel(parent, title, width);
            var root = Node(parent, "panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 200));
            var bg = Fill(root, "bg");
            NoLayout(bg);
            Round(bg, Ink, 28, 0, -8, 0, 8).name = "drop";
            Round(bg, Ink, 28).name = "border";
            Round(bg, new Grad((0, "#FDFFFF"), (0.5f, "#F6FCFF"), (1, "#EBF8FE")), 24, 4, 4, 4, 4).name = "frost ring";
            var fill = Round(bg, new Grad((0, "#F2FCFF"), (0.5f, "#CDEFFF"), (1, "#9EDDF8")), 20, 8, 8, 8, 8);
            fill.name = "ice";
            fill.InsetBottom = 8;
            fill.InsetColor = Look.Hex("#2A8FDA29");
            fill.gameObject.AddComponent<Mask>();
            // Glare streaks across the ice (linear-gradient 112deg bands).
            var glare = Fill(fill.transform, "glare").gameObject.AddComponent<Vec>();
            glare.raycastTarget = false;
            glare.Stretch = true;
            glare.ViewBox = new Rect(0, 0, 100, 100);
            glare.Add(Shape.Path("M6 0H16L4 100H-6Z").WithFill(new Color(1, 1, 1, 0.65f)));
            glare.Add(Shape.Path("M20 0H23.5L11.5 100H8Z").WithFill(new Color(1, 1, 1, 0.45f)));

            // Icicles under the bottom edge.
            var ice = Node(root, "icicles", new Vector2(0.5f, 0), new Vector2(0, -28), new Vector2(width - 68, 22));
            NoLayout(ice);
            var iv = ice.gameObject.AddComponent<Vec>();
            iv.raycastTarget = false;
            iv.ViewBox = new Rect(0, 0, width - 68, 22);
            string[] tips = { "M6 1h14l-6 19z", "M31 1h10l-4 12z", "M53 1h16l-7 20z", "M81 1h9l-4 10z", "M99 1h14l-6 16z" };
            for (float x0 = 0; x0 < width - 68; x0 += 120)
                foreach (var tip in tips)
                {
                    var s = Shape.Path(tip, 1, x0, 0).WithFill("#D6F5FF").WithStroke(InkHex, 2.2f);
                    bool inside = true;
                    foreach (var p in s.Contours[0]) inside &= p.x <= width - 68;
                    if (inside) iv.Add(s);
                }

            // Ribbon (symbol #ribbonS, 340×84, stretched to the panel; strokes keep their width).
            float rw = width + 8;
            var rib = Node(root, "ribbon", new Vector2(0.5f, 1), new Vector2(0, 46), new Vector2(rw, 84));
            NoLayout(rib);
            float sx = rw / 340f;
            var tails = Fill(rib, "tails").gameObject.AddComponent<Vec>();
            tails.raycastTarget = false;
            tails.Stretch = true;
            tails.ViewBox = new Rect(0, 0, 340, 84);
            Shape S(string d, string fill, float w) { var s = Shape.Path(d).WithFill(fill).WithStroke(InkHex, w); s.NonScalingStroke = true; return s; }
            tails.Add(S("M2 34H48V80H2L17 57Z", "#2A8FDA", 3.5f));
            tails.Add(S("M338 34H292V80H338L323 57Z", "#2A8FDA", 3.5f));
            tails.Add(S("M26 70H48V82Z", "#175C9E", 3));
            tails.Add(S("M314 70H292V82Z", "#175C9E", 3));
            var band = Node(rib, "band", new Vector2(0, 1), new Vector2(24 * sx - 2, -10 + 2), new Vector2(292 * sx + 4, 66));
            Round(band, Ink, 18).name = "band edge";
            var bandFill = Round(band, null, 14, 4, 4, 4, 4);
            bandFill.name = "band";
            var shade = Node(rib, "band shade", new Vector2(0, 1), new Vector2(30 * sx, -58), new Vector2(280 * sx, 9));
            Round(shade, new Color(11 / 255f, 46 / 255f, 92 / 255f, 0.14f), 4.5f);
            var cap = Fill(rib, "snow cap").gameObject.AddComponent<Vec>();
            cap.raycastTarget = false;
            cap.Stretch = true;
            cap.ViewBox = new Rect(0, 0, 340, 84);
            cap.Add(S("M24 26C24 15 31 10 40 10H300C309 10 316 15 316 26C310 27 306 23 300 24C294 25 292 31 284 31C276 31 275 24 268 24H230C224 24 222 34 212 34C202 34 201 24 194 24H150C144 24 142 30 134 30C126 30 124 24 118 24H82C76 24 74 32 66 32C58 32 56 24 50 24C40 24 30 28 24 26Z", "#FFFFFF", 3));
            // Centred on the visible band (between the snow cap and the band's lower edge); moved up 7 units
            // when the lettering lost its ink drop (5 Nov 2026), which had sat it low in the band.
            var titleBox = Fill(rib, "title", 0, 3, 0, 7);
            var titleText = Gtext(titleBox, title, 32, 3.5f, 0);

            var parts = new PanelParts { Root = root, Body = root, Title = titleText, Band = bandFill, Tails = tails };
            parts.SetFail(false);

            var lay = root.gameObject.AddComponent<VerticalLayoutGroup>();
            lay.padding = new RectOffset(20, 20, 34, 24);
            lay.spacing = 12;
            lay.childAlignment = TextAnchor.UpperCenter;
            lay.childControlWidth = lay.childControlHeight = true;
            lay.childForceExpandWidth = true;
            lay.childForceExpandHeight = false;
            var fit = root.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            root.gameObject.AddComponent<CanvasGroup>();
            root.gameObject.AddComponent<PopIn>();
            return parts;
        }

        /// <summary>v5 panel: the u3 ice window 9-sliced to the body, the red ribbon header overlapping its
        /// top edge with the title in white lettering on a navy outline (no drop).</summary>
        static PanelParts SpritePanel(Transform parent, string title, float width)
        {
            var root = Node(parent, "panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 200));
            var bg = Fill(root, "bg");
            NoLayout(bg);
            // Corners ~38 units: the slice is ~720 px tall, its border 22 % of that.
            var body = Sliced(bg, "ui.panel", 720f / 4.6f);
            body.raycastTarget = true; // taps on the panel never reach the slope

            var rs = SpriteTable.Ui("ui.ribbon");
            float rw = width - 10, rh = rw * rs.rect.height / rs.rect.width;
            var rib = Node(root, "ribbon", new Vector2(0.5f, 1), new Vector2(0, rh * 0.55f), new Vector2(rw, rh));
            NoLayout(rib);
            var ribbon = Sprite(rib, "ui.ribbon", 0, 0, 0, 0, false);
            // The band between the tails, a little above the slice's middle (its lower part is shadow). Raised
            // by 0.1 rh (5 Nov 2026): at 0.3 / 0.12 the glyph box of "ผ่านด่าน!" sat 10 pt below the red band's
            // centre at the title (band 534..713 px, glyphs 601..706 px on the 3x simulator), the tone marks
            // pulling the line box down; horizontally it was centred (+0.3 pt).
            var titleBox = Fill(rib, "title", rw * 0.18f, rh * 0.4f, rw * 0.18f, rh * 0.02f);
            var titleText = Gtext(titleBox, title, 30, 3.5f, 0);
            var gt = titleText.GetComponent<GameText>();
            gt.Ink = Navy;
            gt.TwoTone = false;
            titleText.resizeTextForBestFit = true;
            titleText.resizeTextMinSize = 18;
            titleText.resizeTextMaxSize = 30;
            titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            titleText.verticalOverflow = VerticalWrapMode.Truncate;

            var parts = new PanelParts { Root = root, Body = root, Title = titleText, Ribbon = ribbon };
            var lay = root.gameObject.AddComponent<VerticalLayoutGroup>();
            lay.padding = new RectOffset(22, 22, Mathf.RoundToInt(rh * 0.45f + 14), 26);
            lay.spacing = 12;
            lay.childAlignment = TextAnchor.UpperCenter;
            lay.childControlWidth = lay.childControlHeight = true;
            lay.childForceExpandWidth = true;
            lay.childForceExpandHeight = false;
            var fit = root.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            root.gameObject.AddComponent<CanvasGroup>();
            root.gameObject.AddComponent<PopIn>();
            return parts;
        }

        /// <summary>A fixed-height slot in a layout; the returned rect is for free positioning inside.</summary>
        public static RectTransform Slot(Transform parent, string name, float height)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Sized(rt, -1, height);
            return rt;
        }

        /// <summary>.gwell: the white rounded well that holds lists.</summary>
        public static RectTransform Well(Transform parent)
        {
            var rt = new GameObject("well", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            var bg = Fill(rt, "bg");
            NoLayout(bg);
            Round(bg, new Color(42 / 255f, 143 / 255f, 218 / 255f, 0.35f), 16);
            Round(bg, new Color(1, 1, 1, 0.7f), 14, 2, 2, 2, 2);
            var lay = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            lay.padding = new RectOffset(14, 14, 10, 10);
            lay.spacing = 6;
            lay.childControlWidth = lay.childControlHeight = true;
            lay.childForceExpandWidth = true;
            lay.childForceExpandHeight = false;
            return rt;
        }

        /// <summary>Full-screen dim behind popups (.scrim); swallows taps.</summary>
        public static Image Scrim(Transform parent)
        {
            var rt = Fill(parent, "scrim");
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(30 / 255f, 26 / 255f, 51 / 255f, 0.55f);
            return img;
        }
    }

    /// <summary>CSS :active: the face sinks into its ink drop while held.</summary>
    public sealed class Press : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public RectTransform Face;
        public Selectable Target;
        public float Depth = 3;
        /// <summary>Face scale while held (1 = no squeeze; the sprite buttons use 0.9–0.95).</summary>
        public float Scale = 1;

        public void OnPointerDown(PointerEventData e)
        {
            if (Target && !Target.IsInteractable()) return;
            Face.anchoredPosition = new Vector2(0, -Depth);
            Face.localScale = Vector3.one * Scale;
        }

        void Up() { Face.anchoredPosition = Vector2.zero; Face.localScale = Vector3.one; }
        public void OnPointerUp(PointerEventData e) => Up();
        public void OnPointerExit(PointerEventData e) => Up();
        void OnDisable() { if (Face) Up(); }
    }

    /// <summary>The logo letters' hop (@keyframes bounce).</summary>
    public sealed class Bob : MonoBehaviour
    {
        public float Period = 1.8f, Delay, Lift = 9, Tilt = -4;
        RectTransform rt;
        Vector2 home;

        void Awake() { rt = (RectTransform)transform; home = rt.anchoredPosition; }

        void Update()
        {
            float p = Mathf.Repeat((Time.unscaledTime - Delay) / Period, 1);
            float k = p < 0.3f ? Ease(p / 0.3f) : p < 0.6f ? 1 - Ease((p - 0.3f) / 0.3f) : 0;
            rt.anchoredPosition = home + new Vector2(0, Lift * k);
            rt.localRotation = Quaternion.Euler(0, 0, -Tilt * k);
        }

        static float Ease(float x) => x * x * (3 - 2 * x);
    }

    /// <summary>Gentle breathing scale (.tapgo, the current level node).</summary>
    public sealed class Pulse : MonoBehaviour
    {
        public float Period = 1.2f, Scale = 1.06f;

        void Update()
        {
            float k = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime / Period * Mathf.PI * 2);
            transform.localScale = Vector3.one * (1 + (Scale - 1) * k);
        }

        void OnDisable() => transform.localScale = Vector3.one;
    }

    /// <summary>Popup entrance (@keyframes panelIn): drops in from above with a little overshoot.
    /// Call Play() each time the popup is shown.</summary>
    public sealed class PopIn : MonoBehaviour
    {
        public float Dur = 0.5f, FromScale = 0.85f, FromY = 60;
        float t0 = -1;
        CanvasGroup cg;
        RectTransform rt;
        Vector2 home;

        void Awake()
        {
            rt = (RectTransform)transform;
            cg = GetComponent<CanvasGroup>();
        }

        public void Play()
        {
            if (t0 >= 0) Finish();
            home = rt.anchoredPosition;
            t0 = Time.unscaledTime;
            Apply(0);
        }

        void Finish()
        {
            rt.anchoredPosition = home;
            rt.localScale = Vector3.one;
            if (cg) cg.alpha = 1;
            t0 = -1;
        }

        void OnDisable()
        {
            if (t0 >= 0) Finish();
        }

        void Update()
        {
            if (t0 < 0) return;
            float k = Mathf.Clamp01((Time.unscaledTime - t0) / Dur);
            if (k >= 1) Finish();
            else Apply(k);
        }

        void Apply(float k)
        {
            // cubic-bezier(.2, 1.35, .5, 1) is close to ease-out-back.
            float c = 1.70158f, x = k - 1, e = 1 + (c + 1) * x * x * x + c * x * x;
            rt.localScale = Vector3.one * Mathf.LerpUnclamped(FromScale, 1, e);
            rt.anchoredPosition = home + new Vector2(0, Mathf.LerpUnclamped(FromY, 0, e));
            if (cg) cg.alpha = Mathf.Clamp01(k * 2.5f);
        }
    }

    /// <summary>The shine that sweeps across the play button every few seconds (.big-btn::after).</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class Shimmer : MaskableGraphic
    {
        RectTransform host;

        public static void AddTo(RRect face)
        {
            face.gameObject.AddComponent<Mask>();
            var rt = K.Node(face.transform, "shine", new Vector2(0, 0.5f), Vector2.zero, new Vector2(34, 120));
            var s = rt.gameObject.AddComponent<Shimmer>();
            s.raycastTarget = false;
            s.host = (RectTransform)face.transform;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            float skew = Mathf.Tan(20 * Mathf.Deg2Rad) * r.height / 2;
            Color clear = new Color(1, 1, 1, 0), mid = new Color(1, 1, 1, 0.8f);
            void V(float x, float y, Color c) => vh.AddVert(new Vector3(x + (y > r.center.y ? skew : -skew), y), c, Vector2.zero);
            V(r.xMin, r.yMax, clear); V(r.center.x, r.yMax, mid); V(r.center.x, r.yMin, mid); V(r.xMin, r.yMin, clear);
            V(r.center.x, r.yMax, mid); V(r.xMax, r.yMax, clear); V(r.xMax, r.yMin, clear); V(r.center.x, r.yMin, mid);
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(2, 3, 0);
            vh.AddTriangle(4, 5, 6); vh.AddTriangle(6, 7, 4);
        }

        void Update()
        {
            if (!host) return;
            // 0–64% of the 3.4 s loop waits off the left edge, then the band crosses.
            float p = Mathf.Repeat(Time.unscaledTime / 3.4f, 1);
            float k = p < 0.64f ? 0 : (p - 0.64f) / 0.36f;
            k = k * k * (3 - 2 * k);
            rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-70, host.rect.width + 70, k), 0);
        }
    }
}
