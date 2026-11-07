// One run down the slope: steering, speed, hold-to-boost, jump + mid-air flip, collisions, items,
// the power crystal, answer gates, rival race and scoring. Ported from step() and friends in
// prototype/web/js/game.js (boost is Unity-only, GDD 18.o). Pure C#: presentation hooks go through
// RunListener.
using System;
using System.Collections.Generic;

namespace Pip.Core
{
    public static class Tuning
    {
        public const double JumpV = 258, Grav = 832, Tumble = 1.1;
        public const double Power = 6, PowerSpeed = 1.15; // power crystal: seconds of glow, speed multiplier
        // Hold to boost: up to ×(1 + BoostSpeed), easing in over BoostRise s and out over BoostFall s;
        // a press must be held BoostHold s (still) before it counts. Boost × crystal never passes MaxSpeedMul.
        public const double BoostSpeed = 0.35, BoostRise = 0.45, BoostFall = 0.35, BoostHold = 0.25;
        public const double MaxSpeedMul = 1.5;
        public const double SteerMin = -160, SteerMax = 160;
        public const double CoinStreak = 0.7;
    }

    /// <summary>Presentation hooks; every method is optional.</summary>
    public class RunListener
    {
        public virtual void Toast(string text, string color) { }
        public virtual void Sfx(string id) { }
        public virtual void Music(string id) { }
        public virtual void Duck(bool on) { }
        /// <summary>Cosmetic particles at a course position (kind: puff / spark / confetti).</summary>
        public virtual void Burst(double x, double z, double h, int n, string kind, params string[] colors) { }
        public virtual void ShowQuestion(GateRow row) { }
        public virtual void HideQuestion() { }
        public virtual void CoinCollected(CourseObj coin) { }
        public virtual void Smashed(CourseObj o) { }
        public virtual void RowResolved(GateRow row, bool right) { }
        public virtual void Hit(CourseObj o) { }
        public virtual void Finished() { }
        public virtual void Failed() { }
    }

    public sealed class Flip { public double T0, Dur; }

    public sealed class Rival { public double Z = 60, X, Lean, Speed; public bool Passed; }

    /// <summary>One ski-track sample: centre between the skis, and whether Pip was off the snow.</summary>
    public readonly struct TrailPt
    {
        public readonly double X, Z;
        public readonly bool Air;
        public TrailPt(double x, double z, bool air) { X = x; Z = z; Air = air; }
    }

    public sealed class RunResult
    {
        public bool Win;
        public int Stars, Total, TimeBonus, Distance;
        public bool AllRight, Perfect, Star3;
    }

    public sealed class RunSim
    {
        public readonly LevelDef L;
        public readonly Course Course;
        public readonly RunListener Out;

        public double Mul, Par;
        public double Clock;
        // Skier
        public double Z, X, Tx, Vx, V, Lean, Sway;
        public bool Air; public double AirT, AirH, AirV, FlipA; public Flip Flip;
        public bool KeyLeft, KeyRight;
        // Boost: the player's intent (finger held still / Shift), and the 0..1 ramp the speed follows.
        public bool Boost; public double BoostT;
        bool boostToasted;
        // Status
        public int Hearts = 3, Coins, Correct, Answered, Tumbles, Combo;
        public readonly int Graded;
        public double Time, TumbleT, Invuln, Slow, Magnet, Slowmo, Icy, Power;
        public bool Shield, Started, Finished, Failed, RivalFirst;
        public double FinishT, FailT, Shake;
        // Expression timers for the character view
        public double HappyT, SadT, ScaredT, PowerT, SquashT, LookT; public int LookX;
        public double ReadTime;
        public GateRow Row;
        public ObjType? LastHit;
        public readonly Rival Rival;
        public readonly List<TrailPt> Trail = new List<TrailPt>();
        double trailT;
        int oi;
        double lastNear = -9, lastHop = -9, lastSmash = -9;

        public RunSim(LevelDef level, Course course, double speedMul = 1, RunListener listener = null)
        {
            L = level;
            Course = course;
            Out = listener ?? new RunListener();
            Mul = speedMul;
            double avg = (L.V0 + L.V1) / 2 * Mul;
            Par = L.Endless ? 0 : Rand.JsRound(course.Len / avg * 1.04 + 1.5);
            V = L.V0 * Mul;
            Graded = course.Rows.Count;
            ReadTime = L.Id >= 5 || L.Endless ? 3.1 : 3.5;
            if (L.Rival) Rival = new Rival { Speed = course.Len / (Par * 0.96) };
        }

        public bool Tumbling => TumbleT > 0;

