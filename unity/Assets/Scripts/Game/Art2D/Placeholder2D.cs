// Procedural sprites for the 2D layer: what the sprite sheets don't carry (ground shadows, moguls and
// shade patches, the crest, shield / magnet rings, answer shapes, wooden fork sign, banners and poles
// for a gate row without the s06 gates, the blizzard haze) plus a stand-in for any role whose sheet
// slice is missing (a coloured blob in the web's colours, so a bad slice table shows but never breaks).
// Drawn with the Art/Tex software canvas at 2 px per unit.
using System.Collections.Generic;
using Pip.Game.Art;
using UnityEngine;

namespace Pip.Game.Art2D
{
    public static class Placeholder2D
    {
        public const string Ink = "#1F2A4D";
        const float K = 2; // canvas pixels per unit

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        static Sprite Make(string key, int w, int h, Vector2 pivot, System.Action<Canvas2> draw, bool repeat = false)
        {
            if (cache.TryGetValue(key, out var s)) return s;
            var c = new Canvas2(Mathf.CeilToInt(w * K), Mathf.CeilToInt(h * K), new Color(1, 1, 1, 0));
            draw(c);
            var t = c.ToTexture(repeat);
            t.name = "ph " + key;
            s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), pivot, K, 0, SpriteMeshType.FullRect);
            s.name = key;
            cache[key] = s;
            return s;
        }

        static Color C(string hex, float a = 1) => Tex.C(hex, a);

        /// <summary>Even-odd scanline fill of a polygon in canvas pixels (y down).</summary>
        public static void Poly(Canvas2 c, IList<Vector2> p, Color col)
        {
            float y0 = float.MaxValue, y1 = float.MinValue;
            foreach (var v in p) { y0 = Mathf.Min(y0, v.y); y1 = Mathf.Max(y1, v.y); }
            var xs = new List<float>();
            for (int y = Mathf.Max(0, (int)y0); y <= Mathf.Min(c.H - 1, (int)y1); y++)
            {
                float sy = y + 0.5f;
                xs.Clear();
                for (int i = 0; i < p.Count; i++)
                {
                    var a = p[i]; var b = p[(i + 1) % p.Count];
                    if ((a.y <= sy && b.y > sy) || (b.y <= sy && a.y > sy)) xs.Add(a.x + (sy - a.y) / (b.y - a.y) * (b.x - a.x));
                }
                xs.Sort();
                for (int i = 0; i + 1 < xs.Count; i += 2)
                    for (int x = Mathf.Max(0, Mathf.RoundToInt(xs[i])); x < Mathf.Min(c.W, Mathf.RoundToInt(xs[i + 1])); x++) c.Blend(x, y, col);
            }
        }

        static void Stroke(Canvas2 c, IList<Vector2> p, float width, Color col, bool closed = true)
        {
            for (int i = 0; i + (closed ? 0 : 1) < p.Count; i++) c.Line(p[i].x, p[i].y, p[(i + 1) % p.Count].x, p[(i + 1) % p.Count].y, width, col);
        }

        static List<Vector2> RoundRect(float x, float y, float w, float h, float r)
        {
            var p = new List<Vector2>();
            r = Mathf.Min(r, w / 2, h / 2);
            void Arc(float cx, float cy, float a0)
            {
                for (int i = 0; i <= 6; i++)
                {
                    float a = a0 + i / 6f * Mathf.PI / 2;
                    p.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
                }
            }
            Arc(x + w - r, y + h - r, 0); Arc(x + r, y + h - r, Mathf.PI / 2); Arc(x + r, y + r, Mathf.PI); Arc(x + w - r, y + r, Mathf.PI * 1.5f);
            return p;
        }

        static List<Vector2> Scale(List<Vector2> p) { for (int i = 0; i < p.Count; i++) p[i] *= K; return p; }

        // ---- ground ----

        /// <summary>Soft dark ellipse (64×24 units), centre pivot.</summary>
        public static Sprite Shadow => Make("shadow", 64, 24, new Vector2(0.5f, 0.5f), c =>
        {
            for (int i = 0; i < 6; i++) c.Ellipse(64, 24, 62 - i * 6, 22 - i * 2.2f, C(Ink, 0.08f));
        });

        public static Sprite Mogul => Make("mogul", 64, 22, new Vector2(0.5f, 0.5f), c =>
        {
            c.Ellipse(64, 24, 60, 18, C("#DAE6FF"));
            c.Ellipse(58, 18, 52, 14, C("#FBFDFF"));
            c.Ellipse(44, 14, 16, 4, C("#FFFFFF"));
        });

        public static Sprite ShadePatch => Make("shade", 64, 32, new Vector2(0.5f, 0.5f), c =>
        {
            for (int i = 0; i < 5; i++) c.Ellipse(64, 32, 62 - i * 9, 30 - i * 4.5f, C("#C9D9F5", 0.12f));
        });

        /// <summary>Slope-edge snow bank, 28 × 128 units, tiling top to bottom (whole sine periods): a white
        /// bank whose inner side waves, a thin ice-blue rim and a soft shadow on the slope; its outer quarter
        /// fades in over the edge strip. Drawn for the left side (flipX for the right).</summary>
        public static Sprite Bank => Make("bank", 28, 128, new Vector2(0.5f, 0.5f), c =>
        {
            var snow = C("#FFFFFF"); var rim = C("#A9C4EA"); var shade = C("#BFD3F0");
            for (int py = 0; py < c.H; py++)
            {
                float y = py / K;
                float e = 15 + 3.5f * Mathf.Sin(y / 128 * Mathf.PI * 4) + 1.8f * Mathf.Sin(y / 128 * Mathf.PI * 6 + 1.3f);
                for (int px = 0; px < c.W; px++)
                {
                    float x = (px + 0.5f) / K;
                    if (x < e)
                    {
                        float a = Mathf.Clamp01(x / 6);
                        var col = Color.Lerp(C("#EEF5FD"), snow, Mathf.Clamp01((x - 4) / (e - 4)));
                        col.a = a;
                        c.Set(px, py, col);
                    }
                    else if (x < e + 1.6f) c.Set(px, py, rim);
                    else if (x < e + 9) { var sh = shade; sh.a = 0.35f * (1 - (x - e - 1.6f) / 7.4f); c.Set(px, py, sh); }
                }
            }
        });

        // ---- crest: the snow line where the slope tips over (web ART.crest), 470 wide, 100 tall ----

        static float Quad(float a, float b, float cc, float t) => (1 - t) * (1 - t) * a + 2 * (1 - t) * t * b + t * t * cc;

        /// <summary>Height (y down, from the crest sprite's top) of a web crest path at x (0..390).</summary>
        static float CrestLine(float x, float off)
        {
            // M0 46 Q90 6 200 32 Q300 56 390 20 (+ off), solved for t on x per segment.
            if (x <= 200)
            {
                float t = SolveT(0, 90, 200, x);
                return Quad(46, 6, 32, t) + off;
            }
            float u = SolveT(200, 300, 390, Mathf.Min(390, x));
            return Quad(32, 56, 20, u) + off;
        }

        static float SolveT(float a, float b, float cc, float x)
        {
            float lo = 0, hi = 1;
            for (int i = 0; i < 24; i++) { float m = (lo + hi) / 2; if (Quad(a, b, cc, m) < x) lo = m; else hi = m; }
            return (lo + hi) / 2;
        }

        public static Sprite Crest(string snowHex, string shadeHex) => Make("crest" + snowHex + shadeHex, 470, 110, new Vector2(0, 1), c =>
        {
            var snow = C(snowHex); var shade = C(shadeHex, 0.45f);
            for (int px = 0; px < c.W; px++)
            {
                float x = px / K - 40;
                float xc = Mathf.Clamp(x, 0, 390);
                float l0 = CrestLine(xc, 0), l1 = CrestLine(xc, 12), l2 = CrestLine(xc, 18);
                for (int py = 0; py < c.H; py++)
                {
                    float y = py / K;
                    // Fade the bottom 22 units into the ground below.
                    float fade = Mathf.Clamp01((110 - y) / 22);
                    if (y >= l0) c.Set(px, py, new Color(snow.r, snow.g, snow.b, Mathf.Clamp01((y - l0) * K) * (y > 88 ? fade : 1)));
                    if (y >= l1 && y < l2 + 1) c.Blend(px, py, shade);
                }
            }
        });

        // ---- characters' gear ----

        public static Sprite Ring => Make("ring", 100, 100, new Vector2(0.5f, 0.5f), c =>
        {
            c.Ellipse(100, 100, 96, 96, C("#BFE6FF", 0.35f));
            for (int i = 0; i < 72; i++)
            {
                float a = i / 72f * Mathf.PI * 2, b = (i + 1) / 72f * Mathf.PI * 2;
                c.Line(100 + Mathf.Cos(a) * 95, 100 + Mathf.Sin(a) * 95, 100 + Mathf.Cos(b) * 95, 100 + Mathf.Sin(b) * 95, 6, C("#3FA9F5", 0.9f));
            }
        });

        public static Sprite DashRing => Make("dashring", 124, 124, new Vector2(0.5f, 0.5f), c =>
        {
            for (int i = 0; i < 18; i++)
            {
                float a0 = i / 18f * Mathf.PI * 2, a1 = a0 + Mathf.PI * 2 / 18 * 0.45f;
                for (int k = 0; k < 4; k++)
                {
                    float a = Mathf.Lerp(a0, a1, k / 4f), b = Mathf.Lerp(a0, a1, (k + 1) / 4f);
                    c.Line(124 + Mathf.Cos(a) * 118, 124 + Mathf.Sin(a) * 118, 124 + Mathf.Cos(b) * 118, 124 + Mathf.Sin(b) * 118, 6, C("#FF5C93", 0.75f));
                }
            }
        });

        public static Sprite Halo => Make("halo", 64, 64, new Vector2(0.5f, 0.5f), c => c.Radial(64, 64, 0, 64, Color.white, new Color(1, 1, 1, 0)));

        public static Sprite White => Make("white", 4, 4, new Vector2(0.5f, 0.5f), c => c.Rect(0, 0, c.W, c.H, Color.white));

        /// <summary>Vertical haze: white at the top and bottom, lighter in the middle (tinted at runtime).</summary>
        public static Sprite Haze => Make("haze", 4, 64, new Vector2(0.5f, 0.5f), c =>
        {
            for (int y = 0; y < c.H; y++)
            {
                float t = y / (float)(c.H - 1);
                float a = t < 0.5f ? Mathf.Lerp(0.55f, 0.2f, t * 2) : Mathf.Lerp(0.2f, 0.5f, (t - 0.5f) * 2);
                for (int x = 0; x < c.W; x++) c.Set(x, y, new Color(1, 1, 1, a));
            }
        });

        public static Sprite Cloud => cloud ? cloud : cloud = Sprite.Create(Tex.Cloud, new Rect(0, 0, Tex.Cloud.width, Tex.Cloud.height), new Vector2(0.5f, 0.5f), 2.56f);
        static Sprite cloud;

        // ---- answer shapes (white with an ink rim; tinted by the option's ShapeColor) ----

        public static Sprite Shape(string kind) => Make("shape" + kind, 40, 40, new Vector2(0.5f, 0.5f), c =>
        {
            var p = new List<Vector2>();
            float r = 15;
            switch (kind)
            {
                case "circle": for (int i = 0; i < 36; i++) p.Add(new Vector2(20 + Mathf.Cos(i / 36f * Mathf.PI * 2) * r, 20 + Mathf.Sin(i / 36f * Mathf.PI * 2) * r)); break;
                case "square": p = RoundRect(20 - r * 0.9f, 20 - r * 0.9f, r * 1.8f, r * 1.8f, r * 0.3f); break;
                case "tri": p.Add(new Vector2(20, 20 - r * 1.05f)); p.Add(new Vector2(20 + r * 1.05f, 20 + r * 0.85f)); p.Add(new Vector2(20 - r * 1.05f, 20 + r * 0.85f)); break;
                case "diamond": p.Add(new Vector2(20, 20 - r * 1.1f)); p.Add(new Vector2(20 + r * 0.9f, 20)); p.Add(new Vector2(20, 20 + r * 1.1f)); p.Add(new Vector2(20 - r * 0.9f, 20)); break;
                default:
                    for (int i = 0; i < 10; i++)
                    {
                        float a = -Mathf.PI / 2 + i * Mathf.PI / 5, rr = i % 2 == 0 ? r * 1.1f : r * 0.48f;
                        p.Add(new Vector2(20 + Mathf.Cos(a) * rr, 20 + Mathf.Sin(a) * rr));
                    }
                    break;
            }
            Scale(p);
            Poly(c, p, Color.white);
            Stroke(c, p, 5, C(Ink));
        });

        /// <summary>Four-point sparkle (white), the tumble stars and fallback sparks.</summary>
        public static Sprite Star => Make("star", 24, 24, new Vector2(0.5f, 0.5f), c =>
        {
            var p = new List<Vector2>();
            for (int i = 0; i < 10; i++)
            {
                float a = -Mathf.PI / 2 + i * Mathf.PI / 5, rr = i % 2 == 0 ? 11 : 5;
                p.Add(new Vector2(12 + Mathf.Cos(a) * rr, 12 + Mathf.Sin(a) * rr));
            }
            Scale(p);
            Poly(c, p, Color.white);
            Stroke(c, p, 4, C(Ink));
        });

        // ---- gate row / arch / sign parts ----

        public static Sprite Pole => Make("pole", 12, 106, new Vector2(0.5f, 0), c =>
        {
            var p = Scale(RoundRect(1, 1, 10, 104, 5));
            Poly(c, p, Color.white);
            for (float y = 0; y < 104; y += 22) c.Rect(0, (1 + y) * K, c.W, 11 * K, C("#FF5C93"));
            // Re-cut the stripes to the pole's rounded outline by redrawing the outside clear.
            Stroke(c, p, 4, C(Ink));
        });

        public static Sprite Banner => Make("banner", 128, 64, new Vector2(0.5f, 0.5f), c =>
        {
            Poly(c, Scale(RoundRect(2, 7, 124, 56, 16)), C(Ink));
            var p = Scale(RoundRect(2, 2, 124, 56, 16));
            Poly(c, p, Color.white);
            Stroke(c, p, 5, C(Ink));
            Poly(c, Scale(RoundRect(10, 6, 108, 10, 6)), new Color(1, 1, 1, 0.35f));
        });

        public static Sprite Plank(string key, int w, int h, string fill, Vector2 pivot) => Make("plank" + key + w + "x" + h + fill, w, h, pivot, c =>
        {
            var p = Scale(RoundRect(1.5f, 1.5f, w - 3, h - 3, Mathf.Min(10, h / 3f)));
            Poly(c, p, C(fill));
            Stroke(c, p, 5, C(Ink));
            Poly(c, Scale(RoundRect(6, 4, w - 12, Mathf.Min(6, h / 5f), 3)), new Color(1, 1, 1, 0.25f));
        });

        // ---- stand-ins for missing sheet slices ----

        /// <summary>Blob in a role's web colour, bottom-centre pivot (centre for flat / VFX roles).</summary>
        public static Sprite Blob(string role, string fill, bool centre) => Make("blob" + role, 60, 60, centre ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0), c =>
        {
            c.Ellipse(60, 60, 56, 56, C(Ink));
            c.Ellipse(60, 60, 50, 50, C(fill));
            c.Ellipse(44, 40, 14, 9, new Color(1, 1, 1, 0.5f));
        });

        /// <summary>Soft white puff for VFX stand-ins (one texture, so the particle mesh can use it).</summary>
        public static Sprite Puff => Make("puff", 32, 32, new Vector2(0.5f, 0.5f), c =>
        {
            c.Radial(32, 32, 10, 31, Color.white, new Color(0.86f, 0.91f, 0.98f, 0));
        });
    }
}
