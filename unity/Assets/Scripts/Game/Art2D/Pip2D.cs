// Pip on the 2D slope: one pose sprite picked from the run (PoseFor, a pure function of the sim) at
// x = W/2 + X + 8·Sway, standing on SY; lean tilts it ±12°, air lifts it by AirH on screen and grows it
// by AirH/400, a flip turns the backflip pose by FlipA, a tumble rolls the snowball; a spring squashes
// it on landing. Under it a ground shadow that shrinks in the air; around it the shield bubble, the
// magnet's dashed ring, the power-crystal glow and dizzy stars. With no run (title) Pip idles on the
// slope with the s02 wave pose, hopping every 6 s like the web title. Ski tracks: Trail2D.
// While skiing on the snow a speed-driven ski-pump cycle (Cycle, a pure function) bobs, squashes and
// sways Pip so a straight run reads as motion; on the low half of each pump the straight pose swaps to
// "pip.straightB" (knees bent). Benny2D runs the same cycle at his own speed (no second frame). Animation
// runs on View2D's clock, which stops while the game is paused.
using System.Collections.Generic;
using Pip.Core;
using Pip.Game.Art;
using UnityEngine;

namespace Pip.Game.Art2D
{
    public enum Pose { Straight, CarveL, CarveR, Tuck, Jump, Flip, Tumble, Cheer, Scared, Dizzy, Proud, Wave }

    public sealed class Pip2D : MonoBehaviour
    {
        public const float LeanDeg = 12, CarveAt = 0.35f;

        // Ski-pump cycle (tune here). Frequency in rad/s, lengths in design units, angles in degrees.
        public const float PumpFreq = 7, PumpFreqSpd = 7;          // ω = PumpFreq + PumpFreqSpd·spd01
        public const float PumpBob = 3.5f, PumpBobSpd = 3.5f;      // bob amplitude (up) = PumpBob + PumpBobSpd·spd01
        public const float PumpSquashY = 0.045f, PumpStretchX = 0.03f; // scaleY = 1 + a·sin φ, scaleX = 1 − b·sin φ (tall up, squat low)
        public const float PumpSwayDeg = 4, PumpWiggle = 1.5f;     // at half the pump frequency
        public const float PumpEaseIn = 0.3f;                      // s to full cycle after the start / a landing

        /// <summary>One frame of the pump cycle: Bob up (units), ScaleX / ScaleY, Sway (deg), Wiggle (units, x).</summary>
        public struct Pump
        {
            public float Bob, ScaleX, ScaleY, Sway, Wiggle;
            public static readonly Pump None = new Pump { ScaleX = 1, ScaleY = 1 };

            public Pump Scaled(float w) => new Pump
            {
                Bob = Bob * w, ScaleX = 1 + (ScaleX - 1) * w, ScaleY = 1 + (ScaleY - 1) * w, Sway = Sway * w, Wiggle = Wiggle * w,
            };
        }

        /// <summary>The pump cycle at speed spd01 (0..1) and phase (radians); flat when not on the snow.</summary>
        public static Pump Cycle(float spd01, float phase, bool onSnow)
        {
            if (!onSnow) return Pump.None;
            spd01 = Mathf.Clamp01(spd01);
            float s = Mathf.Sin(phase), h = Mathf.Sin(phase * 0.5f);
            return new Pump
            {
                Bob = (PumpBob + PumpBobSpd * spd01) * s,
                ScaleX = 1 - PumpStretchX * s,
                ScaleY = 1 + PumpSquashY * s,
                Sway = PumpSwayDeg * h,
                Wiggle = PumpWiggle * h,
            };
        }

        /// <summary>Pump angular speed (rad/s) at spd01.</summary>
        public static float PumpOmega(float spd01) => PumpFreq + PumpFreqSpd * Mathf.Clamp01(spd01);

