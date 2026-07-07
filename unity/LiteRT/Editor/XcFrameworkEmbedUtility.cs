using System.IO;
using UnityEngine;
#if UNITY_IOS
using System.IO.Compression;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
#endif

namespace LiteRT.Unity.Editor
{
    /// <summary>
    /// Embeds zipped dynamic <c>.xcframework</c> payloads into the generated Xcode project.
    /// The LiteRT packages ship iOS natives as <c>Plugins/iOS/*.xcframework.zip</c> (a loose
    /// <c>.dylib</c> can't be embedded/code-signed on iOS, and a <c>.xcframework</c> directory
    /// is easier to embed from a zip than to manage as an imported plugin). Unity imports the
    /// zip as a plain asset; this helper unzips it into the project's <c>Libraries/</c>, links
    /// the framework into the UnityFramework target (so dyld loads it at launch and the
    /// bindings' iOS <c>[DllImport("__Internal")]</c> resolves its symbols), and embeds +
    /// code-signs it in the main app target. Shared by the LiteRT core and LiteRT.LM
    /// post-build steps.
    /// </summary>
    public static class XcFrameworkEmbedUtility
    {
#if UNITY_IOS
        /// <param name="buildPath">The generated Xcode project directory.</param>
        /// <param name="zipPath">A <c>&lt;Name&gt;.xcframework.zip</c> whose archive root is
        /// <c>&lt;Name&gt;.xcframework</c>.</param>
        public static void EmbedZippedXcFramework(string buildPath, string zipPath)
        {
            // "<Name>.xcframework.zip" -> "<Name>.xcframework" (the archive's root entry).
            string frameworkName = Path.GetFileNameWithoutExtension(zipPath);

            string librariesDir = Path.Combine(buildPath, "Libraries");
            string dstFramework = Path.Combine(librariesDir, frameworkName);
            if (Directory.Exists(dstFramework))
            {
                Directory.Delete(dstFramework, true);
            }

            Directory.CreateDirectory(librariesDir);
            ZipFile.ExtractToDirectory(zipPath, librariesDir);

            string projectRelative = Path.Combine("Libraries", frameworkName);

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
            Debug.Log($"[LiteRT.Unity] Unzipped + embedded {frameworkName} into the Xcode project.");
        }
#endif
    }
}
