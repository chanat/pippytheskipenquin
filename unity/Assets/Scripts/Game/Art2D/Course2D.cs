// The course near Pip as sprites, laid out like render.js: moguls and shade patches flat on the snow,
// ice patches and holes flat above the ski tracks, then everything standing on the slope (props,
// coins, items, gate rows with their answers, START / FINISH arches, fork signs) sorted by screen y,
// faded in below the crest. Sprite views are pooled per role. Gate rows use the s06 gate in the
// colourway nearest each option's colour (state colours: right → pine, picked wrong → grey, reveal →
// sunny), the answer text / shape drawn on the banner; arches carry START / FINISH on their banner.
using System.Collections.Generic;
using Pip.Core;
using Pip.Game.Art;
using UnityEngine;

namespace Pip.Game.Art2D
{
    public sealed class Course2D : MonoBehaviour
    {
        sealed class View
        {
            public string Key;
            public GameObject Go;
            public SpriteRenderer A, B;   // main sprite, extra (item shadow / crystal halo)
            public Label2D L;
        }

        sealed class RowView
        {
            public GameObject Go;
            public SpriteRenderer[] Gates, Poles, Banners, Shapes;
            public Label2D[] Labels, Marks;
        }

        sealed class ArchView
        {
            public GameObject Go;
            public SpriteRenderer Arch, PostL, PostR, Board;
            /// <summary>The u4 START / FINISH lettering on the banner (null: the Label2D text instead).</summary>
            public SpriteRenderer Word;
            public string WordRole;
            public Label2D L;
        }

        RunSim sim;
        int first, firstDecor;
        readonly Dictionary<CourseObj, View> live = new Dictionary<CourseObj, View>();
        readonly Dictionary<Decor, View> liveDecor = new Dictionary<Decor, View>();
        readonly Dictionary<string, Stack<View>> pool = new Dictionary<string, Stack<View>>();
        readonly Dictionary<GateRow, RowView> rows = new Dictionary<GateRow, RowView>();
        readonly HashSet<object> seen = new HashSet<object>();
        readonly List<object> drop = new List<object>();
        ArchView start, finish;

        /// <summary>Draw the START / FINISH lettering on the arches (off behind the title's logo).</summary>
        public bool ArchWords = true;
        SpriteRenderer crest;
        string crestWorld;

        /// <summary>Active sprite renderers under the course (PlayMode check).</summary>
        public int LiveSprites
        {
            get
            {
                int n = 0;
                foreach (var r in GetComponentsInChildren<SpriteRenderer>(false)) if (r.enabled && r.sprite != null) n++;
                return n;
            }
        }

        public void Bind(RunSim run)
        {
            foreach (var kv in live) Release(kv.Value);
            live.Clear();
            foreach (var kv in liveDecor) Release(kv.Value);
            liveDecor.Clear();
            foreach (var rv in rows.Values) Destroy(rv.Go);
            rows.Clear();
            sim = run;
            first = firstDecor = 0;
            if (start == null) start = BuildArch("START");
            if (finish == null) finish = BuildArch("FINISH");
            string w = run.L.World ?? "meadow";
            if (crest == null) crest = Kit2D.SR(transform, "crest");
            if (crestWorld != w)
            {
                crestWorld = w;
                crest.sprite = w == "sunset" ? Placeholder2D.Crest("#FFF9F2", "#F3DACB") : Placeholder2D.Crest("#FBFDFF", "#DAE6FF");
            }
        }

        void Release(View v)
        {
            v.Go.SetActive(false);
            if (!pool.TryGetValue(v.Key, out var st)) pool[v.Key] = st = new Stack<View>();
            st.Push(v);
        }

        View Acquire(string key)
        {
            if (pool.TryGetValue(key, out var st) && st.Count > 0)
            {
                var v = st.Pop();
                v.Go.SetActive(true);
                return v;
            }
            var go = new GameObject(key);
            go.transform.SetParent(transform, false);
            var nv = new View { Key = key, Go = go, A = Kit2D.SR(go.transform, "sprite") };
            if (key.StartsWith("item") || key == "coin") nv.B = Kit2D.SR(go.transform, "under");
            if (key == "sign") nv.L = new Label2D(go.transform, "sign text");
            return nv;
        }