        /// <summary>Boost as felt (0..1): the ramp through smoothstep. Speed, tuck, streaks, camera and HUD read this.</summary>
        public double BoostLevel => BoostT * BoostT * (3 - 2 * BoostT);

        public void Start()
        {
            if (Started) return;
            Started = true;
            Out.Music(Levels.SongFor(L));
            Out.Toast("ไปเลย!", "#FFE066");
        }

        /// <summary>Advance the world clock; simulates only once the run has started.</summary>
        public void Advance(double dt)
        {
            Clock += dt;
            if (Started) Step(dt);
        }

        public void Steer(double tx) => Tx = Rand.Clamp(tx, Tuning.SteerMin, Tuning.SteerMax);

        public void Jump()
        {
            if (!Started || TumbleT > 0 || Finished) return;
            if (Air)
            {
                // Second tap in mid-air: a somersault that relaunches from the current height, so the
                // skier stays up long enough to clear two obstacles in a row. Once per jump.
                if (Flip != null || AirT < 0.06) return;
                AirV = Tuning.JumpV;
                Flip = new Flip { T0 = AirT, Dur = (AirV + Math.Sqrt(AirV * AirV + 2 * Tuning.Grav * AirH)) / Tuning.Grav };
                Out.Sfx("flip");
                Out.Toast("ตีลังกา!", "#FFE066");
                Out.Burst(X, Z, AirH + 30, 8, "spark", "#FFE066", "#FFFFFF");
                return;
            }
            Air = true;
            AirT = 0;
            AirH = 0;
            AirV = Tuning.JumpV;
            Flip = null;
            Out.Sfx("jump");
            Out.Burst(X, Z, 4, 6, "puff", "#FFFFFF");
        }

        void Land()
        {
            Air = false;
            AirH = 0;
            Flip = null;
            FlipA = 0;
            SquashT = 0.18;
            Out.Sfx("land");
            Out.Burst(X, Z, 4, 8, "puff", "#FFFFFF");
        }

        void HitObj(CourseObj o)
        {
            if (Invuln > 0 || TumbleT > 0 || Finished) return;
            if (o.Type == ObjType.Ball) o.Gone = true;
            if (Shield)
            {
                Shield = false;
                Invuln = 1.0;
                Out.Sfx("shield");
                Out.Burst(X, Z, 4, 14, "puff", "#BFE6FF", "#FFFFFF");
                Out.Toast("ฟองกันชนช่วยไว้!", "#8FD3FF");
                return;
            }
            Hearts -= 1; Tumbles += 1; TumbleT = Tuning.Tumble; Invuln = Tuning.Tumble + 1.3; Combo = 0; Shake = 1;
            Air = false; AirH = 0; Flip = null; FlipA = 0;
            LastHit = o.Type;
            if (o.Type == ObjType.Puff) o.HopT = 0.6;
            Out.Sfx("crash");
            Out.Burst(X, Z, 4, 18, "puff", "#FFFFFF", "#DCE7FF");
            string ouch = o.Type == ObjType.Hole ? "ตกหลุม!" : o.Type == ObjType.Bear ? "จ๊ะเอ๋! โดนหมี" : "โอ๊ะ! ชนแล้ว";
            Out.Toast(Hearts > 0 ? ouch : "หัวใจหมดแล้ว!", "#FF8DB3");
            Out.Hit(o);
        }

        void CollectItem(CourseObj o)
        {
            o.Gone = true;
            PowerT = 0.9;
            switch (o.Kind)
            {
                case "magnet": Magnet = 8; Out.Toast("แม่เหล็ก! ดูดเกล็ดหิมะ", "#FF8DB3"); break;
                case "shield": Shield = true; Out.Toast("ฟองกันชน! กันล้ม 1 ครั้ง", "#8FD3FF"); break;
                case "crystal": Power = Tuning.Power; Out.Toast("ผลึกพลัง! ชนอะไรก็พัง", "#8FF3FF"); break;
                default: Slowmo = 4; Out.Toast("นาฬิกาทราย! โลกช้าลง", "#FFE066"); break;
            }
            Out.Sfx("power");
            Out.Burst(X, Z, 4, 10, "spark", o.Kind == "crystal" ? "#8FF3FF" : "#FFC93C", "#FFFFFF");
        }

        // Power crystal: anything Pip runs into while glowing shatters instead of knocking Pip down.
        void Smash(CourseObj o)
        {
            o.Gone = true;
            Shake = Math.Max(Shake, 0.35);
            Out.Sfx("smash");
            Out.Smashed(o);
            if (Clock - lastSmash > 1.2) { Out.Toast(o.Type == ObjType.Bear ? "หมีกระเด็น!" : "ตูม! พังเลย", "#8FF3FF"); lastSmash = Clock; }
        }

