using LLama.Native;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PersonaEngine
{
    public static class ModelPathProvider
    {
        public static float Temperature = 0.7f;
        public static float TopP = 0.9f;
        public static int MaxTokens = 256;
        public static float FrequencyPenalty = 0.0f;
        public static float PresencePenalty = 0.0f;
        public static int ContextSize = 2048;
        public static int GpuLayerCount = 10;
        public static bool ContextSizeAuto = true;
        public static bool GpuLayerCountAuto = true;

        public static LLamaBackendType SelectedBackend = LLamaBackendType.CPU;

        private static string editorSelectedPath = null;

        public static void SetEditorPath(string path)
        {
            editorSelectedPath = path;
        }

        public static string GetModelPath()
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(editorSelectedPath) && File.Exists(editorSelectedPath))
            {
                return editorSelectedPath;
            }
#endif
            string modelsFolder = Path.Combine(Application.streamingAssetsPath, "Models");
            if (Directory.Exists(modelsFolder))
            {
                string[] ggufs = Directory.GetFiles(modelsFolder, "*.gguf");
                if (ggufs.Length > 0)
                {
                    return ggufs[0];
                }
            }
            return null;
        }

        public static void ApplyBackendToLLama(LLamaBackendType backend)
        {
            try
            {
                var configProperty = typeof(NativeLibraryConfig).GetProperty("All", BindingFlags.Public | BindingFlags.Static)
                                    ?? typeof(NativeLibraryConfig).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (configProperty == null)
                {
                    return;
                }
                object config = configProperty.GetValue(null);

                var loadedProp = config.GetType().GetProperty("LibraryHasLoaded");
                if (loadedProp != null && (bool)loadedProp.GetValue(config))
                {
                    return;
                }

                if (backend == LLamaBackendType.CUDA12)
                {
                    var method = config.GetType().GetMethod("WithCuda", new[] { typeof(bool) })
                                 ?? config.GetType().GetMethod("WithCuda", Type.EmptyTypes);
                    method?.Invoke(config, method.GetParameters().Length > 0 ? new object[] { true } : null);
                }
                else if (backend == LLamaBackendType.Vulkan)
                {
                    var method = config.GetType().GetMethod("WithVulkan", new[] { typeof(bool) })
                                 ?? config.GetType().GetMethod("WithVulkan", Type.EmptyTypes);
                    method?.Invoke(config, method.GetParameters().Length > 0 ? new object[] { true } : null);
                }
                else
                {
                    var cudaMethod = config.GetType().GetMethod("WithCuda", new[] { typeof(bool) });
                    cudaMethod?.Invoke(config, new object[] { false });
                    var vulkanMethod = config.GetType().GetMethod("WithVulkan", new[] { typeof(bool) });
                    vulkanMethod?.Invoke(config, new object[] { false });
                }
            }
            catch (Exception) { }
        }

        public static void LoadPreferences()
        {
#if UNITY_EDITOR
            Temperature = UnityEditor.EditorPrefs.GetFloat("ModelSettings_Temperature", 0.7f);
            TopP = UnityEditor.EditorPrefs.GetFloat("ModelSettings_TopP", 0.9f);
            MaxTokens = UnityEditor.EditorPrefs.GetInt("ModelSettings_MaxTokens", 256);
            FrequencyPenalty = UnityEditor.EditorPrefs.GetFloat("ModelSettings_FrequencyPenalty", 0.0f);
            PresencePenalty = UnityEditor.EditorPrefs.GetFloat("ModelSettings_PresencePenalty", 0.0f);
            ContextSize = UnityEditor.EditorPrefs.GetInt("ModelSettings_ContextSize", 2048);
            GpuLayerCount = UnityEditor.EditorPrefs.GetInt("ModelSettings_GpuLayerCount", 10);
            ContextSizeAuto = UnityEditor.EditorPrefs.GetInt("ModelSettings_ContextSizeAuto", 1) == 1;
            GpuLayerCountAuto = UnityEditor.EditorPrefs.GetInt("ModelSettings_GpuLayerCountAuto", 0) == 1;
            SelectedBackend = (LLamaBackendType)UnityEditor.EditorPrefs.GetInt("ModelSettings_SelectedBackend", 0);
#else
        Temperature     = PlayerPrefs.GetFloat("Model_Temperature", 0.7f);
        TopP            = PlayerPrefs.GetFloat("Model_TopP", 0.9f);
        MaxTokens       = PlayerPrefs.GetInt("Model_MaxTokens", 256);
        FrequencyPenalty = PlayerPrefs.GetFloat("Model_FrequencyPenalty", 0.0f);
        PresencePenalty = PlayerPrefs.GetFloat("Model_PresencePenalty", 0.0f);
        ContextSize     = PlayerPrefs.GetInt("Model_ContextSize", 2048);
        GpuLayerCount   = PlayerPrefs.GetInt("Model_GpuLayerCount", 10);
        ContextSizeAuto = PlayerPrefs.GetInt("Model_ContextSizeAuto", 1) == 1;
        GpuLayerCountAuto = PlayerPrefs.GetInt("Model_GpuLayerCountAuto", 0) == 1;
#endif
        }
    }
}