        static string KeyOf(CourseObj o)
        {
            switch (o.Type)
            {
                case ObjType.Tree: return "tree" + Mathf.Abs((int)(o.X * 7 + o.Z)) % 4;
                case ObjType.Rock: return "rock" + Mathf.Abs((int)(o.X * 3 + o.Z)) % 2;
                case ObjType.Log: return "log";
                case ObjType.Snowman: return "snowman";
                case ObjType.Puff: return "puff";
                case ObjType.Ball: return "ball";
                case ObjType.Bear: return "bear";
                case ObjType.Hole: return "hole";
                case ObjType.Ice: return "ice";
                case ObjType.Coin: return "coin";
                case ObjType.Item: return "item." + (o.Kind ?? "shield");
                case ObjType.Sign: return "sign";
                default: return o.Type.ToString();
            }
        }

        void LateUpdate()
        {
            if (sim == null) return;
            float H = View2D.H, SY = View2D.SY;
            double pz = sim.Z;
            float t = (float)sim.Clock;
            double zMin = pz + (View2D.SkyH - 60 - SY), zMax = pz + (H + 140 - SY);
            seen.Clear();

            // The crest: the slope's snow line, standing among the objects at its own screen y.
            Kit2D.Put(crest, new Slice { S = crest.sprite, Scale = 1 }, -40, View2D.SkyH - 62, 470, 110, new Vector2(0, 1));
            crest.sortingOrder = View2D.Order(View2D.SkyH + 24);

            var decor = sim.Course.Decor;
            while (firstDecor < decor.Count && decor[firstDecor].Z < zMin - 400) firstDecor++;
            for (int i = firstDecor; i < decor.Count; i++)
            {
                var d = decor[i];
                if (d.Z > zMax + 300) break;
                float y = View2D.ToY(d.Z, pz);
                if (y < View2D.SkyH - 40 || y > H + 60) continue;
                if (!liveDecor.TryGetValue(d, out var v)) liveDecor[d] = v = Acquire(d.Mogul ? "mogul" : "shade");
                seen.Add(d);
                float sc = View2D.Persp(y);
                var sl = new Slice { S = d.Mogul ? Placeholder2D.Mogul : Placeholder2D.ShadePatch, Scale = 1 };
                if (d.Mogul) Kit2D.Put(v.A, sl, View2D.ToX(d.X, y), y, 60 * (float)d.S * sc, 20 * (float)d.S * sc, Kit2D.Centre);
                else Kit2D.Put(v.A, sl, View2D.ToX(d.X, y), y, (float)d.W * sc * 1.2f, (float)d.H * sc * 1.6f, Kit2D.Centre);
                v.A.sortingOrder = View2D.Decor;
                Kit2D.Alpha(v.A, View2D.FadeTop(y));
            }

            var objs = sim.Course.Objs;
            while (first < objs.Count && objs[first].Z < zMin - 400) first++;
            for (int i = first; i < objs.Count; i++)
            {
                var o = objs[i];
                if (o.Z > zMax) break;
                if (o.Gone || o.Z < zMin) continue;
                float y = View2D.ToY(o.Z, pz);
                if (!live.TryGetValue(o, out var v)) live[o] = v = Acquire(KeyOf(o));
                seen.Add(o);
                Place(o, v, y, t);
            }
            drop.Clear();
            foreach (var kv in live) if (!seen.Contains(kv.Key)) drop.Add(kv.Key);
            foreach (CourseObj o in drop) { Release(live[o]); live.Remove(o); }
            drop.Clear();
            foreach (var kv in liveDecor) if (!seen.Contains(kv.Key)) drop.Add(kv.Key);
            foreach (Decor d in drop) { Release(liveDecor[d]); liveDecor.Remove(d); }

            foreach (var row in sim.Course.Rows)
            {
                float y = View2D.ToY(row.Z, pz);
                bool show = !row.Smashed && y > View2D.SkyH - 60 && y < H + 140;
                if (!rows.TryGetValue(row, out var rv))
                {
                    if (!show) continue;
                    rows[row] = rv = BuildRow(row);
                }
                if (rv.Go.activeSelf != show) rv.Go.SetActive(show);
                if (show) PlaceRow(row, rv, y);
            }

            float sy0 = View2D.ToY(-40, pz);
            PlaceArch(start, sy0, sy0 > View2D.SkyH - 60 && sy0 < H + 160, "#3FA9F5");
            float fy = View2D.ToY(sim.Course.Len, pz);
            PlaceArch(finish, fy, !sim.Course.Endless && fy > View2D.SkyH - 60 && fy < H + 160, "#FF5C93");
        }

