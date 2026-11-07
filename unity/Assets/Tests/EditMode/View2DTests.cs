// The 2D projection matches the web prototype's render.js numbers (persp, SY, toY, toX).
using NUnit.Framework;
using Pip.Game.Art2D;

namespace Pip.Tests
{
    public class View2DTests
    {
        const float H = 844;

        [Test]
        public void PerspGoesFrom082To112()
        {
            Assert.AreEqual(0.82f, View2D.Persp(0, H), 1e-5);
            Assert.AreEqual(1.12f, View2D.Persp(H, H), 1e-5);
            Assert.AreEqual(0.97f, View2D.Persp(H / 2, H), 1e-5);
        }

        [Test]
        public void PipSitsAt37PercentOfTheHeight()
        {
            Assert.AreEqual(0.37f * H, View2D.PipY(H), 1e-4);
            Assert.AreEqual(View2D.PipY(H), View2D.ToY(500, 500, H), 1e-4, "Pip's own z is his row");
        }

        [Test]
        public void AheadIsLowerOneToOne()
        {
            float y0 = View2D.ToY(1000, 1000, H);
            Assert.AreEqual(y0 + 100, View2D.ToY(1100, 1000, H), 1e-4, "100 ahead → 100 lower");
            Assert.AreEqual(y0 - 50, View2D.ToY(950, 1000, H), 1e-4, "behind → higher, toward the crest");
        }

        [Test]
        public void ToXIsSymmetricAndScaledByPersp()
        {
            float sy = View2D.PipY(H);
            Assert.AreEqual(195, View2D.ToX(0, 600, H), 1e-4, "x 0 is the centre at any depth");
            Assert.AreEqual(195 + 100, View2D.ToX(100, sy, H), 1e-3, "on Pip's row x is 1:1");
            float y = 700;
            float r = View2D.ToX(120, y, H) - 195, l = 195 - View2D.ToX(-120, y, H);
            Assert.AreEqual(r, l, 1e-4, "symmetric about the centre");
            Assert.AreEqual(120 * View2D.Persp(y, H) / View2D.Persp(sy, H), r, 1e-3, "scaled by persp(y)/persp(SY)");
            Assert.Greater(r, 120, "nearer (lower) spreads wider");
        }

        [Test]
        public void FadeTopAndDesignHeight()
        {
            Assert.AreEqual(0, View2D.FadeTop(View2D.SkyH - 6), 1e-5);
            Assert.AreEqual(1, View2D.FadeTop(View2D.SkyH + 64), 1e-5);
            Assert.AreEqual(390 * 2868f / 1320f, View2D.DesignHeight(1320, 2868), 1e-3, "iPhone 17 Pro Max");
            Assert.AreEqual(640, View2D.DesignHeight(1920, 1080), 1e-3, "landscape windows keep a portrait-like frame");
            Assert.Greater(View2D.Order(400), View2D.Order(300), "lower on screen draws on top");
        }
    }
}
