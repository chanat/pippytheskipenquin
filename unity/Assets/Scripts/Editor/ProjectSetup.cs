// One-shot project setup, safe to re-run: URP asset, the Game scene, build list and player basics.
// Menu: Pip ▸ Setup Project, or batch mode: -executeMethod Pip.EditorTools.ProjectSetup.Run
using Pip.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Pip.EditorTools
{
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Game.unity";

        [MenuItem("Pip/Setup Project")]
        public static void Run()
        {
            System.IO.Directory.CreateDirectory("Assets/Settings");
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PipURP.asset");
            if (urp == null)
            {
                var data = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(data, "Assets/Settings/PipRenderer.asset");
                urp = UniversalRenderPipelineAsset.Create(data);
                AssetDatabase.CreateAsset(urp, "Assets/Settings/PipURP.asset");
            }
            // 2D sprites only (GDD §18.k): no MSAA, no HDR, no shadows, no post-processing.
            urp.msaaSampleCount = 1;
            urp.supportsHDR = false;
            urp.shadowDistance = 0;
            var so = new SerializedObject(urp);
            foreach (var prop in new[] { "m_MainLightShadowsSupported", "m_AdditionalLightShadowsSupported", "m_SoftShadowsSupported" })
            {
                var sp = so.FindProperty(prop);
                if (sp != null) sp.boolValue = false;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PipRenderer.asset");
            if (renderer != null && renderer.postProcessData != null)
            {
                // Nothing uses post-processing; without the data its shaders and textures stay out of the build.
                renderer.postProcessData = null;
                EditorUtility.SetDirty(renderer);
            }
            EditorUtility.SetDirty(urp);
            GraphicsSettings.defaultRenderPipeline = urp;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            QualitySettings.SetQualityLevel(current, false);
            PlayerSettings.colorSpace = ColorSpace.Linear;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var boot = new GameObject("Game").AddComponent<GameBoot>();
            boot.unlitTemplate = Template("PipUnlit", "Pip/Unlit");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.productName = "Pippy The Ski Penguin";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.everyimagine.pippy");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.everyimagine.pippy");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.everyimagine.pippy");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            AssetDatabase.SaveAssets();
            Debug.Log("[Pip] Project setup done: URP asset, " + ScenePath + ", portrait.");
        }

        /// <summary>The template material referenced by the scene keeps the Pip/Unlit shader in player builds
        /// (runtime materials are created from their shaders).</summary>
        static Material Template(string name, string shader)
        {
            System.IO.Directory.CreateDirectory("Assets/Materials");
            string path = "Assets/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(m, path);
            }
            else m.shader = Shader.Find(shader);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Mac build for unattended smoke runs: -executeMethod Pip.EditorTools.ProjectSetup.BuildMac -pipOut path.app</summary>
        public static void BuildMac()
        {
            string outPath = "Builds/Mac/PippyTheSkiPenguin.app";
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == "-pipOut") outPath = a[i + 1];
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 430;
            PlayerSettings.defaultScreenHeight = 932;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.runInBackground = true; // unattended runs launch without window focus
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outPath,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None,
            });
            Debug.Log("[Pip] Mac build: " + report.summary.result + " → " + outPath);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        /// <summary>Xcode project for the iOS Simulator (Apple-silicon arm64, no signing needed):
        /// -executeMethod Pip.EditorTools.ProjectSetup.BuildIOSSimulator -pipOut Builds/iOS-Sim.
        /// Then build it with xcodebuild -sdk iphonesimulator (see docs/UNITY.md).</summary>
        public static void BuildIOSSimulator()
        {
            string outPath = "Builds/iOS-Sim";
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == "-pipOut") outPath = a[i + 1];
            if (!PrepareIOS()) return;
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
            PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outPath,
                target = BuildTarget.iOS,
                options = BuildOptions.Development,
            });
            Debug.Log("[Pip] iOS Simulator Xcode project: " + report.summary.result + " → " + outPath);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        /// <summary>Xcode project for a real iPhone / iPad (Device SDK, Release, automatic signing):
        /// -executeMethod Pip.EditorTools.ProjectSetup.BuildIOSDevice -pipOut Builds/iOS-Device [-pipTeam TEAMID].
        /// Without -pipTeam, pick the Team in Xcode (Unity-iPhone target ▸ Signing &amp; Capabilities) and press Run;
        /// see docs/UNITY.md.</summary>
        public static void BuildIOSDevice()
        {
            string outPath = "Builds/iOS-Device";
            string team = null;
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
            {
                if (a[i] == "-pipOut") outPath = a[i + 1];
                else if (a[i] == "-pipTeam") team = a[i + 1];
            }
            if (!PrepareIOS()) return;
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            if (!string.IsNullOrEmpty(team)) PlayerSettings.iOS.appleDeveloperTeamID = team;
            // Xcode's Run uses Release: real frame rate on the phone and no "Development Build" mark.
            EditorUserBuildSettings.iOSXcodeBuildConfig = XcodeBuildConfig.Release;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outPath,
                target = BuildTarget.iOS,
                options = BuildOptions.None,
            });
            Debug.Log("[Pip] iOS device Xcode project: " + report.summary.result + " → " + outPath);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        /// <summary>Shared by the simulator and device builds; false (and the editor exits) when iOS support is missing.</summary>
        static bool PrepareIOS()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
            {
                Debug.LogError("[Pip] iOS Build Support is not installed for this editor (Unity Hub ▸ Installs ▸ Add modules).");
                EditorApplication.Exit(2);
                return false;
            }
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            // Placeholder primitives come with colliders; keep the physics classes so they can be created.
            PlayerSettings.stripEngineCode = false;
            return true;
        }
    }
}
