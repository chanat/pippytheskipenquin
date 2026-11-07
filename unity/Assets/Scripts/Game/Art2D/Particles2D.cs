// ParticlesView's emission and motion on the 2D projection: bursts (puff / spark / confetti), ski spray
// off the outside ski (more with speed, carve and boost), boost speed streaks rushing up the slope,
// crystal sparks, a landing poof; drawn with the s07 VFX sprites as two meshes (alpha: snow puffs,
// confetti, streaks; additive: sparkles). A particle at course (x, z) with height h sits at screen
// (toX(x, toY(z)), toY(z) − h). Falling snow is screen-space like the web's flakes (snow 70,
// blizzard 160 and windier, a few on sunny days). While skiing on the snow both ski tails leave a
// continuous wake of small world-anchored puffs that the slope carries up the screen, and from
// spd01 ≥ StreakFrom faint speed streaks rush past even without boost.
using System.Collections.Generic;
using Pip.Core;
using Pip.Game.Art;
using UnityEngine;

namespace Pip.Game.Art2D
{
    public sealed class Particles2D : MonoBehaviour
    {
        sealed class P
        {
            public float X, Z, H, Vx, Vz, Vh, Life, Max, Size, Rot, VRot, Len, Side;
            public Color Col;
            public string Role;
            public bool Glow, Spray, Streak, Poof, Wake;
        }

        sealed class Flake { public float X, Y, R, Vy, Vx, O, Ph; }

        const int Max = 900;
        readonly List<P> ps = new List<P>();
        readonly List<Flake> flakes = new List<Flake>();
        Mesh soft, back, glow;
        MeshRenderer backR;
        Texture2D tex;
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Color> c = new List<Color>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> tri = new List<int>();
        float sprayAcc, streakAcc, wakeAcc;

        // Wake off both ski tails (tune here): rate = WakeRate + WakeRateSpd·spd01 per second.
        public const float WakeRate = 18, WakeRateSpd = 40, WakeTailX = 12;
        // A fresh wake puff draws over Pip's boots for WakeFrontT s, then behind him as the slope carries it up.
        public const float WakeFrontT = 0.06f, WakeDraw = 1.6f;
        public const float WakeSizeMin = 7, WakeSizeMax = 13, WakeLifeMin = 0.55f, WakeLifeMax = 0.9f;
        // Over its life a puff drifts WakeSpread units outward, grows 1 → WakeGrow and fades only in the last
        // WakeFadeLast of its life, so the trail reads as kicked-up snow rather than dots.
        public const float WakeSpread = 6, WakeGrow = 1.35f, WakeFadeLast = 0.4f;
        // Contrast on white snow: a clearly bluer fill over a darker rim (a second quad of the same sprite,
        // RimScale × the size, drawn under every puff) — the cartoon edge the thick-outline art has.
        public const string WakeColor = "#AFC9EA", RimColor = "#86A9DB";
        public const float WakeAlpha = 0.85f, RimAlpha = 0.5f, RimScale = 1.2f;
        // Carve spray, same contrast rule (rate unchanged): tint, alpha and size factor, with the rim.
        public const string SprayColor = "#AFC9EA";
        public const float SprayAlpha = 0.9f, SprayDraw = 1.25f;
        // Spray starts this far up the slope from Pip's feet, so the bolder puffs go behind him (and fly out
        // sideways) instead of covering his belly.
        public const float SprayBehind = 3;
        static readonly Color RimTint = Tex.C(RimColor);
        // Speed streaks without boost: from spd01 ≥ StreakFrom, alpha StreakAlpha·(spd01 − StreakFrom)/(1 − StreakFrom).
        public const float StreakFrom = 0.55f, StreakAlpha = 0.35f;
        bool wasAir;
        string weather = "snow";

        public RunSim Sim;
        public int Count => ps.Count;

        static readonly string[] Sprays = { "vfx.spray0", "vfx.spray1", "vfx.spray2" };
        static readonly string[] Sparkles = { "vfx.sparkle0", "vfx.sparkle1" };
        static readonly string[] Confetti = { "vfx.confetti0", "vfx.confetti1", "vfx.confetti2", "vfx.confetti3" };

        bool real;

