// Small helpers for the 2D views: sprite renderers on Sprites/Default, placing a slice so a chosen
// anchor of it (0..1 across, 0..1 up from its bottom) lands on a screen point at a given size, and
// TextMesh labels (the UI's Thai font, LabelFont) with a four-way ink outline.
using Pip.Game.Art;
using UnityEngine;

namespace Pip.Game.Art2D
{
    public static class Kit2D
    {
        /// <summary>Font for the gate / arch / sign labels (GameBoot sets the HUD's Thai body font).</summary>
        public static Font LabelFont;

        static Material spriteMat;

        /// <summary>The built-in Sprites/Default material (unlit, vertex colour, no lights).</summary>
        public static Material SpriteMat
        {
            get
            {
                if (spriteMat) return spriteMat;
                var sh = Shader.Find("Sprites/Default");
                if (sh != null) spriteMat = new Material(sh) { name = "pip sprites" };
                else
                {
                    var tmp = new GameObject("tmp").AddComponent<SpriteRenderer>();
                    spriteMat = tmp.sharedMaterial;
                    Object.Destroy(tmp.gameObject);
                }
                return spriteMat;
            }
        }

        public static SpriteRenderer SR(Transform parent, string name, int order = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sharedMaterial = SpriteMat;
            r.sortingOrder = order;
            return r;
        }

        /// <summary>Puts the slice so its anchor sits at screen (sx, sy), w × h units, rotated rotDeg
        /// (counter-clockwise on screen) about that anchor.</summary>
        public static void Put(SpriteRenderer r, Slice s, float sx, float sy, float w, float h, Vector2 anchor, float rotDeg = 0, bool flip = false)
        {
            if (r.sprite != s.S) r.sprite = s.S;
            r.flipX = s.FlipX ^ flip;
            var native = (Vector2)s.S.bounds.size;
            var pn = new Vector2(s.S.pivot.x / s.S.rect.width, s.S.pivot.y / s.S.rect.height);
            var t = r.transform;
            t.localScale = new Vector3(w / Mathf.Max(1e-4f, native.x), h / Mathf.Max(1e-4f, native.y), 1);
            var rot = Quaternion.Euler(0, 0, rotDeg);
            t.localRotation = rot;
            var off = new Vector3((anchor.x - pn.x) * w, (anchor.y - pn.y) * h, 0);
            t.localPosition = View2D.P(sx, sy) - rot * off;
        }

        /// <summary>Put at the slice's own role size × k.</summary>
        public static void PutK(SpriteRenderer r, Slice s, float sx, float sy, float k, Vector2 anchor, float rotDeg = 0, bool flip = false)
        {
            var sz = s.Size * k;
            Put(r, s, sx, sy, sz.x, sz.y, anchor, rotDeg, flip);
        }

        public static void Alpha(SpriteRenderer r, float a, Color? tint = null)
        {
            var c = tint ?? Color.white;
            c.a *= a;
            if (r.color != c) r.color = c;
        }

        public static readonly Vector2 Bottom = new Vector2(0.5f, 0), Centre = new Vector2(0.5f, 0.5f);
    }

    /// <summary>A TextMesh label with an ink outline (four offset copies), sized by cap height in units.</summary>
    public sealed class Label2D
    {
        static float unitHeight = -1;
        readonly Transform root;
        readonly TextMesh main;
        readonly TextMesh[] outline = new TextMesh[4];
        float alpha = 1;
        Color color = Color.white;

        public Transform Root => root;

        public Label2D(Transform parent, string name)
        {
            root = new GameObject(name).transform;
            root.SetParent(parent, false);
            for (int i = 0; i < 4; i++) outline[i] = Make(root, "ink", Tex.C(Placeholder2D.Ink));
            main = Make(root, "text", Color.white);
        }

        static TextMesh Make(Transform parent, string name, Color c)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tm = go.AddComponent<TextMesh>();
            var font = Kit2D.LabelFont ? Kit2D.LabelFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            tm.fontSize = 64;
            tm.characterSize = 1;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = c;
            return tm;
        }

        /// <summary>Height of "8" at characterSize 1 (measured once from a real mesh).</summary>
        static float UnitHeight(TextMesh probe)
        {
            if (unitHeight > 0) return unitHeight;
            string keep = probe.text;
            probe.text = "8";
            var b = probe.GetComponent<MeshRenderer>().bounds;
            float h = b.size.y / Mathf.Max(1e-4f, probe.transform.lossyScale.y);
            probe.text = keep;
            unitHeight = h > 0.01f ? h : 4.6f;
            return unitHeight;
        }

        public void Set(string text, float sx, float sy, float capHeight, Color fill, float a, int order)
        {
            if (main.text != text)
            {
                main.text = text;
                foreach (var o in outline) o.text = text;
            }
            root.localPosition = View2D.P(sx, sy);
            float cs = capHeight / UnitHeight(main);
            float d = Mathf.Max(0.8f, capHeight * 0.09f);
            for (int i = 0; i < 4; i++)
            {
                var o = outline[i];
                o.characterSize = cs;
                o.transform.localPosition = new Vector3(i < 2 ? -d : d, i % 2 == 0 ? -d : d, 0);
                o.GetComponent<MeshRenderer>().sortingOrder = order;
            }
            main.characterSize = cs;
            main.GetComponent<MeshRenderer>().sortingOrder = order + 1;
            if (fill != color || a != alpha)
            {
                color = fill; alpha = a;
                var f = fill; f.a *= a; main.color = f;
                var ink = Tex.C(Placeholder2D.Ink, a);
                foreach (var o in outline) o.color = ink;
            }
        }

        public void Show(bool on) { if (root.gameObject.activeSelf != on) root.gameObject.SetActive(on); }
    }
}
