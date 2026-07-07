using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LiteRT.Unity.Editor
{
    /// <summary>
    /// iOS post-build step for LiteRT. This package ships the iOS core as
    /// <c>Plugins/iOS/LiteRt.xcframework.zip</c>; see <see cref="XcFrameworkEmbedUtility"/> for
    /// why it's a zip and how it gets linked/embedded into the generated Xcode project.
    /// </summary>
    public sealed class LiteRtPostprocessBuild : IPostprocessBuildWithReport
    {
        private const string ZipName = "LiteRt.xcframework.zip";

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
#if UNITY_IOS
            if (report.summary.platform != BuildTarget.iOS)
            {
                return;
            }

            string zipPath = FindXcFrameworkZip();
            if (zipPath == null)
            {
                Debug.LogError(
                    $"[LiteRT.Unity] '{ZipName}' not found in the package's Plugins/iOS folder — run " +
                    "scripts/fetch-natives.sh + scripts/sync-unity-natives.sh to populate it. " +
                    "The iOS app will fail to load LiteRT.");
                return;
            }

            XcFrameworkEmbedUtility.EmbedZippedXcFramework(report.summary.outputPath, zipPath);
#endif
        }

#if UNITY_IOS
        private static string FindXcFrameworkZip()
        {
            // The zip ships inside this package's Plugins/iOS folder. Resolve the package on disk
            // (works for file:, registry, and Library/PackageCache installs).
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(LiteRtPostprocessBuild).Assembly);
            if (package == null)
            {
                return null;
            }

            string zipPath = Path.Combine(package.resolvedPath, "Plugins", "iOS", ZipName);
            return File.Exists(zipPath) ? zipPath : null;
        }
#endif
    }
}
