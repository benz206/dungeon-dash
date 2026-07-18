using UnityEditor;
using UnityEngine;

namespace DungeonDash.EditorTools
{
    public static class QaBuildTools
    {
        [MenuItem("Tools/Dungeon Dash/Build QA Player")]
        public static void BuildQaPlayer()
        {
            const string path = "Builds/QA/DungeonDash.app";
            var report = BuildPipeline.BuildPlayer(
                new[] { "Assets/Scenes/SampleScene.unity" },
                path,
                BuildTarget.StandaloneOSX,
                BuildOptions.None);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception($"QA build failed: {report.summary.result}");
            Debug.Log($"[DungeonDash] QA build: {path}");
        }
    }
}
