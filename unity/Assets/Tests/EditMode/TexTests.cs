// The colour helper and the little software canvas the 2D placeholders and clouds are drawn with.
using NUnit.Framework;
using Pip.Game.Art;
using UnityEngine;

namespace Pip.Tests
{
    public class TexTests
    {
        [Test]
        public void HexColoursParseWithAlpha()
        {
            var c = Tex.C("#FF8000", 0.5f);
            Assert.AreEqual(1f, c.r, 1e-3);
            Assert.AreEqual(128 / 255f, c.g, 1e-3);
            Assert.AreEqual(0f, c.b, 1e-3);
            Assert.AreEqual(0.5f, c.a, 1e-3);
        }

        [Test]
        public void CanvasBlendsSourceOverWithTopRowFirst()
        {
            var cv = new Canvas2(4, 4, new Color(1, 1, 1, 0));
            cv.Rect(0, 0, 4, 1, new Color(1, 0, 0, 1));    // canvas row 0 = the top
            cv.Blend(0, 3, new Color(0, 0, 1, 0.5f));      // half blue over transparent
            Assert.AreEqual(Color.red, cv.Get(2, 0), "Rect fills the top row");
            var b = cv.Get(0, 3);
            Assert.AreEqual(0.5f, b.a, 1e-4, "source-over alpha");
            Assert.AreEqual(1f, b.b, 1e-4, "straight (not premultiplied) colour");
            Assert.AreEqual(0f, cv.Get(2, 1).a, 1e-6, "untouched pixels keep the fill");
            var t = cv.ToTexture();
            try
            {
                Assert.AreEqual(4, t.width);
                Assert.AreEqual(4, t.height);
            }
            finally { Object.DestroyImmediate(t); }
        }
    }
}
