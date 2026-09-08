using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace DungeonDash.EditorTools
{
    public static class IosBuildTools
    {
        [MenuItem("Tools/Dungeon Dash/Export iPhone Player")]
        public static void ExportDevice() => Export(false);

        [MenuItem("Tools/Dungeon Dash/Export iPhone Simulator")]
        public static void ExportSimulator() => Export(true);

        static void Export(bool simulator)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
                throw new InvalidOperationException("Install iOS Build Support for this Unity editor first.");

            string identifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
            if (!simulator && (string.IsNullOrWhiteSpace(identifier) || identifier.Contains("DefaultCompany")))
                throw new InvalidOperationException("Set your App Store Connect Bundle ID in iOS Player Settings before exporting.");

            BrandingTools.Apply();

            var previousSdk = PlayerSettings.iOS.sdkVersion;
            try
            {
                PlayerSettings.iOS.sdkVersion = simulator ? iOSSdkVersion.SimulatorSDK : iOSSdkVersion.DeviceSDK;
                if (simulator) PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
                PlayerSettings.allowedAutorotateToPortrait = false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                PlayerSettings.allowedAutorotateToLandscapeRight = true;
                PlayerSettings.iOS.targetOSVersionString = "15.0";
                string path = simulator ? "Builds/iOS-Simulator" : "Builds/iOS";
                var report = BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/SampleScene.unity" },
                    path, BuildTarget.iOS, BuildOptions.None);
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"iPhone export failed: {report.summary.result}");
                string plistPath = Path.Combine(path, "Info.plist");
                var plist = new PlistDocument();
                plist.ReadFromFile(plistPath);
                plist.root.SetString("CFBundleDisplayName", "Dungeon Dash");
                plist.WriteToFile(plistPath);
                Debug.Log($"[DungeonDash] iPhone Xcode project: {path}");
            }
            finally
            {
                PlayerSettings.iOS.sdkVersion = previousSdk;
            }
        }
    }
}
