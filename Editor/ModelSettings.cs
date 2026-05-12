using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PersonaEngine
{
    public class ModelSettings : EditorWindow
    {
        [MenuItem("Tools/Persona Engine/Model Settings", false, 0)]
        public static void ShowExample()
        {
            ModelSettings wnd = GetWindow<ModelSettings>("Model Settings");
            wnd.minSize = new Vector2(400, 500);
            wnd.maxSize = new Vector2(450, 550);
        }

        private DropdownField modelDrop;
        private readonly List<string> modelNames = new();
        private readonly List<string> modelPaths = new();
        private const string PREFS_SELECTED_MODEL = "ModelSettings_SelectedModelPath";
        private Label modelInfo;
        private TextField modelName;
        private Button modelSave;
        private string pendingModelPath;

        private Slider temperature;
        private Slider topP;
        private SliderInt maxTokens;
        private Slider freqPenalty;
        private Slider presPenalty;
        private SliderInt contextSize;
        private Toggle contextAutoToggle;
        private SliderInt gpuLayers;
        private Toggle gpuAutoToggle;

        public void CreateGUI()
        {
            ModelPathProvider.LoadPreferences();

            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.minWidth = 400;
            root.style.minHeight = 500;
            root.style.maxWidth = 450;
            root.style.maxHeight = 550;
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/PersonaEngine/Editor/UI/ModelSettings.uxml");
            visualTree.CloneTree(root);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/PersonaEngine/Editor/UI/ModelSettings.uss");
            root.styleSheets.Add(styleSheet);

            var dropArea = root.Q<VisualElement>("drop-model");
            dropArea.AddToClassList("drop-model");
            dropArea.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            dropArea.RegisterCallback<DragPerformEvent>(OnDragPerform);

            var backends = root.Q<EnumField>("backend-choice");
            if (backends != null)
            {
                backends.Init(LLamaBackendType.CPU);
                LLamaBackendType savedBackend = BackendPrefs.GetSelectedBackend();
                backends.SetValueWithoutNotify(savedBackend);
                backends.RegisterValueChangedCallback(OnBackendChanged);
                SwitchBackend(savedBackend);
                ModelPathProvider.SelectedBackend = savedBackend;
                ModelPathProvider.ApplyBackendToLLama(savedBackend);
            }

            modelName = root.Q<TextField>("model-name");
            modelSave = root.Q<Button>("model-save");
            modelInfo = root.Q<Label>("model-info-label");

            if (modelName != null)
            {
                modelName.style.display = DisplayStyle.None;
            }
            if (modelSave != null)
            {
                modelSave.style.display = DisplayStyle.None;
                modelSave.clicked += ConfirmCopyModel;
            }

            modelDrop = root.Q<DropdownField>("model-choice");
            RefreshModelList();
            modelDrop.RegisterValueChangedCallback(evt =>
            {
                int idx = modelDrop.index;
                if (idx >= 0 && idx < modelPaths.Count)
                {
                    string selectedPath = modelPaths[idx];
                    SaveSelectedModelPath(selectedPath);
                    DisplayModelInfo(selectedPath);
                }
                UpdateSettingsEnabled();
            });

            string savedPath = GetSelectedModelPath();
            if (!string.IsNullOrEmpty(savedPath) && File.Exists(savedPath))
            {
                SelectModelByPath(savedPath);
            }
            else if (modelPaths.Count > 0)
            {
                modelDrop.SetValueWithoutNotify(modelNames[0]);
                SaveSelectedModelPath(modelPaths[0]);
                DisplayModelInfo(modelPaths[0]);
            }
            else if (modelInfo != null)
            {
                modelInfo.text = "No .gguf models found in StreamingAssets/Models";
            }

            temperature = new Slider("Temperature", 0f, 2f)
            {
                showInputField = true,
                value = ModelPathProvider.Temperature
            };
            temperature.AddToClassList("temp");
            temperature.RegisterValueChangedCallback(evt =>
            {
                float f = Mathf.Round(evt.newValue / 0.1f) * 0.1f;
                f = Mathf.Clamp(f, 0f, 2f);
                if (!Mathf.Approximately(temperature.value, f))
                {
                    temperature.SetValueWithoutNotify(f);
                }
                ModelPathProvider.Temperature = f;
                EditorPrefs.SetFloat("ModelSettings_Temperature", f);
            });
            root.Add(temperature);

            topP = new Slider("Top-P", 0f, 1f)
            {
                showInputField = true,
                value = ModelPathProvider.TopP
            };
            topP.AddToClassList("top-p");
            topP.RegisterValueChangedCallback(evt =>
            {
                float f = Mathf.Round(evt.newValue / 0.1f) * 0.1f;
                f = Mathf.Clamp(f, 0f, 1f);
                if (!Mathf.Approximately(topP.value, f))
                {
                    topP.SetValueWithoutNotify(f);
                }
                ModelPathProvider.TopP = f;
                EditorPrefs.SetFloat("ModelSettings_TopP", f);
            });
            root.Add(topP);

            maxTokens = new SliderInt("Max Tokens", 64, 1024)
            {
                showInputField = true,
                value = ModelPathProvider.MaxTokens
            };
            maxTokens.AddToClassList("max-t");
            maxTokens.RegisterValueChangedCallback(evt =>
            {
                int value = evt.newValue;
                int exponent = Mathf.RoundToInt(Mathf.Log(value, 2));
                int newValue = Mathf.Clamp((int)Mathf.Pow(2, exponent), 64, 1024);
                if (newValue != maxTokens.value)
                {
                    maxTokens.SetValueWithoutNotify(newValue);
                }
                ModelPathProvider.MaxTokens = newValue;
                EditorPrefs.SetInt("ModelSettings_MaxTokens", newValue);
            });
            root.Add(maxTokens);

            freqPenalty = new Slider("Frequency Penalty", 0f, 2f)
            {
                showInputField = true,
                value = ModelPathProvider.FrequencyPenalty
            };
            freqPenalty.AddToClassList("freq-p");
            freqPenalty.RegisterValueChangedCallback(evt =>
            {
                float f = Mathf.Round(evt.newValue / 0.1f) * 0.1f;
                f = Mathf.Clamp(f, 0f, 2f);
                if (!Mathf.Approximately(freqPenalty.value, f))
                {
                    freqPenalty.SetValueWithoutNotify(f);
                }
                ModelPathProvider.FrequencyPenalty = f;
                EditorPrefs.SetFloat("ModelSettings_FrequencyPenalty", f);
            });
            root.Add(freqPenalty);

            presPenalty = new Slider("Presence Penalty", 0f, 2f)
            {
                showInputField = true,
                value = ModelPathProvider.PresencePenalty
            };
            presPenalty.AddToClassList("pres-p");
            presPenalty.RegisterValueChangedCallback(evt =>
            {
                float f = Mathf.Round(evt.newValue / 0.1f) * 0.1f;
                f = Mathf.Clamp(f, 0f, 2f);
                if (!Mathf.Approximately(presPenalty.value, f))
                {
                    presPenalty.SetValueWithoutNotify(f);
                }
                ModelPathProvider.PresencePenalty = f;
                EditorPrefs.SetFloat("ModelSettings_PresencePenalty", f);
            });
            root.Add(presPenalty);

            contextSize = new SliderInt("Context Size", 512, 65536)
            {
                showInputField = true,
                value = ModelPathProvider.ContextSize
            };
            contextSize.SetEnabled(!ModelPathProvider.ContextSizeAuto);
            contextSize.AddToClassList("cnt-size");
            contextSize.RegisterValueChangedCallback(evt =>
            {
                if (!ModelPathProvider.ContextSizeAuto)
                {
                    int value = evt.newValue;
                    int exponent = Mathf.RoundToInt(Mathf.Log(value, 2));
                    int newValue = Mathf.Clamp((int)Mathf.Pow(2, exponent), 512, 65536);
                    if (newValue != contextSize.value)
                    {
                        contextSize.SetValueWithoutNotify(newValue);
                    }
                    ModelPathProvider.ContextSize = newValue;
                    EditorPrefs.SetInt("ModelSettings_ContextSize", newValue);
                }
            });
            root.Add(contextSize);

            contextAutoToggle = new Toggle("Auto Context Size")
            {
                value = ModelPathProvider.ContextSizeAuto
            };
            contextAutoToggle.AddToClassList("cnt-size");
            contextAutoToggle.RegisterValueChangedCallback(evt =>
            {
                ModelPathProvider.ContextSizeAuto = evt.newValue;
                contextSize.SetEnabled(!evt.newValue);
                EditorPrefs.SetInt("ModelSettings_ContextSizeAuto", evt.newValue ? 1 : 0);
                UpdateSettingsEnabled();
            });
            root.Add(contextAutoToggle);

            gpuLayers = new SliderInt("GPU Layers", -1, 20)
            {
                showInputField = true,
                value = ModelPathProvider.GpuLayerCount
            };
            gpuLayers.SetEnabled(!ModelPathProvider.GpuLayerCountAuto);
            gpuLayers.AddToClassList("gpu-l");
            gpuLayers.RegisterValueChangedCallback(evt =>
            {
                int i = Mathf.RoundToInt(evt.newValue);
                i = Mathf.Clamp(i, -1, 20);
                if (gpuLayers.value != i)
                {
                    gpuLayers.SetValueWithoutNotify(i);
                }
                if (!ModelPathProvider.GpuLayerCountAuto)
                {
                    ModelPathProvider.GpuLayerCount = i;
                    EditorPrefs.SetInt("ModelSettings_GpuLayerCount", i);
                }
            });
            root.Add(gpuLayers);

            gpuAutoToggle = new Toggle("Auto GPU Layers")
            {
                value = ModelPathProvider.GpuLayerCountAuto
            };
            gpuAutoToggle.AddToClassList("gpu-l");
            gpuAutoToggle.RegisterValueChangedCallback(evt =>
            {
                ModelPathProvider.GpuLayerCountAuto = evt.newValue;
                gpuLayers.SetEnabled(!evt.newValue);
                EditorPrefs.SetInt("ModelSettings_GpuLayerCountAuto", evt.newValue ? 1 : 0);
                if (!evt.newValue)
                {
                    ModelPathProvider.GpuLayerCount = (int)gpuLayers.value;
                    EditorPrefs.SetInt("ModelSettings_GpuLayerCount", (int)gpuLayers.value);
                }
                UpdateSettingsEnabled();
            });
            root.Add(gpuAutoToggle);

            UpdateSettingsEnabled();
        }

        private void UpdateSettingsEnabled()
        {
            bool modelAvailable = modelPaths.Count > 0
                               && modelDrop.index >= 0
                               && modelDrop.index < modelPaths.Count
                               && File.Exists(modelPaths[modelDrop.index]);

            temperature?.SetEnabled(modelAvailable);
            topP?.SetEnabled(modelAvailable);
            maxTokens?.SetEnabled(modelAvailable);
            freqPenalty?.SetEnabled(modelAvailable);
            presPenalty?.SetEnabled(modelAvailable);
            contextSize?.SetEnabled(modelAvailable && !ModelPathProvider.ContextSizeAuto);
            contextAutoToggle?.SetEnabled(modelAvailable);
            gpuLayers?.SetEnabled(modelAvailable && !ModelPathProvider.GpuLayerCountAuto);
            gpuAutoToggle?.SetEnabled(modelAvailable);
        }

        private void RefreshModelList()
        {
            modelPaths.Clear();
            modelNames.Clear();
            string modelsFolder = Path.Combine(Application.dataPath, "StreamingAssets/Models");
            if (Directory.Exists(modelsFolder))
            {
                string[] files = Directory.GetFiles(modelsFolder, "*.gguf", SearchOption.TopDirectoryOnly);
                foreach (string f in files)
                {
                    modelPaths.Add(f);
                    modelNames.Add(Path.GetFileName(f));
                }
            }
            modelDrop.choices = modelNames;
            modelDrop.SetEnabled(modelNames.Count > 0);
        }

        private void SelectModelByPath(string path)
        {
            int idx = modelPaths.IndexOf(path);
            if (idx >= 0)
            {
                modelDrop.SetValueWithoutNotify(modelNames[idx]);
                DisplayModelInfo(path);
            }
        }

        private static void SaveSelectedModelPath(string path)
        {
            EditorPrefs.SetString(PREFS_SELECTED_MODEL, path);
            ModelPathProvider.SetEditorPath(path);
        }

        public static string GetSelectedModelPath()
        {
            return EditorPrefs.GetString(PREFS_SELECTED_MODEL, "");
        }

        private void DisplayModelInfo(string modelPath)
        {
            if (ModelParser.TryGetModelInfo(modelPath, out var info))
            {
                modelInfo.text = $"Architecture: {info.architecture}\n" +
                                 $"Context Length: {info.contextLength}\n" +
                                 $"Embedding dim: {info.embeddingLength}\n" +
                                 $"GPU Layers: {info.blockCount}\n";
            }
            else
            {
                modelInfo.text = $"Failed to parse GGUF metadata for {Path.GetFileName(modelPath)}";
            }
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            DragAndDrop.AcceptDrag();
            if (DragAndDrop.paths == null || DragAndDrop.paths.Length == 0)
            {
                return;
            }

            string sourcePath = DragAndDrop.paths[0];
            if (!File.Exists(sourcePath) || Path.GetExtension(sourcePath).ToLower() != ".gguf")
            {
                return;
            }

            pendingModelPath = sourcePath;
            if (modelName != null)
            {
                modelName.value = Path.GetFileNameWithoutExtension(sourcePath);
                modelName.style.display = DisplayStyle.Flex;
                modelName.Focus();
            }
            if (modelSave != null)
            {
                modelSave.style.display = DisplayStyle.Flex;
            }
        }

        private void ConfirmCopyModel()
        {
            if (string.IsNullOrEmpty(pendingModelPath))
            {
                return;
            }

            string desiredName = modelName.value.Trim();
            if (string.IsNullOrEmpty(desiredName))
            {
                EditorUtility.DisplayDialog("Error", "Model name cannot be empty.", "OK");
                return;
            }

            if (desiredName.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
            {
                desiredName = desiredName[..^5];
            }

            string targetFolder = Path.Combine(Application.dataPath, "StreamingAssets/Models");
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            string targetPath = Path.Combine(targetFolder, desiredName + ".gguf");
            if (File.Exists(targetPath))
            {
                if (!EditorUtility.DisplayDialog("Overwrite?", $"Model '{desiredName}.gguf' already exists. Overwrite?", "Yes", "No"))
                {
                    return;
                }
            }

            try
            {
                File.Copy(pendingModelPath, targetPath, true);
                AssetDatabase.Refresh();

                if (modelName != null)
                {
                    modelName.style.display = DisplayStyle.None;
                }

                if (modelSave != null)
                {
                    modelSave.style.display = DisplayStyle.None;
                }

                pendingModelPath = null;
                modelName.value = "";
                RefreshModelList();
                SelectModelByPath(targetPath);
                SaveSelectedModelPath(targetPath);
                UpdateSettingsEnabled();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to copy model: {ex.Message}", "OK");
            }
        }

        public static class BackendPrefs
        {
            private const string PrefKey = "ModelSettings_SelectedBackend";

            public static LLamaBackendType GetSelectedBackend()
            {
                return (LLamaBackendType)EditorPrefs.GetInt(PrefKey, (int)LLamaBackendType.CPU);
            }

            public static void SetSelectedBackend(LLamaBackendType backend)
            {
                EditorPrefs.SetInt(PrefKey, (int)backend);
            }
        }

        private void OnBackendChanged(ChangeEvent<Enum> evt)
        {
            var newBackend = (LLamaBackendType)evt.newValue;
            SwitchBackend(newBackend);
        }

        private void SwitchBackend(LLamaBackendType backend)
        {
            string baseFolder = Path.Combine(Application.streamingAssetsPath, "LLamaBackends/Windows", backend.ToString());
            if (backend == LLamaBackendType.CPU)
            {
                baseFolder = Path.Combine(baseFolder, "avx2");
            }

            string targetFolder = Path.Combine(Application.dataPath, "PersonaEngine/Plugins/LlamaBackend");

            if (!Directory.Exists(baseFolder))
            {
                return;
            }

            if (Directory.Exists(targetFolder))
            {
                try
                {
                    Directory.Delete(targetFolder, true);
                }
                catch (Exception) { }
            }

            Directory.CreateDirectory(targetFolder);

            foreach (string file in Directory.GetFiles(baseFolder, "*", SearchOption.AllDirectories))
            {
                string relativePath = file[(baseFolder.Length + 1)..];
                string dest = Path.Combine(targetFolder, relativePath);
                if (Path.GetExtension(dest).Equals(".bytes", StringComparison.OrdinalIgnoreCase))
                {
                    dest = Path.ChangeExtension(dest, ".dll");
                }
                else if (!Path.HasExtension(dest))
                {
                    dest += ".dll";
                }
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                try
                {
                    File.Copy(file, dest, true);
                }
                catch (Exception) { }
            }

            foreach (string metaFile in Directory.GetFiles(targetFolder, "*.bytes.meta", SearchOption.AllDirectories))
            {
                try
                {
                    File.Delete(metaFile);
                }
                catch { }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            EditorApplication.delayCall += () =>
            {
                string[] dllFiles = Directory.GetFiles(targetFolder, "*.dll", SearchOption.AllDirectories);
                foreach (string fullPath in dllFiles)
                {
                    string relativePath = "Assets" + fullPath.Replace(Application.dataPath, "").Replace('\\', '/');
                    PluginImporter importer = AssetImporter.GetAtPath(relativePath) as PluginImporter;
                    if (importer != null)
                    {
                        importer.SetCompatibleWithEditor(true);
                        importer.SetCompatibleWithAnyPlatform(false);
                        importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, false);
                        importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows, false);
                        importer.SaveAndReimport();
                    }
                }

                AssetDatabase.Refresh();
                BackendPrefs.SetSelectedBackend(backend);
                ModelPathProvider.SelectedBackend = backend;
                ModelPathProvider.ApplyBackendToLLama(backend);
            };
        }
    }
}