        public void Init()
        {
            // All VFX roles must come from one texture (one mesh per blend): the s07 sheet, else stand-ins.
            real = true;
            foreach (var role in SpriteTable.All) if (role.StartsWith("vfx.") && !SpriteTable.Has(role)) real = false;
            tex = real ? SpriteTable.Get("vfx.spray0").S.texture : Placeholder2D.Puff.texture;
            soft = Layer("vfx", Mats.Unlit(tex, Color.white, Blend.Alpha, false, 3000), View2D.Particles);
            // Snow thrown back up the slope (ground y above Pip's) goes behind Pip, not over his belly.
            back = Layer("vfx behind pip", Mats.Unlit(tex, Color.white, Blend.Alpha, false, 3000), 0);
            backR = transform.Find("vfx behind pip").GetComponent<MeshRenderer>();
            glow = Layer("vfx glow", Mats.Unlit(tex, Color.white, Blend.Additive, false, 3001), View2D.Glow);
        }

        Mesh Layer(string name, Material m, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.sortingOrder = order;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return mesh;
        }

        public void SetWeather(string w)
        {
            weather = w ?? "snow";
            flakes.Clear();
        }

        public void Clear() => ps.Clear();

        static string Pick(string[] set) => set[Random.Range(0, set.Length)];

        /// <summary>kind: puff (snow), spark (additive glints), confetti (party colours).</summary>
        public void Burst(double x, double z, double h, int n, string kind, string[] colors)
        {
            for (int i = 0; i < n && ps.Count < Max; i++)
            {
                float up = kind == "confetti" ? 160 : kind == "spark" ? 90 : 80;
                float spread = kind == "confetti" ? 260 : kind == "spark" ? 160 : 200;
                float life = (kind == "confetti" ? 1.0f : kind == "spark" ? 0.55f : 0.6f) * (0.7f + Random.value * 0.5f);
                var col = Tex.C(colors != null && colors.Length > 0 ? colors[i % colors.Length] : "#FFFFFF");
                bool conf = kind == "confetti";
                ps.Add(new P
                {
                    X = (float)x + (Random.value - 0.5f) * 20, Z = (float)z + (Random.value - 0.5f) * 10, H = (float)h,
                    Vx = (Random.value - 0.5f) * spread, Vz = (Random.value - 0.5f) * 60, Vh = up + Random.value * 80,
                    Life = life, Max = life, Size = conf ? 7 : 4 + Random.value * 4, Col = col, Glow = kind == "spark",
                    Role = kind == "spark" ? Pick(Sparkles) : conf ? Confetti[i % 4] : Pick(Sprays),
                    Rot = Random.value * 360, VRot = (Random.value - 0.5f) * 720,
                });
            }
        }

