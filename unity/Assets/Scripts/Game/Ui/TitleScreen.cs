// Title screen (#screen-title): the hopping ice-crystal sign of "Pippy The Ski Penguin" (t1; drawn letters
// as the fallback) over the idling
// Pippy (Pip2D wave sprite), then the sheet —
// play (next unfinished level), daily endless with the best distance, level map with the star
// count, and the settings chips (speed, music, sound, graphics). Saved settings live in Settings.
using System;
using System.Collections.Generic;
using System.Globalization;
using Pip.Core;
using Pip.Game.Art2D;
using UnityEngine;
using UnityEngine.UI;

namespace Pip.Game.Ui
{
    public sealed class TitleScreen : MonoBehaviour
    {
        public event Action Play, Endless, Map;
        public event Action<Settings> SettingsChanged;

        sealed class Chip
        {
            public RRect Fill;
            public Text Label;
            public GameText Outline;
            public Image Img; // ice kit chip (sprite swapped on / off), else null
            public Func<Settings, bool> On;
            public Action<Settings> Toggle;
        }

        readonly List<Chip> chips = new List<Chip>();
        Settings settings;
        Text endlessLabel, endlessBest, mapLabel, mapCount;
        RectTransform endlessRow, mapRow, countChip;

        public static TitleScreen Build(RectTransform safe)
        {
            var root = K.Fill(safe, "Title");
            var t = root.gameObject.AddComponent<TitleScreen>();
            t.BuildLogo(root);
            t.BuildSheet(root);
            return t;
        }

        void BuildLogo(RectTransform root)
        {
            // The ice-crystal title sign (t1, 7 Nov 2026) when it is imported, else the drawn letters.
            var sign = SpriteTable.Logo();
            float taglineY = sign != null ? BuildSign(root, sign) : BuildLetters(root);

            var ribbonText = "คิดไว · เลี้ยวตอบ!";
            var rib = K.Node(root, "tagline", new Vector2(0.5f, 1), new Vector2(0, taglineY), new Vector2(200, 36));
            var face = K.IceSlice(rib, "ui.tagline", 36, out _) ?? K.Block(rib, new Grad((0, "#8FD8FF"), (1, "#2A8FDA")), 12, 3, 4, out _);
            var label = K.CentreBody(K.Outlined(face, ribbonText, 17));
            rib.sizeDelta = new Vector2(K.Width(label) + 46, 36);
        }

        /// <summary>The t1 sign (gold "Pippy" + "The Ski Penguin" baked into the ice), hopping gently as a
        /// whole; returns the tagline ribbon's y, which tucks in front of the sign's lower crystal edge.</summary>
        float BuildSign(RectTransform root, Sprite sp)
        {
            const float w = 340;
            float h = w * sp.rect.height / sp.rect.width;
            var rt = K.Node(root, "logo", new Vector2(0.5f, 1), new Vector2(0, 2), new Vector2(w, h));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sp;
            img.preserveAspect = true;
            img.raycastTarget = false;
            var bob = rt.gameObject.AddComponent<Bob>(); // after the position is set: Bob keeps it as home
            bob.Period = 2.4f;
            bob.Lift = 6;
            bob.Tilt = -1.5f;
            return -172; // 7 Nov 2026: 22 lower than first set (the top read crowded), under the slab's body
        }

        /// <summary>Fallback when t1 is missing: the drawn letters; returns the tagline ribbon's y.</summary>
        float BuildLetters(RectTransform root)
        {
            // "Pippy" with the three colours cycled over the five letters, each letter hopping in turn;
            // "The Ski Penguin" under it (29 pt so its ink stays clear of the arch bunting).
            var logo = K.Node(root, "logo", new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(358, 150));
            string[] letters = { "P", "i", "p", "p", "y" };
            string[] colors = { "#FF5C93", "#FFC93C", "#3FA9F5" };
            int n = letters.Length;
            var texts = new Text[n];
            float total = 0;
            for (int i = 0; i < n; i++)
            {
                var slot = K.Node(logo, "letter " + i + " " + letters[i], new Vector2(0.5f, 1), Vector2.zero, new Vector2(80, 84));
                var tx = K.Label(slot, letters[i], 78, Look.Hex(colors[i % colors.Length]), TextAnchor.UpperCenter, true);
                var g = tx.gameObject.AddComponent<GameText>();
                g.TwoTone = false;
                g.Stroke = 4;
                g.Drop = 5; // the logo keeps its ink drop (the rest of the lettering has none)
                texts[i] = tx;
                total += K.Width(tx);
            }
            float x = -total / 2;
            for (int i = 0; i < n; i++)
            {
                float w = K.Width(texts[i]);
                var slot = (RectTransform)texts[i].transform.parent;
                slot.sizeDelta = new Vector2(w + 12, 84);
                slot.anchoredPosition = new Vector2(x + w / 2, 0);
                x += w;
                var bob = slot.gameObject.AddComponent<Bob>();
                bob.Delay = i * 0.12f;
            }
            var sub = K.Node(logo, "subtitle", new Vector2(0.5f, 1), new Vector2(0, -82), new Vector2(358, 44)); // 4 pt lower than under "Pip": keeps the p / p / y descenders clear of the subtitle
            K.Outlined(sub, "The Ski Penguin", 29, 3.5f, 5);
            return -138;
        }

