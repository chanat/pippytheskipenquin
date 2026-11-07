// The Unity port must build the same slopes as the web prototype. Fixtures are exported from
// prototype/web/js/levels.js (see docs/UNITY.md; exporter: tools/export-fixtures.js).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Pip.Core;

namespace Pip.Tests
{
    public class ParityTests
    {
        const double Tol = 1e-5;
        static readonly string Dir = Path.Combine(Directory.GetCurrentDirectory(), "Assets/Tests/EditMode/Fixtures");

        static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);
        static string[][] Lines(string file, string tag) =>
            File.ReadAllLines(Path.Combine(Dir, file)).Select(l => l.Split('\t')).Where(p => p[0] == tag).ToArray();

        [Test]
        public void RngStreamMatchesWeb()
        {
            var expected = File.ReadAllLines(Path.Combine(Dir, "Rng-12345.txt")).Where(l => l.Length > 0).Select(D).ToArray();
            var r = new JsRng(12345);
            foreach (double e in expected) Assert.AreEqual(e, r.Next(), 0, "mulberry32 must be bit-exact");
        }

        static void AssertCourse(string file, Course c, bool compareQuestions)
        {
            var objs = Lines(file, "O");
            Assert.AreEqual(objs.Length, c.Objs.Count, "object count");
            for (int i = 0; i < objs.Length; i++)
            {
                var p = objs[i];
                var o = c.Objs[i];
                string ctx = file + " obj #" + i;
                Assert.AreEqual(p[1], o.Type.ToString().ToLowerInvariant(), ctx + " type");
                Assert.AreEqual(D(p[2]), o.X, Tol, ctx + " x");
                Assert.AreEqual(D(p[3]), o.Z, Tol, ctx + " z");
                if (o.Type == ObjType.Tree || o.Type == ObjType.Rock) Assert.AreEqual(D(p[4]), o.S, Tol, ctx + " s");
                Assert.AreEqual(D(p[5]), o.Hw, Tol, ctx + " hw");
                Assert.AreEqual(p[6], o.Kind ?? "", ctx + " kind");
            }
            var rows = Lines(file, "R");
            Assert.AreEqual(rows.Length, c.Rows.Count, "row count");
            for (int i = 0; i < rows.Length; i++)
            {
                Assert.AreEqual(D(rows[i][1]), c.Rows[i].Z, Tol, file + " row z");
                Assert.AreEqual(int.Parse(rows[i][2]), c.Rows[i].N, file + " row n");
                if (!compareQuestions) continue;
                var q = c.Rows[i].Q;
                Assert.AreEqual(rows[i][3], q.Text, file + " row text #" + i);
                string opts = string.Join(",", q.Options.Select(op => (op.Label ?? op.Shape) + (op.Correct ? "*" : "")));
                Assert.AreEqual(rows[i][4], opts, file + " row options #" + i);
            }
            var forks = Lines(file, "F");
            Assert.AreEqual(forks.Length, c.Forks.Count, "fork count");
            for (int i = 0; i < forks.Length; i++) Assert.AreEqual(D(forks[i][1]), c.Forks[i].Z0, Tol);
            var decor = Lines(file, "D");
            Assert.AreEqual(decor.Length, c.Decor.Count, "decor count");
            for (int i = 0; i < decor.Length; i++)
            {
                Assert.AreEqual(decor[i][1] == "mogul", c.Decor[i].Mogul);
                Assert.AreEqual(D(decor[i][2]), c.Decor[i].X, Tol);
                Assert.AreEqual(D(decor[i][3]), c.Decor[i].Z, Tol);
            }
            foreach (var s in Lines(file, "S")) Assert.AreEqual(D(s[2]), c.SafeX(D(s[1])), Tol, "safeX");
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void LevelCourseMatchesWeb(int id)
        {
            var L = Levels.World1[id - 1];
            AssertCourse("L" + id + ".txt", CourseBuilder.Build(L, new SysRng(7)), false);
        }

        [Test]
        public void EndlessCourseMatchesWebIncludingQuestions()
        {
            AssertCourse("Endless-20261104.txt", CourseBuilder.BuildEndless(20261104), true);
        }

        [Test]
        public void LevelLayoutDoesNotDependOnQuestionRandomness()
        {
            var a = CourseBuilder.Build(Levels.World1[2], new SysRng(1));
            var b = CourseBuilder.Build(Levels.World1[2], new SysRng(99));
            Assert.AreEqual(a.Objs.Count, b.Objs.Count);
            for (int i = 0; i < a.Objs.Count; i++) Assert.AreEqual(a.Objs[i].Z, b.Objs[i].Z);
        }

        [Test]
        public void DailySeedFormat()
        {
            Assert.AreEqual(20261104u, Levels.DailySeed(new DateTime(2026, 11, 4, 23, 0, 0, DateTimeKind.Utc)));
        }
    }
}
