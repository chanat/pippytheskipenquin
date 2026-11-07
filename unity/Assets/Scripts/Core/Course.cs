// Course data: everything placed on the slope. Units: 1 world unit = 1 logical pixel at the
// skier's depth in the web prototype; z grows downhill, x is −160…160 across the piste.
using System;
using System.Collections.Generic;

namespace Pip.Core
{
    public enum ObjType { Tree, Rock, Snowman, Log, Hole, Bear, Puff, Ball, Ice, Item, Coin, Sign }

    public sealed class CourseObj
    {
        public ObjType Type;
        public double X, Z;
        public double S = 1;      // visual scale
        public double Hw;         // half-width used for collisions (0 = none)
        public bool Jump;         // can be hopped over
        // hole / ice footprint
        public double H, W;
        // bear
        public double Up; public bool Warn; public double Ph;
        // puff: walks side to side around X0
        public double X0, Amp, Freq, HopT; public int Dir;
        // rolling snowball
        public double Vx, Vz, Rad, Rot; public bool Active;
        // item kind: shield / magnet / slow / crystal
        public string Kind;
        // fork sign labels
        public string Left, Right;
        // runtime
        public bool Gone, Passed;

        public bool Is(ObjType t) => Type == t;
    }

    public sealed class Decor
    {
        public bool Mogul; // false = shade patch
        public double X, Z, W, H, S;
    }

    public sealed class GateRow
    {
        public double Z;
        public Question Q;
        public int N;
        public bool Done, Shown, Reveal, Smashed;
        public int Correct;
        public int Picked = -1;
    }

    public struct Fork { public double Z0, Z1; }

    public sealed class Course
    {
        public double Len;
        public List<CourseObj> Objs = new List<CourseObj>();
        public List<Decor> Decor = new List<Decor>();
        public List<GateRow> Rows = new List<GateRow>();
        public List<Fork> Forks = new List<Fork>();
        public Func<double, double> SafeX;
        public bool Endless;
    }
}