        void Place(CourseObj o, View v, float y, float t)
        {
            float sc = View2D.Persp(y);
            float x = View2D.ToX(o.X, y);
            float a = View2D.FadeTop(y);
            int order = View2D.Order(y);
            var r = v.A;
            switch (o.Type)
            {
                case ObjType.Hole:
                {
                    var s = SpriteTable.Get("hole");
                    Kit2D.Put(r, s, x, y, ((float)o.Hw * 2 + 18) * sc, (float)o.H * sc * 1.35f, Kit2D.Centre);
                    r.sortingOrder = View2D.Flat;
                    Kit2D.Alpha(r, a);
                    return;
                }
                case ObjType.Ice:
                {
                    var s = SpriteTable.Get("ice");
                    Kit2D.Put(r, s, x, y, (float)o.W * sc, (float)o.H * sc * 1.15f, Kit2D.Centre);
                    r.sortingOrder = View2D.Flat;
                    Kit2D.Alpha(r, a);
                    return;
                }
                case ObjType.Tree:
                case ObjType.Rock:
                    Kit2D.PutK(r, SpriteTable.Get(v.Key), x, y + 2, (float)o.S * sc, Kit2D.Bottom);
                    break;
                case ObjType.Log:
                    Kit2D.PutK(r, SpriteTable.Get("log"), x, y + 3, sc, Kit2D.Bottom);
                    break;
                case ObjType.Snowman:
                    Kit2D.PutK(r, SpriteTable.Get("snowman"), x, y + 2, sc * 1.05f, Kit2D.Bottom);
                    break;
                case ObjType.Puff:
                {
                    float hop = o.HopT > 0 ? Mathf.Sin((float)(o.HopT / 0.6) * Mathf.PI) * 18 : Mathf.Abs(Mathf.Sin(t * 7 + (float)o.Ph)) * 6;
                    float b = Mathf.Abs(Mathf.Sin(t * 7 + (float)o.Ph));
                    var s = SpriteTable.Get("puff");
                    var sz = s.Size * sc * (o.HopT > 0 ? 0.9f : 1);
                    Kit2D.Put(r, s, x, y + 2 - hop * sc, sz.x * (1 + (1 - b) * 0.06f), sz.y * (1 - (1 - b) * 0.06f), Kit2D.Bottom, 0, o.Dir < 0);
                    break;
                }
                case ObjType.Ball:
                {
                    var s = SpriteTable.Get("ball");
                    float d = (float)o.Rad * 2 * sc * 1.15f;
                    var bs = s.S.bounds.size;
                    // The sheet's snowball has motion lines on its left: keep its aspect, height = diameter.
                    Kit2D.Put(r, s, x, y + 2, d * bs.x / bs.y, d, Kit2D.Bottom, 0, o.Vx < 0);
                    break;
                }
                case ObjType.Bear:
                {
                    float up = (float)o.Up;
                    float shake = o.Warn && up < 0.05f ? Mathf.Sin(t * 46 + (float)o.Ph) * 2.2f * sc : 0;
                    string role = up > 0.25f ? "bear.up" : o.Warn ? "bear.peek" : "mound";
                    var s = SpriteTable.Get(role);
                    var sz = s.Size * sc * 0.95f;
                    float pop = up > 0.25f ? 0.92f + 0.08f * Mathf.Clamp01(up) : 1;
                    Kit2D.Put(r, s, x + shake, y + 4, sz.x, sz.y * pop, Kit2D.Bottom);
                    break;
                }
                case ObjType.Coin:
                {
                    var s = SpriteTable.Get("coin");
                    float k = 0.25f + 0.75f * Mathf.Abs(Mathf.Cos(t * 4 + (float)o.Ph));
                    var sz = s.Size * sc;
                    Kit2D.Put(r, s, x, y - 14 * sc, sz.x * k, sz.y, Kit2D.Centre);
                    Kit2D.Put(v.B, new Slice { S = Placeholder2D.Shadow, Scale = 1 }, x, y, 18 * sc, 5 * sc, Kit2D.Centre);
                    v.B.sortingOrder = View2D.Shadow;
                    Kit2D.Alpha(v.B, a * 0.7f);
                    break;
                }
                case ObjType.Item:
                {
                    var s = SpriteTable.Get(v.Key);
                    float by = y - 24 * sc + Mathf.Sin(t * 3) * 4 * sc;
                    Kit2D.PutK(r, s, x, by, sc, Kit2D.Centre);
                    bool crystal = o.Kind == "crystal";
                    if (crystal)
                    {
                        float gs = 64 * sc * (1 + Mathf.Sin(t * 5) * 0.08f);
                        Kit2D.Put(v.B, new Slice { S = Placeholder2D.Halo, Scale = 1 }, x, by, gs, gs, Kit2D.Centre);
                        v.B.sortingOrder = order;
                        Kit2D.Alpha(v.B, a * 0.6f, Tex.C("#8FF3FF"));
                    }
                    else
                    {
                        Kit2D.Put(v.B, new Slice { S = Placeholder2D.Shadow, Scale = 1 }, x, y, 28 * sc, 8 * sc, Kit2D.Centre);
                        v.B.sortingOrder = View2D.Shadow;
                        Kit2D.Alpha(v.B, a * 0.8f);
                    }
                    break;
                }
                case ObjType.Sign:
                {
                    var board = new Slice { S = Placeholder2D.Plank("sign", 156, 34, "#C98A5A", Kit2D.Bottom), Scale = 1 };
                    Kit2D.Put(r, board, x, y - 62 * sc, 156 * sc, 34 * sc, Kit2D.Bottom);
                    v.L.Set("← " + o.Left + "   " + o.Right + " →", x, y - 79 * sc, 12 * sc, Color.white, a, order + 2);
                    break;
                }
            }
            r.sortingOrder = order + 1;
            Kit2D.Alpha(r, a);
        }

