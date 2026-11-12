// App icons from design/icon/icon-master.png (2048², full-bleed square), safe to re-run after the master changes.
// Menu: Pip ▸ Setup App Icons, or batch mode: -executeMethod Pip.EditorTools.IconSetup.Run
// Writes the derived PNGs into Assets/Icons/ (outside Resources, so they are not shipped as game data) and
// assigns them in Player Settings: every iOS icon kind, Android adaptive (background + foreground) / round /
// legacy, and the default icon (Standalone). Nothing else in Player Settings is touched.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Pip.EditorTools
{
    public static class IconSetup
    {
        public const string MasterPath = "../design/icon/icon-master.png"; // relative to the Unity project
        public const string Dir = "Assets/Icons";
        public const string IOS = Dir + "/icon-ios-1024.png";
        public const string Foreground = Dir + "/icon-android-foreground.png";
        public const string Background = Dir + "/icon-android-background.png";
        public const string Round = Dir + "/icon-android-round.png";
        public const string Legacy = Dir + "/icon-android-legacy.png";

        const int Master = 2048;
        const int Big = 1024;
        const int Small = 512;
        const int CornerRegion = 128; // background colour = mean of four 128² corner squares of the master
        const int MarginBlur = 6;     // box radius of the light blur on the foreground's extended margin
        const float MarginFade = 24f; // px outside the picture over which that blur fades in

        [MenuItem("Pip/Setup App Icons")]
        public static void Run()
        {
            string master = Path.GetFullPath(MasterPath);
            if (!File.Exists(master))
            {
                Debug.LogError("[Pip] icon master missing: " + master);
                return;
            }
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!src.LoadImage(File.ReadAllBytes(master)) || src.width != Master || src.height != Master)
            {
                Debug.LogError($"[Pip] icon master must be a {Master}x{Master} PNG: {master} ({src.width}x{src.height})");
                UnityEngine.Object.DestroyImmediate(src);
                return;
            }
            Color32[] m = src.GetPixels32();
            UnityEngine.Object.DestroyImmediate(src);
            for (int i = 0; i < m.Length; i++) m[i].a = 255; // the picture is opaque; ignore the file's alpha channel

            Directory.CreateDirectory(Dir);
            Write(IOS, Big, Box(m, Master, 2), false);
            Write(Foreground, Big, AdaptiveForeground(m), false);
            Write(Background, Big, Solid(CornerMean(m), Big), false);
            Color32[] small = Box(m, Master, 4);
            Write(Legacy, Small, small, false);
            Write(Round, Small, Circle(small, Small), true);
            AssetDatabase.Refresh();

            Import(IOS, TextureImporterAlphaSource.None, false);
            Import(Foreground, TextureImporterAlphaSource.FromInput, false);
            Import(Background, TextureImporterAlphaSource.FromInput, false);
            Import(Legacy, TextureImporterAlphaSource.FromInput, false);
            Import(Round, TextureImporterAlphaSource.FromInput, true);

            var ios = AssetDatabase.LoadAssetAtPath<Texture2D>(IOS);
            AssignIOS(ios);
            AssignAndroid(AssetDatabase.LoadAssetAtPath<Texture2D>(Background),
                AssetDatabase.LoadAssetAtPath<Texture2D>(Foreground),
                AssetDatabase.LoadAssetAtPath<Texture2D>(Round),
                AssetDatabase.LoadAssetAtPath<Texture2D>(Legacy));
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { ios }, IconKind.Any);
            Debug.Log("[Pip] icon default (Standalone / any platform): " + IOS);
            AssetDatabase.SaveAssets();
            Debug.Log("[Pip] icon setup done from " + master);
        }

        // ---------- Player Settings ----------

        static void AssignIOS(Texture2D tex)
        {
            var kinds = PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.iOS);
            if (kinds == null || kinds.Length == 0)
            {
                Debug.LogWarning("[Pip] icon iOS: no icon kinds (iOS Build Support missing?) — iOS icons not assigned.");
                return;
            }
            foreach (var kind in kinds)
            {
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, kind);
                foreach (var icon in icons) icon.SetTexture(tex); // Unity resizes the 1024 to every slot
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.iOS, kind, icons);
                Debug.Log($"[Pip] icon iOS {kind}: {icons.Length} slots ← {tex.name}");
            }
        }

        static void AssignAndroid(Texture2D bg, Texture2D fg, Texture2D round, Texture2D legacy)
        {
            var kinds = PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android);
            if (kinds == null || kinds.Length == 0)
            {
                Debug.LogWarning("[Pip] icon Android: no icon kinds (Android Build Support missing?) — Android icons not assigned.");
                return;
            }
            bool adaptive = false, rnd = false, leg = false;
            foreach (var kind in kinds)
            {
                string name = kind.ToString();
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                if (name.StartsWith("Adaptive", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var icon in icons) icon.SetTextures(bg, fg); // layer 0 background, layer 1 foreground
                    adaptive = true;
                    Debug.Log($"[Pip] icon Android {name}: {icons.Length} slots ← {bg.name} + {fg.name}");
                }
                else if (name.StartsWith("Round", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var icon in icons) icon.SetTexture(round);
                    rnd = true;
                    Debug.Log($"[Pip] icon Android {name}: {icons.Length} slots ← {round.name}");
                }
                else if (name.StartsWith("Legacy", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var icon in icons) icon.SetTexture(legacy);
                    leg = true;
                    Debug.Log($"[Pip] icon Android {name}: {icons.Length} slots ← {legacy.name}");
                }
                else
                {
                    Debug.Log($"[Pip] icon Android {name}: left as is");
                    continue;
                }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            }
            // Unity offers Round / Legacy only below Android API 26; with Minimum API Level 26+ every launcher uses the
            // adaptive icon, so the round / legacy PNGs are written but have no slot to go into.
            if (!adaptive || !rnd || !leg)
                Debug.LogWarning($"[Pip] icon Android: kinds not offered by Unity (adaptive {adaptive}, round {rnd}, legacy {leg}; "
                                 + $"min API level {(int)PlayerSettings.Android.minSdkVersion}); found: "
                                 + string.Join(", ", kinds.Select(k => k.ToString())));
        }

        // ---------- Files ----------

        static void Write(string path, int size, Color32[] px, bool alpha)
        {
            var t = new Texture2D(size, size, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);
            t.SetPixels32(px);
            t.Apply(false);
            File.WriteAllBytes(path, t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
            Debug.Log($"[Pip] icon {path}: {size}x{size} {(alpha ? "RGBA" : "RGB")}");
        }

        static void Import(string path, TextureImporterAlphaSource alphaSource, bool alphaIsTransparency)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.mipmapEnabled = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 1024;
            ti.isReadable = false;
            ti.alphaSource = alphaSource;
            ti.alphaIsTransparency = alphaIsTransparency;
            ti.SaveAndReimport();
        }

        // ---------- Pixels (Texture2D order: row 0 at the bottom; the maps below are symmetric) ----------

        /// <summary>Exact f × f box average (size must divide by f).</summary>
        static Color32[] Box(Color32[] s, int size, int f)
        {
            int n = size / f;
            var o = new Color32[n * n];
            float inv = 1f / (f * f);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float r = 0, g = 0, b = 0;
                for (int j = 0; j < f; j++)
                {
                    int row = (y * f + j) * size + x * f;
                    for (int i = 0; i < f; i++)
                    {
                        var c = s[row + i];
                        r += c.r; g += c.g; b += c.b;
                    }
                }
                o[y * n + x] = new Color32(B(r * inv), B(g * inv), B(b * inv), 255);
            }
            return o;
        }

        /// <summary>The master drawn at exactly 2/3 of a 1024 canvas, centred: 2048 / (2048/3) = 3, and the picture's
        /// left edge sits at 1024/6, so canvas pixel d covers master pixels 3d − 512 … 3d − 510 exactly (a 3 × 3 box).
        /// Outside the picture the same box reads clamped coordinates, i.e. the edge pixels extended outward, and a
        /// light blur fades in over the first <see cref="MarginFade"/> px of that margin.</summary>
        static Color32[] AdaptiveForeground(Color32[] m)
        {
            const int N = Big;
            var e = new Color32[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float r = 0, g = 0, b = 0;
                for (int j = 0; j < 3; j++)
                {
                    int sy = Mathf.Clamp(3 * y - 512 + j, 0, Master - 1);
                    for (int i = 0; i < 3; i++)
                    {
                        int sx = Mathf.Clamp(3 * x - 512 + i, 0, Master - 1);
                        var c = m[sy * Master + sx];
                        r += c.r; g += c.g; b += c.b;
                    }
                }
                e[y * N + x] = new Color32(B(r / 9f), B(g / 9f), B(b / 9f), 255);
            }
            Color32[] blur = BoxBlur(BoxBlur(e, N, MarginBlur, true), N, MarginBlur, false);
            const float lo = Big / 6f, hi = Big - Big / 6f; // the picture spans [170.67, 853.33]
            var o = new Color32[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float cx = x + 0.5f, cy = y + 0.5f;
                float dx = Mathf.Max(lo - cx, cx - hi, 0f), dy = Mathf.Max(lo - cy, cy - hi, 0f);
                float d = Mathf.Max(dx, dy);
                int k = y * N + x;
                if (d <= 0f) { o[k] = e[k]; continue; }
                float t = Mathf.SmoothStep(0f, 1f, d / MarginFade);
                o[k] = Color32.Lerp(e[k], blur[k], t);
                o[k].a = 255;
            }
            return o;
        }

        static Color32[] BoxBlur(Color32[] s, int n, int rad, bool horizontal)
        {
            var o = new Color32[n * n];
            float inv = 1f / (2 * rad + 1);
            for (int a = 0; a < n; a++)
            for (int b = 0; b < n; b++)
            {
                float r = 0, g = 0, bl = 0;
                for (int k = -rad; k <= rad; k++)
                {
                    int q = Mathf.Clamp(b + k, 0, n - 1);
                    var c = horizontal ? s[a * n + q] : s[q * n + a];
                    r += c.r; g += c.g; bl += c.b;
                }
                int idx = horizontal ? a * n + b : b * n + a;
                o[idx] = new Color32(B(r * inv), B(g * inv), B(bl * inv), 255);
            }
            return o;
        }

        public static Color32 CornerMean(Color32[] m)
        {
            double r = 0, g = 0, b = 0;
            int n = 0;
            foreach (int oy in new[] { 0, Master - CornerRegion })
            foreach (int ox in new[] { 0, Master - CornerRegion })
                for (int y = oy; y < oy + CornerRegion; y++)
                for (int x = ox; x < ox + CornerRegion; x++)
                {
                    var c = m[y * Master + x];
                    r += c.r; g += c.g; b += c.b; n++;
                }
            return new Color32(B((float)(r / n)), B((float)(g / n)), B((float)(b / n)), 255);
        }

        static Color32[] Solid(Color32 c, int size)
        {
            var o = new Color32[size * size];
            for (int i = 0; i < o.Length; i++) o[i] = c;
            return o;
        }

        /// <summary>Masked to the inscribed circle; the edge alpha is the pixel's coverage (4 × 4 supersampled).</summary>
        static Color32[] Circle(Color32[] s, int size)
        {
            var o = new Color32[size * size];
            float c = size * 0.5f, r2 = c * c;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int inside = 0;
                for (int j = 0; j < 4; j++)
                for (int i = 0; i < 4; i++)
                {
                    float px = x + (i + 0.5f) / 4f - c, py = y + (j + 0.5f) / 4f - c;
                    if (px * px + py * py <= r2) inside++;
                }
                var p = s[y * size + x];
                p.a = B(255f * inside / 16f);
                if (p.a == 0) p = new Color32(0, 0, 0, 0);
                o[y * size + x] = p;
            }
            return o;
        }

        static byte B(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v), 0, 255);
    }
}
