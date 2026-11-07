// Gameplay rules carried over from the web prototype: jump + flip timing, the log+hole combo,
// the power crystal, the four-operator questions, ski tracks, and whole runs finishing cleanly;
// plus Unity's hold-to-boost (ramp in / out, grounded only, capped together with the crystal).
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pip.Core;

namespace Pip.Tests
{
    public class SimTests
    {
        const double Dt = 1.0 / 60;

        static LevelDef Flat(double len = 4000) => new LevelDef { Id = 2, Len = len, V0 = 225, V1 = 225, Opts = 2 };

        static RunSim Sim(List<CourseObj> objs, List<GateRow> rows = null, double len = 4000, RunListener listener = null)
        {
            var course = new Course { Len = len, Objs = objs.OrderBy(o => o.Z).ToList(), Rows = rows ?? new List<GateRow>(), SafeX = z => 0 };
            var sim = new RunSim(Flat(len), course, 1, listener);
            sim.Start();
            return sim;
        }

        static void RunUntil(RunSim sim, Func<bool> done, int max = 20000)
        {
            for (int i = 0; i < max && !done(); i++) sim.Advance(Dt);
        }

        static void RunFor(RunSim sim, double seconds)
        {
            for (int i = 0, n = (int)Math.Round(seconds / Dt); i < n; i++) sim.Advance(Dt);
        }

        sealed class Toasts : RunListener
        {
            public readonly List<string> Seen = new List<string>();
            public override void Toast(string text, string color) => Seen.Add(text);
        }

        [Test]
        public void SingleJumpAirtimeMatchesPhysics()
        {
            var sim = Sim(new List<CourseObj>());
            sim.Jump();
            double t = 0;
            while (sim.Air) { sim.Advance(Dt); t += Dt; }
            Assert.AreEqual(2 * Tuning.JumpV / Tuning.Grav, t, 0.03);
        }

        [Test]
        public void MidAirTapFlipsOnceAndStaysUpLonger()
        {
            var sim = Sim(new List<CourseObj>());
            sim.Jump();
            RunUntil(sim, () => sim.AirT >= 0.3);
            sim.Jump();
            Assert.NotNull(sim.Flip);
            var flip = sim.Flip;
            sim.Jump(); // a third tap does nothing
            Assert.AreSame(flip, sim.Flip);
            double t = 0.3;
            while (sim.Air) { sim.Advance(Dt); t += Dt; }
            Assert.Greater(t, 0.95);
            Assert.AreEqual(0, sim.FlipA);
        }

        static List<CourseObj> LogThenHole() => new List<CourseObj>
        {
            new CourseObj { Type = ObjType.Log, X = 0, Z = 1000, Hw = 46, Jump = true },
            new CourseObj { Type = ObjType.Hole, X = 0, Z = 1120, Hw = 40, H = 30, Jump = true },
        };

        [Test]
        public void ComboNeedsTheFlip()
        {
            var single = Sim(LogThenHole());
            RunUntil(single, () => single.Z >= 970);
            single.Jump();
            RunUntil(single, () => single.Z >= 1300);
            Assert.Less(single.Hearts, 3, "one hop cannot clear a log and a hole back to back");

            var both = Sim(LogThenHole());
            RunUntil(both, () => both.Z >= 970);
            both.Jump();
            RunUntil(both, () => both.AirT >= 0.3);
            both.Jump();
            RunUntil(both, () => both.Z >= 1300);
            Assert.AreEqual(3, both.Hearts, "jump + flip clears both");
        }

        [Test]
        public void PowerCrystalSmashesObstaclesAndAnswersTheGate()
        {
            var objs = new List<CourseObj>
            {
                new CourseObj { Type = ObjType.Item, Kind = "crystal", X = 0, Z = 300 },
                new CourseObj { Type = ObjType.Tree, X = 0, Z = 600, Hw = 16 },
                new CourseObj { Type = ObjType.Rock, X = 5, Z = 800, Hw = 16, Jump = true },
                new CourseObj { Type = ObjType.Hole, X = 0, Z = 950, Hw = 40, H = 30, Jump = true },
            };
            var q = Questions.Arith(new JsRng(3), 2, "×");
            Questions.PaintGates(q);
            // Steer to the wrong gate on purpose: while powered, any gate counts as right.
            var row = new GateRow { Z = 1200, Q = q, N = 2, Correct = q.CorrectIndex };
            var sim = Sim(objs, new List<GateRow> { row });
            double before = sim.V;
            RunUntil(sim, () => sim.Power > 0);
            RunUntil(sim, () => sim.Z > 650);
            Assert.Greater(sim.V, before * 1.1, "faster while glowing");
            RunUntil(sim, () => sim.Z > 980);
            sim.Steer(row.Correct == 0 ? 100 : -100);
            RunUntil(sim, () => row.Done);
            Assert.IsTrue(objs[1].Gone && objs[2].Gone, "tree and rock shattered");
            Assert.AreEqual(3, sim.Hearts);
            Assert.IsTrue(row.Reveal && row.Smashed);
            Assert.AreEqual(row.Correct, row.Picked);
            Assert.AreEqual(1, sim.Correct);
        }

