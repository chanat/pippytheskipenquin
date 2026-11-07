// Answer-gate question generators, ported from prototype/web/js/levels.js. Draw order from the
// rng is kept identical so the seeded Daily Endless course matches the web prototype.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Pip.Core
{
    public sealed class Option
    {
        public string Label;      // number answers
        public string Shape;      // picture / odd-one-out answers
        public string ShapeColor;
        public bool Correct;
        public string Color;      // gate banner colour
    }

    public sealed class Question
    {
        public string Text;
        public string Hint;
        public List<Option> Options;

        public int CorrectIndex => Options.FindIndex(o => o.Correct);
    }

    public static class Questions
    {
        public static readonly string[] GateColors = { "#3FA9F5", "#FF5C93", "#9B7BF0" };
        public static readonly string[] Shapes = { "circle", "square", "tri", "star", "diamond" };
        public static readonly Dictionary<string, string> ShapeName = new Dictionary<string, string>
        {
            { "circle", "วงกลม" }, { "square", "สี่เหลี่ยม" }, { "tri", "สามเหลี่ยม" }, { "star", "ดาว" }, { "diamond", "ข้าวหลามตัด" },
        };
        public static readonly string[] Ops = { "+", "−", "×", "÷" };
        public static readonly string[] MixedKinds = { "math1", "pattern", "odd", "compare" };

        const string TurnHint = "เลี้ยวผ่านประตูคำตอบ!";

        static string S(int v) => v.ToString(CultureInfo.InvariantCulture);

        static List<Option> NumberOptions(IRng r, int answer, int n)
        {
            var set = new List<int> { answer };
            while (set.Count < n)
            {
                int v = answer + Rand.Int(r, 1, 4) * (r.Next() < 0.5 ? -1 : 1);
                if (v >= 0 && !set.Contains(v)) set.Add(v);
            }
            var opts = new List<Option>();
            foreach (int v in Rand.Shuffle(r, set)) opts.Add(new Option { Label = S(v), Correct = v == answer });
            return opts;
        }

        /// <summary>Easy arithmetic, one operator per question. − and ÷ are the inverse of + and ×,
        /// so all four sit at the same small-number level (sums ≤ 18, times tables 2–5).</summary>
        public static Question Arith(IRng r, int n, string op)
        {
            int a, b, ans;
            if (op == "+") { a = Rand.Int(r, 2, 9); b = Rand.Int(r, 1, 9); ans = a + b; }
            else if (op == "−") { ans = Rand.Int(r, 1, 9); b = Rand.Int(r, 1, 9); a = ans + b; }
            else if (op == "×") { a = Rand.Int(r, 2, 5); b = Rand.Int(r, 2, 5); ans = a * b; }
            else { ans = Rand.Int(r, 2, 5); b = Rand.Int(r, 2, 5); a = ans * b; }
            return new Question { Text = S(a) + " " + op + " " + S(b) + " = ?", Hint = TurnHint, Options = NumberOptions(r, ans, n) };
        }

        /// <summary>Deals operators from a shuffled bag so a run mixes all four instead of repeating one.</summary>
        public static Func<string> OpDealer(IRng r)
        {
            var bag = new List<string>();
            return () =>
            {
                if (bag.Count == 0) bag.AddRange(Rand.Shuffle(r, new List<string>(Ops)));
                string op = bag[bag.Count - 1];
                bag.RemoveAt(bag.Count - 1);
                return op;
            };
        }

        public static Question Make(string kind, IRng r, int n, string op = null)
        {
            switch (kind)
            {
                case "picture": return Picture(r, n);
                case "math1": return Arith(r, n, op ?? Rand.Pick(r, Ops));
                case "odd": return Odd(r, n);
                case "pattern": return Pattern(r, n);
                case "compare": return Compare(r, n);
                default: throw new ArgumentException("Unknown question kind " + kind);
            }
        }

        static Question Picture(IRng r, int n)
        {
            var kinds = Rand.Shuffle(r, new List<string>(Shapes)).GetRange(0, n);
            string target = kinds[0];
            var opts = new List<Option>();
            foreach (string k in kinds) opts.Add(new Option { Shape = k, Correct = k == target });
            return new Question { Text = "ประตูไหนเป็นรูป" + ShapeName[target] + "?", Hint = TurnHint, Options = Rand.Shuffle(r, opts) };
        }

        static Question Odd(IRng r, int n)
        {
            var kinds = Rand.Shuffle(r, new List<string>(Shapes));
            var opts = new List<Option>();
            for (int i = 0; i < n - 1; i++) opts.Add(new Option { Shape = kinds[0], Correct = false });
            opts.Add(new Option { Shape = kinds[1], Correct = true });
            var tints = Rand.Shuffle(r, new List<string> { "#FFFFFF", "#FFE066", "#BDF2E9" });
            Rand.Shuffle(r, opts);
            for (int i = 0; i < opts.Count; i++) opts[i].ShapeColor = tints[i % tints.Count];
            return new Question { Text = "อันไหนไม่เข้าพวก?", Hint = "ดูที่รูปทรง ไม่ใช่สี", Options = opts };
        }

        static Question Pattern(IRng r, int n)
        {
            int step = Rand.Int(r, 2, 5);
            int start = Rand.Int(r, 1, 9);
            return new Question
            {
                Text = S(start) + ", " + S(start + step) + ", " + S(start + 2 * step) + ", ?",
                Hint = "ตัวต่อไปคืออะไร?",
                Options = NumberOptions(r, start + 3 * step, n),
            };
        }

        static Question Compare(IRng r, int n)
        {
            var vals = new List<int>();
            while (vals.Count < n)
            {
                int v = Rand.Int(r, 11, 99);
                if (!vals.Contains(v)) vals.Add(v);
            }
            int max = int.MinValue;
            foreach (int v in vals) max = Math.Max(max, v);
            var opts = new List<Option>();
            foreach (int v in vals) opts.Add(new Option { Label = S(v), Correct = v == max });
            return new Question { Text = "เลขไหนมากที่สุด?", Hint = "เลี้ยวผ่านเลขที่มากที่สุด!", Options = opts };
        }

        public static void PaintGates(Question q)
        {
            for (int k = 0; k < q.Options.Count; k++) q.Options[k].Color = GateColors[k % GateColors.Length];
        }
    }
}
