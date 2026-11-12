// Android launcher label = ProjectSetup.HomeScreenName ("Pippy"). Unity writes the product name into app_name of the
// generated Gradle project's strings.xml; this rewrites it after generation. Compiled only when the active build target is
// Android (UnityEditor.Android lives in the Android module), so the editor compiles without that module.
#if UNITY_ANDROID
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEngine;

namespace Pip.EditorTools
{
    public class AndroidAppName : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 0;

        static readonly Regex AppName = new Regex("(<string\\s+name=\"app_name\"[^>]*>)(.*?)(</string>)", RegexOptions.Singleline);

        /// <param name="path">The unityLibrary module of the generated project; the label sits in the launcher module.</param>
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string[] candidates =
            {
                Path.Combine(path, "..", "launcher", "src", "main", "res", "values", "strings.xml"),
                Path.Combine(path, "src", "main", "res", "values", "strings.xml"),
            };
            foreach (string file in candidates)
            {
                if (!File.Exists(file)) continue;
                string xml = File.ReadAllText(file);
                if (!AppName.IsMatch(xml)) continue;
                string label = System.Security.SecurityElement.Escape(ProjectSetup.HomeScreenName);
                File.WriteAllText(file, AppName.Replace(xml, m => m.Groups[1].Value + label + m.Groups[3].Value, 1));
                Debug.Log("[Pip] Android app_name = " + ProjectSetup.HomeScreenName + " in " + Path.GetFullPath(file));
                return;
            }
            Debug.LogWarning("[Pip] Android app_name not found (looked in " + string.Join(", ", candidates)
                             + "); the launcher label stays the product name.");
        }
    }
}
#endif