        void Emit(RunSim g, float dt)
        {
            if (g == null || dt <= 0) return;
            float boost = (float)g.BoostLevel;
            if (g.Started && !g.Air && g.TumbleT <= 0 && !g.Finished)
            {
                float vMin = (float)(g.L.V0 * g.Mul * 0.85), vMax = (float)(g.L.V1 * g.Mul);
                float spd = Mathf.Clamp01(((float)g.V - vMin) / Mathf.Max(1, vMax - vMin));
                float lean = (float)g.Lean;
                sprayAcc += (18 + spd * 70) * (0.35f + Mathf.Min(1, Mathf.Abs(lean)) * 1.8f) * (1 + 1.2f * boost) * dt;
                int side = lean > 0 ? 1 : lean < 0 ? -1 : (Random.value < 0.5f ? -1 : 1);
                while (sprayAcc >= 1 && ps.Count < Max)
                {
                    sprayAcc -= 1;
                    ps.Add(new P
                    {
                        X = (float)g.X + side * 6 + (Random.value - 0.5f) * 26, H = 3, Z = (float)g.Z - SprayBehind - Random.value * 8,
                        Vx = -side * (40 + Random.value * 90) * (0.35f + Mathf.Abs(lean)), Vh = 45 + Random.value * 85, Vz = (float)g.V * (0.12f + Random.value * 0.25f),
                        Life = 0.45f + Random.value * 0.35f, Max = 0.8f, Size = 6 + Random.value * 7, Col = Tex.C(SprayColor), Spray = true,
                        Role = Pick(Sprays), Rot = (Random.value - 0.5f) * 60,
                    });
                }
                // Wake: puffs off both ski tails, left where they fell (Vz 0) so the slope scrolls them up.
                float s01 = Pip2D.Spd01(g);
                wakeAcc += (WakeRate + WakeRateSpd * s01) * dt;
                while (wakeAcc >= 1 && ps.Count < Max)
                {
                    wakeAcc -= 1;
                    float tail = Random.value < 0.5f ? -1 : 1;
                    float life = WakeLifeMin + Random.value * (WakeLifeMax - WakeLifeMin);
                    ps.Add(new P
                    {
                        X = (float)g.X + tail * (WakeTailX + Random.value * 4), H = 1 + Random.value * 3, Z = (float)g.Z - 2 - Random.value * 5,
                        Vx = tail * (6 + Random.value * 10), Vh = 25 + Random.value * 35, Vz = 0, Side = tail,
                        Life = life, Max = life, Size = WakeSizeMin + Random.value * (WakeSizeMax - WakeSizeMin), Col = Tex.C(WakeColor),
                        Wake = true, Role = Pick(Sprays), Rot = Random.value * 360, VRot = (Random.value - 0.5f) * 120,
                    });
                }
            }
            else wakeAcc = 0;
            // Streaks: full boost behaviour as before; otherwise faint ones once the run is fast enough.
            float fast = g.Started && !g.Finished && !g.Air ? Mathf.Clamp01((Pip2D.Spd01(g) - StreakFrom) / (1 - StreakFrom)) : 0;
            bool boosted = boost > 0.2f;
            if ((boosted || fast > 0) && g.Started && !g.Finished)
            {
                float alpha = boosted ? 0.8f * boost : StreakAlpha * fast;
                streakAcc += 55 * (boosted ? boost : fast) * dt;
                while (streakAcc >= 1 && ps.Count < Max)
                {
                    streakAcc -= 1;
                    float s = Random.value < 0.5f ? -1 : 1;
                    ps.Add(new P
                    {
                        X = (float)g.X + s * (40 + Random.value * 80), H = 10 + Random.value * 50, Z = (float)g.Z + 120 + Random.value * 180,
                        Vz = -(float)g.V * 1.6f, Life = 0.25f, Max = 0.25f, Size = 5 + Random.value * 2, Len = 46 + Random.value * 30,
                        Col = Tex.C("#A8CCF5", alpha), Streak = true, Role = Random.value < 0.5f ? "vfx.streak0" : "vfx.streak1",
                    });
                }
            }
            else streakAcc = 0;
            if (g.Power > 0 && Random.value < 0.6f) Burst(g.X, g.Z, 4, 1, "spark", new[] { "#8FF3FF", "#FFE98A", "#FFFFFF" });
            // Landing poof under the skis.
            if (wasAir && !g.Air && g.TumbleT <= 0 && ps.Count < Max)
                ps.Add(new P { X = (float)g.X, Z = (float)g.Z, H = 0, Vz = (float)g.V * 0.6f, Life = 0.4f, Max = 0.4f, Size = 34, Col = Tex.C("#EAF2FF"), Poof = true, Role = "vfx.poof" });
            wasAir = g.Air;
        }

        void Flakes(float dt)
        {
            int want = weather == "blizzard" ? 160 : weather == "snow" ? 70 : 16;
            float H = View2D.H, W = View2D.W;
            while (flakes.Count < want)
                flakes.Add(new Flake { X = Random.value * (W + 40) - 20, Y = Random.value * H, R = 1.2f + Random.value * 2.2f, Vy = 30 + Random.value * 50, Vx = (Random.value - 0.5f) * 20 + (weather == "blizzard" ? 70 : 0), O = weather == "sunny" ? 0.35f + Random.value * 0.3f : 0.55f + Random.value * 0.4f, Ph = Random.value * 6 });
            float t = View2D.T;
            foreach (var f in flakes)
            {
                f.Y += f.Vy * dt;
                f.X += (f.Vx + Mathf.Sin(t * 1.3f + f.Ph) * 12) * dt;
                if (f.Y > H + 8) { f.Y = -8; f.X = Random.value * (W + 40) - 20; }
                if (f.X > W + 24) f.X -= W + 48; else if (f.X < -24) f.X += W + 48;
            }
        }

        void LateUpdate()
        {
            if (soft == null) return;
            float dt = View2D.Dt;
            Emit(Sim, dt);
            Flakes(dt);
            double pz = Sim != null ? Sim.Z : 0;
            for (int i = ps.Count - 1; i >= 0; i--)
            {
                var p = ps[i];
                p.Life -= dt;
                if (p.Life <= 0) { ps.RemoveAt(i); continue; }
                p.X += p.Vx * dt; p.Z += p.Vz * dt; p.H += p.Vh * dt; p.Rot += p.VRot * dt;
                if (p.Streak || p.Poof) continue;
                p.Vh -= (p.Spray ? 300 : p.Wake ? 160 : 260) * dt;
                if (p.H < 0) { p.H = 0; p.Vh *= -0.3f; }
            }
            backR.sortingOrder = View2D.Order(View2D.SY) + 2;
            Fill(back, false, pz, true);
            Fill(soft, false, pz, false);
            Fill(glow, true, pz, null);
        }

