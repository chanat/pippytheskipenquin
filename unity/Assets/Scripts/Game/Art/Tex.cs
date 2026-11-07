// Procedural colour helpers and textures for the 2D layer and the UI: hex colours, a hash, the soft
// cloud puff (Placeholder2D) and a white texel. A tiny software canvas (Canvas2) does the drawing.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pip.Game.Art
{
    /// <summary>Minimal RGBA canvas (straight alpha, source-over) for building textures in code.</summary>
    public sealed class Canvas2
    {
        public readonly int W, H;
        readonly Color[] px;

        public Canvas2(int w, int h, Color fill)
        {
            W = w; H = h;
            px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = fill;
        }

        // y = 0 is the top row, like an HTML canvas.
        public void Blend(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H || c.a <= 0) return;
            int i = (H - 1 - y) * W + x;
            var d = px[i];
            float a = c.a + d.a * (1 - c.a);
            if (a <= 0) return;
            px[i] = new Color((c.r * c.a + d.r * d.a * (1 - c.a)) / a, (c.g * c.a + d.g * d.a * (1 - c.a)) / a, (c.b * c.a + d.b * d.a * (1 - c.a)) / a, a);
        }

        /// <summary>The pixel at canvas (x, y), y = 0 on top (clear outside the canvas).</summary>
        public Color Get(int x, int y) => x >= 0 && y >= 0 && x < W && y < H ? px[(H - 1 - y) * W + x] : Color.clear;

        public void Set(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < W && y < H) px[(H - 1 - y) * W + x] = c; }

        /// <summary>Radial gradient between two stops, from an inner radius r0 to r1.</summary>
        public void Radial(float cx, float cy, float r0, float r1, Color c0, Color c1, Rect? clip = null)
        {
            var area = clip ?? new Rect(cx - r1, cy - r1, r1 * 2, r1 * 2);
            for (int y = Mathf.Max(0, (int)area.yMin); y < Mathf.Min(H, (int)Mathf.Ceil(area.yMax)); y++)
                for (int x = Mathf.Max(0, (int)area.xMin); x < Mathf.Min(W, (int)Mathf.Ceil(area.xMax)); x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    if (d > r1 && clip == null) continue;
                    Blend(x, y, Color.Lerp(c0, c1, Mathf.Clamp01((d - r0) / Mathf.Max(0.001f, r1 - r0))));
                }
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color c, float soft = 1.2f)
        {
            for (int y = Mathf.Max(0, (int)(cy - ry - 2)); y < Mathf.Min(H, (int)(cy + ry + 2)); y++)
                for (int x = Mathf.Max(0, (int)(cx - rx - 2)); x < Mathf.Min(W, (int)(cx + rx + 2)); x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    float edge = (1 - Mathf.Sqrt(dx * dx + dy * dy)) * Mathf.Min(rx, ry) / soft;
                    if (edge <= 0) continue;
                    var k = c; k.a *= Mathf.Clamp01(edge);
                    Blend(x, y, k);
                }
        }

        public void Line(float x0, float y0, float x1, float y1, float width, Color c)
        {
            float minX = Mathf.Min(x0, x1) - width, maxX = Mathf.Max(x0, x1) + width, minY = Mathf.Min(y0, y1) - width, maxY = Mathf.Max(y0, y1) + width;
            var ab = new Vector2(x1 - x0, y1 - y0);
            float len2 = Mathf.Max(1e-5f, ab.sqrMagnitude);
            for (int y = Mathf.Max(0, (int)minY); y < Mathf.Min(H, (int)maxY + 1); y++)
                for (int x = Mathf.Max(0, (int)minX); x < Mathf.Min(W, (int)maxX + 1); x++)
                {
                    var p = new Vector2(x + 0.5f - x0, y + 0.5f - y0);
                    float t = Mathf.Clamp01(Vector2.Dot(p, ab) / len2);
                    float d = (p - ab * t).magnitude;
                    float a = Mathf.Clamp01(width / 2 - d + 0.5f);
                    if (a <= 0) continue;
                    var k = c; k.a *= a;
                    Blend(x, y, k);
                }
        }

        public void Rect(float x, float y, float w, float h, Color c)
        {
            for (int j = Mathf.Max(0, (int)y); j < Mathf.Min(H, (int)(y + h)); j++)
                for (int i = Mathf.Max(0, (int)x); i < Mathf.Min(W, (int)(x + w)); i++) Blend(i, j, c);
        }

        public Texture2D ToTexture(bool repeat = false, bool linear = false, FilterMode filter = FilterMode.Bilinear)
        {
            var t = new Texture2D(W, H, TextureFormat.RGBA32, true, linear)
            {
                wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                filterMode = filter,
                anisoLevel = 4,
            };
            t.SetPixels(px);
            t.Apply(true, true);
            return t;
        }
    }

    public static class Tex
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        static Texture2D Get(string key, Func<Texture2D> make)
        {
            if (cache.TryGetValue(key, out var t)) return t;
            t = make();
            t.name = key;
            cache[key] = t;
            return t;
        }

        public static Color C(string hex, float a = 1)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            c.a = a;
            return c;
        }

        public static double Hash2(double x, double y)
        {
            double s = Math.Sin(x * 127.1 + y * 311.7) * 43758.5453;
            return s - Math.Floor(s);
        }

        public static Texture2D Cloud => Get("cloud", () =>
        {
            var c = new Canvas2(256, 128, new Color(1, 1, 1, 0));
            for (int i = 0; i < 16; i++)
            {
                float cx = 40 + (float)Hash2(i, 3) * 176, cy = 70 + ((float)Hash2(i, 9) - 0.5f) * 34, r = 22 + (float)Hash2(i, 4) * 26;
                c.Radial(cx, cy - r * 0.2f, r * 0.1f, r * 0.7f, C("#FFFFFF", 0.95f), C("#F0F6FF", 0.75f));
                c.Radial(cx, cy, r * 0.7f, r, C("#F0F6FF", 0.75f), C("#E1EBFC", 0));
            }
            return c.ToTexture();
        });

        public static Texture2D White => Get("white", () => new Canvas2(4, 4, Color.white).ToTexture());
    }
}
