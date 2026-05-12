using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PersonaEngine
{
    public static class RuntimeBackendSelector
    {

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SelectAndSetupBackend()
        {
            if (Application.platform == RuntimePlatform.WindowsPlayer)
            {
                SelectBackendWindows();
            }
        }

        private static void SelectBackendWindows()
        {
            string backendName = DetermineBackendWindows();

            if (string.IsNullOrEmpty(backendName))
            {
                return;
            }

            string sourceFolder = Path.Combine(Application.streamingAssetsPath, "LLamaBackends", "Windows", backendName);
            if (backendName == "CPU")
            {
                string cpuSubfolder = GetOptimalCpuSubfolder();
                sourceFolder = Path.Combine(sourceFolder, cpuSubfolder);
            }

            string targetFolder = Path.GetDirectoryName(Application.dataPath);
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            CopyBackendFiles(sourceFolder, targetFolder);

            LLamaBackendType selected = backendName switch
            {
                "CUDA12" => LLamaBackendType.CUDA12,
                "Vulkan" => LLamaBackendType.Vulkan,
                _ => LLamaBackendType.CPU
            };

            ModelPathProvider.SelectedBackend = selected;
            ModelPathProvider.ApplyBackendToLLama(selected);
        }

        private static string DetermineBackendWindows()
        {
            if (LoadLibrary("nvcuda.dll") != IntPtr.Zero)
            {
                return "CUDA12";
            }

            if (LoadLibrary("vulkan-1.dll") != IntPtr.Zero)
            {
                return "Vulkan";
            }

            return "CPU";
        }

        private static string GetOptimalCpuSubfolder()
        {
            if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
            {
                return "noavx";
            }

            string procInfo = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
            if (string.IsNullOrEmpty(procInfo))
            {
                return "noavx";
            }

            procInfo = procInfo.ToUpperInvariant();
            if (procInfo.Contains("AVX512F") || procInfo.Contains("AVX-512"))
            {
                return "avx512";
            }
            if (procInfo.Contains("AVX2"))
            {
                return "avx2";
            }
            if (procInfo.Contains("AVX"))
            {
                return "avx";
            }
            return "noavx";
        }

        private static void CopyBackendFiles(string sourceDir, string destDir)
        {
            if (!Directory.Exists(sourceDir))
            {
                return;
            }

            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            foreach (string sourceFile in Directory.GetFiles(sourceDir, "*", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(sourceFile);
                string destFileName = fileName;
                if (destFileName.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase))
                {
                    destFileName = Path.ChangeExtension(fileName, ".dll");
                }
                string destFile = Path.Combine(destDir, destFileName);
                File.Copy(sourceFile, destFile, true);
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string subDirName = Path.GetFileName(subDir);
                string destSubDir = Path.Combine(destDir, subDirName);
                CopyBackendFiles(subDir, destSubDir);
            }
        }
    }
}