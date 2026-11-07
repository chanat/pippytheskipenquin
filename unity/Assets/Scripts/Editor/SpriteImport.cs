// 2D sprite sheets (design/sprites2d, GDD §18.k) → Assets/Resources/Sprites2D: menu Pip ▸ Import 2D
// Sprites, or batch -executeMethod Pip.EditorTools.SpriteImport.Run. The raw sheets are keyed here (the
// AI cutouts also erased white snow inside the sprites): s01–s03 flood-fill from the border over
// near-white pixels (all channels ≥ 235), so only the background connected to the edge goes
// transparent and every white inside the thick ink outline stays, with a 2-px feathered edge; the v5
// toy-3D sheets s04–s06 and the UI kits u2 (HUD), u3 (windows), u4 (START / FINISH lettering) flood the
// border-connected light pixels (min channel ≥ 150) and un-matte them against white (UnmatteKey), so
// their soft drop shadows and the crystal's glow come out translucent; their islands are cut at
// alpha ≥ 0.15, s06 / u2–u4 slice rects grow over the translucent halo, u4 joins each row's letters,
// and the stretchable UI slices get 9-slice borders (NineSlice); s07
// (on black) is luminance-keyed (alpha = brightest channel, colour un-premultiplied) so it works both
// alpha-blended and additive; b1 is used as it is, b2 is made seamless (offset by half its size and cross-faded
// across the old edges, MakeSeamless) and blended 50 % toward flat snow #F7FBFF; b3 keeps the left 56 % (the bank), cuts
// its right edge crisply (2–3 px) and cross-fades its bottom band into its top so the strip tiles seamlessly. Every
// sheet is scaled to ≤ 2048 (sides a multiple of 4) and written on a power-of-two canvas: the non-POT
// sheets stayed RGBA32 on iOS (16 MB each) despite the ASTC override.
// Sheets are then sliced: connected alpha islands → rects (min 24 px), specks under 5.5 % of the
// largest island merged into the nearest island within 3 % of the sheet width, named <sheet>_<n> in
// reading order (rows by vertical overlap, then left to right), pivot bottom-centre (s07: centre),
// Full Rect meshes, mipmaps, ASTC 6x6 on iOS / Android, not readable; b1–b3 are one sprite each
// (pivot: b1 bottom-centre, b2 / b3 centre). Pixels-per-unit: the character
// sheets so their reference pose stands 96 units tall (Pip ≈ 96 css px at SY), the rest 100; the game
// fits props to web sizes at runtime anyway (Art2D/SpriteTable). Each slice is logged as
// "[Pip] slice s01_0 x y w h" so the role table can be checked against the sheet.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Pip.EditorTools
{
    public sealed class SpriteImport : AssetPostprocessor
    {
        public const string Dir = "Assets/Resources/Sprites2D/";
        /// <summary>The ice UI kit (7 Nov 2026): buttons, chips, tagline, counter pill, map nodes / tags / back.</summary>
        public static readonly string[] IceKit =
        {
            "ice_green", "ice_blue", "ice_frost", "ice_tagline", "ice_pill", "ice_tag", "ice_chipOn", "ice_chipOff",
            "ice_node", "ice_nodeBoss", "ice_nodeLock", "ice_back",
        };
        static readonly string[] Keyed = { "s01", "s02", "s03", "s04", "s05", "s06", "u2", "u3", "u4" };

        /// <summary>White-background sheets keyed by un-matting (v5 toy-3D art, 5 Nov 2026): s01–s03 keep FloodKey.</summary>
        static readonly HashSet<string> Unmatte = new HashSet<string> { "s04", "s05", "s06", "u2", "u3", "u4" };

        /// <summary>Sheets whose slice rects grow over the translucent pixels around each item (drop shadows,
        /// the crystal's glow) instead of cutting them at the opaque island.</summary>
        static readonly HashSet<string> GrowOverHalo = new HashSet<string> { "s06", "u2", "u3", "u4" };

        /// <summary>Sheets where every island of a row is one sprite (u4: the letters of START / FINISH).</summary>
        static readonly HashSet<string> RowMerge = new HashSet<string> { "u4" };

        /// <summary>Un-matted slices that have no dark outline around a pale inside (u3_3 the grey star socket,
        /// s06_4 the hourglass glass): the un-matting would turn their whole inside translucent, so it is kept
        /// only within SolidCoreRim px of the fully transparent background around them (the anti-aliased edge
        /// and the shadow); deeper pixels get their original colour back at alpha 1.</summary>
        static readonly Dictionary<string, int> SolidCore = new Dictionary<string, int> { { "u3", 3 }, { "s06", 4 } };
        const int SolidCoreRim = 8;

        /// <summary>9-slice borders (fractions of the slice: x = left / right, y = bottom / top) for the
        /// stretchable UI slices, by sheet slice name. Pills / bars: 45 % of the height on every side;
        /// panel / plaque: 22 % of width and height.</summary>
        static readonly Dictionary<string, string> NineSlice = new Dictionary<string, string>
        {
            { "u2_4", "panel" }, { "u2_5", "pill" }, { "u2_8", "pill" }, { "u2_9", "pill" },
            { "u3_0", "panel" }, { "u3_4", "pill" }, { "u3_5", "pill" }, { "u3_6", "pill" }, { "u3_7", "panel" }, { "u3_9", "pill" },
            { "ice_green_0", "pill" }, { "ice_blue_0", "pill" }, { "ice_frost_0", "pill" }, { "ice_tagline_0", "pill" },
            { "ice_pill_0", "pill" }, { "ice_tag_0", "pill" }, { "ice_chipOn_0", "pill" }, { "ice_chipOff_0", "pill" },
        };

        /// <summary>Character sheets: reference slice and its height in units.</summary>
        static readonly Dictionary<string, (int slice, float units)> CharacterRef = new Dictionary<string, (int, float)>
        {
            { "s01", (0, 96) }, { "s02", (4, 96) }, { "s03", (0, 100) },
        };

        static string Sources => Path.GetFullPath(Path.Combine(Application.dataPath, "../../design/sprites2d/sheets"));

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Dir)) return;
            var imp = (TextureImporter)assetImporter;
            string id = Path.GetFileNameWithoutExtension(assetPath);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Multiple;
            imp.mipmapEnabled = true;
            imp.alphaIsTransparency = true;
            imp.isReadable = false;
            imp.wrapMode = id == "b2" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.maxTextureSize = 2048;
            var st = new TextureImporterSettings();
            imp.ReadTextureSettings(st);
            st.spriteMeshType = SpriteMeshType.FullRect;
            st.spriteExtrude = 1;
            if (imp.importSettingsMissing) st.spritePixelsPerUnit = 100;
            imp.SetTextureSettings(st);
            foreach (var platform in new[] { "iPhone", "Android" })
                imp.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = platform, overridden = true, maxTextureSize = 2048,
                    // the title sign's lettering stays crisper at 4x4 (1 MB at 1024 x 1024)
                    format = id == "t1" || id.StartsWith("ice_") ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6,
                });
        }

        [MenuItem("Pip/Import 2D Sprites")]
        public static void Run() => Import(null);

        /// <summary>Only the character sheets s01–s03 (Pip, Benny): -executeMethod Pip.EditorTools.SpriteImport.RunCharacters.
        /// Leaves the props / items / VFX / backdrop sheets as they are.</summary>
        [MenuItem("Pip/Import 2D Sprites (characters only)")]
        public static void RunCharacters() => Import(new HashSet<string> { "s01", "s02", "s03" });

        /// <summary>Only the b3 edge strip: -executeMethod Pip.EditorTools.SpriteImport.RunEdge.</summary>
        [MenuItem("Pip/Import 2D Sprites (edge strip only)")]
        public static void RunEdge() => Import(new HashSet<string> { "b3" });

        /// <summary>Only the t1 title sign: -executeMethod Pip.EditorTools.SpriteImport.RunTitle.</summary>
        [MenuItem("Pip/Import 2D Sprites (title sign only)")]
        public static void RunTitle() => Import(new HashSet<string> { "t1" });

        /// <summary>Only the ice UI kit: -executeMethod Pip.EditorTools.SpriteImport.RunIce.</summary>
        [MenuItem("Pip/Import 2D Sprites (ice UI kit only)")]
        public static void RunIce() => Import(new HashSet<string>(IceKit));

        static void Import(HashSet<string> only)
        {
            Directory.CreateDirectory(Dir);
            var slices = new Dictionary<string, List<RectInt>>();
            foreach (var id in Keyed.Concat(new[] { "s07" }))
            {
                if (only != null && !only.Contains(id)) continue;
                var src = Path.Combine(Sources, id + ".png");
                if (!File.Exists(src)) { Debug.LogWarning("[Pip] missing sheet " + src); continue; }
                var tex = Fit(Load(src));
                var px = tex.GetPixels32();
                var raw = SolidCore.ContainsKey(id) ? (Color32[])px.Clone() : null;
                if (id == "s07") LumaKey(px);
                else if (Unmatte.Contains(id)) UnmatteKey(px, tex.width, tex.height);
                else FloodKey(px, tex.width, tex.height);
                bool um = Unmatte.Contains(id);
                var rects = Slice(px, tex.width, tex.height, um ? (byte)38 : (byte)128);
                if (RowMerge.Contains(id)) rects = MergeRows(rects);
                if (GrowOverHalo.Contains(id)) rects = GrowOverTranslucent(px, tex.width, tex.height, rects, tex.width * 0.025f);
                if (raw != null && SolidCore[id] < rects.Count) KeepCore(px, raw, tex.width, tex.height, rects[SolidCore[id]]);
                slices[id] = rects;
                tex.SetPixels32(px);
                tex.Apply();
                WritePot(tex, Dir + id + ".png");
            }
            // t1: the ice-crystal title sign (7 Nov 2026), already cut out (transparent PNG, 1024 x 520).
            // ice_*: the ice UI kit (7 Nov 2026), one transparent piece per file, cut out and scaled beforehand.
            foreach (var id in new[] { "b1", "b2", "t1" }.Concat(IceKit))
            {
                if (only != null && !only.Contains(id)) continue;
                // b2: the v5 tile's edges don't match (its grooves broke at the tile edge in game), so it is made
                // seamless here (MakeSeamless); the flat v3 tile is only a fallback when the v5 file is missing.
                var src = Path.Combine(Sources, id + ".png");
                if (id == "b2" && !File.Exists(src)) src = Path.Combine(Sources, "v3-flat/b2.png");
                if (!File.Exists(src)) continue;
                var tex = Fit(Load(src));
                if (id == "b2") MakeSeamless(tex, 0.14f);
                if (id == "b2") Soften(tex, new Color32(0xF7, 0xFB, 0xFF, 255), 0.5f);
                slices[id] = new List<RectInt> { new RectInt(0, 0, tex.width, tex.height) };
                WritePot(tex, Dir + id + ".png");
            }
            var b3 = Path.Combine(Sources, "b3.png");
            if (File.Exists(b3) && (only == null || only.Contains("b3")))
            {
                var tex = EdgeStrip(b3);
                slices["b3"] = new List<RectInt> { new RectInt(0, 0, tex.width, tex.height) };
                WritePot(tex, Dir + "b3.png");
            }
            AssetDatabase.Refresh();

            foreach (var kv in slices) ApplySlices(kv.Key, kv.Value);
            AssetDatabase.SaveAssets();
            Debug.Log("[Pip] 2D sprites imported: " + string.Join(", ", slices.Select(kv => kv.Key + "×" + kv.Value.Count)));
        }

        /// <summary>Logs each sheet's imported format and size for the active build target.</summary>
        public static void Check()
        {
            foreach (var path in Directory.GetFiles(Dir, "*.png"))
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path.Replace('\\', '/'));
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                var ps = imp.GetPlatformTextureSettings("iPhone");
                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[Pip] sheet {0} {1}x{2} {3} target={4} iPhone override={5} {6}", path, t ? t.width : 0, t ? t.height : 0, t ? t.format.ToString() : "-", EditorUserBuildSettings.activeBuildTarget, ps.overridden, ps.format);
            }
        }

        static Texture2D Load(string path)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.LoadImage(File.ReadAllBytes(path));
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        /// <summary>Offset-and-crossfade, one axis at a time: the image is shifted by half its width (wrapping), so
        /// its old left / right edges meet in the middle and its new edges are neighbouring source columns; a
        /// band (band × the size, smoothstep) around the middle cross-fades into the unshifted image, which is
        /// continuous there. Then the same vertically on that result. Every edge of the output then continues
        /// the opposite edge exactly (the vertical pass blends both columns of an edge pair with equal weights).</summary>
        static void MakeSeamless(Texture2D t, float band)
        {
            int w = t.width, h = t.height;
            var a = t.GetPixels();
            a = SeamPass(a, w, h, band, true);
            a = SeamPass(a, w, h, band, false);
            t.SetPixels(a);
            t.Apply();
        }

        static Color[] SeamPass(Color[] a, int w, int h, float band, bool horizontal)
        {
            var o = new Color[a.Length];
            int n = horizontal ? w : h, half = n / 2;
            float hb = band * n / 2;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int sx = horizontal ? (x + half) % w : x, sy = horizontal ? y : (y + half) % h;
                    int c = horizontal ? x : y;
                    // Distance to the seam of the shifted image (between half − 1 and half).
                    float d = Mathf.Abs(c + 0.5f - half);
                    float k = d >= hb ? 0 : 1 - Mathf.SmoothStep(0, 1, d / hb);
                    o[y * w + x] = Color.Lerp(a[sy * w + sx], a[y * w + x], k);
                }
            return o;
        }

        /// <summary>SolidCore: inside rect r, pixels further than SolidCoreRim px from the fully transparent
        /// background (alpha 0 connected to the rect's border) get their raw colour back, opaque; transparent
        /// holes inside the item (pure-white glints) are filled the same way.</summary>
        static void KeepCore(Color32[] px, Color32[] raw, int w, int h, RectInt r)
        {
            int rw = r.width, rh = r.height;
            var outside = new bool[rw * rh];
            var stack = new Stack<int>();
            void Push(int lx, int ly)
            {
                int j = ly * rw + lx;
                if (outside[j] || px[(r.y + ly) * w + r.x + lx].a != 0) return;
                outside[j] = true;
                stack.Push(j);
            }
            for (int x = 0; x < rw; x++) { Push(x, 0); Push(x, rh - 1); }
            for (int y = 0; y < rh; y++) { Push(0, y); Push(rw - 1, y); }
            while (stack.Count > 0)
            {
                int j = stack.Pop(), lx = j % rw, ly = j / rw;
                if (lx > 0) Push(lx - 1, ly);
                if (lx < rw - 1) Push(lx + 1, ly);
                if (ly > 0) Push(lx, ly - 1);
                if (ly < rh - 1) Push(lx, ly + 1);
            }
            var rim = Dilate(outside, rw, rh, SolidCoreRim);
            int kept = 0;
            for (int ly = 0; ly < rh; ly++)
                for (int lx = 0; lx < rw; lx++)
                {
                    if (rim[ly * rw + lx]) continue;
                    int i = (r.y + ly) * w + r.x + lx;
                    if (px[i].a == 255) continue;
                    var c = raw[i];
                    px[i] = new Color32(c.r, c.g, c.b, 255);
                    kept++;
                }
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[Pip] solid core {0} {1} {2} {3} {4}: {5} px made opaque", r.x, r.y, r.width, r.height, SolidCoreRim, kept);
        }

        /// <summary>b2: blend the tile k of the way toward flat snow, so its bumps are a faint texture.</summary>
        static void Soften(Texture2D t, Color32 flat, float k)
        {
            var px = t.GetPixels32();
            for (int i = 0; i < px.Length; i++) px[i] = Color32.Lerp(px[i], flat, k);
            t.SetPixels32(px);
            t.Apply();
        }

        /// <summary>Writes the sheet onto a power-of-two canvas (transparent above / right of it): iOS and
        /// Android left every non-power-of-two sheet RGBA32 (16 MB each) despite the ASTC override, while
        /// the square b2 compressed fine. Sprite rects only cover the drawn part.</summary>
        static void WritePot(Texture2D t, string dst)
        {
            int w = Mathf.NextPowerOfTwo(t.width), h = Mathf.NextPowerOfTwo(t.height);
            if (w != t.width || h != t.height)
            {
                var o = new Texture2D(w, h, TextureFormat.RGBA32, false);
                o.SetPixels32(new Color32[w * h]);
                o.SetPixels32(0, 0, t.width, t.height, t.GetPixels32());
                o.Apply();
                Object.DestroyImmediate(t);
                t = o;
            }
            File.WriteAllBytes(dst, t.EncodeToPNG());
            Object.DestroyImmediate(t);
        }

        /// <summary>Scales a sheet down to at most 2048 on its long side with both sides a multiple of 4:
        /// iOS / Android only compress (ASTC) textures whose imported size is a multiple of 4, and letting
        /// the importer's max size do the scaling gives sizes like 2048×1374 that stay RGBA32 (16 MB each).</summary>
        static Texture2D Fit(Texture2D t)
        {
            float k = Mathf.Min(1, 2048f / Mathf.Max(t.width, t.height));
            int w = Mathf.Max(4, Mathf.RoundToInt(t.width * k / 4) * 4), h = Mathf.Max(4, Mathf.RoundToInt(t.height * k / 4) * 4);
            if (w == t.width && h == t.height) return t;
            var o = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var d = new Color[w * h];
            // Box-filtered: average the source pixels each target pixel covers (k ≈ 0.8, 2×2 taps suffice).
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u0 = x / (float)w, v0 = y / (float)h, du = 0.25f / w, dv = 0.25f / h;
                    Color c = t.GetPixelBilinear(u0 + du, v0 + dv) + t.GetPixelBilinear(u0 + 3 * du, v0 + dv)
                        + t.GetPixelBilinear(u0 + du, v0 + 3 * dv) + t.GetPixelBilinear(u0 + 3 * du, v0 + 3 * dv);
                    d[y * w + x] = c * 0.25f;
                }
            o.SetPixels(d);
            o.Apply();
            Object.DestroyImmediate(t);
            return o;
        }

        /// <summary>Background = near-white pixels connected to the border; everything else stays opaque.
        /// Alpha is then feathered over ~2 px (3×3 box blur twice of the coverage mask).</summary>
        static void FloodKey(Color32[] px, int w, int h)
        {
            var bg = new bool[px.Length];
            var stack = new Stack<int>();
            bool White(int i) => px[i].r >= 235 && px[i].g >= 235 && px[i].b >= 235;
            void Push(int i) { if (!bg[i] && White(i)) { bg[i] = true; stack.Push(i); } }
            for (int x = 0; x < w; x++) { Push(x); Push((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { Push(y * w); Push(y * w + w - 1); }
            while (stack.Count > 0)
            {
                int i = stack.Pop(), x = i % w, y = i / w;
                if (x > 0) Push(i - 1);
                if (x < w - 1) Push(i + 1);
                if (y > 0) Push(i - w);
                if (y < h - 1) Push(i + w);
            }
            var a = new float[px.Length];
            for (int i = 0; i < a.Length; i++) a[i] = bg[i] ? 0 : 1;
            a = Blur(Blur(a, w, h), w, h);
            for (int i = 0; i < px.Length; i++)
            {
                // Fully inside stays fully opaque; only the boundary band softens.
                float k = bg[i] ? Mathf.Clamp01(a[i] * 1.6f - 0.3f) : Mathf.Clamp01(0.45f + a[i]);
                px[i].a = (byte)Mathf.RoundToInt(255 * k);
            }
        }

        /// <summary>White-background sheets with soft shadows / glows (s04–s06, u2–u4): the background is the
        /// "light" pixels (min channel ≥ 150) connected to the border — every item's dark outline stops the
        /// flood — and inside it each pixel is un-matted against white: a = 1 − min/255, colour =
        /// (p − 255·(1 − a)) / a. Drop shadows and the crystal's glow become translucent instead of grey /
        /// cyan blobs; a &lt; 0.03 is fully transparent. Pixels the flood did not reach stay opaque.</summary>
        static void UnmatteKey(Color32[] px, int w, int h)
        {
            var bg = new bool[px.Length];
            var stack = new Stack<int>();
            bool Light(int i) => Mathf.Min(px[i].r, Mathf.Min(px[i].g, px[i].b)) >= 150;
            void Push(int i) { if (!bg[i] && Light(i)) { bg[i] = true; stack.Push(i); } }
            for (int x = 0; x < w; x++) { Push(x); Push((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { Push(y * w); Push(y * w + w - 1); }
            while (stack.Count > 0)
            {
                int i = stack.Pop(), x = i % w, y = i / w;
                if (x > 0) Push(i - 1);
                if (x < w - 1) Push(i + 1);
                if (y > 0) Push(i - w);
                if (y < h - 1) Push(i + w);
            }
            // Seal leaks: the flood may slip through a gap of a few pixels into a large pale area inside an
            // item (the arch's blank ribbon, the hourglass glass, a sparkle on a star's tip). Open the
            // background mask (erode then dilate, 4 px) and flood again from the border over the opened
            // mask only, so narrow channels no longer connect; edge pixels beside wide background stay.
            var open = Dilate(Erode(bg, w, h, 4), w, h, 4);
            var reach = new bool[px.Length];
            void Push2(int i) { if (!reach[i] && open[i] && bg[i]) { reach[i] = true; stack.Push(i); } }
            for (int x = 0; x < w; x++) { Push2(x); Push2((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { Push2(y * w); Push2(y * w + w - 1); }
            while (stack.Count > 0)
            {
                int i = stack.Pop(), x = i % w, y = i / w;
                if (x > 0) Push2(i - 1);
                if (x < w - 1) Push2(i + 1);
                if (y > 0) Push2(i - w);
                if (y < h - 1) Push2(i + w);
            }
            // Background pixels beside the reached area (the anti-aliased rim the opening shaved off) join it.
            var near = Dilate(reach, w, h, 5);
            for (int i = 0; i < px.Length; i++)
            {
                if (!bg[i] || !near[i]) { px[i].a = 255; continue; }
                var c = px[i];
                float a = 1 - Mathf.Min(c.r, Mathf.Min(c.g, c.b)) / 255f;
                if (a < 0.03f) { px[i] = new Color32(255, 255, 255, 0); continue; }
                byte U(byte v) => (byte)Mathf.Clamp(Mathf.RoundToInt((v - 255 * (1 - a)) / a), 0, 255);
                px[i] = new Color32(U(c.r), U(c.g), U(c.b), (byte)Mathf.RoundToInt(a * 255));
            }
        }

        static bool[] Erode(bool[] m, int w, int h, int r) => Morph(m, w, h, r, true);
        static bool[] Dilate(bool[] m, int w, int h, int r) => Morph(m, w, h, r, false);

        /// <summary>Square min (erode) / max (dilate) filter of radius r, separable, via running counts.</summary>
        static bool[] Morph(bool[] m, int w, int h, int r, bool erode)
        {
            var t = new bool[m.Length];
            var o = new bool[m.Length];
            for (int y = 0; y < h; y++)
            {
                int n = 0, row = y * w;
                for (int x = -r; x < w; x++)
                {
                    int add = x + r, sub = x - r - 1;
                    if (add < w && m[row + add] != erode) n++;
                    if (sub >= 0 && m[row + sub] != erode) n--;
                    if (x >= 0) t[row + x] = erode ? n == 0 : n > 0;
                }
            }
            for (int x = 0; x < w; x++)
            {
                int n = 0;
                for (int y = -r; y < h; y++)
                {
                    int add = y + r, sub = y - r - 1;
                    if (add < h && t[add * w + x] != erode) n++;
                    if (sub >= 0 && t[sub * w + x] != erode) n--;
                    if (y >= 0) o[y * w + x] = erode ? n == 0 : n > 0;
                }
            }
            return o;
        }

        /// <summary>Joins every rect of a reading-order row into one (u4's words are a few letter islands).</summary>
        static List<RectInt> MergeRows(List<RectInt> ordered)
        {
            var o = new List<RectInt>();
            foreach (var r in ordered)
            {
                int k = o.FindIndex(m => Mathf.Min(m.yMax, r.yMax) - Mathf.Max(m.yMin, r.yMin) >= 0.3f * Mathf.Min(m.height, r.height));
                if (k < 0) { o.Add(r); continue; }
                var m0 = o[k];
                var mm = new RectInt(Mathf.Min(m0.xMin, r.xMin), Mathf.Min(m0.yMin, r.yMin), 0, 0);
                mm.xMax = Mathf.Max(m0.xMax, r.xMax); mm.yMax = Mathf.Max(m0.yMax, r.yMax);
                o[k] = mm;
            }
            return ReadingOrder(o);
        }

        /// <summary>Grows each rect over the translucent pixels (a ≥ 0.03) within margin of it that are
        /// nearer to it than to any other rect: the soft drop shadow and glow stay with their item.</summary>
        static List<RectInt> GrowOverTranslucent(Color32[] px, int w, int h, List<RectInt> rects, float margin)
        {
            var lo = rects.Select(r => new Vector2Int(r.xMin, r.yMin)).ToArray();
            var hi = rects.Select(r => new Vector2Int(r.xMax, r.yMax)).ToArray();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (px[y * w + x].a < 8) continue;
                    int best = -1; float bd = float.MaxValue;
                    for (int k = 0; k < rects.Count; k++)
                    {
                        var r = rects[k];
                        float dx = Mathf.Max(0, Mathf.Max(r.xMin - x, x - (r.xMax - 1))), dy = Mathf.Max(0, Mathf.Max(r.yMin - y, y - (r.yMax - 1)));
                        float d = dx * dx + dy * dy;
                        if (d < bd) { bd = d; best = k; }
                    }
                    if (best < 0 || bd == 0 || bd > margin * margin) continue;
                    lo[best] = Vector2Int.Min(lo[best], new Vector2Int(x, y));
                    hi[best] = Vector2Int.Max(hi[best], new Vector2Int(x + 1, y + 1));
                }
            var o = new List<RectInt>();
            for (int k = 0; k < rects.Count; k++) o.Add(new RectInt(lo[k].x, lo[k].y, hi[k].x - lo[k].x, hi[k].y - lo[k].y));
            return o;
        }

        static float[] Blur(float[] a, int w, int h)
        {
            var o = new float[a.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float s = 0; int n = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int yy = y + dy;
                        if (yy < 0 || yy >= h) continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx;
                            if (xx < 0 || xx >= w) continue;
                            s += a[yy * w + xx]; n++;
                        }
                    }
                    o[y * w + x] = s / n;
                }
            return o;
        }

        /// <summary>Black background → alpha: a = max channel (with a small floor cut), rgb / a.</summary>
        static void LumaKey(Color32[] px)
        {
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                int m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                float a = Mathf.Clamp01((m - 14) / 226f);
                if (a <= 0) { px[i] = new Color32(0, 0, 0, 0); continue; }
                float k = 255f / Mathf.Max(1, m);
                px[i] = new Color32((byte)Mathf.Min(255, c.r * k), (byte)Mathf.Min(255, c.g * k), (byte)Mathf.Min(255, c.b * k), (byte)Mathf.RoundToInt(a * 255));
            }
        }

        /// <summary>b3: left 56 % (bank + trees), cut crisply on the right (the game covers the cut with a
        /// drawn snow bank), and the bottom 10 % cross-faded
        /// into the top rows so the strip tiles top-to-bottom without a seam (output is 10 % shorter).</summary>
        static Texture2D EdgeStrip(string src)
        {
            var t = Load(src);
            int w = Mathf.RoundToInt(t.width * 0.56f), h = t.height, band = h / 10, oh = h - band;
            var o = new Texture2D(w, oh, TextureFormat.RGBA32, false);
            var s = t.GetPixels32();
            var d = new Color32[w * oh];
            int fade0 = w - 3; // crisp: a 2–3 px cut; the game lays a snow-bank strip (Placeholder2D.Bank) over it
            for (int y = 0; y < oh; y++)
                for (int x = 0; x < w; x++)
                {
                    // Rows are bottom-up: out row y < band blends source row y + oh (just above the kept
                    // top) into row y, so the tile's bottom continues the top of the tile below it.
                    Color32 c = s[y * t.width + x];
                    if (y < band) c = Color32.Lerp(s[(y + oh) * t.width + x], c, y / (float)band);
                    c.a = (byte)(x < fade0 ? 255 : Mathf.RoundToInt(255 * (1 - (x - fade0) / (float)(w - fade0))));
                    d[y * w + x] = c;
                }
            o.SetPixels32(d);
            o.Apply();
            Object.DestroyImmediate(t);
            return Fit(o);
        }

        /// <summary>Connected islands of alpha ≥ 128 → padded rects in reading order (texture space, y up).</summary>
        public static List<RectInt> Slice(Color32[] px, int w, int h) => Slice(px, w, h, 128);

        /// <summary>Islands of alpha ≥ minAlpha (the un-matted sheets use a low cut so pale items without a dark
        /// outline, like the grey star socket, still form an island while their drop shadows do not).</summary>
        public static List<RectInt> Slice(Color32[] px, int w, int h, byte minAlpha)
        {
            var label = new int[px.Length];
            var boxes = new List<RectInt>();
            var areas = new List<int>();
            var stack = new Stack<int>();
            for (int i = 0; i < px.Length; i++)
            {
                if (label[i] != 0 || px[i].a < minAlpha) continue;
                int id = boxes.Count + 1;
                int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1, n = 0;
                label[i] = id;
                stack.Push(i);
                while (stack.Count > 0)
                {
                    int j = stack.Pop(), x = j % w, y = j / w;
                    n++;
                    if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                            int k = yy * w + xx;
                            if (label[k] == 0 && px[k].a >= minAlpha) { label[k] = id; stack.Push(k); }
                        }
                }
                boxes.Add(new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1));
                areas.Add(n);
            }
            // Drop dust, then fold specks (spray drops, motion dashes) into their nearest big neighbour.
            var list = new List<RectInt>();
            for (int i = 0; i < boxes.Count; i++) if (boxes[i].width >= 6 || boxes[i].height >= 6) list.Add(boxes[i]);
            float maxArea = list.Count > 0 ? list.Max(b => (float)b.width * b.height) : 1;
            float gap = w * 0.03f;
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < list.Count && !changed; i++)
                {
                    var a = list[i];
                    bool small = a.width * (float)a.height < maxArea * 0.055f || (a.width < 24 && a.height < 24);
                    int best = -1; float bestD = float.MaxValue;
                    for (int j = 0; j < list.Count; j++)
                    {
                        if (j == i) continue;
                        var b = list[j];
                        bool inside = a.xMin >= b.xMin && a.yMin >= b.yMin && a.xMax <= b.xMax && a.yMax <= b.yMax;
                        float d = inside ? 0 : Dist(a, b);
                        bool bigger = b.width * (float)b.height > a.width * (float)a.height;
                        if ((inside || (small && bigger && d <= gap)) && d < bestD) { best = j; bestD = d; }
                    }
                    if (best >= 0)
                    {
                        var b = list[best];
                        var m = new RectInt(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), 0, 0);
                        m.xMax = Mathf.Max(a.xMax, b.xMax); m.yMax = Mathf.Max(a.yMax, b.yMax);
                        list[best] = m;
                        list.RemoveAt(i);
                        changed = true;
                    }
                    else if (small && a.width < 24 && a.height < 24) { list.RemoveAt(i); changed = true; }
                }
            }
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                int x0 = Mathf.Max(0, r.xMin - 4), y0 = Mathf.Max(0, r.yMin - 4);
                list[i] = new RectInt(x0, y0, Mathf.Min(w, r.xMax + 4) - x0, Mathf.Min(h, r.yMax + 4) - y0);
            }
            return ReadingOrder(list);
        }

        static float Dist(RectInt a, RectInt b)
        {
            float dx = Mathf.Max(0, Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax));
            float dy = Mathf.Max(0, Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Rows top to bottom (a rect joins a row when it overlaps the row's span by ≥ 30 % of the
        /// smaller height), each row left to right (near-equal x: top first).</summary>
        public static List<RectInt> ReadingOrder(List<RectInt> rects)
        {
            var byTop = rects.OrderByDescending(r => r.yMax).ToList();
            var rows = new List<(int lo, int hi, List<RectInt> items)>();
            foreach (var r in byTop)
            {
                int k = -1;
                for (int i = 0; i < rows.Count; i++)
                {
                    int ov = Mathf.Min(rows[i].hi, r.yMax) - Mathf.Max(rows[i].lo, r.yMin);
                    int minH = Mathf.Min(rows[i].hi - rows[i].lo, r.height);
                    if (ov >= 0.3f * minH) { k = i; break; }
                }
                if (k < 0) rows.Add((r.yMin, r.yMax, new List<RectInt> { r }));
                else
                {
                    var row = rows[k];
                    row.items.Add(r);
                    rows[k] = (Mathf.Min(row.lo, r.yMin), Mathf.Max(row.hi, r.yMax), row.items);
                }
            }
            var o = new List<RectInt>();
            foreach (var row in rows.OrderByDescending(rw => rw.hi))
                o.AddRange(row.items.OrderBy(r => Mathf.Round(r.center.x / 40f)).ThenByDescending(r => r.yMax));
            return o;
        }

        static void ApplySlices(string id, List<RectInt> rects)
        {
            string path = Dir + id + ".png";
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp == null) { Debug.LogError("[Pip] no importer for " + path); return; }
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(imp);
            dp.InitSpriteEditorDataProvider();
            var old = dp.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
            var srs = new List<SpriteRect>();
            var names = new List<SpriteNameFileIdPair>();
            bool centre = id == "s07" || id == "b2" || id == "b3" || id.StartsWith("u") || id.StartsWith("ice_");
            for (int i = 0; i < rects.Count; i++)
            {
                var r = rects[i];
                string name = id + "_" + i;
                var sr = new SpriteRect
                {
                    name = name,
                    rect = new Rect(r.x, r.y, r.width, r.height),
                    alignment = centre ? SpriteAlignment.Center : SpriteAlignment.BottomCenter,
                    pivot = centre ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0),
                    spriteID = old.TryGetValue(name, out var g) ? g : GUID.Generate(),
                };
                if (NineSlice.TryGetValue(name, out var kind))
                {
                    float bx = kind == "pill" ? r.height * 0.45f : r.width * 0.22f, by = kind == "pill" ? r.height * 0.45f : r.height * 0.22f;
                    bx = Mathf.Min(bx, r.width * 0.49f);
                    sr.border = new Vector4(Mathf.Round(bx), Mathf.Round(by), Mathf.Round(bx), Mathf.Round(by));
                }
                srs.Add(sr);
                names.Add(new SpriteNameFileIdPair(name, sr.spriteID));
                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[Pip] slice {0} {1} {2} {3} {4}", name, r.x, r.y, r.width, r.height);
            }
            dp.SetSpriteRects(srs.ToArray());
            dp.GetDataProvider<ISpriteNameFileIdDataProvider>()?.SetNameFileIdPairs(names);
            dp.Apply();
            if (CharacterRef.TryGetValue(id, out var cr) && cr.slice < rects.Count)
                imp.spritePixelsPerUnit = rects[cr.slice].height / cr.units;
            imp.SaveAndReimport();
        }
    }
}
