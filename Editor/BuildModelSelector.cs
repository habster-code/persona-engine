using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PersonaEngine
{
    public class BuildModelSelector : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        private static readonly string streamingAssetsPath = Path.Combine(Application.dataPath, "StreamingAssets", "Models");
        private static readonly string tempBackupPath = Path.Combine(Application.dataPath, "..", "TempModelBackup");

        private const string SelectedModelPathKey = "ModelSettings_SelectedModelPath";

        public void OnPreprocessBuild(BuildReport report)
        {
            string selectedModelPath = EditorPrefs.GetString(SelectedModelPathKey, "");
            if (string.IsNullOrEmpty(selectedModelPath))
            {
                return;
            }

            if (!File.Exists(selectedModelPath))
            {
                return;
            }

            if (!Directory.Exists(streamingAssetsPath))
            {
                Directory.CreateDirectory(streamingAssetsPath);
            }

            if (Directory.Exists(tempBackupPath))
            {
                Directory.Delete(tempBackupPath, true);
            }
            Directory.CreateDirectory(tempBackupPath);

            var allModels = Directory.GetFiles(streamingAssetsPath, "*.gguf");
            foreach (var file in allModels)
            {
                if (Path.GetFullPath(file).Equals(Path.GetFullPath(selectedModelPath), System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string dest = Path.Combine(tempBackupPath, Path.GetFileName(file));
                File.Move(file, dest);
            }

            string selectedFileName = Path.GetFileName(selectedModelPath);
            string targetPath = Path.Combine(streamingAssetsPath, selectedFileName);
            if (!File.Exists(targetPath) || !Path.GetFullPath(targetPath).Equals(Path.GetFullPath(selectedModelPath)))
            {
                File.Copy(selectedModelPath, targetPath, true);
            }

            AssetDatabase.Refresh();
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (Directory.Exists(tempBackupPath))
            {
                foreach (var file in Directory.GetFiles(tempBackupPath))
                {
                    string dest = Path.Combine(streamingAssetsPath, Path.GetFileName(file));
                    File.Move(file, dest);
                }
                Directory.Delete(tempBackupPath);
            }

            AssetDatabase.Refresh();
        }

        [InitializeOnLoadMethod]
        private static void RegisterBuildFailureHandler()
        {
            BuildPlayerWindow.RegisterBuildPlayerHandler(BuildPlayerHandler);
        }

        private static void BuildPlayerHandler(BuildPlayerOptions options)
        {
            try
            {
                BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                if (Directory.Exists(tempBackupPath))
                {
                    foreach (var file in Directory.GetFiles(tempBackupPath))
                    {
                        string dest = Path.Combine(streamingAssetsPath, Path.GetFileName(file));
                        if (!File.Exists(dest))
                        {
                            File.Move(file, dest);
                        }
                    }
                    Directory.Delete(tempBackupPath);
                    AssetDatabase.Refresh();
                }
            }
        }
    }
}