        void BuildSheet(RectTransform root)
        {
            float y = 10;
            // Controls help (keyboard line only off-phone).
            string help = "แตะค้างแล้วเลื่อนซ้าย-ขวา = เลี้ยว · แตะค้างนิ่งๆ = เร่ง · แตะสั้นหรือปัดขึ้น = กระโดด · แตะซ้ำกลางอากาศ = ตีลังกา";
            if (!Application.isMobilePlatform) help += "\nคีย์บอร์ด: ← → เลี้ยว · Shift เร่ง · Space กระโดด · P หยุด";
            var helpRt = K.Node(root, "help", new Vector2(0.5f, 0), new Vector2(0, y), new Vector2(358, 40));
            var helpBg = K.Round(helpRt, new Color(1, 1, 1, 0.8f), 12);
            var helpText = K.Label(helpRt, help, 12, K.InkSoft);
            helpText.fontStyle = FontStyle.Normal;
            helpText.horizontalOverflow = HorizontalWrapMode.Wrap;
            helpText.lineSpacing = 1.1f;
            helpText.rectTransform.offsetMin = new Vector2(12, 3);
            helpText.rectTransform.offsetMax = new Vector2(-12, -3);
            float hh = Mathf.Ceil(helpText.preferredHeight) + 8;
            helpRt.sizeDelta = new Vector2(358, hh);
            y += hh + 12;

            // Settings: speed on one row, music / sound / graphics on the next.
            var row2 = Row(root, ref y);
            AddChip(row2, "เพลง", s => s.Music, s => s.Music = !s.Music);
            AddChip(row2, "เสียง", s => s.Sfx, s => s.Sfx = !s.Sfx);
            AddChip(row2, "ภาพสูง", s => s.HighGfx, s => s.HighGfx = !s.HighGfx);
            Center(row2);
            y -= 4; // the two chip rows sit 8 apart
            var row1 = Row(root, ref y);
            var speedLabel = K.Label(K.Node(row1, "speed label", new Vector2(0, 0.5f), Vector2.zero, new Vector2(80, 34)), "ความเร็ว", 13, K.InkSoft, TextAnchor.MiddleLeft, true);
            ((RectTransform)speedLabel.transform.parent).sizeDelta = new Vector2(K.Width(speedLabel), 34);
            AddChip(row1, "ช้า", s => Math.Abs(s.Speed - 0.85) < 0.01, s => s.Speed = 0.85);
            AddChip(row1, "ปกติ", s => Math.Abs(s.Speed - 1) < 0.01, s => s.Speed = 1);
            AddChip(row1, "เร็ว", s => Math.Abs(s.Speed - 1.15) < 0.01, s => s.Speed = 1.15);
            Center(row1);

            // Level map with the star count.
            var mapBtn = K.Node(root, "map", new Vector2(0.5f, 0), new Vector2(0, y), new Vector2(358, 56));
            K.IceButton(mapBtn, () => Map?.Invoke(), out var mapContent);
            mapRow = K.Node(mapContent, "row", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 32));
            var mapIcon = K.MapIcon(mapRow);
            K.Place(mapIcon, new Vector2(0, 0.5f), Vector2.zero, new Vector2(30, 30));
            mapLabel = K.CentreBody(K.Label(K.Node(mapRow, "label", new Vector2(0, 0.5f), new Vector2(38, 0), new Vector2(100, 32)), "แผนที่ด่าน", 19, K.Ink, TextAnchor.MiddleLeft, true));
            countChip = K.Node(mapRow, "count", new Vector2(0, 0.5f), Vector2.zero, new Vector2(70, 26));
            // v6 ice kit: the blue ice pill, the star and the count on its face under the snow cap.
            var countFace = K.IceSlice(countChip, "ui.icePill", 26, out _);
            bool icePill = countFace != null;
            if (!icePill)
            {
                countFace = countChip;
                if (K.Sliced(countChip, "ui.pill", 26) == null)
                {
                    K.Round(countChip, K.Ink, 13);
                    K.Round(countChip, new Color(1, 1, 1, 0.8f), 11, 2, 2, 2, 2);
                }
            }
            var star = K.StarIcon(countFace);
            K.Place(star, new Vector2(0, 0.5f), new Vector2(6, 0), new Vector2(18, 18));
            mapCount = K.CentreBody(K.Label(K.Node(countFace, "n", new Vector2(0, 0.5f), new Vector2(27, 0), new Vector2(50, 26)), "0/18", 15, K.Ink, TextAnchor.MiddleLeft, true));
            if (icePill)
            {
                mapCount.color = Color.white;
                var cg = mapCount.GetComponent<GameText>();
                cg.Stroke = 2;
                cg.Dirs = 12;
                cg.Ink = K.Navy;
            }
            y += 56 + 12;

