// HUD and in-run popups in UGUI, built in code with the toy UI kit to match index.html: top bar
// (pause, hearts, timer plank, snowflake count), progress track with Benny's marker and the finish
// flag, power-up pill, the boost pill (Unity-only), question card with its countdown ring, toast, the
// coin "+N" streak, and the intro, pause and result panels. The title and map screens share this canvas.
// v5 art (5 Nov 2026): the pause button, hearts, timer plank, coin pill, progress track / fill / markers /
// flag, panel windows, ribbons, buttons, result stars, score plaque and the new-best badge are the u2 / u3
// sprite slices (SpriteTable ui.*); every piece falls back to the procedural toy widget without its slice.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Pip.Core;
using Pip.Game.Art2D;
using Pip.Game.Ui;
using UnityEngine;
using UnityEngine.UI;

namespace Pip.Game
{
    public sealed class Hud : MonoBehaviour
    {
        public event Action Pause, Resume, Restart, Home, IntroBack, ResultMap, Retry, Next;
        public event Action<string> Sfx;
        /// <summary>A result star (0–2) just landed.</summary>
        public event Action<int> StarLanded;

        public RectTransform CanvasRoot { get; private set; }
        readonly List<RectTransform> safeRects = new List<RectTransform>();
        RectTransform safe;

        // In-run HUD
        GameObject run;
        Vec[] hearts;
        Image[] heartImgs;
        int shownHearts = -1;
        RectTransform plank, coinChip;
        Text timer, coins;
        float chipBump = -9;
        GameObject prog;
        RectTransform progFill, progMe, progRival, progBar;
        Image progFillImg;
        RectTransform pupsRt;
        Text pups;
        RectTransform boostRt;
        PopIn boostPop;
        GameObject qCard;
        Ring qRing;
        Text qHint, qText;
        Text toast;
        float toastT = -9;
        Text coinPop;
        float coinPopLast = -9, coinPopBorn;
        int coinPopN;
        bool coinPopLeft;

        // Intro
        GameObject intro;
        K.PanelParts introPanel;
        Text introBadge, introTip;
        RectTransform introBadgeRt, introGoals;

        // Pause
        GameObject pause;

        // Result
        GameObject result;
        K.PanelParts resPanel;
        GameObject resStars, resFail, resTotalRow;
        RectTransform resRows;
        StarPop[] starPops;
        Text failPct, failTip, resTotal;
        RectTransform failFill, failMe, failRow, totalRow, failBar;
        Image failFillImg;
        GameObject resBadge;
        Text failA, failB, totalLabel;
        GameObject retryIcon, retryBig, nextBtn;
        float resT0, failTarget, countTotal, countDelay, lastTick;
        bool counting;

        /// <summary>Gold stripes of both progress bars (run HUD and the fail result), user request 5 Nov 2026.</summary>
        const string ProgGoldA = "#FFD45A", ProgGoldB = "#F7B52C";

        /// <summary>Body text font, for the 2D gate / arch / sign labels too (Kit2D.LabelFont).</summary>
        public static Font ThaiFont() => K.Body;

        void Awake()
        {
            var go = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            // Screen Space Overlay: drawn after the 2D camera, independent of its zoom and shake.
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390, 844);
            scaler.matchWidthOrHeight = 0;
            CanvasRoot = (RectTransform)go.transform;
            safe = SafeArea(CanvasRoot);

            run = K.Fill(safe, "run").gameObject;
            BuildTopBar();
            BuildProgress();
            BuildQuestion();
            BuildBoost();
            toast = K.Label(K.Node(run.transform, "toast", new Vector2(0.5f, 0.78f), Vector2.zero, new Vector2(380, 50)), "", 30, Color.white, TextAnchor.MiddleCenter, true);
            var tg = toast.gameObject.AddComponent<GameText>();
            tg.TwoTone = false;
            tg.Stroke = 3;
            tg.Drop = 0;
            var cp = K.Node(run.transform, "coin pop", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 44));
            coinPop = K.Gtext(cp, "", 28, 3, 0, TextAnchor.MiddleLeft);
            coinPop.GetComponent<GameText>().SetGold();
            cp.gameObject.SetActive(false);

