// Small animated pieces of the HUD and popups: the question countdown ring, candy-striped progress
// fills, rows that fade in one after another, and the result stars that slam into their sockets.
using UnityEngine;
using UnityEngine.UI;

namespace Pip.Game.Ui
{
    /// <summary>A thick arc from 12 o'clock, clockwise, covering Fraction of the circle (#qring).</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class Ring : MaskableGraphic
    {
        public float Width = 7;
        float fraction = 1;

        public float Fraction
        {
            get => fraction;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Abs(value - fraction) < 0.002f) return;
                fraction = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (fraction <= 0) return;
            var r = GetPixelAdjustedRect();
            float rad = Mathf.Min(r.width, r.height) / 2 - Width / 2, hw = Width / 2, px = Aa.Px(this);
            int n = Mathf.Max(2, Mathf.CeilToInt(64 * fraction));
            var c = r.center;
            var outer = new System.Collections.Generic.List<Vector2>(n + 1);
            var inner = new System.Collections.Generic.List<Vector2>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.PI / 2 - fraction * Mathf.PI * 2 * i / n;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                outer.Add(c + d * (rad + hw));
                inner.Add(c + d * (rad - hw));
                vh.AddVert(outer[i], color, Vector2.zero);
                vh.AddVert(inner[i], color, Vector2.zero);
                if (i > 0) { int b = i * 2; vh.AddTriangle(b - 2, b, b + 1); vh.AddTriangle(b + 1, b - 1, b - 2); }
            }
            Aa.Edge(vh, outer, inner, color, px);
            Aa.Edge(vh, inner, outer, color, px);
            // Round caps.
            foreach (float a in new[] { Mathf.PI / 2, Mathf.PI / 2 - fraction * Mathf.PI * 2 })
                Aa.Disc(vh, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad, hw, color, px);
        }
    }

    /// <summary>Diagonal two-colour stripes (repeating-linear-gradient 135deg); clip with a Mask.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class Stripes : MaskableGraphic
    {
        public Color A = Look.Hex("#3FA9F5"), B = Look.Hex("#2F8FD8");
        public float Band = 8, Period = 14;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            void Q(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col)
            {
                int i = vh.currentVertCount;
                vh.AddVert(a, col, Vector2.zero); vh.AddVert(b, col, Vector2.zero); vh.AddVert(c, col, Vector2.zero); vh.AddVert(d, col, Vector2.zero);
                vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i + 2, i + 3, i);
            }
            Q(new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin), new Vector2(r.xMin, r.yMin), A * color);
            // 135deg: the colour runs toward the bottom-right, so the bands lean "/"; the period is
            // measured across them.
            float step = Period * Mathf.Sqrt(2), w = (Period - Band) * Mathf.Sqrt(2), h = r.height;
            for (float x = r.xMin - h + Band * Mathf.Sqrt(2); x < r.xMax; x += step)
                Q(new Vector2(x + h, r.yMax), new Vector2(x + h + w, r.yMax), new Vector2(x + w, r.yMin), new Vector2(x, r.yMin), B * color);
        }
    }

    /// <summary>Fades a CanvasGroup in after a delay, sliding from the left (#resRows li).</summary>
    public sealed class FadeIn : MonoBehaviour
    {
        public float Delay, Dur = 0.3f, Slide = 14;
        float t0;
        CanvasGroup cg;
        RectTransform inner;

        public static FadeIn On(RectTransform row, RectTransform inner, float delay)
        {
            var f = row.gameObject.AddComponent<FadeIn>();
            f.cg = row.gameObject.AddComponent<CanvasGroup>();
            f.inner = inner;
            f.Delay = delay;
            return f;
        }

        void OnEnable() { t0 = Time.unscaledTime; Update(); }

        void Update()
        {
            float k = Mathf.Clamp01((Time.unscaledTime - t0 - Delay) / Dur);
            k = 1 - (1 - k) * (1 - k);
            if (cg) cg.alpha = k;
            if (inner) inner.anchoredPosition = new Vector2(-Slide * (1 - k), 0);
        }
    }

    /// <summary>A result star: grey socket, then the gold star spins in big and settles (@keyframes
    /// starIn) with a golden ring bursting behind it.</summary>
    public sealed class StarPop : MonoBehaviour
    {
        public RectTransform Gold, Burst;
        public float At = -1;
        public bool Chimed;
        CanvasGroup goldCg, burstCg;

        public void Init()
        {
            goldCg = Gold.gameObject.AddComponent<CanvasGroup>();
            burstCg = Burst.gameObject.AddComponent<CanvasGroup>();
            Hide();
        }

        public void Hide()
        {
            At = -1;
            Chimed = false;
            goldCg.alpha = 0;
            burstCg.alpha = 0;
        }

        public void Play(float delay) => At = Time.unscaledTime + delay;

        void Update()
        {
            if (At < 0) return;
            float t = Time.unscaledTime - At;
            if (t < 0) { goldCg.alpha = 0; burstCg.alpha = 0; return; }
            // cubic-bezier(.2, 1.6, .45, 1) over 0.55 s: overshoot then settle.
            float k = Mathf.Clamp01(t / 0.55f), c = 2.4f, x = k - 1, e = 1 + (c + 1) * x * x * x + c * x * x;
            Gold.localScale = Vector3.one * Mathf.LerpUnclamped(2.3f, 1, e);
            Gold.localRotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(40, 0, e));
            goldCg.alpha = Mathf.Clamp01(k / 0.55f);
            float b = Mathf.Clamp01((t - 0.22f) / 0.6f);
            Burst.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.7f, 1 - (1 - b) * (1 - b));
            burstCg.alpha = t < 0.22f ? 0 : 1 - b;
        }
    }
}