        /// <summary>Speed between 0.6·V0 and V1 (× the run's speed setting), unclamped — the ski sound's
        /// measure; Spd01 clamps it.</summary>
        public static float SkiSpeed(RunSim g)
        {
            double lo = g.L.V0 * g.Mul * 0.6;
            return (float)((g.V - lo) / System.Math.Max(1, g.L.V1 * g.Mul - lo));
        }

        public static float Spd01(RunSim g) => g == null ? 0 : Mathf.Clamp01(SkiSpeed(g));

        /// <summary>Second straight frame (knees bent) for the low half of the pump, if the sheet carries it.</summary>
        public const string StraightB = "pip.straightB";
        static int hasStraightB = -1;
        static bool HasStraightB => (hasStraightB < 0 ? hasStraightB = SpriteTable.Has(StraightB) ? 1 : 0 : hasStraightB) == 1;

        public RunSim Sim;

        SpriteRenderer body, shadow, shield, magnet, glow;
        readonly SpriteRenderer[] stars = new SpriteRenderer[3];
        float sq, sqV;
        bool wasAir;
        float pumpPhase, pumpW;
        Vector3 head;

        /// <summary>World point beside Pip's head, for the HUD's coin streak popup.</summary>
        public Vector3 HeadWorld => head;

        /// <summary>The sprite pose for a run state (null = the title's idle wave). Order matters: the
        /// strongest state wins (tumble, finish, flip, air, a scare or a wrong answer, boost tuck, carve,
        /// a right answer).</summary>
        public static Pose PoseFor(RunSim g)
        {
            if (g == null) return Pose.Wave;
            if (g.TumbleT > 0) return Pose.Tumble;
            if (g.Failed) return Pose.Dizzy;
            if (g.Finished) return Pose.Cheer;
            if (g.Air) return g.Flip != null ? Pose.Flip : Pose.Jump;
            if (g.ScaredT > 0) return Pose.Scared;
            if (g.SadT > 0) return Pose.Dizzy;
            if (g.BoostLevel > 0.5) return Pose.Tuck;
            if (g.Lean > CarveAt) return Pose.CarveR;
            if (g.Lean < -CarveAt) return Pose.CarveL;
            if (g.HappyT > 0) return Pose.Proud;
            return Pose.Straight;
        }

        public static string RoleOf(Pose p) => "pip." + (p == Pose.CarveL ? "carveL" : p == Pose.CarveR ? "carveR" : p.ToString().ToLowerInvariant());

        public void Init()
        {
            var t = transform;
            shadow = Kit2D.SR(t, "shadow", View2D.Shadow);
            glow = Kit2D.SR(t, "crystal glow");
            body = Kit2D.SR(t, "pip");
            shield = Kit2D.SR(t, "shield");
            magnet = Kit2D.SR(t, "magnet");
            for (int i = 0; i < 3; i++) stars[i] = Kit2D.SR(t, "dizzy star");
            var trail = new GameObject("ski tracks").AddComponent<Trail2D>();
            trail.transform.SetParent(t, false);
            trail.Pip = this;
        }

