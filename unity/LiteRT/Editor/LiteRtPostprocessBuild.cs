using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_IOS
using System.IO.Compression;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
#endif

namespace LiteRT.Unity.Editor
{
    /// <summary>
    /// iOS post-build step for LiteRT. This package ships the iOS core as
    /// <c>Plugins/iOS/LiteRt.xcframework.zip</c> (a loose <c>.dylib</c> can't be
    /// embedded/code-signed on iOS, and a <c>.xcframework</c> directory is easier to embed from a
    /// zip than to manage as an imported plugin). Unity imports the zip as a plain asset, so here
    /// we unzip it into the generated Xcode project, link the framework into the UnityFramework
    /// target (so dyld loads it at launch and the bindings' iOS <c>[DllImport("__Internal")]</c>
    /// resolves its symbols), and embed + code-sign it in the main app target.
    /// </summary>
    public sealed class LiteRtPostprocessBuild : IPostprocessBuildWithReport
    {
        private const string FrameworkName = "LiteRt.xcframework";
        private const string ZipName = "LiteRt.xcframework.zip";

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
#if UNITY_IOS
            if (report.summary.platform != BuildTarget.iOS)
            {
                return;
            }

            EmbedXcFramework(report.summary.outputPath);
#endif
        }

#if UNITY_IOS
        private static void EmbedXcFramework(string buildPath)
        {
            string zipPath = FindXcFrameworkZip();
            if (zipPath == null)
            {
                Debug.LogError(
                    $"[LiteRT.Unity] '{ZipName}' not found in the package's Plugins/iOS folder — run " +
                    "scripts/fetch-natives.sh + scripts/sync-unity-natives.sh to populate it. " +
                    "The iOS app will fail to load LiteRT.");
                return;
            }

            // Unzip into the Xcode project's Libraries/ dir (archive root is the .xcframework).
            string librariesDir = Path.Combine(buildPath, "Libraries");
            string dstFramework = Path.Combine(librariesDir, FrameworkName);
            if (Directory.Exists(dstFramework))
            {
                Directory.Delete(dstFramework, true);
            }

            Directory.CreateDirectory(librariesDir);
            ZipFile.ExtractToDirectory(zipPath, librariesDir);

            string projectRelative = Path.Combine("Libraries", FrameworkName);

            string pbxPath = PBXProject.GetPBXProjectPath(buildPath);
            var pbx = new PBXProject();
            pbx.ReadFromFile(pbxPath);

            string fileGuid = pbx.AddFile(dstFramework, projectRelative, PBXSourceTree.Source);

            // Link into UnityFramework — that's where the IL2CPP/managed code lives; linking
            // makes dyld load the dynamic framework at launch so __Internal resolves it.
            string unityFrameworkGuid = pbx.GetUnityFrameworkTargetGuid();
            string frameworksPhaseGuid = pbx.AddFrameworksBuildPhase(unityFrameworkGuid);
            pbx.AddFileToBuildSection(unityFrameworkGuid, frameworksPhaseGuid, fileGuid);

            // Embed + code-sign in the main app target so the .app ships the dynamic framework.
            string mainGuid = pbx.GetUnityMainTargetGuid();
            pbx.AddFileToEmbedFrameworks(mainGuid, fileGuid);

            pbx.AddBuildProperty(mainGuid, "LD_RUNPATH_SEARCH_PATHS", "@executable_path/Frameworks");
            pbx.AddBuildProperty(unityFrameworkGuid, "LD_RUNPATH_SEARCH_PATHS",
                "@executable_path/Frameworks @loader_path/Frameworks");

            pbx.WriteToFile(pbxPath);
            Debug.Log($"[LiteRT.Unity] Unzipped + embedded {FrameworkName} into the Xcode project.");
        }

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