        // ---- gate rows ----

        RowView BuildRow(GateRow row)
        {
            int n = row.N;
            var go = new GameObject("gates");
            go.transform.SetParent(transform, false);
            var rv = new RowView
            {
                Go = go, Gates = new SpriteRenderer[n], Banners = new SpriteRenderer[n], Shapes = new SpriteRenderer[n],
                Poles = new SpriteRenderer[n + 1], Labels = new Label2D[n], Marks = new Label2D[n],
            };
            bool real = SpriteTable.Has("gate.berry");
            for (int i = 0; i < n; i++)
            {
                if (real) rv.Gates[i] = Kit2D.SR(go.transform, "gate");
                else rv.Banners[i] = Kit2D.SR(go.transform, "banner");
                if (row.Q.Options[i].Shape != null) rv.Shapes[i] = Kit2D.SR(go.transform, "shape");
                rv.Labels[i] = new Label2D(go.transform, "answer");
                rv.Marks[i] = new Label2D(go.transform, "tick");
            }
            if (!real) for (int i = 0; i <= n; i++) rv.Poles[i] = Kit2D.SR(go.transform, "pole");
            return rv;
        }

        static readonly Color Grey = new Color(0.62f, 0.66f, 0.74f);

        void PlaceRow(GateRow row, RowView rv, float y)
        {
            int n = row.N;
            float sc = View2D.Persp(y), a = View2D.FadeTop(y);
            int order = View2D.Order(y);
            float poleH = 104 * sc;
            for (int i = 0; i < n; i++)
            {
                var op = row.Q.Options[i];
                float xl = View2D.ToX(-195 + 390.0 / n * i, y), xr = View2D.ToX(-195 + 390.0 / n * (i + 1), y);
                float cx = (xl + xr) / 2, seg = xr - xl;
                string fill = op.Color ?? "#3FA9F5";
                Color tint = Color.white;
                string gate = SpriteTable.GateFor(fill);
                if (row.Done && i == row.Correct) { fill = "#2FB37A"; gate = "gate.pine"; }
                else if (row.Done && i == row.Picked) { fill = "#9AA3B8"; tint = Grey; }
                else if (row.Reveal) { if (i == row.Correct) { fill = "#FFB81F"; gate = "gate.sunny"; } else { fill = "#9AA3B8"; tint = Grey; } }
                float bannerY, bannerW, bannerH;
                if (rv.Gates[i] != null)
                {
                    var s = SpriteTable.Get(gate);
                    float gw = seg - 6 * sc, gh = Mathf.Max(112 * sc, gw * 0.6f);
                    Kit2D.Put(rv.Gates[i], s, cx, y, gw, gh, Kit2D.Bottom);
                    rv.Gates[i].sortingOrder = order + 1;
                    Kit2D.Alpha(rv.Gates[i], a, tint);
                    bannerY = y - gh * SpriteTable.GateBannerY;
                    bannerW = gw * SpriteTable.GateBannerW;
                    bannerH = gh * SpriteTable.GateBannerH;
                }
                else
                {
                    float w = Mathf.Min(seg - 26 * sc, 124 * sc), h = 58 * sc;
                    float top = y - poleH + 4 * sc;
                    Kit2D.Put(rv.Banners[i], new Slice { S = Placeholder2D.Banner, Scale = 1 }, cx, top + h / 2, w, h * 64 / 56f, Kit2D.Centre);
                    rv.Banners[i].sortingOrder = order + 2;
                    Kit2D.Alpha(rv.Banners[i], a, Tex.C(fill));
                    bannerY = top + h / 2;
                    bannerW = w;
                    bannerH = h;
                }
                if (op.Shape != null)
                {
                    Kit2D.Put(rv.Shapes[i], new Slice { S = Placeholder2D.Shape(op.Shape), Scale = 1 }, cx, bannerY, 34 * sc, 34 * sc, Kit2D.Centre);
                    rv.Shapes[i].sortingOrder = order + 3;
                    Kit2D.Alpha(rv.Shapes[i], a, Tex.C(op.ShapeColor ?? "#FFFFFF"));
                    rv.Labels[i].Show(false);
                }
                else
                {
                    string label = op.Label ?? "";
                    rv.Labels[i].Show(true);
                    // 1.5× the first pass (22 / 15 at persp 1). Label2D's cap height is the "8" glyph quad, which
                    // runs ~30 % taller than the ink, so the limits are the banner's width per glyph and its height.
                    float cap = (label.Length > 3 ? 22 : 33) * sc;
                    cap = Mathf.Min(cap, bannerW / Mathf.Max(1, label.Length) * 1.25f, bannerH * 1.15f);
                    rv.Labels[i].Set(label, cx, bannerY, cap, Color.white, a, order + 4);
                }
                bool tick = (row.Done || row.Reveal) && i == row.Correct;
                rv.Marks[i].Show(tick);
                if (tick) rv.Marks[i].Set("✓", cx + bannerW / 2 - 4 * sc, bannerY - 26 * sc, 18 * sc, Tex.C("#7DE0A8"), a, order + 6);
            }
            for (int i = 0; i <= n; i++)
            {
                if (rv.Poles[i] == null) continue;
                float px = View2D.ToX(-195 + 390.0 / n * i, y);
                if (i == 0) px += 14 * sc; else if (i == n) px -= 14 * sc;
                Kit2D.Put(rv.Poles[i], new Slice { S = Placeholder2D.Pole, Scale = 1 }, px, y, 12 * sc, 106 * sc, Kit2D.Bottom);
                rv.Poles[i].sortingOrder = order + 1;
                Kit2D.Alpha(rv.Poles[i], a);
            }
        }

