using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LiteRT.LM.Unity.Editor
{
    /// <summary>
    /// iOS post-build step for LiteRT.LM. This package ships the iOS LM engine and its
    /// load-time Gemma constraint-provider dependency as zipped dynamic xcframeworks
    /// (<c>Plugins/iOS/LiteRtLmC.xcframework.zip</c> +
    /// <c>Plugins/iOS/GemmaModelConstraintProvider.xcframework.zip</c>); every
    /// <c>Plugins/iOS/*.xcframework.zip</c> is linked into UnityFramework and embedded in the
    /// app via the core package's <see cref="LiteRT.Unity.Editor.XcFrameworkEmbedUtility"/>.
    /// Runs after the core package's step (callbackOrder 1) for deterministic ordering.
    /// </summary>
    public sealed class LiteRtLmPostprocessBuild : IPostprocessBuildWithReport
    {
        public int callbackOrder => 1;

        public void OnPostprocessBuild(BuildReport report)
        {
#if UNITY_IOS
            if (report.summary.platform != BuildTarget.iOS)
            {
                return;
            }

            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(LiteRtLmPostprocessBuild).Assembly);
            string pluginsDir = package != null ? Path.Combine(package.resolvedPath, "Plugins", "iOS") : null;
            string[] zips = pluginsDir != null && Directory.Exists(pluginsDir)
                ? Directory.GetFiles(pluginsDir, "*.xcframework.zip")
                : System.Array.Empty<string>();
            if (zips.Length == 0)
            {
                Debug.LogError(
                    "[LiteRT.LM] No *.xcframework.zip found in the package's Plugins/iOS folder — run " +
                    "scripts/litert-lm-c/build.sh <litert-lm> out/ios ios, then scripts/fetch-natives.sh + " +
                    "scripts/sync-unity-natives.sh to populate it. The iOS app will fail to load LiteRT-LM.");
                return;
            }

            foreach (string zip in zips)
            {
                LiteRT.Unity.Editor.XcFrameworkEmbedUtility.EmbedZippedXcFramework(report.summary.outputPath, zip);
            }
#endif
        }
    }
}