        void LateUpdate()
        {
            if (body == null) return;
            float t = View2D.T;
            float H = View2D.H, SY = View2D.SY;
            var g = Sim;
            if (g == null) { Idle(t, H, SY); return; }

            bool tumbling = g.TumbleT > 0;
            bool skiing = g.Started && !g.Finished && !tumbling;
            float airH = g.Air ? (float)System.Math.Max(0, g.AirH) : 0;
            float x = View2D.W / 2 + (float)(g.X + g.Sway * 8);
            float fy = g.Finished && !g.Failed ? Mathf.Abs(Mathf.Sin(t * 7)) * 18 : 0;

            // Ski-pump cycle: only with the skis on the snow mid-run; eases in after the start and each landing.
            float dt = View2D.Dt;
            bool onSnow = skiing && !g.Air && !g.Failed;
            float spd01 = Spd01(g);
            pumpW = onSnow ? Mathf.Min(1, pumpW + dt / PumpEaseIn) : 0;
            if (onSnow) pumpPhase = (pumpPhase + PumpOmega(spd01) * dt) % (Mathf.PI * 4);
            var pump = Cycle(spd01, pumpPhase, onSnow).Scaled(pumpW);
            x += pump.Wiggle;

            // Landing spring (plus the sim's own landing squash), web drawPlayer's squash in spirit.
            if (wasAir && !g.Air && !tumbling) sqV += g.Flip != null || g.AirT > 0.5 ? 5.5f : 4f;
            wasAir = g.Air;
            sqV += (-sq * 320 - sqV * 15) * dt;
            sq += sqV * dt;
            float squash = Mathf.Clamp(sq + (g.SquashT > 0 ? (float)g.SquashT / 0.18f * 0.12f : 0), -0.25f, 0.3f);

            var pose = PoseFor(g);
            string role = RoleOf(pose);
            // Low half of the bob (sin φ < 0): the knees-bent frame.
            if (pose == Pose.Straight && onSnow && Mathf.Sin(pumpPhase) < 0 && HasStraightB) role = StraightB;
            var s = SpriteTable.Get(role);
            float grow = 1 + airH / 400;
            float sx = (1 + squash * 0.7f) * grow * pump.ScaleX, sy = (1 - squash) * grow * pump.ScaleY;
            var size = s.Size;
            float rot = 0;
            Vector2 anchor = Kit2D.Bottom;
            float footY = SY - airH - fy - pump.Bob;
            bool flip = false;
            if (pose == Pose.Flip)
            {
                // The sheet's backflip is upside down: FlipA = π shows it as drawn.
                rot = ((float)g.FlipA - Mathf.PI) * Mathf.Rad2Deg;
                anchor = Kit2D.Centre;
                footY -= size.y * 0.5f;
            }
            else if (pose == Pose.Tumble)
            {
                rot = -(1 - (float)(g.TumbleT / Tuning.Tumble)) * 360;
                anchor = Kit2D.Centre;
                footY -= size.y * 0.5f;
            }
            else if (pose != Pose.Jump && pose != Pose.Cheer) rot = -Mathf.Clamp((float)(g.Lean + g.Sway * 0.35), -1, 1) * LeanDeg + pump.Sway;
            if (pose == Pose.Tuck) flip = g.Lean < -0.05;
            Kit2D.Put(body, s, x, footY, size.x * sx, size.y * sy, anchor, rot, flip);
            body.sortingOrder = View2D.Order(SY) + 3;
            Kit2D.Alpha(body, g.Invuln > 0 && !tumbling && Mathf.FloorToInt(t * 12) % 2 == 0 ? 0.45f : 1);
            head = View2D.P(x - pump.Wiggle, SY - airH - fy - 78);

            float k = Mathf.Max(0.3f, 1 - airH / 120);
            Kit2D.Put(shadow, new Slice { S = Placeholder2D.Shadow, Scale = 1 }, x, SY + 1, 64 * k, 20 * k, Kit2D.Centre);
            shadow.sortingOrder = View2D.Shadow;

            float cy = SY - 34 - airH - fy;
            shield.gameObject.SetActive(g.Shield);
            if (g.Shield)
            {
                Kit2D.Put(shield, new Slice { S = Placeholder2D.Ring, Scale = 1 }, x, SY - 40 - airH, 104, 104, Kit2D.Centre);
                shield.sortingOrder = body.sortingOrder + 2;
                Kit2D.Alpha(shield, 0.75f + Mathf.Sin(t * 6) * 0.2f);
            }
            magnet.gameObject.SetActive(g.Magnet > 0);
            if (g.Magnet > 0)
            {
                Kit2D.Put(magnet, new Slice { S = Placeholder2D.DashRing, Scale = 1 }, x, SY - 25, 124, 124, Kit2D.Centre, t * 40);
                magnet.sortingOrder = body.sortingOrder + 1;
            }
            float pw = g.Power > 0 ? Mathf.Min(1, (float)g.Power / 0.4f) * (g.Power < 1.5 ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 14)) : 1) : 0;
            glow.gameObject.SetActive(pw > 0.01f);
            if (pw > 0.01f)
            {
                float gs = 150 + Mathf.Sin(t * 9) * 12;
                Kit2D.Put(glow, new Slice { S = Placeholder2D.Halo, Scale = 1 }, x, cy, gs, gs, Kit2D.Centre);
                glow.sortingOrder = body.sortingOrder - 1;
                Kit2D.Alpha(glow, 0.8f * pw, Tex.C("#5FE3FF"));
            }
            for (int i = 0; i < 3; i++)
            {
                stars[i].gameObject.SetActive(tumbling);
                if (!tumbling) continue;
                float a = t * 5 + i * 2.1f;
                Kit2D.Put(stars[i], new Slice { S = Placeholder2D.Star, Scale = 1 }, x + Mathf.Cos(a) * 26, SY - 96 + Mathf.Sin(a) * 7, 14, 14, Kit2D.Centre);
                stars[i].color = Tex.C("#FFC93C");
                stars[i].sortingOrder = body.sortingOrder + 4;
            }
        }

        // Title: idle wave lower than in play (clear of the logo sign and the tagline; 36 lower since
        // 7 Nov 2026, when the top read crowded), a hop every 6 s.
        void Idle(float t, float H, float SY)
        {
            float x = View2D.W / 2, y = SY + 84;
            float ph = t % 6;
            float hop = ph > 4.4f && ph < 4.8f ? Mathf.Sin((ph - 4.4f) / 0.4f * Mathf.PI) * 16 : 0;
            float bob = Mathf.Sin(t * 3) * 0.025f;
            var s = SpriteTable.Get("pip.wave");
            var size = s.Size * 1.25f;
            Kit2D.Put(body, s, x, y - hop, size.x * (1 - bob * 0.6f), size.y * (1 + bob), Kit2D.Bottom, Mathf.Sin(t * 1.3f) * 3);
            body.sortingOrder = View2D.Order(y) + 3;
            Kit2D.Alpha(body, 1);
            head = View2D.P(x, y - 100);
            Kit2D.Put(shadow, new Slice { S = Placeholder2D.Shadow, Scale = 1 }, x, y + 2, 76 - hop * 1.2f, 22, Kit2D.Centre);
            shield.gameObject.SetActive(false);
            magnet.gameObject.SetActive(false);
            glow.gameObject.SetActive(false);
            foreach (var st in stars) st.gameObject.SetActive(false);
        }
    }

    /// <summary>Ski tracks: two lines 24 apart along sim.Trail through the projection, broken where Pip
    /// was in the air and above the crest band (web render.js: rgba(160,185,225,0.55), 3 px).</summary>
    public sealed class Trail2D : MonoBehaviour
    {
        const int Max = 240;
        public Pip2D Pip;
        readonly List<LineRenderer> lines = new List<LineRenderer>();
        readonly List<Vector3> pts = new List<Vector3>();
        static readonly Color Col = new Color(160 / 255f, 185 / 255f, 225 / 255f, 0.55f);

        /// <summary>Points drawn this frame across all segments (PlayMode test).</summary>
        public int Points { get; private set; }

        LineRenderer Line(int i)
        {
            while (lines.Count <= i)
            {
                var go = new GameObject("track");
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = Kit2D.SpriteMat;
                lr.useWorldSpace = false;
                lr.widthMultiplier = 3;
                lr.numCapVertices = 2;
                lr.numCornerVertices = 1;
                lr.startColor = lr.endColor = Col;
                lr.sortingOrder = View2D.Trail;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lines.Add(lr);
            }
            return lines[i];
        }

        void LateUpdate()
        {
            var g = Pip ? Pip.Sim : null;
            var trail = g?.Trail;
            int used = 0;
            Points = 0;
            if (trail != null && trail.Count > 1)
            {
                int first = Mathf.Max(0, trail.Count - Max);
                for (int side = 0; side < 2; side++)
                {
                    float off = side == 0 ? -12 : 12;
                    pts.Clear();
                    for (int i = first; i <= trail.Count; i++)
                    {
                        bool brk = i == trail.Count;
                        if (!brk)
                        {
                            var p = trail[i];
                            float y = View2D.ToY(p.Z, g.Z);
                            if (p.Air || y < View2D.SkyH + 30) brk = true;
                            else pts.Add(View2D.P(View2D.ToX(p.X, y) + off, y));
                        }
                        if (brk && pts.Count > 0)
                        {
                            if (pts.Count > 1)
                            {
                                var lr = Line(used++);
                                lr.positionCount = pts.Count;
                                lr.SetPositions(pts.ToArray());
                                lr.enabled = true;
                                Points += pts.Count;
                            }
                            pts.Clear();
                        }
                    }
                }
            }
            for (int i = used; i < lines.Count; i++) lines[i].enabled = false;
        }
    }

    /// <summary>Benny in the rival race: s03 pose by his lean (carve mirrored for the other side), cheering
    /// if he beat Pip home; projected and sorted like any object on the slope.</summary>
    public sealed class Benny2D : MonoBehaviour
    {
        public RunSim Sim;
        SpriteRenderer body, shadow;
        // Starts out of step with Pip and runs 7 % slower, so the two never pump in sync.
        float pumpPhase = Mathf.PI * 1.3f, pumpW;

        public void Init()
        {
            shadow = Kit2D.SR(transform, "shadow", View2D.Shadow);
            body = Kit2D.SR(transform, "benny");
        }

        void LateUpdate()
        {
            if (body == null) return;
            var r = Sim?.Rival;
            float y = r != null ? View2D.ToY(r.Z, Sim.Z) : 0;
            bool show = r != null && y > View2D.SkyH - 60 && y < View2D.H + 140;
            body.gameObject.SetActive(show);
            shadow.gameObject.SetActive(show);
            if (!show) return;
            float dt = View2D.Dt;
            string role = Sim.Finished && Sim.RivalFirst ? "benny.cheer" : r.Lean > Pip2D.CarveAt ? "benny.carveR" : r.Lean < -Pip2D.CarveAt ? "benny.carveL" : "benny.straight";
            var s = SpriteTable.Get(role);
            float k = View2D.Persp(y) / View2D.Persp(View2D.SY) * 0.92f;
            float x = View2D.ToX(r.X, y);
            float a = View2D.FadeTop(y);
            // His own pump cycle, at his speed measured like Pip's (0.6·V0 … V1 of the run).
            bool onSnow = Sim.Started && !Sim.Finished;
            double lo = Sim.L.V0 * Sim.Mul * 0.6;
            float spd01 = Mathf.Clamp01((float)((r.Speed - lo) / System.Math.Max(1, Sim.L.V1 * Sim.Mul - lo)));
            pumpW = onSnow ? Mathf.Min(1, pumpW + dt / Pip2D.PumpEaseIn) : 0;
            if (onSnow) pumpPhase = (pumpPhase + Pip2D.PumpOmega(spd01) * 0.93f * dt) % (Mathf.PI * 4);
            var pump = Pip2D.Cycle(spd01, pumpPhase, onSnow).Scaled(pumpW);
            var size = s.Size * k;
            Kit2D.Put(body, s, x + pump.Wiggle * k, y - pump.Bob * k, size.x * pump.ScaleX, size.y * pump.ScaleY, Kit2D.Bottom,
                -(float)r.Lean * Pip2D.LeanDeg + pump.Sway);
            body.sortingOrder = View2D.Order(y) + 3;
            Kit2D.Alpha(body, a);
            Kit2D.Put(shadow, new Slice { S = Placeholder2D.Shadow, Scale = 1 }, x, y + 1, 56 * k, 16 * k, Kit2D.Centre);
            Kit2D.Alpha(shadow, a);
        }
    }
}
