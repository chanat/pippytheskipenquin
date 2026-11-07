// The 2D projection of the web prototype (render.js): a 390-wide design space whose height follows
// the screen (1 unit = 1 web css px), Pip at SY = 0.37·H, course z → screen y = SY + (z − pz) (ahead =
// lower, the slope scrolls up toward the crest), x = W/2 + x·persp(y)/persp(SY), sprites scaled by
// persp(y) = 0.82 + 0.3·y/H, the sky band (SkyH) at the top with objects fading in below the crest.
// Screen y grows downward; the scene puts screen (x, y) at world (x, −y), filmed by an orthographic
// camera centred on (W/2, −H/2). Sorting orders (one sorting layer) are kept here too.
using UnityEngine;

namespace Pip.Game.Art2D
{
    public static class View2D
    {
        public const float W = 390, SkyH = 150;

        /// <summary>Design height for the current screen (390·h/w; at least 640 so a landscape editor
        /// window or a batch-mode test still gets a portrait-like frame).</summary>
        public static float H = 844;

        /// <summary>The 2D views' clock, set by GameBoot.Update before any LateUpdate: Dt is the frame step
        /// (capped at 0.05 s, 0 while the game is paused) and T its running sum, so Pip, Benny, the
        /// particles and the backdrop freeze behind the pause panel and resume without a jump.</summary>
        public static bool Paused;
        public static float T, Dt;

        public static void Step(bool paused, float deltaTime)
        {
            Paused = paused;
            Dt = paused ? 0 : Mathf.Min(0.05f, deltaTime);
            T += Dt;
        }

        public static float DesignHeight(float screenW, float screenH) =>
            screenW > 0 && screenH > 0 ? Mathf.Max(640, W * screenH / screenW) : 844;

        public static float Persp(float y, float h) => 0.82f + 0.3f * (y / h);
        public static float PipY(float h) => 0.37f * h;
        public static float ToY(double z, double pz, float h) => PipY(h) + (float)(z - pz);
        public static float ToX(double x, float y, float h) => W / 2 + (float)x * (Persp(y, h) / Persp(PipY(h), h));
        public static float FadeTop(float y) => Mathf.Clamp01((y - (SkyH - 6)) / 70);

        // Shorthands on the current H.
        public static float Persp(float y) => Persp(y, H);
        public static float SY => PipY(H);
        public static float ToY(double z, double pz) => ToY(z, pz, H);
        public static float ToX(double x, float y) => ToX(x, y, H);

        /// <summary>Screen point (y down) → world position.</summary>
        public static Vector3 P(float sx, float sy) => new Vector3(sx, -sy, 0);

        // Sorting: ground layers under the sky picture (the sky is opaque and hides the tile's top), then the
        // ground's flat things, then everything standing on the snow by screen y (lower = nearer = on top)
        // with the crest among them as an object at its own y, then particles and the weather haze.
        public const int Ground = -1000, Edges = -990, Decor = -980, Sky = -900, Clouds = -890;
        public const int Trail = -700, Flat = -650, Shadow = -600;
        public const int Particles = 30000, Glow = 30001, Haze = 31000;

        /// <summary>Order for something standing on the snow at screen y (4 sub-steps per unit for parts).</summary>
        public static int Order(float y) => Mathf.Clamp(Mathf.RoundToInt(y * 8), 0, 28000);
    }
}