        // ---- START / FINISH ----

        ArchView BuildArch(string label)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var av = new ArchView { Go = go, L = new Label2D(go.transform, "label") };
            if (SpriteTable.Has("arch")) av.Arch = Kit2D.SR(go.transform, "arch");
            else
            {
                av.PostL = Kit2D.SR(go.transform, "post");
                av.PostR = Kit2D.SR(go.transform, "post");
                av.Board = Kit2D.SR(go.transform, "board");
            }
            av.L.Set(label, 0, 0, 10, Color.white, 1, 0);
            av.WordRole = label == "START" ? "ui.start" : "ui.finish";
            if (SpriteTable.Has(av.WordRole))
            {
                av.Word = Kit2D.SR(go.transform, "lettering");
                av.L.Show(false);
            }
            return av;
        }

        void PlaceArch(ArchView av, float y, bool show, string color)
        {
            if (av.Go.activeSelf != show) av.Go.SetActive(show);
            if (!show) return;
            float sc = View2D.Persp(y), a = View2D.FadeTop(y);
            int order = View2D.Order(y);
            float xL = View2D.ToX(-150, y), xR = View2D.ToX(150, y);
            string label = av == start ? "START" : "FINISH";
            float labelY;
            if (av.Arch != null)
            {
                var s = SpriteTable.Get("arch");
                float w = xR - xL + 40 * sc;
                // The sheet's arch is taller than the web's 120 + board: squash it to ~165 so its top stays
                // on screen (and under the crest) when it stands just behind Pip at the start.
                float h = Mathf.Min(w * s.S.bounds.size.y / s.S.bounds.size.x, 165 * sc);
                Kit2D.Put(av.Arch, s, (xL + xR) / 2, y, w, h, Kit2D.Bottom);
                av.Arch.sortingOrder = order + 1;
                Kit2D.Alpha(av.Arch, a);
                labelY = y - h * SpriteTable.ArchBannerY;
            }
            else
            {
                float h = 120 * sc;
                var post = new Slice { S = Placeholder2D.Plank("post", 14, 120, "#B9774A", Kit2D.Bottom), Scale = 1 };
                Kit2D.Put(av.PostL, post, xL, y, 14 * sc, h, Kit2D.Bottom);
                Kit2D.Put(av.PostR, post, xR, y, 14 * sc, h, Kit2D.Bottom);
                var board = new Slice { S = Placeholder2D.Plank("board", 340, 40, "#FFF4E0", Kit2D.Bottom), Scale = 1 };
                Kit2D.Put(av.Board, board, (xL + xR) / 2, y - h + 10 * sc, xR - xL + 20 * sc, 40 * sc, Kit2D.Bottom);
                foreach (var r in new[] { av.PostL, av.PostR, av.Board }) { r.sortingOrder = order + 1; Kit2D.Alpha(r, a); }
                labelY = y - h - 10 * sc;
            }
            av.L.Show(ArchWords && av.Word == null);
            if (av.Word != null && av.Word.gameObject.activeSelf != ArchWords) av.Word.gameObject.SetActive(ArchWords);
            if (!ArchWords) return;
            if (av.Word != null)
            {
                // The chunky gold lettering, as wide as the banner, centred on it (keeps its own aspect).
                var ws = SpriteTable.Get(av.WordRole);
                float ww = (xR - xL + 40 * sc) * SpriteTable.ArchBannerW;
                float wh = ww * ws.S.bounds.size.y / ws.S.bounds.size.x;
                Kit2D.Put(av.Word, ws, (xL + xR) / 2, labelY, ww, wh, Kit2D.Centre);
                av.Word.sortingOrder = order + 3;
                Kit2D.Alpha(av.Word, a);
            }
            else av.L.Set(label, (xL + xR) / 2, labelY, 20 * sc, Tex.C(color), a, order + 3);
        }
    }
}