        void RevealRow(GateRow row)
        {
            row.Reveal = true;
            if (Row == row) Out.ShowQuestion(row);
            Out.Sfx("star");
        }

        void ResolveRow(GateRow row)
        {
            // Crashing through a row while powered counts as the right answer and shatters the gates.
            bool powered = Power > 0;
            int idx = powered ? row.Correct : (int)Rand.Clamp(Math.Floor((X + 195) / (390.0 / row.N)), 0, row.N - 1);
            row.Picked = idx;
            row.Done = true;
            Answered += 1;
            Row = null;
            Out.HideQuestion();
            Out.Duck(false);
            if (powered)
            {
                row.Smashed = true;
                Out.Sfx("smash");
                Shake = Math.Max(Shake, 0.5);
            }
            bool right = idx == row.Correct;
            if (right)
            {
                Correct += 1; Combo += 1; HappyT = 0.7;
                Out.Sfx("right");
                Out.Burst(X, Z + 10, 4, 16, "confetti", "#FF5C93", "#FFC93C", "#7DE0A8", "#3FA9F5");
                if (powered) Out.Toast("ทะลุประตู! ถูกต้อง +300", "#8FF3FF");
                else Out.Toast(Combo >= 2 ? "ถูกต้อง! คอมโบ x" + Combo : "ถูกต้อง! +300", "#7DE0A8");
            }
            else
            {
                Combo = 0; Slow = 1.0; SadT = 0.9;
                Out.Sfx("wrong");
                Out.Toast("อุ๊ย! ไม่ใช่ ช้าลงหน่อย", "#FFE066");
            }
            Out.RowResolved(row, right);
        }

        static bool IsHazard(CourseObj o)
        {
            switch (o.Type)
            {
                case ObjType.Puff: case ObjType.Ball: case ObjType.Log: case ObjType.Rock:
                case ObjType.Tree: case ObjType.Snowman: case ObjType.Hole: return true;
                case ObjType.Bear: return o.Up > 0.5;
                default: return false;
            }
        }

