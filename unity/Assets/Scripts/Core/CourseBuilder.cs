// Procedural course builder, a line-for-line port of build() / buildEndless() in
// prototype/web/js/levels.js. Every rng draw happens in the same order as the web version, so a
// seed produces the same slope in both (EditMode tests check this against exported fixtures).
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pip.Core
{
    public static class CourseBuilder
    {
        static CourseObj Make(ObjType type, double x, double z, IRng r)
        {
            switch (type)
            {
                case ObjType.Tree: return new CourseObj { Type = type, X = x, Z = z, S = 0.95 + r.Next() * 0.45, Hw = 16, Jump = false };
                case ObjType.Rock: return new CourseObj { Type = type, X = x, Z = z, S = 0.6 + r.Next() * 0.25, Hw = 16, Jump = true };
                case ObjType.Snowman: return new CourseObj { Type = type, X = x, Z = z, S = 1, Hw = 16, Jump = false };
                case ObjType.Log: return new CourseObj { Type = type, X = x, Z = z, S = 1, Hw = 46, Jump = true };
                case ObjType.Hole: return new CourseObj { Type = type, X = x, Z = z, Hw = 34 + r.Next() * 18, H = 30, Jump = true };
                case ObjType.Bear: return new CourseObj { Type = type, X = x, Z = z, Hw = 22, Jump = false, Up = 0, Warn = false, Ph = r.Next() * 6 };
                default: return new CourseObj { Type = type, X = x, Z = z };
            }
        }

        static CourseObj Coin(double x, double z, double ph) => new CourseObj { Type = ObjType.Coin, X = x, Z = z, Ph = ph };

        // Holes and bears block the safe line, so clear scenery beside them to leave a way around.
        static void ClearAroundBig(List<CourseObj> objs)
        {
            var big = objs.Where(o => o.Type == ObjType.Hole || o.Type == ObjType.Bear).ToList();
            for (int i = objs.Count - 1; i >= 0; i--)
            {
                var o = objs[i];
                if (o.Type != ObjType.Tree && o.Type != ObjType.Rock && o.Type != ObjType.Snowman) continue;
                if (Math.Abs(o.X) > 175) continue;
                if (big.Any(b => Math.Abs(o.Z - b.Z) < 80 && Math.Abs(o.X - b.X) < (b.Hw != 0 ? b.Hw : 22) + 80)) objs.RemoveAt(i);
            }
        }

        static GateRow Row(double z, Question q, int n)
        {
            Questions.PaintGates(q);
            return new GateRow { Z = z, Q = q, N = n, Correct = q.CorrectIndex };
        }

        /// <summary>Builds a World 1 level. The slope is seeded by the level; questions come from
        /// <paramref name="qr"/> so they are re-rolled every run (players solve, not memorise).</summary>
        public static Course Build(LevelDef L, IRng qr)
        {
            var r = new JsRng(L.Seed);
            double len = L.Len;
            var objs = new List<CourseObj>();
            var decor = new List<Decor>();
            var rows = new List<GateRow>();
            var forks = new List<Fork>();
            double ph1 = r.Next() * 6;
            double ph2 = r.Next() * 6;
            double amp = 55 + L.Id * 9;
            Func<double, double> safeX = z => Rand.Clamp(Math.Sin(z / 260 + ph1) * amp + Math.Sin(z / 110 + ph2) * amp * 0.35, -120, 120);

            var nextOp = Questions.OpDealer(qr);
            int nq = L.Q.Length;
            for (int i = 0; i < nq; i++)
            {
                double z = Rand.JsRound(1500 + (len - 2050) * (nq == 1 ? 0.5 : (double)i / (nq - 1)));
                string kind = L.Q[i] == "mixed" ? Rand.Pick(qr, Questions.MixedKinds) : L.Q[i];
                int n = kind == "odd" ? Math.Max(3, L.Opts) : L.Opts;
                rows.Add(Row(z, Questions.Make(kind, qr, n, kind == "math1" ? nextOp() : null), n));
            }
            bool NearRow(double z) => rows.Any(row => z > row.Z - 280 && z < row.Z + 150);
            for (int i = 0; i < L.Forks; i++)
            {
                double z0 = Rand.JsRound(950 + (len - 1700) * ((i + 0.5) / L.Forks));
                while (NearRow(z0) || NearRow(z0 + 560)) z0 += 90;
                if (z0 + 560 < len - 200) forks.Add(new Fork { Z0 = z0, Z1 = z0 + 560 });
            }
            bool InFork(double z) => forks.Any(f => z > f.Z0 - 90 && z < f.Z1 + 90);
            bool Busy(double z) => NearRow(z) || InFork(z) || z < 380 || z > len - 220;

            for (double z = -260; z < len + 700; z += 44 + r.Next() * 30)
            {
                objs.Add(Make(ObjType.Tree, -188 - r.Next() * 22, z, r));
                objs.Add(Make(ObjType.Tree, 188 + r.Next() * 22, z + r.Next() * 30, r));
            }
            for (double z = 420; z < len - 250; z += 70)
            {
                if (Busy(z) || r.Next() > L.Density + 0.35) continue;
                double sx = safeX(z);
                int tries = 1 + (r.Next() < L.Density ? 1 : 0);
                for (int k = 0; k < tries; k++)
                {
                    double x = -160 + r.Next() * 320;
                    if (Math.Abs(x - sx) < L.Corridor * 0.5 + 22) continue;
                    double roll = r.Next();
                    var type = roll < L.Rocks * 0.5 ? ObjType.Rock : roll > 0.93 ? ObjType.Snowman : ObjType.Tree;
                    objs.Add(Make(type, x, z + r.Next() * 40, r));
                }
            }
            var taken = new List<double>();
            bool Free(double z) => !Busy(z) && !taken.Any(t => Math.Abs(t - z) < 130);
            List<double> Spots(int count, double salt)
            {
                var outZ = new List<double>();
                for (int i = 0; i < count; i++)
                {
                    double z = Rand.JsRound(520 + (len - 900) * ((i + 0.3 + r.Next() * 0.4) / count) + salt);
                    int guard = 0;
                    while (!Free(z) && guard++ < 20) z += 60;
                    if (Free(z)) { outZ.Add(z); taken.Add(z); }
                }
                return outZ;
            }
            foreach (double z in Spots(L.Logs, 0)) objs.Add(Make(ObjType.Log, safeX(z), z, r));
            foreach (double z in Spots(L.Puffs, 35))
                objs.Add(new CourseObj { Type = ObjType.Puff, X0 = safeX(z), X = safeX(z), Z = z, Amp = 70 + r.Next() * 40, Freq = 1.3 + r.Next() * 0.8, Ph = r.Next() * 6, Hw = 18, Jump = true });
            foreach (double z in Spots(L.Balls, 70))
            {
                int side = r.Next() < 0.5 ? -1 : 1;
                objs.Add(new CourseObj { Type = ObjType.Ball, X = side * 205, Z = z, Vx = -side * (100 + r.Next() * 50), Vz = 70, Rad = 20, Hw = 26, Jump = false });
            }
            foreach (double z in Spots(L.Ice, 105)) objs.Add(new CourseObj { Type = ObjType.Ice, X = safeX(z) + (r.Next() - 0.5) * 30, Z = z, W = 120, H = 46 });
            foreach (double z in Spots(L.Holes, 140)) objs.Add(Make(ObjType.Hole, safeX(z) + (r.Next() - 0.5) * 30, z, r));
            foreach (double z in Spots(L.Bears, 175)) objs.Add(Make(ObjType.Bear, safeX(z) + (r.Next() - 0.5) * 24, z, r));
            // Combo: a log then an ice hole close behind it, too long for one hop: flip over both, or go round.
            foreach (double z in Spots(L.Combos, 210))
            {
                if (Busy(z + 150)) continue;
                taken.Add(z + 120);
                objs.Add(Make(ObjType.Log, safeX(z), z, r));
                objs.Add(Make(ObjType.Hole, safeX(z + 120), z + 120, r));
            }
            ClearAroundBig(objs);
            for (int i = 0; i < L.Items.Length; i++)
            {
                string kind = L.Items[i];
                double z = Rand.JsRound(len * (0.28 + i * (L.Items.Length > 3 ? 0.18 : 0.24)));
                // A power crystal sits ~3 s before the nearest question row so its auto-answer gets used.
                if (kind == "crystal" && rows.Count > 0)
                {
                    double target = z;
                    z = rows.Aggregate((a, b) => Math.Abs(b.Z - 800 - target) < Math.Abs(a.Z - 800 - target) ? b : a).Z - 800;
                }
                while (Busy(z)) z += 60;
                objs.Add(new CourseObj { Type = ObjType.Item, Kind = kind, X = safeX(z) + (r.Next() < 0.5 ? -50 : 50), Z = z });
            }
            for (double z = 460; z < len - 200; z += 240)
            {
                if (Busy(z)) continue;
                for (int k = 0; k < 5; k++) objs.Add(Coin(safeX(z + k * 34), z + k * 34, k * 0.6));
            }
            foreach (var f in forks)
            {
                objs.Add(new CourseObj { Type = ObjType.Sign, X = 0, Z = f.Z0 - 70, Left = "ทางลัด", Right = "ปลอดภัย" });
                for (double z = f.Z0; z <= f.Z1; z += 48) objs.Add(Make(ObjType.Tree, (r.Next() - 0.5) * 12, z, r));
                for (double z = f.Z0 + 60; z < f.Z1 - 40; z += 34) objs.Add(Coin(-100 + Math.Sin(z / 50) * 20, z, z * 0.01));
                objs.Add(Make(ObjType.Log, -100, f.Z0 + 200, r));
                objs.Add(Make(ObjType.Log, -96, f.Z0 + 390, r));
                objs.Add(Make(ObjType.Rock, 96, f.Z0 + 300, r));
                objs.Add(Coin(100, f.Z0 + 140, 0));
                objs.Add(Coin(100, f.Z0 + 450, 1));
            }
            for (double z = 0; z < len + 600; z += 150 + r.Next() * 120) AddDecor(decor, z, r);

            return new Course { Len = len, Objs = objs.OrderBy(o => o.Z).ToList(), Decor = decor, Rows = rows, Forks = forks, SafeX = safeX };
        }

        static void AddDecor(List<Decor> decor, double z, IRng r)
        {
            decor.Add(new Decor { X = -160 + r.Next() * 320, Z = z, W = 160 + r.Next() * 120, H = 34 + r.Next() * 20 });
            if (r.Next() < 0.6) decor.Add(new Decor { Mogul = true, X = -150 + r.Next() * 300, Z = z + 60, S = 0.6 + r.Next() * 0.5 });
        }

        public static Course BuildEndless(uint seed)
        {
            var r = new JsRng(seed);
            double len = Levels.Endless.Len;
            var objs = new List<CourseObj>();
            var decor = new List<Decor>();
            var rows = new List<GateRow>();
            double ph1 = r.Next() * 6;
            double ph2 = r.Next() * 6;
            double Diff(double z) => Math.Min(1, z / 30000);
            Func<double, double> safeX = z =>
            {
                double amp = 60 + Diff(z) * 70;
                return Rand.Clamp(Math.Sin(z / 260 + ph1) * amp + Math.Sin(z / 110 + ph2) * amp * 0.35, -125, 125);
            };
            var nextOp = Questions.OpDealer(r);
            for (double z = 1800; z < len; z += 1000 + Rand.JsRound(r.Next() * 350))
            {
                double d = Diff(z);
                string[] pool = d < 0.12 ? new[] { "picture", "math1" } : d < 0.4 ? new[] { "math1", "odd", "compare" } : new[] { "math1", "odd", "pattern", "compare" };
                string kind = Rand.Pick(r, pool);
                int n = kind == "odd" || d > 0.3 ? 3 : 2;
                rows.Add(Row(z, Questions.Make(kind, r, n, kind == "math1" ? nextOp() : null), n));
            }
            bool NearRow(double z) => rows.Any(row => z > row.Z - 280 && z < row.Z + 150);
            for (double z = -260; z < len; z += 44 + r.Next() * 30)
            {
                objs.Add(Make(ObjType.Tree, -188 - r.Next() * 22, z, r));
                objs.Add(Make(ObjType.Tree, 188 + r.Next() * 22, z + r.Next() * 30, r));
            }
            int itemIx = 0;
            string[] kinds = { "shield", "magnet", "crystal", "slow" };
            for (double z = 420; z < len - 200; z += 70)
            {
                if (NearRow(z)) continue;
                double d = Diff(z);
                double sx = safeX(z);
                if (z > 9000 && r.Next() < 0.012 + d * 0.025 && !NearRow(z + 150))
                {
                    objs.Add(Make(ObjType.Log, sx, z, r));
                    objs.Add(Make(ObjType.Hole, safeX(z + 120), z + 120, r));
                    z += 120;
                    continue;
                }
                double roll = r.Next();
                if (roll < 0.03 + d * 0.05) { objs.Add(Make(ObjType.Log, sx, z, r)); continue; }
                if (z > 2500 && roll < 0.05 + d * 0.08) { objs.Add(new CourseObj { Type = ObjType.Puff, X0 = sx, X = sx, Z = z, Amp = 70 + r.Next() * 40, Freq = 1.3 + r.Next() * 0.9, Ph = r.Next() * 6, Hw = 18, Jump = true }); continue; }
                if (z > 5000 && roll < 0.065 + d * 0.1)
                {
                    int side = r.Next() < 0.5 ? -1 : 1;
                    objs.Add(new CourseObj { Type = ObjType.Ball, X = side * 205, Z = z, Vx = -side * (100 + r.Next() * 60), Vz = 70, Rad = 20, Hw = 26, Jump = false });
                    continue;
                }
                if (z > 8000 && roll < 0.08 + d * 0.1) { objs.Add(new CourseObj { Type = ObjType.Ice, X = sx, Z = z, W = 120, H = 46 }); continue; }
                if (z > 3500 && roll < 0.105 + d * 0.12) { objs.Add(Make(ObjType.Hole, sx + (r.Next() - 0.5) * 30, z, r)); continue; }
                if (z > 6000 && roll < 0.13 + d * 0.15) { objs.Add(Make(ObjType.Bear, sx + (r.Next() - 0.5) * 24, z, r)); continue; }
                if (r.Next() > 0.55 + d * 0.3) continue;
                double corridor = 130 - d * 45;
                // The bound re-rolls every pass, exactly like the web loop.
                for (int k = 0; k < 1 + (r.Next() < 0.2 + d * 0.4 ? 1 : 0); k++)
                {
                    double x = -160 + r.Next() * 320;
                    if (Math.Abs(x - sx) < corridor * 0.5 + 22) continue;
                    objs.Add(Make(r.Next() < 0.25 ? ObjType.Rock : ObjType.Tree, x, z + r.Next() * 40, r));
                }
            }
            for (double z = 460; z < len - 200; z += 240)
            {
                if (NearRow(z)) continue;
                for (int k = 0; k < 5; k++) objs.Add(Coin(safeX(z + k * 34), z + k * 34, k * 0.6));
            }
            for (double z = 3000; z < len; z += 3200 + Rand.JsRound(r.Next() * 800))
            {
                double zz = z;
                while (NearRow(zz)) zz += 60;
                objs.Add(new CourseObj { Type = ObjType.Item, Kind = kinds[itemIx++ % kinds.Length], X = safeX(zz) + (r.Next() < 0.5 ? -50 : 50), Z = zz });
            }
            for (double z = 0; z < len; z += 150 + r.Next() * 120) AddDecor(decor, z, r);
            ClearAroundBig(objs);
            return new Course { Len = len, Objs = objs.OrderBy(o => o.Z).ToList(), Decor = decor, Rows = rows, SafeX = safeX, Endless = true };
        }
    }
}
