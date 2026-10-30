using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SurvivorArena.EditorTools
{
    /// <summary>
    /// 测试用打包入口：把 MainGame 打成 Windows 开发包到系统临时目录，
    /// 方便用真实渲染验证波次、HUD 与胜利流程。
    /// 批处理：Unity -batchmode -quit -projectPath ... -executeMethod SurvivorArena.EditorTools.TestBuild.BuildWindows
    /// </summary>
    public static class TestBuild
    {
        private const string ScenePath = "Assets/Scenes/MainGame.unity";

        [MenuItem("Tools/Survivor Arena/Build Windows Test Player", priority = 2)]
        public static void BuildWindows()
        {
            string outputDirectory = Path.Combine(Path.GetTempPath(), "SurvivorArenaTestBuild");
            Directory.CreateDirectory(outputDirectory);
            string executablePath = Path.Combine(outputDirectory, "SurvivorArenaU.exe");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"[测试打包] {summary.result}，{summary.totalSize / (1024f * 1024f):0.0} MB，输出：{executablePath}");

            if (summary.result != BuildResult.Succeeded)
            {
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                throw new InvalidOperationException($"打包失败：{summary.result}");
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }
    }
}