        [Test]
        public void BoostRampsSpeedAndFadesOnRelease()
        {
            var toasts = new Toasts();
            var sim = Sim(new List<CourseObj>(), listener: toasts);
            sim.Advance(Dt);
            Assert.AreEqual(225, sim.V, 1e-9, "cruising speed on the flat level");
            sim.Boost = true;
            RunFor(sim, 0.2);
            Assert.That(sim.V, Is.GreaterThan(225).And.LessThan(225 * 1.35), "still easing in after 0.2 s");
            RunFor(sim, 0.8);
            Assert.AreEqual(225 * (1 + Tuning.BoostSpeed), sim.V, 225 * 1.35 * 0.01, "full boost after 1 s");
            Assert.AreEqual(1, sim.BoostLevel, 1e-9);
            sim.Boost = false;
            RunFor(sim, 0.6);
            Assert.AreEqual(225, sim.V, 225 * 0.01, "back to cruising within 0.6 s of letting go");
            Assert.AreEqual(0, sim.BoostT);
            sim.Boost = true;
            RunFor(sim, 1);
            Assert.AreEqual(1, toasts.Seen.Count(t => t == "เร่ง!"), "the boost toast shows once per run");
        }

        [Test]
        public void BoostDoesNotRampInTheAir()
        {
            var sim = Sim(new List<CourseObj>());
            sim.Boost = true;
            sim.Jump();
            while (sim.Air)
            {
                sim.Advance(Dt);
                if (sim.Air) Assert.AreEqual(0, sim.BoostT, "no boost while airborne");
            }
            RunFor(sim, 0.3);
            Assert.Greater(sim.BoostT, 0.6, "builds once the skis are back on the snow");
            RunFor(sim, 0.3);
            Assert.AreEqual(1, sim.BoostT);
            sim.Jump();
            RunUntil(sim, () => sim.AirT > 0.4);
            Assert.AreEqual(0, sim.BoostT, "a jump drops the boost even while the finger stays down");
        }

        [Test]
        public void BoostStacksWithCrystalUpToTheCap()
        {
            var sim = Sim(new List<CourseObj>());
            sim.Power = Tuning.Power;
            sim.Advance(Dt);
            Assert.AreEqual(225 * Tuning.PowerSpeed, sim.V, 1e-9, "crystal alone: ×1.15");
            sim.Boost = true;
            RunFor(sim, 1);
            Assert.Greater(sim.Power, 0, "crystal still glowing");
            Assert.AreEqual(225 * Tuning.MaxSpeedMul, sim.V, 225 * 1.5 * 0.01, "boost + crystal capped at ×1.5");
            Assert.Less(sim.V, 225 * Tuning.PowerSpeed * (1 + Tuning.BoostSpeed) * 0.99, "not the uncapped ×1.15 × 1.35");
        }

        [Test]
        public void SkiTracksFollowPipAndBreakInTheAir()
        {
            var sim = Sim(new List<CourseObj>());
            RunUntil(sim, () => sim.Z >= 1500);
            var t = sim.Trail;
            Assert.Greater(t.Count, 20, "a sample every 0.03 s");
            Assert.GreaterOrEqual(t[0].Z, sim.Z - 420 - 225 * 0.04, "only the last 420 units are kept");
            Assert.AreEqual(sim.Z, t[t.Count - 1].Z, 225 * 0.04, "newest sample is under Pip");
            for (int i = 1; i < t.Count; i++) Assert.Greater(t[i].Z, t[i - 1].Z, "samples run down the slope in order");
            Assert.IsTrue(t.All(p => !p.Air), "skis on the snow the whole way");

            sim.Jump();
            RunUntil(sim, () => sim.AirT > 0.2);
            Assert.IsTrue(sim.Trail[sim.Trail.Count - 1].Air, "no groove while airborne");
            RunUntil(sim, () => !sim.Air);
            sim.Advance(0.05);
            Assert.IsFalse(sim.Trail[sim.Trail.Count - 1].Air, "back in the snow after landing");
        }

        [Test]
        public void ArithmeticIsCorrectAndEvenlyMixed()
        {
            var r = new JsRng(42);
            var deal = Questions.OpDealer(r);
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < 4000; i++)
            {
                string op = deal();
                var q = Questions.Arith(r, 3, op);
                var parts = q.Text.Split(' ');
                int a = int.Parse(parts[0]), b = int.Parse(parts[2]);
                int ans = op == "+" ? a + b : op == "−" ? a - b : op == "×" ? a * b : a / b;
                if (op == "÷") Assert.AreEqual(0, a % b, "division always comes out even");
                Assert.GreaterOrEqual(ans, 1);
                Assert.LessOrEqual(ans, 25);
                Assert.AreEqual(1, q.Options.Count(o => o.Correct));
                Assert.AreEqual(ans.ToString(), q.Options[q.CorrectIndex].Label);
                Assert.AreEqual(3, q.Options.Select(o => o.Label).Distinct().Count());
                counts[op] = counts.TryGetValue(op, out int c) ? c + 1 : 1;
            }
            foreach (string op in Questions.Ops) Assert.AreEqual(1000, counts[op], "the bag deals each operator equally");
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void WholeLevelRunsToTheFinish(int id)
        {
            var L = Levels.World1[id - 1];
            var course = CourseBuilder.Build(L, new SysRng(id));
            var sim = new RunSim(L, course);
            sim.Start();
            int steps = 0;
            while (!sim.Finished && steps++ < 60 * 300)
            {
                sim.Hearts = 3;
                sim.Steer(course.SafeX(sim.Z + 120));
                sim.Advance(Dt);
            }
            Assert.IsTrue(sim.Finished, "reached the finish line");
            Assert.AreEqual(course.Rows.Count, sim.Answered, "every gate row was answered");
            Assert.Greater(sim.Coins, 0);
            var res = sim.Result(true);
            Assert.GreaterOrEqual(res.Stars, 1);
            Assert.Greater(res.Total, 0);
        }
    }
}