        void Step(double dt)
        {
            if (Slowmo > 0) Slowmo -= dt;
            double gdt = dt * (Slowmo > 0 ? 0.6 : 1);
            if (!Finished) Time += gdt;

            if (KeyLeft) Tx -= 420 * gdt;
            if (KeyRight) Tx += 420 * gdt;
            Tx = Rand.Clamp(Tx, Tuning.SteerMin, Tuning.SteerMax);
            double prev = X;
            if (TumbleT <= 0) X += (Tx - X) * Math.Min(1, (Icy > 0 ? 2.5 : 12) * gdt);
            X = Rand.Clamp(X, Tuning.SteerMin, Tuning.SteerMax);
            Vx = (X - prev) / Math.Max(gdt, 1e-4);
            Lean += (Rand.Clamp(Vx / 380, -1, 1) - Lean) * Math.Min(1, 10 * dt);

            // Boost only builds with skis on the snow; airborne, tumbling or past the finish it fades.
            bool boosting = Boost && !Air && TumbleT <= 0 && !Finished;
            BoostT = Rand.Clamp(BoostT + (boosting ? gdt / Tuning.BoostRise : -gdt / Tuning.BoostFall), 0, 1);
            if (BoostT >= 1 && !boostToasted) { boostToasted = true; Out.Toast("เร่ง!", "#FFE066"); }

            double v = L.Endless ? Math.Min(L.V1, L.V0 + Z * 0.0045) : L.V0 + (L.V1 - L.V0) * Rand.Clamp(Z / Course.Len, 0, 1);
            v *= Mul;
            // Crystal and boost stack, but together never past MaxSpeedMul (×1 when neither is on).
            v *= Math.Min((Power > 0 ? Tuning.PowerSpeed : 1) * (1 + Tuning.BoostSpeed * BoostLevel), Tuning.MaxSpeedMul);
            if (TumbleT > 0) v *= 0.3 + 0.7 * (1 - TumbleT / Tuning.Tumble);
            if (Slow > 0) v *= 0.55;
            if (Finished) v *= Math.Max(0, 1 - FinishT * 1.2);
            V = v;
            Z += v * gdt;

            if (Air)
            {
                AirT += gdt;
                AirV -= Tuning.Grav * gdt;
                AirH += AirV * gdt;
                FlipA = Flip != null ? Math.Min(1, (AirT - Flip.T0) / Flip.Dur) * Math.PI * 2 : 0;
                if (AirH <= 0 && AirV < 0) Land();
            }
            TumbleT = Math.Max(0, TumbleT - gdt);
            Invuln -= gdt; Slow -= gdt; Magnet -= gdt; Icy -= gdt;
            if (Power > 0)
            {
                Power -= gdt;
                if (Power <= 0) Out.Toast("พลังหมดแล้ว", "#BFE6FF");
            }
            HappyT -= dt; SadT -= dt; SquashT -= dt; ScaredT -= dt; PowerT -= dt; LookT -= dt;
            Shake = Math.Max(0, Shake - dt * 3);

            // Gentle S-carve while going straight; fades out when the player steers.
            bool onSnow = !Air && TumbleT <= 0 && !Finished;
            Sway = onSnow ? Math.Sin(Clock * 2.6) * (1 - Math.Min(1, Math.Abs(Lean) * 2)) : Sway * 0.9;
            // Ski tracks: where the skis were every 0.03 s, kept for 420 units behind Pip.
            trailT -= dt;
            if (trailT <= 0)
            {
                trailT = 0.03;
                Trail.Add(new TrailPt(X + Sway * 8, Z, Air || TumbleT > 0));
                int old = 0;
                while (old < Trail.Count && Trail[old].Z < Z - 420) old++;
                if (old > 0) Trail.RemoveRange(0, old);
            }

            double airH = Air ? AirH : 0;
            var objs = Course.Objs;
            while (oi < objs.Count && objs[oi].Z < Z - 320) oi++;
            for (int i = oi; i < objs.Count; i++)
            {
                var o = objs[i];
                double dz = o.Z - Z;
                if (dz > 900) break;
                if (o.Gone) continue;
                switch (o.Type)
                {
                    case ObjType.Puff:
                    {
                        double nx = o.X0 + Math.Sin(Clock * o.Freq + o.Ph) * o.Amp;
                        o.Dir = nx > o.X ? 1 : -1;
                        o.X = nx;
                        if (o.HopT > 0) o.HopT -= dt;
                        break;
                    }
                    case ObjType.Bear:
                        // Mound shakes as a warning, then the bear pops up and stays in the way.
                        if (dz < 440) o.Warn = true;
                        if (dz < 300)
                        {
                            if (o.Up == 0) Out.Sfx("pop");
                            o.Up = Math.Min(1, o.Up + dt * 5);
                        }
                        break;
                    case ObjType.Ball:
                        if (!o.Active && dz < 560) o.Active = true;
                        if (o.Active)
                        {
                            o.X += o.Vx * gdt; o.Z += o.Vz * gdt; o.Rot += o.Vx * gdt / o.Rad;
                            if (Math.Abs(o.X) > 240) o.Gone = true;
                        }
                        break;
                    case ObjType.Coin:
                        if (Magnet > 0 && Math.Abs(dz) < 170 && Math.Abs(o.X - X) < 170)
                        {
                            o.X += (X - o.X) * Math.Min(1, 9 * dt);
                            o.Z += (Z + 4 - o.Z) * Math.Min(1, 9 * dt);
                        }
                        break;
                }
                bool hazard = IsHazard(o);
                if (hazard && (o.Type == ObjType.Puff || o.Type == ObjType.Ball || o.Type == ObjType.Bear || o.Type == ObjType.Hole)
                    && o.Z - Z > 70 && o.Z - Z < 220 && Math.Abs(o.X - X) < 70)
                {
                    LookX = Rand.Sign(o.X - X);
                    LookT = 0.15;
                }
                double adz = Math.Abs(o.Z - Z);
                if (adz > 40) continue;
                double dx = Math.Abs(o.X - X);
                if (Power > 0 && hazard && o.Type != ObjType.Hole && adz < 16 && dx < (o.Hw != 0 ? o.Hw : 20) + 14) { Smash(o); continue; }
                if (hazard && !o.Passed && adz < 10 && TumbleT <= 0 && Invuln <= 0)
                {
                    if (o.Jump && airH >= 12 && dx < o.Hw + 10)
                    {
                        o.Passed = true;
                        HappyT = 0.5;
                        if (Clock - lastHop > 2) { Out.Toast("ข้ามสวย!", "#7DE0A8"); lastHop = Clock; }
                    }
                    else if (dx >= o.Hw + 10 && dx < o.Hw + 34 && (o.Type != ObjType.Tree || Math.Abs(o.X) < 170))
                    {
                        o.Passed = true;
                        ScaredT = 0.45;
                        LookX = Rand.Sign(o.X - X);
                        LookT = 0.4;
                        if (Clock - lastNear > 3) { Out.Toast("เฉียดฉิว!", "#FFE066"); lastNear = Clock; }
                    }
                }
                switch (o.Type)
                {
                    case ObjType.Coin:
                        if (adz < 16 && dx < 24) { o.Gone = true; Coins += 1; Out.CoinCollected(o); }
                        break;
                    case ObjType.Item: if (adz < 18 && dx < 28) CollectItem(o); break;
                    case ObjType.Ice:
                        if (adz < o.H / 2 && dx < o.W / 2) { if (Icy <= 0) Out.Toast("ลื่น!", "#BFE6FF"); Icy = 0.9; }
                        break;
                    case ObjType.Tree: case ObjType.Snowman: if (adz < 10 && dx < o.Hw + 10) HitObj(o); break;
                    case ObjType.Rock: case ObjType.Log: if (adz < 10 && dx < o.Hw + 10 && airH < 12) HitObj(o); break;
                    case ObjType.Puff: if (adz < 12 && dx < o.Hw + 10 && airH < 14 && !(o.HopT > 0)) HitObj(o); break;
                    case ObjType.Ball: if (o.Active && adz < 16 && dx < o.Hw + 8) HitObj(o); break;
                    case ObjType.Bear: if (o.Up > 0.5 && adz < 12 && dx < o.Hw + 10) HitObj(o); break;
                    case ObjType.Hole: if (adz < o.H / 2 - 2 && dx < o.Hw - 4 && airH < 8 && Power <= 0) HitObj(o); break; // glowing Pip skims over
                }
            }

            foreach (var row in Course.Rows)
            {
                if (row.Done) continue;
                double dz = row.Z - Z;
                if (dz > V * ReadTime + 40) break;
                if (!row.Shown) { row.Shown = true; Row = row; Out.ShowQuestion(row); Out.Duck(true); Out.Sfx("tick"); }
                if (Power > 0 && !row.Reveal) RevealRow(row);
                if (dz <= 0) ResolveRow(row);
            }

            if (Rival != null)
            {
                var r = Rival;
                if (!Finished) r.Z += r.Speed * gdt;
                double tx = Course.SafeX(r.Z + 60);
                double nx = r.X + (tx - r.X) * Math.Min(1, 3 * dt);
                r.Lean = Rand.Clamp((nx - r.X) / Math.Max(dt, 1e-4) / 300, -1, 1);
                r.X = nx;
                if (!r.Passed && Z > r.Z + 20) { r.Passed = true; Out.Toast("แซงเบนนี่แล้ว!", "#FFE066"); }
                else if (r.Passed && r.Z > Z + 40) { r.Passed = false; Out.Toast("เบนนี่แซงกลับ!", "#FF8DB3"); }
                if (r.Z >= Course.Len && !Finished) RivalFirst = true;
            }

            if (!L.Endless && !Finished && Z >= Course.Len)
            {
                Finished = true;
                FinishT = 0;
                Out.Music("clear");
                Out.Toast("เส้นชัย!", "#FFE066");
                Out.Burst(X, Z, 4, 30, "confetti", "#FF5C93", "#FFC93C", "#7DE0A8", "#3FA9F5");
            }
            if (Finished) { FinishT += dt; if (FinishT > 1.6 && FinishT - dt <= 1.6) Out.Finished(); }
            if (Hearts <= 0 && TumbleT <= 0 && !Failed) { Failed = true; FailT = 0; Out.Music("fail"); }
            if (Failed) { FailT += dt; if (FailT > 0.7 && FailT - dt <= 0.7) Out.Failed(); }
        }

        /// <summary>Score and stars, same formula as showResult() in the web prototype.</summary>
        public RunResult Result(bool win)
        {
            var res = new RunResult { Win = win };
            if (L.Endless)
            {
                res.Distance = (int)Math.Floor(Z / 10);
                res.Total = res.Distance + Coins * 10 + Correct * 300;
                return res;
            }
            res.TimeBonus = (int)Math.Max(0, Rand.JsRound(Par - Time));
            res.AllRight = Correct == Graded;
            res.Perfect = win && res.AllRight && Tumbles == 0;
            res.Star3 = L.Rival ? !RivalFirst : Time <= Par;
            res.Stars = win ? 1 + (res.AllRight ? 1 : 0) + (res.Star3 ? 1 : 0) : 0;
            res.Total = win ? Math.Max(0, 1000 + Correct * 300 + Coins * 10 + res.TimeBonus * 20 + (res.Perfect ? 500 : 0) - Tumbles * 100) : 0;
            return res;
        }

        /// <summary>Running total for endless mode (distance in metres).</summary>
        public int Distance => (int)Math.Floor(Z / 10);

        public IReadOnlyList<CourseObj> Objects => Course.Objs;
    }
}
