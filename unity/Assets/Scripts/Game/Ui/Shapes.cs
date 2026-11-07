// Mesh-built UI shapes for the web prototype's toy look: rounded rectangles with vertical CSS-style
// gradients, and SVG paths (fill + round-joined stroke, optional dashes) parsed from the same path
// data as index.html. The canvas is drawn by the gameplay camera with 4× MSAA, so edges stay smooth
// at any phone resolution with no sprite atlas to keep in sync.
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Pip.Game.Ui
{
    /// <summary>Vertical gradient, stops from the top (0) to the bottom (1).</summary>
    public sealed class Grad
    {
        readonly float[] t;
        readonly Color[] c;

        public Grad(params (float t, string hex)[] stops)
        {
            t = new float[stops.Length];
            c = new Color[stops.Length];
            for (int i = 0; i < stops.Length; i++) { t[i] = stops[i].t; c[i] = Look.Hex(stops[i].hex); }
        }

        public static Grad Solid(string hex) => new Grad((0, hex));

        public int Count => t.Length;
        public float StopAt(int i) => t[i];

        public Color At(float y)
        {
            if (y <= t[0]) return c[0];
            for (int i = 1; i < t.Length; i++)
                if (y <= t[i]) return Color.Lerp(c[i - 1], c[i], (y - t[i - 1]) / Mathf.Max(1e-5f, t[i] - t[i - 1]));
            return c[c.Length - 1];
        }
    }

    /// <summary>Edge smoothing for the mesh shapes: the overlay canvas has no MSAA, so every outline
    /// gets a one-pixel fringe that fades its edge colour to transparent.</summary>
    static class Aa
    {
        /// <summary>One screen pixel in canvas units.</summary>
        public static float Px(Graphic g)
        {
            var c = g.canvas;
            return c && c.scaleFactor > 0 ? 1f / c.scaleFactor : 0.5f;
        }

        static readonly List<Vector2> P = new List<Vector2>();
        static readonly List<Color> C = new List<Color>();

        /// <summary>Fringe around a closed outline, given per-point colours (either winding).</summary>
        public static void Fringe(VertexHelper vh, List<Vector2> pts, List<Color> cols, float f)
        {
            P.Clear();
            C.Clear();
            for (int i = 0; i < pts.Count; i++)
                if (P.Count == 0 || (pts[i] - P[P.Count - 1]).sqrMagnitude > 1e-8f) { P.Add(pts[i]); C.Add(cols[i]); }
            if (P.Count > 1 && (P[0] - P[P.Count - 1]).sqrMagnitude <= 1e-8f) { P.RemoveAt(P.Count - 1); C.RemoveAt(C.Count - 1); }
            int n = P.Count;
            if (n < 3) return;
            float area = 0;
            for (int i = 0; i < n; i++) { var a = P[i]; var b = P[(i + 1) % n]; area += a.x * b.y - b.x * a.y; }
            float sg = area > 0 ? 1 : -1;
            Vector2 Out(Vector2 d) => new Vector2(d.y, -d.x).normalized * sg;
            int b0 = vh.currentVertCount;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = P[(i - 1 + n) % n], b = P[i], c = P[(i + 1) % n];
                Vector2 n0 = Out(b - a), n1 = Out(c - b), m = n0 + n1;
                m = m.sqrMagnitude < 1e-8f ? n1 : m.normalized;
                float k = Mathf.Min(3, 1 / Mathf.Max(0.3f, Vector2.Dot(m, n1)));
                var col = C[i];
                vh.AddVert(b, col, Vector2.zero);
                col.a = 0;
                vh.AddVert(b + m * f * k, col, Vector2.zero);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                vh.AddTriangle(b0 + i * 2, b0 + j * 2, b0 + j * 2 + 1);
                vh.AddTriangle(b0 + j * 2 + 1, b0 + i * 2 + 1, b0 + i * 2);
            }
        }

        /// <summary>Round join / cap: a disc with a soft rim.</summary>
        public static void Disc(VertexHelper vh, Vector2 at, float r, Color col, float px)
        {
            int seg = Mathf.Clamp(Mathf.CeilToInt(r / px * 0.6f), 8, 40);
            int c = vh.currentVertCount;
            var clear = col;
            clear.a = 0;
            vh.AddVert(at, col, Vector2.zero);
            for (int j = 0; j < seg; j++)
            {
                float a = j / (float)seg * Mathf.PI * 2;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                vh.AddVert(at + d * r, col, Vector2.zero);
                vh.AddVert(at + d * (r + px), clear, Vector2.zero);
            }
            for (int j = 0; j < seg; j++)
            {
                int i0 = c + 1 + j * 2, i1 = c + 1 + (j + 1) % seg * 2;
                vh.AddTriangle(c, i0, i1);
                vh.AddTriangle(i0, i0 + 1, i1 + 1);
                vh.AddTriangle(i1 + 1, i1, i0);
            }
        }

        /// <summary>Fringe along one side of a strip: inner points and the outward direction at each.</summary>
        public static void Edge(VertexHelper vh, List<Vector2> inner, List<Vector2> other, Color col, float f)
        {
            var clear = col;
            clear.a = 0;
            for (int j = 0; j + 1 < inner.Count; j++)
            {
                var d0 = (inner[j] - other[j]).normalized * f;
                var d1 = (inner[j + 1] - other[j + 1]).normalized * f;
                int b = vh.currentVertCount;
                vh.AddVert(inner[j], col, Vector2.zero);
                vh.AddVert(inner[j + 1], col, Vector2.zero);
                vh.AddVert(inner[j + 1] + d1, clear, Vector2.zero);
                vh.AddVert(inner[j] + d0, clear, Vector2.zero);
                vh.AddTriangle(b, b + 1, b + 2);
                vh.AddTriangle(b + 2, b + 3, b);
            }
        }
    }

    /// <summary>Rounded rectangle (an ellipse when the radius reaches half the size) filled with a
    /// vertical gradient, an optional CSS "inset 0 -N 0" band along the bottom, and optional
    /// vertical stripes (the wooden planks).</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RRect : MaskableGraphic
    {
        static readonly List<Vector2> Outline = new List<Vector2>();
        static readonly List<Color> OutlineCols = new List<Color>();

        public float Radius;
        public Grad Fill;
        public float InsetBottom;
        public Color InsetColor;
        public float StripePeriod, StripeWidth;
        public Color StripeColor;

        public void Set(Grad fill, float radius)
        {
            Fill = fill;
            Radius = radius;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            float w = r.width, h = r.height;
            if (w <= 0 || h <= 0) return;
            float rx = Mathf.Min(Radius, w / 2), ry = Mathf.Min(Radius, h / 2);
            var ys = new List<float> { 0, h };
            if (rx > 0 && ry > 0)
            {
                int seg = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(rx, ry) * 0.5f), 4, 20);
                for (int i = 1; i < seg; i++)
                {
                    float a = ry * (1 - Mathf.Sin(i / (float)seg * Mathf.PI / 2));
                    ys.Add(a);
                    ys.Add(h - a);
                }
            }
            if (Fill != null) for (int i = 0; i < Fill.Count; i++) ys.Add(Fill.StopAt(i) * h);
            if (InsetBottom > 0) ys.Add(h - InsetBottom);
            ys.Sort();

            float Inset(float y)
            {
                float d = y < ry ? ry - y : y > h - ry ? y - (h - ry) : 0;
                if (d <= 0 || ry <= 0) return 0;
                float k = Mathf.Min(1, d / ry);
                return rx * (1 - Mathf.Sqrt(1 - k * k));
            }

            Color ColorAt(float y, bool inset)
            {
                var c = Fill != null ? Fill.At(y / h) : Color.white;
                if (inset)
                {
                    float a = InsetColor.a;
                    c = new Color(c.r * (1 - a) + InsetColor.r * a, c.g * (1 - a) + InsetColor.g * a, c.b * (1 - a) + InsetColor.b * a, c.a + a * (1 - c.a));
                }
                return c * color;
            }

            float last = -1;
            for (int i = 0; i + 1 < ys.Count; i++)
            {
                float y0 = Mathf.Max(ys[i], last), y1 = ys[i + 1];
                if (y1 - y0 < 1e-4f) continue;
                last = y1;
                bool inset = InsetBottom > 0 && (y0 + y1) / 2 > h - InsetBottom;
                float i0 = Inset(y0), i1 = Inset(y1);
                Quad(vh, new Vector2(r.xMin + i0, r.yMax - y0), new Vector2(r.xMax - i0, r.yMax - y0), new Vector2(r.xMax - i1, r.yMax - y1), new Vector2(r.xMin + i1, r.yMax - y1),
                    ColorAt(y0, inset), ColorAt(y1, inset));
            }

            // Soft edge: down the right side, back up the left.
            Outline.Clear();
            OutlineCols.Clear();
            for (int i = 0; i < ys.Count; i++)
            {
                Outline.Add(new Vector2(r.xMax - Inset(ys[i]), r.yMax - ys[i]));
                OutlineCols.Add(ColorAt(ys[i], InsetBottom > 0 && ys[i] > h - InsetBottom + 1e-3f));
            }
            for (int i = ys.Count - 1; i >= 0; i--)
            {
                Outline.Add(new Vector2(r.xMin + Inset(ys[i]), r.yMax - ys[i]));
                OutlineCols.Add(ColorAt(ys[i], InsetBottom > 0 && ys[i] > h - InsetBottom + 1e-3f));
            }
            Aa.Fringe(vh, Outline, OutlineCols, Aa.Px(this));

            if (StripePeriod > 0 && StripeWidth > 0)
            {
                float YInset(float x)
                {
                    float d = x < rx ? rx - x : x > w - rx ? x - (w - rx) : 0;
                    if (d <= 0 || rx <= 0) return 0;
                    float k = Mathf.Min(1, d / rx);
                    return ry * (1 - Mathf.Sqrt(1 - k * k));
                }
                var sc = StripeColor * color;
                for (float x = StripePeriod - StripeWidth; x < w; x += StripePeriod)
                {
                    float x1 = Mathf.Min(w, x + StripeWidth);
                    float yi = Mathf.Max(YInset(x), YInset(x1));
                    if (h - 2 * yi <= 0) continue;
                    Quad(vh, new Vector2(r.xMin + x, r.yMax - yi), new Vector2(r.xMin + x1, r.yMax - yi), new Vector2(r.xMin + x1, r.yMin + yi), new Vector2(r.xMin + x, r.yMin + yi), sc, sc);
                }
            }
        }

        static void Quad(VertexHelper vh, Vector2 tl, Vector2 tr, Vector2 br, Vector2 bl, Color top, Color bottom)
        {
            int i = vh.currentVertCount;
            vh.AddVert(tl, top, Vector2.zero);
            vh.AddVert(tr, top, Vector2.zero);
            vh.AddVert(br, bottom, Vector2.zero);
            vh.AddVert(bl, bottom, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }
    }

    /// <summary>One filled and/or stroked shape in SVG viewBox coordinates.</summary>
    public sealed class Shape
    {
        public readonly List<List<Vector2>> Contours = new List<List<Vector2>>();
        public readonly List<bool> Closed = new List<bool>();
        public Color? Fill;
        public Grad FillGrad;
        public Color Stroke;
        public float StrokeWidth;
        public float[] Dash;
        public bool NonScalingStroke;

        public Shape WithFill(string hex) { Fill = Look.Hex(hex); return this; }
        public Shape WithFill(Color c) { Fill = c; return this; }
        public Shape WithGrad(Grad g) { FillGrad = g; return this; }

        public Shape WithStroke(string hex, float width)
        {
            Stroke = Look.Hex(hex);
            StrokeWidth = width;
            return this;
        }

        public Shape WithDash(float on, float off) { Dash = new[] { on, off }; return this; }

        /// <summary>Scales then moves the shape (an SVG &lt;use&gt; / translate), stroke included.</summary>
        public Shape At(float scale, float ox, float oy)
        {
            foreach (var con in Contours)
                for (int i = 0; i < con.Count; i++) con[i] = con[i] * scale + new Vector2(ox, oy);
            StrokeWidth *= scale;
            return this;
        }

        // ---------- builders ----------

        /// <summary>SVG path data (M L H V C S Q T A Z, absolute and relative), scaled then offset.</summary>
        public static Shape Path(string d, float scale = 1, float ox = 0, float oy = 0)
        {
            var s = new Shape();
            PathData.Parse(d, s, scale, new Vector2(ox, oy));
            return s;
        }

        public static Shape Ellipse(float cx, float cy, float rx, float ry)
        {
            var s = new Shape();
            var pts = new List<Vector2>();
            int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(rx, ry) * 1.2f), 16, 72);
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2;
                pts.Add(new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry));
            }
            s.Contours.Add(pts);
            s.Closed.Add(true);
            return s;
        }

        public static Shape Circle(float cx, float cy, float r) => Ellipse(cx, cy, r, r);

        public static Shape Rect(float x, float y, float w, float h, float rx = 0)
        {
            var s = new Shape();
            var pts = new List<Vector2>();
            rx = Mathf.Min(rx, w / 2, h / 2);
            if (rx <= 0)
            {
                pts.Add(new Vector2(x, y)); pts.Add(new Vector2(x + w, y)); pts.Add(new Vector2(x + w, y + h)); pts.Add(new Vector2(x, y + h));
            }
            else
            {
                int n = 6;
                void Corner(float cx, float cy, float a0)
                {
                    for (int i = 0; i <= n; i++)
                    {
                        float a = a0 + i / (float)n * Mathf.PI / 2;
                        pts.Add(new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * rx));
                    }
                }
                Corner(x + w - rx, y + rx, -Mathf.PI / 2);
                Corner(x + w - rx, y + h - rx, 0);
                Corner(x + rx, y + h - rx, Mathf.PI / 2);
                Corner(x + rx, y + rx, Mathf.PI);
            }
            s.Contours.Add(pts);
            s.Closed.Add(true);
            return s;
        }
    }

    /// <summary>A small SVG: shapes in a viewBox drawn into the rect (uniformly, or stretched like
    /// preserveAspectRatio="none").</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class Vec : MaskableGraphic
    {
        public Rect ViewBox = new Rect(0, 0, 24, 24);
        public bool Stretch;
        public readonly List<Shape> Shapes = new List<Shape>();

        public Vec Add(Shape s)
        {
            Shapes.Add(s);
            SetVerticesDirty();
            return this;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            if (r.width <= 0 || r.height <= 0) return;
            float sx = r.width / ViewBox.width, sy = r.height / ViewBox.height;
            if (!Stretch) sx = sy = Mathf.Min(sx, sy);
            Vector2 c = r.center, vc = ViewBox.center;
            Vector2 Map(Vector2 p) => new Vector2(c.x + (p.x - vc.x) * sx, c.y - (p.y - vc.y) * sy);

            var pts = new List<Vector2>();
            var cols = new List<Color>();
            var tri = new List<int>();
            float px = Aa.Px(this);
            foreach (var s in Shapes)
            {
                if (s.Fill.HasValue || s.FillGrad != null)
                {
                    float top = float.MaxValue, bottom = float.MinValue;
                    if (s.FillGrad != null)
                        foreach (var con in s.Contours) foreach (var p in con) { top = Mathf.Min(top, p.y); bottom = Mathf.Max(bottom, p.y); }
                    foreach (var con in s.Contours)
                    {
                        if (con.Count < 3) continue;
                        pts.Clear();
                        foreach (var p in con) pts.Add(Map(p));
                        tri.Clear();
                        Triangulate(pts, tri);
                        int b = vh.currentVertCount;
                        cols.Clear();
                        for (int i = 0; i < pts.Count; i++)
                        {
                            var col = (s.FillGrad != null ? s.FillGrad.At((con[i].y - top) / Mathf.Max(1e-4f, bottom - top)) : s.Fill.Value) * color;
                            cols.Add(col);
                            vh.AddVert(pts[i], col, Vector2.zero);
                        }
                        for (int i = 0; i < tri.Count; i += 3) vh.AddTriangle(b + tri[i], b + tri[i + 1], b + tri[i + 2]);
                        // A stroke on top covers the fill's edge; only bare fills need the soft edge.
                        if (s.StrokeWidth <= 0 || s.Stroke.a <= 0) Aa.Fringe(vh, pts, cols, px);
                    }
                }
                if (s.StrokeWidth > 0 && s.Stroke.a > 0)
                {
                    float w = s.NonScalingStroke ? s.StrokeWidth : s.StrokeWidth * (sx + sy) / 2;
                    var col = s.Stroke * color;
                    for (int k = 0; k < s.Contours.Count; k++)
                    {
                        pts.Clear();
                        foreach (var p in s.Contours[k]) pts.Add(Map(p));
                        if (s.Dash != null)
                        {
                            if (s.Closed[k] && pts.Count > 2) pts.Add(pts[0]);
                            foreach (var run in Dashes(pts, s.Dash[0] * (sx + sy) / 2, s.Dash[1] * (sx + sy) / 2)) StrokeLine(vh, run, w, col, false, px);
                        }
                        else StrokeLine(vh, pts, w, col, s.Closed[k], px);
                    }
                }
            }
        }

        static List<List<Vector2>> Dashes(List<Vector2> pts, float on, float off)
        {
            var runs = new List<List<Vector2>>();
            List<Vector2> cur = new List<Vector2> { pts[0] };
            bool drawing = true;
            float left = on;
            for (int i = 1; i < pts.Count; i++)
            {
                Vector2 a = pts[i - 1], b = pts[i];
                float len = Vector2.Distance(a, b), pos = 0;
                while (len - pos > left)
                {
                    pos += left;
                    var p = Vector2.Lerp(a, b, pos / len);
                    if (drawing) { cur.Add(p); runs.Add(cur); }
                    else cur = new List<Vector2> { p };
                    drawing = !drawing;
                    left = drawing ? on : off;
                }
                left -= len - pos;
                if (drawing) cur.Add(b);
            }
            if (drawing && cur.Count > 1) runs.Add(cur);
            return runs;
        }

        /// <summary>Thick polyline: one continuous strip through gentle bends (mitred), round joins
        /// at sharp corners and round caps at open ends, as every stroke in the web art is round.</summary>
        static void StrokeLine(VertexHelper vh, List<Vector2> src, float w, Color col, bool closed, float px)
        {
            var p = new List<Vector2>(src.Count);
            foreach (var q in src) if (p.Count == 0 || (q - p[p.Count - 1]).sqrMagnitude > 1e-6f) p.Add(q);
            if (closed && p.Count > 2 && (p[0] - p[p.Count - 1]).sqrMagnitude <= 1e-6f) p.RemoveAt(p.Count - 1);
            int n = p.Count;
            float hw = w / 2;
            if (n == 0) return;
            if (n == 1) { Aa.Disc(vh, p[0], hw, col, px); return; }
            if (n == 2) closed = false;
            Vector2 Nrm(int seg)
            {
                var d = p[(seg + 1) % n] - p[seg];
                return new Vector2(-d.y, d.x).normalized;
            }
            var left = new List<Vector2>();
            var right = new List<Vector2>();
            void Flush()
            {
                for (int j = 0; j + 1 < left.Count; j++)
                {
                    int b = vh.currentVertCount;
                    vh.AddVert(left[j], col, Vector2.zero);
                    vh.AddVert(left[j + 1], col, Vector2.zero);
                    vh.AddVert(right[j + 1], col, Vector2.zero);
                    vh.AddVert(right[j], col, Vector2.zero);
                    vh.AddTriangle(b, b + 1, b + 2);
                    vh.AddTriangle(b + 2, b + 3, b);
                }
                Aa.Edge(vh, left, right, col, px);
                Aa.Edge(vh, right, left, col, px);
                left.Clear();
                right.Clear();
            }
            // A closed loop visits vertex 0 again at the end so the strip meets its start.
            int visits = closed ? n + 1 : n;
            for (int i = 0; i < visits; i++)
            {
                int v = i % n;
                var at = p[v];
                bool hasIn = closed || i > 0, hasOut = closed || i < n - 1;
                if (!hasIn)
                {
                    var no = Nrm(v);
                    Aa.Disc(vh, at, hw, col, px);
                    left.Add(at + no * hw);
                    right.Add(at - no * hw);
                    continue;
                }
                var ni = Nrm((v - 1 + n) % n);
                if (!hasOut)
                {
                    left.Add(at + ni * hw);
                    right.Add(at - ni * hw);
                    Flush();
                    Aa.Disc(vh, at, hw, col, px);
                    continue;
                }
                var nx = Nrm(v);
                if (Vector2.Dot(ni, nx) > 0.82f)
                {
                    var m = (ni + nx).normalized;
                    float k = hw / Mathf.Max(0.5f, Vector2.Dot(m, nx));
                    left.Add(at + m * k);
                    right.Add(at - m * k);
                }
                else
                {
                    left.Add(at + ni * hw);
                    right.Add(at - ni * hw);
                    Flush();
                    Aa.Disc(vh, at, hw, col, px);
                    left.Add(at + nx * hw);
                    right.Add(at - nx * hw);
                }
            }
            Flush();
        }

        // ---------- ear-clipping triangulation for simple polygons ----------

        static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        static void Triangulate(List<Vector2> p, List<int> outIdx)
        {
            var idx = new List<int>();
            for (int i = 0; i < p.Count; i++)
                if (idx.Count == 0 || (p[i] - p[idx[idx.Count - 1]]).sqrMagnitude > 1e-6f) idx.Add(i);
            if (idx.Count > 1 && (p[idx[0]] - p[idx[idx.Count - 1]]).sqrMagnitude <= 1e-6f) idx.RemoveAt(idx.Count - 1);
            float area = 0;
            for (int i = 0; i < idx.Count; i++) { var a = p[idx[i]]; var b = p[idx[(i + 1) % idx.Count]]; area += a.x * b.y - b.x * a.y; }
            if (area < 0) idx.Reverse();
            int guard = idx.Count * idx.Count + 8;
            while (idx.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int i0 = idx[(i + idx.Count - 1) % idx.Count], i1 = idx[i], i2 = idx[(i + 1) % idx.Count];
                    float cr = Cross(p[i0], p[i1], p[i2]);
                    if (Mathf.Abs(cr) < 1e-7f) { idx.RemoveAt(i); clipped = true; break; } // collinear: drop the middle point
                    if (cr < 0) continue; // reflex
                    bool inside = false;
                    for (int j = 0; j < idx.Count && !inside; j++)
                    {
                        int v = idx[j];
                        if (v == i0 || v == i1 || v == i2) continue;
                        inside = Cross(p[i0], p[i1], p[v]) >= 0 && Cross(p[i1], p[i2], p[v]) >= 0 && Cross(p[i2], p[i0], p[v]) >= 0;
                    }
                    if (inside) continue;
                    outIdx.Add(i0); outIdx.Add(i1); outIdx.Add(i2);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (idx.Count == 3) { outIdx.Add(idx[0]); outIdx.Add(idx[1]); outIdx.Add(idx[2]); }
            else if (idx.Count > 3) for (int i = 1; i + 1 < idx.Count; i++) { outIdx.Add(idx[0]); outIdx.Add(idx[i]); outIdx.Add(idx[i + 1]); }
        }
    }

    /// <summary>SVG path data → flattened contours.</summary>
    static class PathData
    {
        public static void Parse(string d, Shape s, float scale, Vector2 off)
        {
            int i = 0;
            char cmd = 'M';
            Vector2 cur = Vector2.zero, start = Vector2.zero, ctrl = Vector2.zero;
            char prev = ' '; // last command (upper case), for the smooth S / T reflections
            List<Vector2> con = null;

            bool More()
            {
                while (i < d.Length && (char.IsWhiteSpace(d[i]) || d[i] == ',')) i++;
                return i < d.Length && (char.IsDigit(d[i]) || d[i] == '-' || d[i] == '+' || d[i] == '.');
            }
            float Num()
            {
                More();
                int st = i;
                if (i < d.Length && (d[i] == '-' || d[i] == '+')) i++;
                bool dot = false;
                while (i < d.Length && (char.IsDigit(d[i]) || (d[i] == '.' && !dot)))
                {
                    if (d[i] == '.') dot = true;
                    i++;
                }
                if (i < d.Length && (d[i] == 'e' || d[i] == 'E'))
                {
                    i++;
                    if (i < d.Length && (d[i] == '-' || d[i] == '+')) i++;
                    while (i < d.Length && char.IsDigit(d[i])) i++;
                }
                return float.Parse(d.Substring(st, i - st), CultureInfo.InvariantCulture);
            }
            Vector2 Pt(bool rel) { float x = Num(), y = Num(); return rel ? cur + new Vector2(x, y) : new Vector2(x, y); }
            void Emit(Vector2 p) => con.Add(p * scale + off);
            void Close(bool closed)
            {
                if (con != null && con.Count > 1) { s.Contours.Add(con); s.Closed.Add(closed); }
                con = null;
            }

            while (true)
            {
                while (i < d.Length && (char.IsWhiteSpace(d[i]) || d[i] == ',')) i++;
                if (i >= d.Length) break;
                if (char.IsLetter(d[i])) cmd = d[i++];
                bool rel = char.IsLower(cmd);
                char up = char.ToUpperInvariant(cmd);
                // Drawing after Z without an M continues from the closed subpath's start.
                if (con == null && up != 'M' && up != 'Z') { con = new List<Vector2>(); Emit(cur); }
                switch (up)
                {
                    case 'M':
                        Close(false);
                        cur = start = Pt(rel);
                        con = new List<Vector2>();
                        Emit(cur);
                        cmd = rel ? 'l' : 'L'; // further pairs are line-tos
                        break;
                    case 'L': cur = Pt(rel); Emit(cur); break;
                    case 'H': { float x = Num(); cur = new Vector2(rel ? cur.x + x : x, cur.y); Emit(cur); break; }
                    case 'V': { float y = Num(); cur = new Vector2(cur.x, rel ? cur.y + y : y); Emit(cur); break; }
                    case 'C':
                    case 'S':
                    {
                        Vector2 a = up == 'S' ? (prev == 'C' || prev == 'S' ? 2 * cur - ctrl : cur) : Pt(rel);
                        Vector2 b = Pt(rel), e = Pt(rel);
                        int n = Steps(cur, a, b, e);
                        for (int k = 1; k <= n; k++)
                        {
                            float t = k / (float)n, u = 1 - t;
                            Emit(u * u * u * cur + 3 * u * u * t * a + 3 * u * t * t * b + t * t * t * e);
                        }
                        ctrl = b;
                        cur = e;
                        break;
                    }
                    case 'Q':
                    case 'T':
                    {
                        Vector2 a = up == 'T' ? (prev == 'Q' || prev == 'T' ? 2 * cur - ctrl : cur) : Pt(rel);
                        Vector2 e = Pt(rel);
                        int n = Steps(cur, a, a, e);
                        for (int k = 1; k <= n; k++)
                        {
                            float t = k / (float)n, u = 1 - t;
                            Emit(u * u * cur + 2 * u * t * a + t * t * e);
                        }
                        ctrl = a;
                        cur = e;
                        break;
                    }
                    case 'A':
                    {
                        float rx = Num(), ry = Num(), phi = Num();
                        bool large = Num() != 0, sweep = Num() != 0;
                        var e = Pt(rel);
                        Arc(cur, rx, ry, phi, large, sweep, e, Emit);
                        cur = e;
                        break;
                    }
                    case 'Z':
                        Close(true);
                        cur = start;
                        break;
                    default:
                        throw new FormatException("Unsupported SVG path command " + cmd);
                }
                prev = up;
            }
            Close(false);
        }

        static int Steps(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float len = Vector2.Distance(a, b) + Vector2.Distance(b, c) + Vector2.Distance(c, d);
            return Mathf.Clamp(Mathf.CeilToInt(len / 3f), 4, 48);
        }

        static void Arc(Vector2 p0, float rx, float ry, float phiDeg, bool large, bool sweep, Vector2 p1, Action<Vector2> emit)
        {
            rx = Mathf.Abs(rx); ry = Mathf.Abs(ry);
            if (rx < 1e-6f || ry < 1e-6f) { emit(p1); return; }
            float phi = phiDeg * Mathf.Deg2Rad, cs = Mathf.Cos(phi), sn = Mathf.Sin(phi);
            float dx2 = (p0.x - p1.x) / 2, dy2 = (p0.y - p1.y) / 2;
            float x1 = cs * dx2 + sn * dy2, y1 = -sn * dx2 + cs * dy2;
            float lam = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
            if (lam > 1) { float k = Mathf.Sqrt(lam); rx *= k; ry *= k; }
            float num = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
            float den = rx * rx * y1 * y1 + ry * ry * x1 * x1;
            float coef = (large != sweep ? 1 : -1) * Mathf.Sqrt(Mathf.Max(0, num / Mathf.Max(1e-9f, den)));
            float cxp = coef * rx * y1 / ry, cyp = -coef * ry * x1 / rx;
            float cx = cs * cxp - sn * cyp + (p0.x + p1.x) / 2, cy = sn * cxp + cs * cyp + (p0.y + p1.y) / 2;
            float Ang(float ux, float uy, float vx, float vy) => Mathf.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
            float t1 = Ang(1, 0, (x1 - cxp) / rx, (y1 - cyp) / ry);
            float dt = Ang((x1 - cxp) / rx, (y1 - cyp) / ry, (-x1 - cxp) / rx, (-y1 - cyp) / ry);
            if (!sweep && dt > 0) dt -= Mathf.PI * 2;
            else if (sweep && dt < 0) dt += Mathf.PI * 2;
            int n = Mathf.Max(4, Mathf.CeilToInt(Mathf.Abs(dt) / (Mathf.PI / 18)));
            for (int k = 1; k <= n; k++)
            {
                float t = t1 + dt * k / n;
                emit(new Vector2(cx + rx * Mathf.Cos(t) * cs - ry * Mathf.Sin(t) * sn, cy + rx * Mathf.Cos(t) * sn + ry * Mathf.Sin(t) * cs));
            }
        }
    }

    /// <summary>Thick ink outline and a two-tone fill for game lettering (the web's .gtext:
    /// -webkit-text-stroke + a white / ice-blue split fill). The ink drop below (text-shadow) is off by
    /// default since 5 Nov 2026 — it read as the letters slipping out of their frame; only the title
    /// logo sets one.</summary>
    [RequireComponent(typeof(Text))]
    public sealed class GameText : BaseMeshEffect
    {
        public float Stroke = 3.5f, Drop = 0;
        public Color Ink = Look.Hex("#2B2140");
        public bool TwoTone = true;
        public Color Top = Color.white, Bottom = Look.Hex("#D3F1FF");
        public float Split = 0.52f;
        public int Dirs = 16;

        /// <summary>Centre the body of the line (baseline to the top of the letters standing on it) in the
        /// rect, CentreOffset units above its middle. uGUI centres the font's line box instead, which keeps
        /// room for Thai marks above and below, so short Thai button labels sat 5-10 pt low (7 Nov 2026).</summary>
        public bool CentreBody;
        public float CentreOffset;

        static readonly List<UIVertex> Src = new List<UIVertex>(), Dst = new List<UIVertex>();
        static readonly List<float> Bottoms = new List<float>(), Tops = new List<float>();

        public void SetGold()
        {
            Top = Look.Hex("#FFF2A8");
            Bottom = Look.Hex("#FFB52E");
            if (graphic) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            Src.Clear();
            Dst.Clear();
            vh.GetUIVertexStream(Src);
            if (CentreBody) CentreOnBody();
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var v in Src) { minY = Mathf.Min(minY, v.position.y); maxY = Mathf.Max(maxY, v.position.y); }
            if (Drop > 0)
                for (int d = 0; d < 8; d++)
                {
                    float a = d / 8f * Mathf.PI * 2;
                    Copy(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Stroke + new Vector2(0, -Drop));
                }
            if (Stroke > 0)
                for (int d = 0; d < Dirs; d++)
                {
                    float a = d / (float)Dirs * Mathf.PI * 2;
                    Copy(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Stroke);
                }
            if (!TwoTone) Dst.AddRange(Src);
            else
            {
                float split = maxY - (maxY - minY) * Split;
                // Text quads arrive as two triangles: 0 1 2, 2 3 0 (top-left, top-right, bottom-right, bottom-left).
                for (int q = 0; q + 5 < Src.Count; q += 6)
                {
                    UIVertex tl = Src[q], tr = Src[q + 1], br = Src[q + 2], bl = Src[q + 4];
                    float yt = tl.position.y, yb = bl.position.y;
                    if (yb >= split) { Add4(tl, tr, br, bl, Top); continue; }
                    if (yt <= split) { Add4(tl, tr, br, bl, Bottom); continue; }
                    float k = (yt - split) / Mathf.Max(1e-5f, yt - yb);
                    UIVertex ml = Lerp(tl, bl, k), mr = Lerp(tr, br, k);
                    Add4(tl, tr, mr, ml, Top);
                    Add4(ml, mr, br, bl, Bottom);
                }
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(Dst);
        }

        /// <summary>Shifts Src vertically: the baseline is the median bottom of the glyph quads, the body top the
        /// median top of the quads standing on that baseline (marks above / below and tall strokes such as
        /// the tail of \u0E44 or "!" are outvoted), and the middle of the two goes to the rect centre.</summary>
        void CentreOnBody()
        {
            Bottoms.Clear();
            for (int q = 0; q + 5 < Src.Count; q += 6)
            {
                float yt = Src[q].position.y, yb = Src[q + 4].position.y;
                if (yt - yb > 0.01f) Bottoms.Add(yb);
            }
            if (Bottoms.Count == 0) return;
            float baseline = Median(Bottoms);
            float tol = (graphic is Text t ? t.fontSize : 20) * 0.12f;
            Tops.Clear();
            for (int q = 0; q + 5 < Src.Count; q += 6)
            {
                float yt = Src[q].position.y, yb = Src[q + 4].position.y;
                if (yt - yb > 0.01f && Mathf.Abs(yb - baseline) <= tol) Tops.Add(yt);
            }
            if (Tops.Count == 0) return;
            float dy = graphic.rectTransform.rect.center.y + CentreOffset - (baseline + Median(Tops)) / 2;
            for (int i = 0; i < Src.Count; i++)
            {
                var v = Src[i];
                v.position.y += dy;
                Src[i] = v;
            }
        }

        static float Median(List<float> xs)
        {
            xs.Sort();
            int n = xs.Count;
            return n % 2 == 1 ? xs[n / 2] : (xs[n / 2 - 1] + xs[n / 2]) / 2;
        }

        void Copy(Vector2 off)
        {
            foreach (var s in Src)
            {
                var v = s;
                v.position += (Vector3)off;
                var c = Ink;
                c.a *= s.color.a / 255f;
                v.color = c;
                Dst.Add(v);
            }
        }

        void Add4(UIVertex a, UIVertex b, UIVertex c, UIVertex d, Color tint)
        {
            foreach (var src in new[] { a, b, c, c, d, a })
            {
                var v = src;
                var col = tint;
                col.a *= src.color.a / 255f;
                v.color = col;
                Dst.Add(v);
            }
        }

        static UIVertex Lerp(UIVertex a, UIVertex b, float k)
        {
            var v = a;
            v.position = Vector3.Lerp(a.position, b.position, k);
            v.uv0 = Vector4.Lerp(a.uv0, b.uv0, k);
            return v;
        }
    }
}
