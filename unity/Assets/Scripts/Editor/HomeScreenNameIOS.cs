// iOS name under the app icon = ProjectSetup.HomeScreenName ("Pippy"): after every iOS export, CFBundleDisplayName in the
// Xcode project's Info.plist is set to it. productName (Xcode product, .app file name, CFBundleName) stays the full name.
// A text edit of the one value (the rest of the file is left byte for byte), so no iOS module types are needed to compile.
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Pip.EditorTools
{
    public static class HomeScreenNameIOS
    {
        static readonly Regex DisplayName = new Regex(@"(<key>CFBundleDisplayName</key>\s*)<string>[^<]*</string>");
        static readonly Regex DictEnd = new Regex(@"(\s*)</dict>(\s*</plist>\s*)$");

        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;
            Patch(Path.Combine(pathToBuiltProject, "Info.plist"), ProjectSetup.HomeScreenName);
        }

        /// <summary>Sets (or adds) CFBundleDisplayName in an XML plist; false with a [Pip] warning when the file isn't usable.</summary>
        public static bool Patch(string plist, string name)
        {
            if (!File.Exists(plist))
            {
                Debug.LogWarning("[Pip] iOS display name: no Info.plist at " + plist);
                return false;
            }
            string xml = File.ReadAllText(plist);
            string value = "<string>" + System.Security.SecurityElement.Escape(name) + "</string>";
            if (DisplayName.IsMatch(xml))
                xml = DisplayName.Replace(xml, m => m.Groups[1].Value + value, 1);
            else if (DictEnd.IsMatch(xml))
                xml = DictEnd.Replace(xml, m => "\n\t<key>CFBundleDisplayName</key>\n\t" + value + m.Groups[1].Value + "</dict>" + m.Groups[2].Value, 1);
            else
            {
                Debug.LogWarning("[Pip] iOS display name: no top-level </dict> in " + plist);
                return false;
            }
            File.WriteAllText(plist, xml);
            Debug.Log("[Pip] iOS display name (CFBundleDisplayName) = " + name + " in " + plist);
            return true;
        }
    }
}
