// Random helpers ported from the web prototype (prototype/web/js/levels.js) so a seeded course
// comes out identical in Unity and in the browser.
using System;
using System.Collections.Generic;

namespace Pip.Core
{
    public interface IRng
    {
        /// <summary>Uniform double in [0, 1), like JavaScript's Math.random().</summary>
        double Next();
    }

    /// <summary>mulberry32, bit-for-bit the same as the web prototype's rng(seed).</summary>
    public sealed class JsRng : IRng
    {
        uint a;

        public JsRng(uint seed) { a = seed; }

        public double Next()
        {
            unchecked
            {
                a += 0x6D2B79F5;
                uint t = a;
                t = (t ^ (t >> 15)) * (t | 1);
                t ^= t + (t ^ (t >> 7)) * (t | 61);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }
    }

    /// <summary>Unseeded randomness for things that should differ every run (level questions).</summary>
    public sealed class SysRng : IRng
    {
        readonly Random r;

        public SysRng() { r = new Random(); }
        public SysRng(int seed) { r = new Random(seed); }

        public double Next() => r.NextDouble();
    }

    public static class Rand
    {
        public static T Pick<T>(IRng r, IList<T> arr) => arr[(int)Math.Floor(r.Next() * arr.Count)];

        /// <summary>Integer in [a, b] inclusive.</summary>
        public static int Int(IRng r, int a, int b) => a + (int)Math.Floor(r.Next() * (b - a + 1));

        /// <summary>In-place Fisher–Yates from the end, same draw order as the web version.</summary>
        public static List<T> Shuffle<T>(IRng r, List<T> arr)
        {
            for (int i = arr.Count - 1; i > 0; i--)
            {
                int j = (int)Math.Floor(r.Next() * (i + 1));
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
            return arr;
        }

        /// <summary>JavaScript Math.round (halves go up), not .NET banker's rounding.</summary>
        public static double JsRound(double x) => Math.Floor(x + 0.5);

        public static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;

        /// <summary>JavaScript Math.sign semantics for non-NaN input.</summary>
        public static int Sign(double v) => v > 0 ? 1 : v < 0 ? -1 : 0;
    }
}
