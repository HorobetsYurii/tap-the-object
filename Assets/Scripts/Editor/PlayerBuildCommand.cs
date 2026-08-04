using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TapTheObject.Editor
{
    /// <summary>
    /// Builds the WebGL demo from a command line:
    /// <code>
    /// Unity -batchmode -quit -projectPath . -buildTarget WebGL
    ///       -executeMethod TapTheObject.Editor.PlayerBuildCommand.BuildWebGL -buildOutput Builds/WebGL
    /// </code>
    /// </summary>
    public static class PlayerBuildCommand
    {
        private const string OutputArgument = "-buildOutput";
        private const string DefaultOutput = "Builds/WebGL";

        public static void BuildWebGL()
        {
            // GitHub Pages serves static files without setting Content-Encoding, so the build has to carry
            // its own decompressor rather than relying on the host to advertise the compression.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;

            var options = new BuildPlayerOptions
            {
                scenes = EnabledScenes(),
                locationPathName = ReadArgument(OutputArgument) ?? DefaultOutput,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[Build] WebGL build {summary.result}: {summary.totalErrors} error(s).");
                Exit(1);
                return;
            }

            Debug.Log($"[Build] WebGL build succeeded in {summary.totalTime:hh\\:mm\\:ss} " +
                      $"({summary.totalSize / (1024 * 1024)} MB) at '{summary.outputPath}'.");
            Exit(0);
        }

        private static string[] EnabledScenes()
        {
            var scenes = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    scenes.Add(scene.path);
                }
            }

            if (scenes.Count == 0)
            {
                throw new InvalidOperationException("No scenes are enabled in the build settings.");
            }

            return scenes.ToArray();
        }

        private static string ReadArgument(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var i = 0; i < arguments.Length - 1; i++)
            {
                if (string.Equals(arguments[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[i + 1];
                }
            }

            return null;
        }

        private static void Exit(int code)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(code);
            }
        }
    }
}