            BuildIntro();
            BuildPause();
            BuildResult();
            ShowRun(false);
        }

        /// <summary>A child of parent that tracks the screen's safe area (clear of the notch / Dynamic Island).</summary>
        public RectTransform SafeArea(Transform parent)
        {
            var rt = K.Fill(parent, "safe area");
            safeRects.Add(rt);
            lastSafe = default;
            FitSafeArea();
            return rt;
        }

        Rect lastSafe;

        void FitSafeArea()
        {
            var sa = Screen.safeArea;
            if (sa == lastSafe || Screen.width == 0) return;
            lastSafe = sa;
            foreach (var rt in safeRects)
            {
                rt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
                rt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
        }

        // ---------- build: in-run HUD ----------

        void BuildTopBar()
        {
            var pauseBtn = K.Node(run.transform, "pause", new Vector2(0, 1), new Vector2(14, -8), new Vector2(44, 44));
            K.RoundButton(pauseBtn, () => Pause?.Invoke(), out var pc, "ui.pause");
            K.Place(K.PauseIcon(pc), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 18));

            hearts = new Vec[3];
            if (SpriteTable.Has("ui.heart") && SpriteTable.Has("ui.heartOff"))
            {
                heartImgs = new Image[3];
                for (int i = 0; i < 3; i++)
                    heartImgs[i] = K.Sprite(K.Node(run.transform, "heart", new Vector2(0, 1), new Vector2(64 + i * 31, -13), new Vector2(32, 32)), "ui.heart");
            }
            else for (int i = 0; i < 3; i++)
            {
                var v = K.Icon(run.transform, "heart");
                v.Add(Shape.Path(K.HeartPath).WithFill("#FF4F6D").WithStroke("#2B2140", 2));
                K.Place(v, new Vector2(0, 1), new Vector2(66 + i * 30, -15), new Vector2(30, 30));
                hearts[i] = v;
            }

            coinChip = K.Node(run.transform, "coins", new Vector2(1, 1), new Vector2(-14, -10), new Vector2(80, 40));
            if (K.Sliced(coinChip, "ui.pill", 40) != null && SpriteTable.Has("ui.coin"))
            {
                // Navy pill with the gold snowflake coin over its left end; white numerals, navy outline.
                K.Sprite(K.Node(coinChip, "coin", new Vector2(0, 0.5f), new Vector2(-3, 1), new Vector2(34, 34)), "ui.coin");
                coins = Lettering(K.Node(coinChip, "n", new Vector2(0, 0.5f), new Vector2(34, 1), new Vector2(60, 34)), "", 19, TextAnchor.MiddleLeft);
            }
            else
            {
                var face = K.Block(coinChip, Grad.Solid("#E4F6FF"), 20, 3, 4, out var chipFill);
                chipFill.raycastTarget = false;
                K.Place(K.CoinIcon(face), new Vector2(0, 0.5f), new Vector2(1, 0), new Vector2(28, 28));
                coins = K.Label(K.Node(face, "n", new Vector2(0, 0.5f), new Vector2(33, 0), new Vector2(60, 34)), "", 19, K.Ink, TextAnchor.MiddleLeft, true);
            }

            plank = K.Node(run.transform, "timer", new Vector2(1, 1), new Vector2(-110, -10), new Vector2(70, 40));
            if (K.Sliced(plank, "ui.plank", 40) != null)
            {
                var box = K.Fill(plank, "time", 0, 2, 0, 0);
                timer = Lettering(box, "", 19);
            }
            else
            {
                var pf = K.Block(plank, Grad.Solid("#C98A5A"), 12, 3, 4, out var wood);
                wood.StripePeriod = 21;
                wood.StripeWidth = 3;
                wood.StripeColor = Look.Hex("#BC7D4E");
                wood.raycastTarget = false;
                timer = K.Outlined(pf, "", 19, 2, 0);
            }
            SetTimer("0:00");
            SetCoins(0);
        }

        /// <summary>HUD numerals on the sprite widgets: white, navy outline 2.5, no drop.</summary>
        static Text Lettering(Transform parent, string s, int size, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var t = K.Outlined(parent, s, size, 2.5f, 0, align);
            t.GetComponent<GameText>().Ink = K.Navy;
            return t;
        }

        static bool SpriteBars => SpriteTable.Has("ui.track") && SpriteTable.Has("ui.fill") && SpriteTable.Has("ui.markerMe") && SpriteTable.Has("ui.markerRival") && SpriteTable.Has("ui.flag");

        /// <summary>v5 progress bar: the u2 track, the gold fill slice (full track length) under a mask that
        /// grows with progress, the gold stripes at 35 % on the fill, the flag at the end. Returns the inner
        /// rect the markers travel along.</summary>
        static RectTransform SpriteBar(RectTransform track, float h, out RectTransform mask, out Image fill)
        {
            K.Sliced(track, "ui.track", h);
            var inner = K.Fill(track, "inner", h * 0.4f, 0, h * 0.4f, 0);
            mask = K.Node(track, "fill", new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, h));
            var mg = mask.gameObject.AddComponent<RRect>();
            mg.Radius = h / 2;
            mg.raycastTarget = false;
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            fill = K.Sliced(mask, "ui.fill", h);
            var frt = fill.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = new Vector2(0, 1);
            frt.pivot = new Vector2(0, 0.5f);
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;
            var st = K.Fill(frt, "stripes", h * 0.3f, h * 0.3f, h * 0.3f, h * 0.25f).gameObject.AddComponent<Stripes>();
            st.A = Look.Hex(ProgGoldA);
            st.B = Look.Hex(ProgGoldB);
            st.color = new Color(1, 1, 1, 0.35f);
            st.raycastTarget = false;
            K.Sprite(K.Node(track, "flag", new Vector2(1, 1), new Vector2(h * 1.6f, h * 1.2f), new Vector2(h * 1.6f, h * 2.1f)), "ui.flag");
            return inner;
        }

        /// <summary>Sizes a sprite bar's fill to progress k of the inner travel (the mask reaches the marker centre).</summary>
        static void SpriteBarSet(RectTransform inner, RectTransform mask, Image fill, float k)
        {
            var track = (RectTransform)mask.parent;
            float w = inner.rect.width, h = track.rect.height;
            mask.sizeDelta = new Vector2(inner.offsetMin.x + w * k, h);
            fill.rectTransform.sizeDelta = new Vector2(track.rect.width, 0);
        }

        static RectTransform SpriteMarker(RectTransform inner, float size, string role)
        {
            var m = K.Node(inner, "marker", new Vector2(0, 0.5f), Vector2.zero, new Vector2(size, size));
            m.pivot = new Vector2(0.5f, 0.5f);
            K.Sprite(m, role);
            return m;
        }

        void BuildProgress()
        {
            if (SpriteBars)
            {
                var sp = K.Node(run.transform, "progress", new Vector2(0, 1), Vector2.zero, new Vector2(0, 18));
                sp.anchorMin = new Vector2(0, 1);
                sp.anchorMax = new Vector2(1, 1);
                sp.offsetMin = new Vector2(22, -66 - 18);
                sp.offsetMax = new Vector2(-40, -66);
                prog = sp.gameObject;
                progBar = SpriteBar(sp, 18, out progFill, out progFillImg);
                progRival = SpriteMarker(progBar, 20, "ui.markerRival");
                progMe = SpriteMarker(progBar, 26, "ui.markerMe");
                BuildPups();
                return;
            }
            var p = K.Node(run.transform, "progress", new Vector2(0, 1), Vector2.zero, new Vector2(0, 16));
            p.anchorMin = new Vector2(0, 1);
            p.anchorMax = new Vector2(1, 1);
            p.offsetMin = new Vector2(22, -66 - 16);
            p.offsetMax = new Vector2(-40, -66);
            prog = p.gameObject;
            K.Round(p, K.Ink, 8);
            var inner = K.Round(p, Color.white, 5, 3, 3, 3, 3).rectTransform;
            progFill = K.Node(inner, "fill", new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, 10));
            var mask = progFill.gameObject.AddComponent<RRect>();
            mask.Radius = 5;
            mask.raycastTarget = false;
            progFill.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var ps = K.Fill(progFill, "stripes").gameObject.AddComponent<Stripes>();
            ps.A = Look.Hex(ProgGoldA);
            ps.B = Look.Hex(ProgGoldB);
            ps.raycastTarget = false;
            K.Place(K.FlagIcon(inner), new Vector2(1, 1), new Vector2(28, 18), new Vector2(26, 32));
            progRival = Marker(inner, 18, "#8B5A3C");
            progMe = Marker(inner, 26, "#2B3A67");
            progBar = inner;
            BuildPups();
        }

        void BuildPups()
        {
            pupsRt = K.Node(run.transform, "power-ups", new Vector2(0.5f, 1), new Vector2(0, -88), new Vector2(120, 22));
            K.Round(pupsRt, new Color(43 / 255f, 33 / 255f, 64 / 255f, 0.8f), 11);
            pups = K.Label(pupsRt, "", 13, Color.white);
            pups.fontStyle = FontStyle.Normal;
        }

        static RectTransform Marker(RectTransform track, float size, string fill)
        {
            var m = K.Node(track, "marker", new Vector2(0, 0.5f), Vector2.zero, new Vector2(size, size));
            m.pivot = new Vector2(0.5f, 0.5f);
            K.Round(m, K.Ink, size / 2);
            K.Round(m, Look.Hex(fill), size / 2 - 3, 3, 3, 3, 3);
            return m;
        }

        void BuildQuestion()
        {
            var q = K.Node(run.transform, "question", new Vector2(0.5f, 1), new Vector2(0, -92), new Vector2(354, QCardH));
            qCard = q.gameObject;
            qCardRt = q;
            var face = K.Block(q, new Grad((0, "#F4FBFF"), (1, "#CDEBFF")), 24, 4, 6, out var fill);
            fill.raycastTarget = false;
            var dial = K.Icon(face, "dial", new Rect(0, 0, 60, 60));
            dial.Add(Shape.Circle(30, 30, 25).WithFill("#FFFFFF").WithStroke("#2B2140", 3));
            // Pinned to the top (the card grows downward for a two-line question; the ring stays put).
            K.Place(dial, new Vector2(0, 1), new Vector2(14, -9), new Vector2(56, 56));
            var ring = K.Node(dial.transform, "ring", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(53.2f, 53.2f));
            qRing = ring.gameObject.AddComponent<Ring>();
            qRing.Width = 6.5f;
            qRing.color = Look.Hex("#FF5C93");
            qRing.raycastTarget = false;
            var col = K.Node(face, "text", new Vector2(0, 1), new Vector2(84, -8), new Vector2(QTextW, 58));
            qHint = K.Label(K.Node(col, "hint", new Vector2(0, 1), Vector2.zero, new Vector2(QTextW, 20)), "เลี้ยวผ่านประตูคำตอบ!", 13, K.InkSoft, TextAnchor.MiddleLeft);
            qText = K.Label(K.Node(col, "q", new Vector2(0, 1), new Vector2(0, -20), new Vector2(QTextW, QLineH)), "?", 34, K.Ink, TextAnchor.MiddleLeft, true);
            var pop = q.gameObject.AddComponent<PopIn>();
            pop.FromScale = 0.6f;
            pop.FromY = 0;
            pop.Dur = 0.35f;
            qCard.SetActive(false);
        }

        // Question card: 354 × 82 for a one-line question; the text column starts at 84 and keeps 16 clear
        // of the card's right edge (4-unit border included).
        const float QCardH = 82, QTextW = 354 - 4 - 84 - 16, QLineH = 38;
        RectTransform qCardRt;

        /// <summary>Fits the question into the card's text column: the largest size from 34 down to 20 on
        /// one line (a taller card would cover Pip's head), else two lines (broken at a space or a Thai word boundary near the middle) from 26 down
        /// to 16, the card growing by the extra line. The hint shrinks from 13 to 10 if it has to.</summary>
        void FitQuestion(string text)
        {
            qText.text = text;
            int size = 34;
            while (size > 20 && QWidth(size) > QTextW) size--;
            float qh = QLineH;
            if (QWidth(size) > QTextW)
            {
                qText.text = BreakInTwo(text);
                for (size = 26; size > 16 && QWidth(size) > QTextW; size--) { }
                qText.fontSize = size;
                qh = Mathf.Max(QLineH, Mathf.Ceil(qText.preferredHeight) + 4);
            }
            qText.fontSize = size;
            ((RectTransform)qText.transform.parent).sizeDelta = new Vector2(QTextW, qh);
            qCardRt.sizeDelta = new Vector2(qCardRt.sizeDelta.x, QCardH + qh - QLineH);
            int hs = 13;
            qHint.fontSize = hs;
            while (hs > 10 && qHint.preferredWidth > QTextW) qHint.fontSize = --hs;
        }

        float QWidth(int size)
        {
            qText.fontSize = size;
            return qText.preferredWidth;
        }

        // Thai has no spaces between words: break after one of these when there is no space near the middle.
        static readonly string[] ThaiBreakAfter = { "ไหน", "เป็น", "รูป", "ไม่", "ที่สุด" };

        /// <summary>Two lines: the space or Thai word boundary nearest the middle; else the nearest point
        /// that doesn't split a Thai syllable (no mark at the start of line 2, no leading vowel at the end
        /// of line 1).</summary>
        static string BreakInTwo(string s)
        {
            int mid = s.Length / 2, best = -1;
            void Try(int at) { if (at > 0 && at < s.Length && (best < 0 || Mathf.Abs(at - mid) < Mathf.Abs(best - mid))) best = at; }
            for (int i = 0; i < s.Length; i++) if (s[i] == ' ') Try(i);
            if (best >= 0) return s.Substring(0, best) + "\n" + s.Substring(best + 1);
            foreach (var w in ThaiBreakAfter)
                for (int i = s.IndexOf(w, StringComparison.Ordinal); i >= 0; i = s.IndexOf(w, i + 1, StringComparison.Ordinal)) Try(i + w.Length);
            if (best < 0)
                for (int i = 1; i < s.Length; i++)
                {
                    char a = s[i - 1], b = s[i];
                    bool mark = b == '\u0E31' || (b >= '\u0E34' && b <= '\u0E3A') || (b >= '\u0E47' && b <= '\u0E4E') || b == '\u0E33';
                    bool lead = a >= '\u0E40' && a <= '\u0E44';
                    if (!mark && !lead) Try(i);
                }
            return best < 0 ? s : s.Substring(0, best) + "\n" + s.Substring(best);
        }

        // Right end of the power-ups row: the question card's empty top-right corner (card 354 wide).
        const float BoostRight = 162;

        /// <summary>"»» เร่ง" in berry on an ice chip, popped in while boost is up. It sits at the right end
        /// of the power-ups row and is built after the question card so it stays on top of the card's
        /// corner: at the gates is exactly when the player should see it and let go.</summary>
        void BuildBoost()
        {
            const string berry = "#E23C78"; // --berry-deep: berry that still reads on the ice
            boostRt = K.Node(run.transform, "boost", new Vector2(0.5f, 1), new Vector2(0, -86), new Vector2(70, 26));
            var pill = K.Fill(boostRt, "pill");
            pill.gameObject.AddComponent<CanvasGroup>();
            var face = K.Block(pill, K.IceBtn, 13, 2.5f, 3, out var fill);
            fill.raycastTarget = false; // never swallows a steering touch
            var chev = K.Icon(face, "chevrons").Add(Shape.Path("M5 6.5l5.5 5.5L5 17.5M12.5 6.5l5.5 5.5-5.5 5.5").WithStroke(berry, 3.2f));
            var label = K.IconLabel(face, chev, 15, "เร่ง", 15, Look.Hex(berry), 3);
            boostRt.sizeDelta = new Vector2(((RectTransform)label.transform.parent).sizeDelta.x + 26, 26);
            boostRt.anchoredPosition = new Vector2(BoostRight - boostRt.sizeDelta.x / 2, -86);
            boostPop = pill.gameObject.AddComponent<PopIn>();
            boostPop.FromScale = 0.6f;
            boostPop.FromY = 0;
            boostPop.Dur = 0.3f;
            boostRt.gameObject.SetActive(false);
        }

        // ---------- build: intro ----------

        void BuildIntro()
        {
            intro = K.Fill(safe, "intro").gameObject;
            var back = K.Node(intro.transform, "back", new Vector2(0, 1), new Vector2(14, -8), new Vector2(44, 44));
            K.RoundButton(back, () => IntroBack?.Invoke(), out var bc);
            K.Place(K.BackIcon(bc), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22));

            var tap = K.Node(intro.transform, "tap to go", new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(358, 32));
            K.Outlined(tap, "แตะค้างที่ไหนก็ได้เพื่อเริ่ม!", 21);
            tap.gameObject.AddComponent<Pulse>();

            introPanel = K.Panel(intro.transform, "", 334);
            var root = introPanel.Root;
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0);
            root.anchoredPosition = new Vector2(0, 30 + 32 + 30);

            var badgeSlot = K.Slot(root, "badge", 26);
            introBadgeRt = K.Node(badgeSlot, "pill", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80, 26));
            if (K.Sliced(introBadgeRt, "ui.tag", 26) != null)
            {
                introBadge = K.Outlined(K.Fill(introBadgeRt, "label", 0, 1, 0, 0), "ด่าน 1", 15, 2, 0);
                introBadge.GetComponent<GameText>().Ink = K.Navy;
            }
            else
            {
                var bface = K.Block(introBadgeRt, K.Badge, 13, 3, 0, out var badgeFill);
                badgeFill.raycastTarget = false;
                introBadge = K.Outlined(bface, "ด่าน 1", 15, 1.2f, 0);
            }

            introTip = K.Label(root, "", 15, K.InkSoft);
            introTip.fontStyle = FontStyle.Normal;
            introTip.horizontalOverflow = HorizontalWrapMode.Wrap;
            introTip.lineSpacing = 1.15f;
            introGoals = K.Well(root);
            intro.SetActive(false);
        }

        /// <summary>One goal line: its stars (or a snowflake for endless) and the text.</summary>
        void Goal(int stars, string text)
        {
            var row = new GameObject("goal", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(introGoals, false);
            var lay = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            lay.spacing = 8;
            lay.childAlignment = TextAnchor.MiddleLeft;
            lay.childControlWidth = lay.childControlHeight = true;
            lay.childForceExpandWidth = lay.childForceExpandHeight = false;
            var icons = K.Node(row, "icons", Vector2.zero, Vector2.zero, new Vector2(56, 20));
            K.Sized(icons, 56, 20);
            for (int i = 0; i < Math.Max(1, stars); i++)
            {
                var ic = stars > 0 ? K.StarIcon(icons) : K.FlakeIcon(icons);
                K.Place(ic, new Vector2(0, 0.5f), new Vector2(i * 18, 0), new Vector2(17, 17));
            }
            var t = K.Label(row, text, 14, K.Ink, TextAnchor.MiddleLeft);
            t.fontStyle = FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            K.Sized(t, -1, -1, 1);
        }

        // ---------- build: pause ----------

        void BuildPause()
        {
            pause = K.Fill(CanvasRoot, "pause").gameObject;
            K.Scrim(pause.transform);
            var panel = K.Panel(pause.transform, "หยุดพัก", 334);
            panel.Root.anchoredPosition = new Vector2(0, -24);

            var resumeSlot = K.Slot(panel.Root, "resume", 64);
            var resume = K.Node(resumeSlot, "button", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(294, 64));
            K.BigButton(resume, false, () => Resume?.Invoke(), out var rc);
            K.IconLabel(rc, K.PlayIcon(rc), 26, "เล่นต่อ", 27, Color.white, 10, true);

            var bar = K.Slot(panel.Root, "bar", 60);
            var restart = K.Node(bar, "restart", new Vector2(0, 0.5f), Vector2.zero, new Vector2(142, 60));
            K.IceButton(restart, () => Restart?.Invoke(), out var sc);
            K.IconLabel(sc, K.RetryIcon(sc), 28, "เริ่มใหม่", 19, K.Ink, 8);
            var home = K.Node(bar, "home", new Vector2(1, 0.5f), Vector2.zero, new Vector2(142, 60));
            K.IceButton(home, () => Home?.Invoke(), out var hc);
            K.IconLabel(hc, K.HomeIcon(hc), 28, "หน้าแรก", 19, K.Ink, 8);
            pause.SetActive(false);
        }

        // ---------- build: result ----------

        void BuildResult()
        {
            result = K.Fill(CanvasRoot, "result").gameObject;
            K.Scrim(result.transform);
            resPanel = K.Panel(result.transform, "ผ่านด่าน!", 334);
            var root = resPanel.Root;
            root.anchoredPosition = new Vector2(0, -24);

            // Stars: grey sockets, gold stars land one by one.
            var stars = K.Slot(root, "stars", 92);
            resStars = stars.gameObject;
            starPops = new StarPop[3];
            if (SpriteTable.Has("ui.star") && SpriteTable.Has("ui.starOff"))
            {
                // Classic 3-star result: grey sockets, the middle one bigger and higher; gold stars land in them.
                for (int i = 0; i < 3; i++)
                {
                    float size = i == 1 ? 88 : 72;
                    var w = K.Node(stars, "star " + i, new Vector2(0.5f, 0), new Vector2((i - 1) * 84, i == 1 ? 2 : -6), new Vector2(size, size));
                    w.localRotation = Quaternion.Euler(0, 0, (1 - i) * 12);
                    K.Sprite(w, "ui.starOff");
                    var burst = K.Fill(w, "burst").gameObject.AddComponent<Vec>();
                    burst.raycastTarget = false;
                    burst.ViewBox = new Rect(0, 0, 100, 100);
                    burst.Add(Shape.Circle(50, 50, 36).WithStroke("#FFE27A", 4 * 100 / size));
                    var gold = K.Sprite(w, "ui.star");
                    var pop = w.gameObject.AddComponent<StarPop>();
                    pop.Gold = gold.rectTransform;
                    pop.Burst = burst.rectTransform;
                    pop.Init();
                    starPops[i] = pop;
                }
            }
            else for (int i = 0; i < 3; i++)
            {
                float size = i == 1 ? 94 : 72;
                var w = K.Node(stars, "star " + i, new Vector2(0.5f, 0), new Vector2((i - 1) * 85, i == 1 ? -2 : -8), new Vector2(size, size));
                w.localRotation = Quaternion.Euler(0, 0, (1 - i) * 12);
                var sock = K.Fill(w, "socket").gameObject.AddComponent<Vec>();
                sock.raycastTarget = false;
                var ss = Shape.Path(K.StarPath).WithFill(new Color(21 / 255f, 95 / 255f, 168 / 255f, 0.22f));
                ss.Stroke = new Color(K.Ink.r, K.Ink.g, K.Ink.b, 0.5f);
                ss.StrokeWidth = 1.4f;
                sock.Add(ss);
                var burst = K.Fill(w, "burst").gameObject.AddComponent<Vec>();
                burst.raycastTarget = false;
                burst.ViewBox = new Rect(0, 0, 100, 100);
                burst.Add(Shape.Circle(50, 50, 36).WithStroke("#FFE27A", 4 * 100 / size));
                var gold = K.Fill(w, "gold").gameObject.AddComponent<Vec>();
                gold.raycastTarget = false;
                gold.Add(Shape.Path(K.StarPath).WithGrad(K.Gold).WithStroke("#2B2140", 1.6f));
                gold.Add(Rotated(Shape.Ellipse(8.6f, 9.2f, 1.3f, 2.5f), 38, 8.6f, 9.2f).WithFill(new Color(1, 1, 1, 0.85f)));
                var pop = w.gameObject.AddComponent<StarPop>();
                pop.Gold = gold.rectTransform;
                pop.Burst = burst.rectTransform;
                pop.Init();
                starPops[i] = pop;
            }

            // Fall: how far you got, and a tip for what tripped you up.
            var fail = new GameObject("fail", typeof(RectTransform)).GetComponent<RectTransform>();
            fail.SetParent(root, false);
            resFail = fail.gameObject;
            var fl = fail.gameObject.AddComponent<VerticalLayoutGroup>();
            fl.spacing = 12;
            fl.childControlWidth = fl.childControlHeight = true;
            fl.childForceExpandWidth = true;
            fl.childForceExpandHeight = false;
            var head = K.Slot(fail, "head", 56);
            failRow = K.Node(head, "row", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 56));
            failA = K.Label(K.Node(failRow, "a", new Vector2(0, 0.5f), Vector2.zero, new Vector2(60, 30)), "ไปได้", 19, K.InkSoft, TextAnchor.MiddleLeft, true);
            failPct = K.Gtext(K.Node(failRow, "pct", new Vector2(0, 0.5f), Vector2.zero, new Vector2(100, 56)), "0%", 44, 3.5f, 0);
            failB = K.Label(K.Node(failRow, "b", new Vector2(0, 0.5f), Vector2.zero, new Vector2(60, 30)), "ของด่าน", 19, K.InkSoft, TextAnchor.MiddleLeft, true);
            var trackSlot = K.Slot(fail, "track", 32);
            var track = K.Fill(trackSlot, "track", 6, 6, 26, 6);
            if (SpriteBars)
            {
                failBar = SpriteBar(track, 20, out failFill, out failFillImg);
                failMe = SpriteMarker(failBar, 28, "ui.markerMe");
            }
            else BuildFailTrack(track);
            BuildFailTip(fail);
            BuildTotal(root);
        }

        void BuildFailTrack(RectTransform track)
        {
            K.Round(track, K.Ink, 10);
            var tin = K.Round(track, Color.white, 7, 3, 3, 3, 3).rectTransform;
            failFill = K.Node(tin, "fill", new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, 14));
            failFill.gameObject.AddComponent<RRect>().Radius = 7;
            failFill.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var fs = K.Fill(failFill, "stripes").gameObject.AddComponent<Stripes>();
            fs.A = Look.Hex(ProgGoldA);
            fs.B = Look.Hex(ProgGoldB);
            fs.raycastTarget = false;
            failMe = Marker(tin, 28, "#2B3A67");
            failBar = tin;
            K.Place(K.FlagIcon(track), new Vector2(1, 1), new Vector2(30, 20), new Vector2(26, 32));
        }

        void BuildFailTip(RectTransform fail)
        {
            var tipBox = new GameObject("tip", typeof(RectTransform)).GetComponent<RectTransform>();
            tipBox.SetParent(fail, false);
            var tbg = K.Fill(tipBox, "bg");
            K.NoLayout(tbg);
            K.Round(tbg, new Color(42 / 255f, 143 / 255f, 218 / 255f, 0.35f), 16);
            K.Round(tbg, new Color(1, 1, 1, 0.72f), 14, 2, 2, 2, 2);
            var tl = tipBox.gameObject.AddComponent<HorizontalLayoutGroup>();
            tl.padding = new RectOffset(12, 12, 10, 10);
            tl.spacing = 8;
            tl.childAlignment = TextAnchor.UpperLeft;
            tl.childControlWidth = tl.childControlHeight = true;
            tl.childForceExpandWidth = tl.childForceExpandHeight = false;
            var tagSlot = new GameObject("tag", typeof(RectTransform)).GetComponent<RectTransform>();
            tagSlot.SetParent(tipBox, false);
            var tag = K.Node(tagSlot, "pill", new Vector2(0, 1), Vector2.zero, new Vector2(60, 22));
            K.Round(tag, K.Ink, 11);
            K.Round(tag, Look.Hex("#FFC93C"), 9, 2, 2, 2, 2);
            var tagText = K.Label(tag, "เคล็ดลับ", 12, K.Ink, TextAnchor.MiddleCenter, true);
            tag.sizeDelta = new Vector2(K.Width(tagText) + 20, 22);
            K.Sized(tagSlot, tag.sizeDelta.x, 22);
            failTip = K.Label(tipBox, "", 14, K.Ink, TextAnchor.UpperLeft);
            failTip.fontStyle = FontStyle.Normal;
            failTip.horizontalOverflow = HorizontalWrapMode.Wrap;
            failTip.lineSpacing = 1.1f;
            K.Sized(failTip, -1, -1, 1);
        }

        void BuildTotal(RectTransform root)
        {
            resRows = K.Well(root);

            var total = K.Slot(root, "total", 58);
            resTotalRow = total.gameObject;
            totalRow = K.Node(total, "row", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 58));
            totalLabel = K.Label(K.Node(totalRow, "label", new Vector2(0, 0.5f), Vector2.zero, new Vector2(70, 30)), "คะแนน", 19, K.InkSoft, TextAnchor.MiddleLeft, true);
            resTotal = K.Gtext(K.Node(totalRow, "n", new Vector2(0, 0.5f), Vector2.zero, new Vector2(140, 58)), "0", 44, 3.5f, 0, TextAnchor.MiddleLeft);
            resTotal.GetComponent<GameText>().SetGold();
            // Symmetric l / r margins are right: u3_7 (591 × 355 px) is opaque over x 4..586, the navy body
            // equally inset on both sides (no one-sided shadow; the extra transparency is at the bottom).
            var plaque = K.Sliced(totalRow, "ui.plaque", 64, -22, -7, -22, 1);
            if (plaque != null)
            {
                // The total on a dark navy plaque: light label, gold numerals.
                plaque.transform.SetAsFirstSibling();
                totalLabel.color = Look.Hex("#CFE3FF");
            }

            if (SpriteTable.Has("ui.badge"))
            {
                // "สถิติใหม่!" starburst, tilted, over the panel's top-right corner (built after the ribbon so it
                // sits on top of it); inset so the rotated starburst stays inside the panel's width.
                var b = K.Node(root, "new best", new Vector2(1, 1), new Vector2(-50, -36), new Vector2(84, 84));
                b.pivot = new Vector2(0.5f, 0.5f);
                b.localRotation = Quaternion.Euler(0, 0, -12);
                K.NoLayout(b);
                K.Sprite(b, "ui.badge");
                var bt = K.Outlined(K.Fill(b, "label", 6, 8, 6, 4), "สถิติใหม่!", 15, 2.5f, 0);
                bt.GetComponent<GameText>().Ink = K.Navy;
                resBadge = b.gameObject;
                resBadge.SetActive(false);
            }

            // Buttons: map · retry · next (retry grows into the main button when there's no next level).
            var bar = K.Slot(root, "bar", 60);
            var map = K.Node(bar, "map", new Vector2(0, 0.5f), Vector2.zero, new Vector2(60, 60));
            K.IceButton(map, () => ResultMap?.Invoke(), out var mc);
            K.Place(K.MapIcon(mc), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28, 28));
            var ri = K.Node(bar, "retry", new Vector2(0, 0.5f), new Vector2(70, 0), new Vector2(60, 60));
            K.IceButton(ri, () => Retry?.Invoke(), out var ric);
            K.Place(K.RetryIcon(ric), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28, 28));
            retryIcon = ri.gameObject;
            var rb = K.Node(bar, "retry big", new Vector2(0, 0.5f), new Vector2(70, 0), new Vector2(224, 60));
            K.BigButton(rb, false, () => Retry?.Invoke(), out var rbc);
            K.IconLabel(rbc, K.RetryIcon(rbc, "#FFFFFF"), 26, "เล่นอีกครั้ง", 22, Color.white, 8, true);
            retryBig = rb.gameObject;
            var nx = K.Node(bar, "next", new Vector2(0, 0.5f), new Vector2(140, 0), new Vector2(154, 60));
            K.BigButton(nx, false, () => Next?.Invoke(), out var nc);
            K.IconLabel(nc, K.PlayIcon(nc), 24, "ด่านต่อไป", 22, Color.white, 8, true, true);
            nextBtn = nx.gameObject;
            result.SetActive(false);
        }

        static Shape Rotated(Shape s, float deg, float cx, float cy)
        {
            float a = deg * Mathf.Deg2Rad, c = Mathf.Cos(a), sn = Mathf.Sin(a);
            foreach (var con in s.Contours)
                for (int i = 0; i < con.Count; i++)
                {
                    var d = con[i] - new Vector2(cx, cy);
                    con[i] = new Vector2(cx + d.x * c - d.y * sn, cy + d.x * sn + d.y * c);
                }
            return s;
        }

        // ---------- screens ----------

        /// <summary>In-run HUD on or off (off on the title and map). Either way the intro, pause and result
        /// panels close, so every screen change (retry, next level, restart, home, map) starts clean.</summary>
        public void ShowRun(bool on)
        {
            run.SetActive(on);
            intro.SetActive(false);
            pause.SetActive(false);
            result.SetActive(false);
            counting = false;
            foreach (var sp in starPops) sp.Hide();
        }

        /// <summary>For tests: which panels are up.</summary>
        public bool RunShown => run.activeSelf;
        public bool IntroShown => intro.activeSelf;
        public bool PauseShown => pause.activeSelf;
        public bool ResultShown => result.activeSelf;

        public void ShowIntro(RunSim sim, Progress progress)
        {
            var L = sim.L;
            ShowRun(true);
            HideQuestion();
            coinPop.transform.parent.gameObject.SetActive(false);
            boostRt.gameObject.SetActive(false);
            toastT = -9;
            shownHearts = -1;
            introPanel.Title.text = L.Name;
            introBadge.text = L.Endless ? "โหมดรายวัน" : "ด่าน " + L.Id;
            introBadgeRt.sizeDelta = new Vector2(K.Width(introBadge) + 34, 26);
            introTip.text = L.Tip;
            foreach (Transform ch in introGoals) if (ch.name == "goal") Destroy(ch.gameObject);
            if (L.Endless)
            {
                Goal(0, "ไปให้ไกลที่สุดก่อนหัวใจหมด");
                Goal(0, "ตอบถูก +300 · เกล็ดหิมะ +10");
                Goal(0, progress.EndlessBest > 0 ? "สถิติของคุณ " + N(progress.EndlessBest) + " ม." : "ยังไม่มีสถิติ ลองเลย!");
            }
            else
            {
                Goal(1, "เข้าเส้นชัย");
                Goal(2, "ตอบถูกทุกข้อ (" + sim.Course.Rows.Count + " ข้อ)");
                Goal(3, L.Rival ? "ถึงเส้นชัยก่อนเบนนี่" : "ไม่เกิน " + sim.Par + " วินาที");
            }
            intro.SetActive(true);
            introPanel.Root.GetComponent<PopIn>().Play();
        }

        public void HideIntro() => intro.SetActive(false);

        public void ShowPause(bool on)
        {
            pause.SetActive(on);
            if (on) pause.GetComponentInChildren<PopIn>().Play();
        }

        static string N(int v) => v.ToString("N0", CultureInfo.InvariantCulture);

        static readonly Dictionary<ObjType, string> FailTips = new Dictionary<ObjType, string>
        {
            { ObjType.Rock, "ก้อนหินกับขอนไม้ แตะสั้นๆ เพื่อกระโดดข้าม · เจอสองอันติดกัน แตะซ้ำกลางอากาศเพื่อตีลังกา" },
            { ObjType.Log, "ก้อนหินกับขอนไม้ แตะสั้นๆ เพื่อกระโดดข้าม · เจอสองอันติดกัน แตะซ้ำกลางอากาศเพื่อตีลังกา" },
            { ObjType.Puff, "ปุยหิมะเดินไปมา ดูจังหวะแล้วกระโดดข้าม" },
            { ObjType.Ball, "ก้อนหิมะกลิ้งมาจากข้างทาง ดูทิศแล้วเลี้ยวหลบ" },
            { ObjType.Hole, "เจอหลุมน้ำแข็ง แตะกระโดดข้าม หรือเลี้ยวอ้อม · ขอนไม้+หลุมติดกัน แตะซ้ำกลางอากาศเพื่อตีลังกา" },
            { ObjType.Bear, "กองหิมะสั่นๆ = หมีกำลังจะโผล่ เลี้ยวหลบไว้ก่อนเลย" },
        };

        public void ShowResult(RunSim sim, RunResult res, bool newBest, bool hasNext)
        {
            var L = sim.L;
            HideQuestion();
            coinPop.transform.parent.gameObject.SetActive(false);
            run.SetActive(false); // like the web, the run HUD steps aside for the results
            bool failed = !res.Win && !L.Endless;
            resPanel.SetFail(failed);
            resStars.SetActive(!L.Endless && res.Win);
            resFail.SetActive(failed);
            resRows.gameObject.SetActive(!failed);
            resTotalRow.SetActive(!failed);
            foreach (Transform ch in resRows) if (ch.name == "row") Destroy(ch.gameObject);
            int rowIndex = 0;
            void Row(string k, string v, bool neg = false)
            {
                var row = new GameObject("row", typeof(RectTransform)).GetComponent<RectTransform>();
                row.SetParent(resRows, false);
                K.Sized(row, -1, 22);
                var inner = K.Fill(row, "inner");
                K.Label(inner, k, 14, K.Ink, TextAnchor.MiddleLeft).fontStyle = FontStyle.Normal;
                K.Label(inner, v, 16, neg ? Look.Hex("#B8333A") : K.Ink, TextAnchor.MiddleRight, true);
                FadeIn.On(row, inner, 0.25f + rowIndex++ * 0.08f);
            }

            int total = res.Total;
            if (L.Endless)
            {
                resPanel.Title.text = newBest ? "สถิติใหม่!" : "ไปได้ไกลมาก!";
                Row("ระยะทาง " + N(res.Distance) + " ม.", "+" + N(res.Distance));
                Row("เกล็ดหิมะ " + sim.Coins, "+" + N(sim.Coins * 10));
                Row("ตอบถูก " + sim.Correct + " / " + sim.Answered, "+" + N(sim.Correct * 300));
            }
            else if (res.Win)
            {
                resPanel.Title.text = "ผ่านด่าน!";
                Row("เข้าเส้นชัย " + (int)(sim.Time / 60) + ":" + ((int)(sim.Time % 60)).ToString("00"), "+1,000");
                Row("ตอบถูก " + sim.Correct + " / " + sim.Graded, "+" + N(sim.Correct * 300));
                Row("เกล็ดหิมะ " + sim.Coins, "+" + N(sim.Coins * 10));
                Row("โบนัสเวลา (par " + sim.Par + " วิ)", "+" + N(res.TimeBonus * 20));
                if (res.Perfect) Row("Perfect! ไม่ล้มเลย ตอบถูกหมด", "+500");
                if (sim.Tumbles > 0) Row("ล้ม " + sim.Tumbles + " ครั้ง", "−" + N(sim.Tumbles * 100), true);
                if (L.Rival) Row(sim.RivalFirst ? "เบนนี่ถึงก่อน" : "ชนะเบนนี่!", res.Star3 ? "★" : "–");
            }
            else
            {
                resPanel.Title.text = "ล้มแล้วลุกได้!";
                failTarget = Mathf.Clamp01((float)(sim.Z / sim.Course.Len));
                failPct.text = Mathf.RoundToInt(failTarget * 100) + "%";
                failTip.text = sim.LastHit.HasValue && FailTips.TryGetValue(sim.LastHit.Value, out var tip) ? tip : "เกล็ดหิมะเรียงแถว = ทางปลอดภัย ลองไถลตามไป";
                float a = K.Width(failA), p = K.Width(failPct) + 8, b = K.Width(failB);
                ((RectTransform)failA.transform.parent).sizeDelta = new Vector2(a, 30);
                var pr = (RectTransform)failPct.transform.parent;
                pr.sizeDelta = new Vector2(p, 56);
                pr.anchoredPosition = new Vector2(a + 10, 0);
                var br = (RectTransform)failB.transform.parent;
                br.sizeDelta = new Vector2(b, 30);
                br.anchoredPosition = new Vector2(a + 10 + p + 10, 0);
                failRow.sizeDelta = new Vector2(a + p + b + 20, 56);
                SetFail(0);
            }

            for (int i = 0; i < 3; i++)
            {
                starPops[i].Hide();
                if (i < res.Stars) starPops[i].Play(0.55f + i * 0.42f);
            }
            resT0 = Time.unscaledTime;
            countTotal = total;
            countDelay = 0.65f + res.Stars * 0.42f;
            counting = !failed && total > 0;
            SetTotal(0);

            if (resBadge) resBadge.SetActive(newBest && !failed);
            retryIcon.SetActive(hasNext);
            retryBig.SetActive(!hasNext);
            nextBtn.SetActive(hasNext);
            result.SetActive(false);
            result.SetActive(true);
            resPanel.Root.GetComponent<PopIn>().Play();
        }

        public void HideResult() => result.SetActive(false);

        void SetTotal(int v)
        {
            resTotal.text = N(v);
            // The row is exactly as wide as the drawn glyphs ("คะแนน" ink left .. last digit ink right), so the
            // plaque's symmetric margins centre the pill on what is seen. Sizing by preferredWidth (+10, as
            // before 5 Nov 2026) counted side bearings and dead space on the right: the content sat 6 pt left of
            // the pill's centre on the simulator (10 pt in the user's screenshot).
            float lw = K.Width(totalLabel), nw = K.Width(resTotal);
            Vector2 li = K.InkSpan(totalLabel), ni = K.InkSpan(resTotal);
            var lr = (RectTransform)totalLabel.transform.parent;
            lr.sizeDelta = new Vector2(lw, 30);
            lr.anchoredPosition = new Vector2(-li.x, 0);
            var nr = (RectTransform)resTotal.transform.parent;
            nr.sizeDelta = new Vector2(nw, 58);
            nr.anchoredPosition = new Vector2(-li.x + lw + 12, 0);
            totalRow.sizeDelta = new Vector2(-li.x + lw + 12 + ni.y, 58);
        }

        void SetFail(float k)
        {
            if (failFillImg != null)
            {
                SpriteBarSet(failBar, failFill, failFillImg, k);
                failMe.anchoredPosition = new Vector2(failBar.rect.width * k, 0);
                return;
            }
            var tin = (RectTransform)failFill.parent;
            float w = tin.rect.width;
            failFill.sizeDelta = new Vector2(w * k, 14);
            failMe.anchoredPosition = new Vector2(w * k, 0);
        }

        void Update()
        {
            if (!result.activeSelf) return;
            float t = Time.unscaledTime - resT0;
            for (int i = 0; i < starPops.Length; i++)
            {
                var sp = starPops[i];
                if (sp.At >= 0 && !sp.Chimed && Time.unscaledTime >= sp.At)
                {
                    sp.Chimed = true;
                    StarLanded?.Invoke(i);
                }
            }
            if (resFail.activeSelf)
            {
                float k = Mathf.Clamp01((t - 0.45f) / 0.9f);
                SetFail(failTarget * (1 - Mathf.Pow(1 - k, 3)));
            }
            if (counting && t > countDelay)
            {
                float k = Mathf.Clamp01((t - countDelay) / 0.9f);
                SetTotal(Mathf.RoundToInt(countTotal * (1 - Mathf.Pow(1 - k, 3))));
                if (k < 1 && Time.unscaledTime - lastTick > 0.09f) { lastTick = Time.unscaledTime; Sfx?.Invoke("tick"); }
                if (k >= 1) { counting = false; Sfx?.Invoke("pop"); }
            }
        }

        // ---------- in-run updates ----------

        public void ShowQuestion(GateRow row)
        {
            var ans = row.Q.Options[row.Correct];
            // With the power crystal the card fills in number answers, like the web prototype.
            bool fillIn = row.Reveal && ans.Label != null && Regex.IsMatch(row.Q.Text, @"\d.*\?$");
            qHint.text = row.Reveal ? "ผลึกพลังเฉลยให้แล้ว! พุ่งทะลุได้เลย" : row.Q.Hint;
            qCard.SetActive(true);
            FitQuestion(fillIn ? row.Q.Text.Substring(0, row.Q.Text.Length - 1) + ans.Label : row.Q.Text);
            qCard.GetComponent<PopIn>().Play();
        }

        public void HideQuestion() => qCard.SetActive(false);

        public void Toast(string text, string color)
        {
            toast.text = text;
            toast.color = Look.Hex(color);
            toastT = Time.unscaledTime;
        }

        /// <summary>One gold "+N" beside Pip's head that counts up while pickups keep coming.</summary>
        public void Coin()
        {
            var go = coinPop.transform.parent.gameObject;
            if (!go.activeSelf || Time.unscaledTime - coinPopLast > (float)Tuning.CoinStreak)
            {
                coinPopN = 0;
                coinPopBorn = Time.unscaledTime;
                coinPopLeft = false;
            }
            coinPopN++;
            coinPopLast = Time.unscaledTime;
            coinPop.text = "+" + coinPopN;
            go.SetActive(true);
            var c = coinPop.color; c.a = 1; coinPop.color = c;
            go.transform.localScale = Vector3.one * 0.55f;
            chipBump = Time.unscaledTime;
        }

        void SetTimer(string s)
        {
            if (timer.text == s && plank.sizeDelta.x > 0) return;
            timer.text = s;
            plank.sizeDelta = new Vector2(K.Width(timer) + 30, 40);
            plank.anchoredPosition = new Vector2(-14 - coinChip.sizeDelta.x - 8, -10);
        }

        void SetCoins(int n)
        {
            string s = n.ToString(CultureInfo.InvariantCulture);
            if (coins.text == s && coinChip.sizeDelta.x > 0) return;
            coins.text = s;
            float w = 4 + 28 + 4 + K.Width(coins) + 12 + 6;
            if (Mathf.Abs(w - coinChip.sizeDelta.x) > 0.5f)
            {
                coinChip.sizeDelta = new Vector2(w, 40);
                plank.anchoredPosition = new Vector2(-14 - w - 8, -10);
            }
        }

        public void Tick(RunSim sim, Camera cam, Vector3 pipHead)
        {
            FitSafeArea();
            if (!run.activeSelf || sim == null) return;
            if (sim.Hearts != shownHearts)
            {
                shownHearts = sim.Hearts;
                if (heartImgs != null)
                    for (int i = 0; i < 3; i++) heartImgs[i].sprite = SpriteTable.Ui(i < sim.Hearts ? "ui.heart" : "ui.heartOff");
                else for (int i = 0; i < 3; i++)
                {
                    hearts[i].Shapes[0].Fill = Look.Hex(i < sim.Hearts ? "#FF4F6D" : "#D9D3EA");
                    hearts[i].SetVerticesDirty();
                }
            }
            SetCoins(sim.Coins);
            SetTimer(sim.L.Endless ? N(sim.Distance) + " ม." : (int)(sim.Time / 60) + ":" + ((int)(sim.Time % 60)).ToString("00"));
            float bump = Time.unscaledTime - chipBump;
            coinChip.localScale = Vector3.one * (bump < 0.22f ? 1 + 0.14f * Mathf.Sin(bump / 0.22f * Mathf.PI) : 1);

            prog.SetActive(!sim.L.Endless);
            if (!sim.L.Endless)
            {
                float w = progBar.rect.width;
                float p = Mathf.Clamp01((float)(sim.Z / sim.Course.Len));
                if (progFillImg != null) SpriteBarSet(progBar, progFill, progFillImg, p);
                else progFill.sizeDelta = new Vector2(w * p, 10);
                progMe.anchoredPosition = new Vector2(w * p, 0);
                progRival.gameObject.SetActive(sim.Rival != null);
                if (sim.Rival != null) progRival.anchoredPosition = new Vector2(w * Mathf.Clamp01((float)(sim.Rival.Z / sim.Course.Len)), 0);
            }

            var chips = new List<string>(4);
            if (sim.Shield) chips.Add("ฟองกันชน");
            if (sim.Magnet > 0) chips.Add("แม่เหล็ก " + Math.Ceiling(sim.Magnet));
            if (sim.Slowmo > 0) chips.Add("ช้าลง " + Math.Ceiling(sim.Slowmo));
            if (sim.Power > 0) chips.Add("ผลึกพลัง " + Math.Ceiling(sim.Power));
            string pt = string.Join(" · ", chips);
            pupsRt.gameObject.SetActive(chips.Count > 0);
            if (pups.text != pt)
            {
                pups.text = pt;
                pupsRt.sizeDelta = new Vector2(K.Width(pups) + 24, 22);
            }

            // Boost pill pops in past 0.3 and goes when boost fades below it; a long power-ups pill
            // slides left so the two never overlap.
            bool boostOn = sim.BoostLevel > 0.3;
            if (boostOn != boostRt.gameObject.activeSelf)
            {
                boostRt.gameObject.SetActive(boostOn);
                if (boostOn) boostPop.Play();
            }
            float pupsX = boostOn ? Mathf.Min(0, BoostRight - boostRt.sizeDelta.x - 6 - pupsRt.sizeDelta.x / 2) : 0;
            pupsRt.anchoredPosition = new Vector2(pupsX, -88);

            if (qCard.activeSelf && sim.Row != null && !sim.Row.Done)
                qRing.Fraction = (float)((sim.Row.Z - sim.Z) / Math.Max(1, sim.V * sim.ReadTime));

            float tt = Time.unscaledTime - toastT;
            toast.gameObject.SetActive(tt < 1.15f);
            if (tt < 1.15f)
            {
                float k = tt / 1.15f;
                toast.transform.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(0.6f, 1.15f, k / 0.15f) : k < 0.3f ? Mathf.Lerp(1.15f, 1f, (k - 0.15f) / 0.15f) : 1f);
                toast.rectTransform.anchoredPosition = new Vector2(0, k < 0.15f ? -12 * (1 - k / 0.15f) : k > 0.75f ? (k - 0.75f) / 0.25f * 30 : 0);
                var col = toast.color; col.a = k < 0.15f ? k / 0.15f : k > 0.75f ? 1 - (k - 0.75f) / 0.25f : 1; toast.color = col;
            }

            var pop = coinPop.transform.parent.gameObject;
            if (pop.activeSelf && cam != null)
            {
                var prt = (RectTransform)pop.transform;
                float since = Time.unscaledTime - coinPopLast;
                var sp = cam.WorldToScreenPoint(pipHead);
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)run.transform, sp, null, out var local);
                var rr = ((RectTransform)run.transform).rect;
                local -= new Vector2(rr.center.x, rr.center.y);
                // Beside the head, not above it, so it never covers the question card; flips left near the edge.
                if (Time.unscaledTime - coinPopBorn < 0.02f) coinPopLeft = local.x > rr.width / 2 - 90;
                prt.pivot = new Vector2(coinPopLeft ? 1 : 0, 0.5f);
                coinPop.alignment = coinPopLeft ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
                float bumpK = Mathf.Clamp01((Time.unscaledTime - coinPopLast) / 0.32f);
                float s = bumpK < 0.55f ? Mathf.Lerp(0.55f, 1.25f, bumpK / 0.55f) : Mathf.Lerp(1.25f, 1, (bumpK - 0.55f) / 0.45f);
                if (since <= (float)Tuning.CoinStreak)
                {
                    prt.localScale = Vector3.one * s;
                    prt.anchoredPosition = local + new Vector2(coinPopLeft ? -24 : 24, -14);
                }
                else
                {
                    float k = (since - (float)Tuning.CoinStreak) / 0.55f;
                    prt.localScale = Vector3.one * Mathf.Lerp(1, 0.85f, k);
                    prt.anchoredPosition += new Vector2(0, Time.unscaledDeltaTime * 70);
                    var col = coinPop.color; col.a = 1 - k * k; coinPop.color = col;
                    if (k >= 1) pop.SetActive(false);
                }
            }
        }
    }
}
