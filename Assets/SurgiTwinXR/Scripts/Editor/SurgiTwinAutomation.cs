using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SurgiTwinXR.EditorTools
{
    /// <summary>
    /// Development helper: lets external tools drive the open editor through files in
    /// &lt;project&gt;/SurgiTwinAutomation (outside Assets, never imported).
    ///   request.txt  one command per line: build | validate | screenshots | quest-render | build-android
    ///   result.txt   appended results
    ///   heartbeat.txt  written after every successful script compilation / domain reload
    /// Also available as menu items under SurgiTwin > Phase 1.
    /// </summary>
    [InitializeOnLoad]
    public static class SurgiTwinAutomation
    {
        static readonly string Dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "SurgiTwinAutomation");
        static readonly string RequestPath = Path.Combine(Dir, "request.txt");
        static readonly string ResultPath = Path.Combine(Dir, "result.txt");
        static double nextPoll;
        static bool busy;

        static SurgiTwinAutomation()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(Path.Combine(Dir, "heartbeat.txt"),
                    $"compiled-and-loaded {DateTime.Now:yyyy-MM-dd HH:mm:ss} unity {Application.unityVersion} target {EditorUserBuildSettings.activeBuildTarget}\n");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SurgiTwin Automation] " + e.Message);
            }
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (busy || EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1.5;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(RequestPath)) return;

            string[] commands;
            try
            {
                commands = File.ReadAllLines(RequestPath);
                File.Delete(RequestPath);
            }
            catch (IOException)
            {
                return; // still being written
            }

            busy = true;
            try
            {
                foreach (string raw in commands)
                {
                    string cmd = raw.Trim();
                    if (cmd.Length == 0) continue;
                    Append($"=== {DateTime.Now:HH:mm:ss} {cmd}");
                    try
                    {
                        Append(Run(cmd));
                    }
                    catch (Exception e)
                    {
                        Append("EXCEPTION: " + e);
                    }
                }
            }
            finally
            {
                busy = false;
            }
        }

        static string Run(string cmd)
        {
            switch (cmd)
            {
                case "build":
                    return SurgiTwinPhase1Builder.BuildAll(false);
                case "validate":
                    return SurgiTwinPhase1Validator.Validate(out _, out _);
                case "quest-render":
                    SurgiTwinBuilderUtil.Report.Clear();
                    SurgiTwinPhase1Builder.ApplyQuestRenderSettings();
                    AssetDatabase.SaveAssets();
                    return SurgiTwinBuilderUtil.Report.ToString();
                case "screenshots":
                    return SurgiTwinScreenshots.CaptureAll(Path.Combine(Dir, "shots"));
                case "build-android":
                    return BuildAndroid();
                default:
                    return "Unknown command.";
            }
        }

        [MenuItem("SurgiTwin/Phase 1/Build Quest APK (Android)", priority = 40)]
        static void BuildAndroidMenu()
        {
            Debug.Log(BuildAndroid());
        }

        public static string BuildAndroid()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                return "Active build target is " + EditorUserBuildSettings.activeBuildTarget + ". Switch to Android first (not done automatically).";
            }
            EditorSceneManager.SaveOpenScenes();
            string outDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Builds", "Android");
            Directory.CreateDirectory(outDir);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { SurgiTwinPhase1Builder.ScenePath },
                locationPathName = Path.Combine(outDir, "SurgiTwinXR_Phase1.apk"),
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            var sb = new StringBuilder();
            sb.AppendLine($"Result: {s.result}");
            sb.AppendLine($"Output: {s.outputPath}");
            sb.AppendLine($"Size: {s.totalSize / (1024f * 1024f):0.0} MB, time: {s.totalTime}, errors: {s.totalErrors}, warnings: {s.totalWarnings}");
            foreach (var step in report.steps)
            {
                foreach (var m in step.messages)
                {
                    if (m.type == LogType.Error || m.type == LogType.Exception)
                    {
                        sb.AppendLine("  " + m.type + ": " + m.content);
                    }
                }
            }
            return sb.ToString();
        }

        static void Append(string text)
        {
            File.AppendAllText(ResultPath, text + "\n");
        }
    }
}
