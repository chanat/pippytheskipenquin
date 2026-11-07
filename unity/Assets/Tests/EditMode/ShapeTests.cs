// The UI draws the web's inline SVG icons from their path data; check the parser on the
// commands they use (relative lines, smooth curves, arcs, implicit repeats).
using NUnit.Framework;
using Pip.Game.Ui;
using UnityEngine;

namespace Pip.Tests
{
    public class ShapeTests
    {
        static Rect Bounds(Shape s)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var c in s.Contours)
                foreach (var p in c) { x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y); }
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        [Test]
        public void StarIsOneClosedTenPointOutline()
        {
            var s = Shape.Path(K.StarPath);
            Assert.AreEqual(1, s.Contours.Count);
            Assert.IsTrue(s.Closed[0]);
            Assert.AreEqual(10, s.Contours[0].Count);
            var b = Bounds(s);
            Assert.AreEqual(2.5f, b.xMin, 1e-3);
            Assert.AreEqual(21.5f, b.xMax, 1e-3);
            Assert.AreEqual(2.5f, b.yMin, 1e-3);
            Assert.AreEqual(20.5f, b.yMax, 1e-3);
        }

        [Test]
        public void LockShackleArcGoesOverTheTop()
        {
            var s = Shape.Path("M5 8V6a4 4 0 0 1 8 0v2");
            var c = s.Contours[0];
            Assert.IsFalse(s.Closed[0]);
            Assert.AreEqual(new Vector2(13, 8), c[c.Count - 1]);
            Assert.AreEqual(2, Bounds(s).yMin, 0.05f, "arc peaks at the circle's top");
        }

        [Test]
        public void HeartUsesSmoothCurvesAndImplicitRepeats()
        {
            var s = Shape.Path(K.HeartPath);
            Assert.AreEqual(1, s.Contours.Count);
            Assert.IsTrue(s.Closed[0]);
            var b = Bounds(s);
            Assert.AreEqual(21, b.yMax, 0.05f);
            Assert.Less(b.yMin, 5.5f);
            Assert.Less(b.xMin, 2.5f);
            Assert.Greater(b.xMax, 21.5f);
        }

        [Test]
        public void MultipleSubpathsSplitIntoContours()
        {
            var s = Shape.Path("M8.5 4v13.5M15.5 6.5V20");
            Assert.AreEqual(2, s.Contours.Count);
            Assert.AreEqual(new Vector2(8.5f, 17.5f), s.Contours[0][1]);
            Assert.AreEqual(new Vector2(15.5f, 20), s.Contours[1][1]);
        }
    }
}
