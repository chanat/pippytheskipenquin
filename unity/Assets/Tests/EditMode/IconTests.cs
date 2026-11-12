using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Pip.Tests
{
    /// <summary>App icons written by Pip ▸ Setup App Icons (Editor/IconSetup.cs) from design/icon/icon-master.png.</summary>
    public class IconTests
    {
        const string Master = "../design/icon/icon-master.png";
        const string IOS = "Assets/Icons/icon-ios-1024.png";
        const string Foreground = "Assets/Icons/icon-android-foreground.png";
        const string Background = "Assets/Icons/icon-android-background.png";

        [Test]
        public void IconsAssignedAndDerivedFromMaster()
        {
            // iOS: every Application slot holds the 1024 icon.
            var ios = AssetDatabase.LoadAssetAtPath<Texture2D>(IOS);
            Assert.IsNotNull(ios, IOS);
            var iosKinds = PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.iOS);
            var app = iosKinds.FirstOrDefault(k => k.ToString().StartsWith("Application"));
            Assert.IsNotNull(app, "iOS Application icon kind");
            var appIcons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, app);
            Assert.Greater(appIcons.Length, 0);
            foreach (var icon in appIcons) Assert.AreEqual(ios, icon.GetTexture(), "iOS Application slot " + icon);

            // Android adaptive: background (layer 0) + foreground (layer 1) in every slot.
            var adaptive = PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android)
                .FirstOrDefault(k => k.ToString().StartsWith("Adaptive"));
            Assert.IsNotNull(adaptive, "Android Adaptive icon kind");
            var bg = AssetDatabase.LoadAssetAtPath<Texture2D>(Background);
            var fg = AssetDatabase.LoadAssetAtPath<Texture2D>(Foreground);
            var adaptiveIcons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, adaptive);
            Assert.Greater(adaptiveIcons.Length, 0);
            foreach (var icon in adaptiveIcons)
            {
                var t = icon.GetTextures();
                Assert.AreEqual(bg, t[0], "adaptive background");
                Assert.AreEqual(fg, t[1], "adaptive foreground");
            }

            // icon-ios-1024.png: 1024², PNG colour type 2 (RGB, no alpha channel), imported without alpha.
            byte[] png = File.ReadAllBytes(IOS);
            Assert.AreEqual(1024, BigEndian(png, 16), "width");
            Assert.AreEqual(1024, BigEndian(png, 20), "height");
            Assert.AreEqual(2, png[25], "PNG colour type (2 = RGB)");
            Assert.AreEqual(TextureImporterAlphaSource.None, ((TextureImporter)AssetImporter.GetAtPath(IOS)).alphaSource);

            // The adaptive foreground's centre 2/3 is the master: canvas pixel d = 3 × 3 box at master 3d − 512.
            var master = Load(File.ReadAllBytes(Master));
            var fore = Load(File.ReadAllBytes(Foreground));
            Assert.AreEqual(1024, fore.width);
            var m = master.GetPixels32();
            var f = fore.GetPixels32();
            foreach (var (x, y) in new[] { (171, 171), (512, 512), (852, 852), (300, 700), (700, 260), (430, 610), (600, 400) })
            {
                float r = 0, g = 0, b = 0;
                for (int j = 0; j < 3; j++)
                for (int i = 0; i < 3; i++)
                {
                    var c = m[(3 * y - 512 + j) * 2048 + 3 * x - 512 + i];
                    r += c.r; g += c.g; b += c.b;
                }
                var p = f[y * 1024 + x];
                Assert.AreEqual(r / 9f, p.r, 1f, $"r at {x},{y}");
                Assert.AreEqual(g / 9f, p.g, 1f, $"g at {x},{y}");
                Assert.AreEqual(b / 9f, p.b, 1f, $"b at {x},{y}");
            }
            Object.DestroyImmediate(master);
            Object.DestroyImmediate(fore);
        }

        [Test]
        public void HomeScreenNameIsShortProductNameUnchanged()
        {
            Assert.AreEqual("Pippy", Pip.EditorTools.ProjectSetup.HomeScreenName);
            Assert.AreEqual("Pippy The Ski Penguin", PlayerSettings.productName, "product name (Xcode product / .app name)");
            Assert.AreEqual("com.everyimagine.pippy", PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS));

            // The iOS post-build step turns the exported CFBundleDisplayName (= product name) into the short name.
            string plist = Path.Combine(Path.GetTempPath(), "pip-test-Info.plist");
            File.WriteAllText(plist, "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
                + "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n"
                + "<plist version=\"1.0\"><dict><key>CFBundleDisplayName</key><string>Pippy The Ski Penguin</string>"
                + "<key>CFBundleName</key><string>Pippy The Ski Penguin</string></dict></plist>");
            Assert.IsTrue(Pip.EditorTools.HomeScreenNameIOS.Patch(plist, Pip.EditorTools.ProjectSetup.HomeScreenName));
            string xml = File.ReadAllText(plist);
            File.Delete(plist);
            StringAssert.IsMatch(@"<key>CFBundleDisplayName</key>\s*<string>Pippy</string>", xml);
            StringAssert.Contains("<string>Pippy The Ski Penguin</string>", xml, "CFBundleName keeps the full name");
            StringAssert.Contains("PropertyList-1.0.dtd\">\n<plist", xml, "DOCTYPE left exactly as it was");
        }

        static int BigEndian(byte[] b, int at) => (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];

        static Texture2D Load(byte[] bytes)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.LoadImage(bytes);
            return t;
        }
    }
}