        Rect Uv(string role)
        {
            var s = real ? SpriteTable.Get(role).S : Placeholder2D.Puff;
            var r = s.textureRect;
            return new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
        }

        /// <summary>behind: true = only particles above Pip's row, false = the rest, null = all.</summary>
        void Fill(Mesh m, bool glowLayer, double pz, bool? behind)
        {
            v.Clear(); c.Clear(); uv.Clear(); tri.Clear();
            var flakeUv = Uv("vfx.spray2");
            float sy = View2D.SY;
            if (!glowLayer && behind == false)
                foreach (var f in flakes)
                    Quad(View2D.P(f.X, f.Y), f.R * 2.2f, f.R * 2.2f, 0, new Color(1, 1, 1, f.O).linear, flakeUv);
            // Pass 0: the rims under every wake / spray puff; pass 1: the particles themselves.
            for (int pass = 0; pass < 2; pass++)
            foreach (var p in ps)
            {
                if (p.Glow != glowLayer) continue;
                if (pass == 0 && !(p.Wake || p.Spray)) continue;
                float k = Mathf.Max(0, p.Life / p.Max);
                float gy = View2D.ToY(p.Z, pz);
                bool back = gy < sy - 2 && !(p.Wake && p.Max - p.Life < WakeFrontT);
                if (behind != null && back != behind.Value) continue;
                float y = gy - p.H, x = View2D.ToX(p.X, gy);
                if (gy < View2D.SkyH - 20) continue;
                float sc = View2D.Persp(gy);
                var col = p.Col;
                float w, h, rot = p.Rot;
                if (p.Streak)
                {
                    // Vertical dash rushing up past Pip; the sheet's streak is drawn horizontal.
                    col.a *= Mathf.Sin(k * Mathf.PI);
                    w = p.Len * sc; h = p.Size * sc * 1.6f; rot = 90;
                }
                else if (p.Poof) { w = p.Size * (1.4f - k * 0.6f) * sc * 2; h = w * 0.55f; col.a = k * 0.85f; rot = 0; }
                else if (p.Glow) { w = h = p.Size * 3.2f * sc; col = new Color(col.r, col.g, col.b, k); }
                else if (p.Role.StartsWith("vfx.confetti")) { w = h = p.Size * 2 * sc; col = real ? new Color(1, 1, 1, k) : new Color(col.r, col.g, col.b, k); }
                else if (p.Wake)
                {
                    float age = 1 - k;
                    x += p.Side * WakeSpread * age * sc;
                    w = h = p.Size * Mathf.Lerp(1, WakeGrow, age) * WakeDraw * sc;
                    col.a = WakeAlpha * Mathf.Clamp01(k / WakeFadeLast);
                }
                else if (p.Spray) { w = h = p.Size * (1.9f - k) * 1.1f * SprayDraw * sc; col.a = SprayAlpha * Mathf.Min(1, k * 1.6f); }
                else { w = h = p.Size * (1.6f - k * 0.6f) * 2.4f * sc; col.a = 0.9f * k; }
                if (pass == 0)
                {
                    // Rim: same sprite, a little larger, darker blue, under the puff.
                    var rim = RimTint;
                    rim.a = RimAlpha * col.a / (p.Wake ? WakeAlpha : SprayAlpha);
                    Quad(View2D.P(x, y), w * RimScale, h * RimScale, rot, rim.linear, Uv(p.Role));
                    continue;
                }
                Quad(View2D.P(x, y), w, h, rot, col.linear, Uv(p.Role));
            }
            m.Clear();
            m.SetVertices(v);
            m.SetColors(c);
            m.SetUVs(0, uv);
            m.SetTriangles(tri, 0);
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 100000);
        }

        void Quad(Vector3 centre, float w, float h, float rotDeg, Color col, Rect r)
        {
            int b = v.Count;
            var q = Quaternion.Euler(0, 0, rotDeg);
            v.Add(centre + q * new Vector3(-w / 2, -h / 2)); v.Add(centre + q * new Vector3(w / 2, -h / 2));
            v.Add(centre + q * new Vector3(w / 2, h / 2)); v.Add(centre + q * new Vector3(-w / 2, h / 2));
            uv.Add(new Vector2(r.xMin, r.yMin)); uv.Add(new Vector2(r.xMax, r.yMin)); uv.Add(new Vector2(r.xMax, r.yMax)); uv.Add(new Vector2(r.xMin, r.yMax));
            c.Add(col); c.Add(col); c.Add(col); c.Add(col);
            tri.Add(b); tri.Add(b + 1); tri.Add(b + 2); tri.Add(b); tri.Add(b + 2); tri.Add(b + 3);
        }
    }
}
