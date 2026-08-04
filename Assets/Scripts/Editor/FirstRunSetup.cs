using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace TapTheObject.Editor
{
    /// <summary>
    /// Prepares a freshly cloned project once: builds the Addressables content and selects the play mode
    /// script that loads it, so pressing Play downloads the round images over HTTPS instead of reading them
    /// from the asset database.
    /// </summary>
    /// <remarks>
    /// The play mode choice lives in Library/AddressablesConfig.dat, which is generated rather than
    /// committed, so it cannot simply be checked in. The marker file sits next to it, which means this runs
    /// once per clone and never overrides a choice made afterwards.
    /// </remarks>
    [InitializeOnLoad]
    internal static class FirstRunSetup
    {
        private const string MarkerPath = "Library/TapTheObjectSetup.done";

        static FirstRunSetup()
        {
            // Batch mode is the test and build pipeline, which selects what it needs for itself.
            if (Application.isBatchMode || File.Exists(MarkerPath))
            {
                return;
            }

            EditorApplication.delayCall += Configure;
        }

        private static void Configure()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                return;
            }

            // Written first, so a failure below does not repeat the attempt on every domain reload.
            File.WriteAllText(MarkerPath, string.Empty);

            // Packed play mode hands the editor the bundles built for the active build target, and bundles
            // built for a target the editor cannot run, WebGL among them, load as missing assets.
            var target = EditorUserBuildSettings.activeBuildTarget;
            if (!CanEditorLoadBundlesFor(target))
            {
                Debug.Log($"[TapTheObject] The active build target is {target}, whose bundles the editor " +
                          "cannot load, so play mode stays on Use Asset Database. Switch to a standalone " +
                          "target and run Tools > Tap the Object > Build Addressables Content to play " +
                          "against the content served over HTTPS.");
                return;
            }

            AddressableAssetSettings.BuildPlayerContent(out var result);
            if (!string.IsNullOrEmpty(result.Error))
            {
                Debug.LogWarning($"[TapTheObject] Could not build the Addressables content ({result.Error}). " +
                                 "Play mode is left on Use Asset Database, which works without it.");
                return;
            }

            if (!TrySelectPackedPlayMode(settings))
            {
                return;
            }

            Debug.Log("[TapTheObject] Built the Addressables content and set play mode to Use Existing Build, " +
                      "so round images are downloaded over HTTPS. The Addressables Groups window switches it back.");
        }

        private static bool CanEditorLoadBundlesFor(BuildTarget target)
        {
            return target == BuildTarget.StandaloneWindows64
                   || target == BuildTarget.StandaloneWindows
                   || target == BuildTarget.StandaloneOSX
                   || target == BuildTarget.StandaloneLinux64;
        }

        private static bool TrySelectPackedPlayMode(AddressableAssetSettings settings)
        {
            for (var index = 0; index < settings.DataBuilders.Count; index++)
            {
                if (settings.DataBuilders[index] is BuildScriptPackedPlayMode)
                {
                    settings.ActivePlayModeDataBuilderIndex = index;
                    EditorUtility.SetDirty(settings);
                    return true;
                }
            }

            return false;
        }
    }
}