            // Daily endless with the player's best distance.
            var endless = K.Node(root, "endless", new Vector2(0.5f, 0), new Vector2(0, y), new Vector2(358, 56));
            K.BigButton(endless, true, () => Endless?.Invoke(), out var endContent);
            endlessRow = K.Node(endContent, "row", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 40));
            endlessLabel = K.Outlined(K.Node(endlessRow, "label", new Vector2(0, 0.5f), Vector2.zero, new Vector2(120, 40)), "ไถลไม่รู้จบ", 22);
            endlessLabel.alignment = TextAnchor.MiddleLeft;
            K.CentreBody(endlessLabel);
            endlessBest = K.Outlined(K.Node(endlessRow, "best", new Vector2(0, 0.5f), Vector2.zero, new Vector2(120, 24)), "ยังไม่มีสถิติ", 12, 1.5f, 0);
            endlessBest.alignment = TextAnchor.MiddleLeft;
            K.CentreBody(endlessBest);
            y += 56 + 12;

            // Play: the next level still short of three stars.
            var play = K.Node(root, "play", new Vector2(0.5f, 0), new Vector2(0, y), new Vector2(358, 64));
            K.BigButton(play, false, () => Play?.Invoke(), out var playContent);
            K.IconLabel(playContent, K.PlayIcon(playContent), 26, "เล่นเลย!", 27, Color.white, 10, true);
        }

        RectTransform Row(RectTransform root, ref float y)
        {
            var r = K.Node(root, "chips", new Vector2(0.5f, 0), new Vector2(0, y), new Vector2(358, 34));
            y += 34 + 12;
            return r;
        }

        void AddChip(RectTransform row, string label, Func<Settings, bool> on, Action<Settings> toggle)
        {
            var rt = K.Node(row, label, new Vector2(0, 0.5f), Vector2.zero, new Vector2(60, 34));
            var c = new Chip { On = on, Toggle = toggle };
            RectTransform face, press;
            if (SpriteTable.Has("ui.chipOn") && SpriteTable.Has("ui.chipOff"))
            {
                press = K.Fill(rt, "lift");
                face = K.IceSlice(press, "ui.chipOff", 34, out c.Img);
                c.Img.raycastTarget = true;
            }
            else face = press = K.Block(rt, K.Toy, 17, 3, 3, out c.Fill);
            c.Label = K.Label(face, label, 13, K.Ink, TextAnchor.MiddleCenter, true);
            c.Outline = c.Label.gameObject.AddComponent<GameText>();
            c.Outline.TwoTone = false;
            c.Outline.Stroke = 1.2f;
            c.Outline.Drop = 0;
            c.Outline.Dirs = 10;
            K.CentreBody(c.Label);
            rt.sizeDelta = new Vector2(K.Width(c.Label) + 26, 34);
            var btn = K.Button(rt, press, c.Fill, () =>
            {
                c.Toggle(settings);
                Refresh(null, settings);
                SettingsChanged?.Invoke(settings);
            });
            if (c.Img) btn.targetGraphic = c.Img;
            chips.Add(c);
        }

        static void Center(RectTransform row)
        {
            const float gap = 8;
            float w = -gap;
            foreach (RectTransform ch in row) w += ch.sizeDelta.x + gap;
            float x = (row.sizeDelta.x - w) / 2;
            foreach (RectTransform ch in row)
            {
                ch.anchoredPosition = new Vector2(x, 0);
                x += ch.sizeDelta.x + gap;
            }
        }

        /// <summary>Re-reads the best distance, star total and setting chips.</summary>
        public void Refresh(Progress p, Settings s)
        {
            settings = s;
            foreach (var c in chips)
            {
                bool on = c.On(s);
                if (c.Img) c.Img.sprite = SpriteTable.Ui(on ? "ui.chipOn" : "ui.chipOff");
                else c.Fill.Set(on ? K.ChipOn : K.Toy, c.Fill.Radius);
                c.Label.color = on ? Color.white : K.Ink;
                c.Outline.enabled = on;
            }
            if (p == null) return;
            endlessBest.text = p.EndlessBest > 0 ? "สถิติของคุณ " + p.EndlessBest.ToString("N0", CultureInfo.InvariantCulture) + " ม." : "ยังไม่มีสถิติ";
            float lw = K.Width(endlessLabel), bw = K.Width(endlessBest);
            ((RectTransform)endlessLabel.transform.parent).sizeDelta = new Vector2(lw + 4, 40);
            var bestRt = (RectTransform)endlessBest.transform.parent;
            bestRt.sizeDelta = new Vector2(bw + 4, 24);
            bestRt.anchoredPosition = new Vector2(lw + 8, -2);
            endlessRow.sizeDelta = new Vector2(lw + 8 + bw, 40);

            mapCount.text = p.TotalStars + "/" + Progress.MaxStars;
            float mw = K.Width(mapLabel), cw = K.Width(mapCount);
            ((RectTransform)mapLabel.transform.parent).sizeDelta = new Vector2(mw + 2, 32);
            countChip.sizeDelta = new Vector2(27 + cw + 14, 26);
            countChip.anchoredPosition = new Vector2(38 + mw + 12, 0);
            mapRow.sizeDelta = new Vector2(38 + mw + 12 + countChip.sizeDelta.x, 32);
        }
    }
}
