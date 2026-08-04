using System;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace TapTheObject.Editor
{
    /// <summary>
    /// Builds the Addressables content from the editor menu or from a command line:
    /// <code>
    /// Unity -batchmode -quit -projectPath . -executeMethod TapTheObject.Editor.AddressablesBuildCommand.Build
    ///       -remoteLoadPath https://user.github.io/repo/ServerData/[BuildTarget]
    /// </code>
    /// </summary>
    public static class AddressablesBuildCommand
    {
        private const string RemoteLoadPathArgument = "-remoteLoadPath";

        [MenuItem("Tools/Tap the Object/Build Addressables Content")]
        public static void Build()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Fail("Addressables have not been initialised in this project.");
                return;
            }

            var remoteLoadPath = ReadArgument(RemoteLoadPathArgument);
            if (remoteLoadPath != null)
            {
                SetRemoteLoadPath(settings, remoteLoadPath);
            }

            AddressableAssetSettings.CleanPlayerContent();
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);

            if (!string.IsNullOrEmpty(result.Error))
            {
                Fail($"Addressables build failed: {result.Error}");
                return;
            }

            Debug.Log($"[Addressables] Built {CountFiles(result)} files in {result.Duration:F1}s.");

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        private static void SetRemoteLoadPath(AddressableAssetSettings settings, string remoteLoadPath)
        {
            settings.profileSettings.SetValue(
                settings.activeProfileId, AddressableAssetSettings.kRemoteLoadPath, remoteLoadPath);

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Addressables] Remote load path set to '{remoteLoadPath}'.");
        }

        private static int CountFiles(AddressablesPlayerBuildResult result)
        {
            if (result.FileRegistry == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var _ in result.FileRegistry.GetFilePaths())
            {
                count++;
            }

            return count;
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

        private static void Fail(string message)
        {
            Debug.LogError($"[Addressables] {message}");

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
