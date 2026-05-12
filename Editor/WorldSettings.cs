using LLama;
using LLama.Common;
using LLama.Sampling;
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PersonaEngine
{
    public class WorldSettings : EditorWindow
    {
        [MenuItem("Tools/Persona Engine/World Settings", false, 1)]
        public static void ShowExample()
        {
            WorldSettings wnd = GetWindow<WorldSettings>("World Settings");
            wnd.minSize = new Vector2(550, 520);
            wnd.minSize = new Vector2(550, 520);
        }

        private VisualElement promptSettings;
        private TextField positivePrompt;
        private TextField negativePrompt;
        private Slider temperature;
        private Slider topP;
        private SliderInt maxTokens;
        private Slider freqPenalty;
        private Slider presPenalty;
        private SliderInt gpuLayers;
        private SliderInt contextSize;
        private Button startGen;

        private TextField descWorld;
        private Toggle generateDesc;
        private Button save;

        private const string PREFS_POSITIVE = "WorldSettings_PositivePrompt";
        private const string PREFS_NEGATIVE = "WorldSettings_NegativePrompt";
        private const string PREFS_TEMP = "WorldSettings_Temperature";
        private const string PREFS_TOPP = "WorldSettings_TopP";
        private const string PREFS_MAXTOKENS = "WorldSettings_MaxTokens";
        private const string PREFS_FREQPEN = "WorldSettings_FreqPenalty";
        private const string PREFS_PRESPEN = "WorldSettings_PresencePenalty";
        private const string PREFS_GPULAYERS = "WorldSettings_GpuLayers";
        private const string PREFS_CONTEXTSIZE = "WorldSettings_ContextSize";

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.minWidth = 550;
            root.style.minHeight = 520;
            root.style.maxWidth = 550;
            root.style.maxHeight = 520;
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/PersonaEngine/Editor/UI/WorldSettings.uxml");
            visualTree.CloneTree(root);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/PersonaEngine/Editor/UI/WorldSettings.uss");
            root.styleSheets.Add(styleSheet);

            descWorld = root.Q<TextField>("desc-world");
            descWorld.AddToClassList("desc-world");
            generateDesc = root.Q<Toggle>("generate-desc-world");
            save = root.Q<Button>("save");
            promptSettings = root.Q<VisualElement>("generate-settings");
            promptSettings.AddToClassList("prompt-settings-hide");
            ShowPromptSettings(promptSettings);

            string worldPath = Path.Combine(Application.streamingAssetsPath, "WorldInfo.txt");
            if (File.Exists(worldPath))
            {
                descWorld.value = File.ReadAllText(worldPath);
            }
            else
            {
                descWorld.value = "";
            }

            if (generateDesc != null && descWorld != null)
            {
                generateDesc.RegisterValueChangedCallback(evt =>
                {
                    bool isOn = evt.newValue;
                    promptSettings.style.display = isOn ? DisplayStyle.Flex : DisplayStyle.None;
                });
            }

            if (save != null)
            {
                save.clicked += () =>
                {
                    SaveWorld(descWorld.value);
                };
            }
        }

        private void ShowPromptSettings(VisualElement promptSettings)
        {
            promptSettings.Clear();

            positivePrompt = new TextField("Positive Prompt");
            positivePrompt.AddToClassList("positive-prompt");
            promptSettings.Add(positivePrompt);

            negativePrompt = new TextField("Negative Prompt");
            negativePrompt.AddToClassList("negative-prompt");
            promptSettings.Add(negativePrompt);

            temperature = new Slider("Temperature", 0f, 2f);
            temperature.AddToClassList("temp");
            temperature.showInputField = true;
            temperature.RegisterValueChangedCallback(evt =>
            {
                float f = Mathf.Round(evt.newValue / 0.1f) * 0.1f;
                f = Mathf.Clamp(f, 0f, 2f);
                if (!Mathf.Approximately(temperature.value, f))
                {
                    temperature.SetValueWithoutNotify(f);
                }
            });
            temperature.value = 0.7f;
            promptSettings.Add(temperature);

            topP = new Slider("Top-P", 0f, 1f);
            topP.AddToClassList("top-p");
            topP.showInputField = true;
            topP.RegisterValueChangedCallback(evt =>
            {
                float f = Mathf.Round(evt.newValue / 0.1f) * 0.1f;
                f = Mathf.Clamp(f, 0f, 2f);
                if (!Mathf.Approximately(topP.value, f))
                {
                    topP.SetValueWithoutNotify(f);
                }
            });
            topP.value = 0.9f;
            promptSettings.Add(topP);

            maxTokens = new SliderInt("Max Tokens", 64, 1024);
            maxTokens.AddToClassList("max-tokens");
            maxTokens.showInputField = true;
            maxTokens.RegisterValueChangedCallback(evt =>
            {
                int value = evt.newValue;
                int exponent = Mathf.RoundToInt(Mathf.Log(value, 2));
                int newValue = Mathf.Clamp((int)Mathf.Pow(2, exponent), 64, 1024);

                if (newValue != maxTokens.value)
                {
                    maxTokens.SetValueWithoutNotify(newValue);
                }
            });
            maxTokens.value = 512;
            promptSettings.Add(maxTokens);

            freqPenalty = new Slider("Frequency Penalty", 0f, 2f);
            freqPenalty.AddToClassList("freq-pen");
            freqPenalty.showInputField = true;
            freqPenalty.RegisterValueChangedCallback(evt =>
            {
                float f = Mathf.Round(evt.newValue / 0.1f) * 0.1f;
                f = Mathf.Clamp(f, 0f, 2f);
                if (!Mathf.Approximately(freqPenalty.value, f))
                {
                    freqPenalty.SetValueWithoutNotify(f);
                }
            });
            freqPenalty.value = 0.5f;
            promptSettings.Add(freqPenalty);

            presPenalty = new Slider("Presence Penalty", 0f, 2f);
            presPenalty.AddToClassList("pres-pen");
            presPenalty.showInputField = true;
            presPenalty.RegisterValueChangedCallback(evt =>
            {
                float f = Mathf.Round(evt.newValue / 0.1f) * 0.1f;
                f = Mathf.Clamp(f, 0f, 2f);
                if (!Mathf.Approximately(presPenalty.value, f))
                {
                    presPenalty.SetValueWithoutNotify(f);
                }
            });
            presPenalty.value = 0.5f;
            promptSettings.Add(presPenalty);

            gpuLayers = new SliderInt("GPU Layers", -1, 20);
            gpuLayers.AddToClassList("gpu-layers");
            gpuLayers.showInputField = true;
            gpuLayers.RegisterValueChangedCallback(evt =>
            {
                int i = Mathf.Clamp(evt.newValue, -1, 20);
                if (gpuLayers.value != i)
                {
                    gpuLayers.SetValueWithoutNotify(i);
                }
            });
            gpuLayers.value = 20;
            promptSettings.Add(gpuLayers);

            contextSize = new SliderInt("Context Size", 512, 4096);
            contextSize.AddToClassList("context-size");
            contextSize.showInputField = true;
            contextSize.RegisterValueChangedCallback(evt =>
            {
                int value = evt.newValue;
                int exponent = Mathf.RoundToInt(Mathf.Log(value, 2));
                int newValue = Mathf.Clamp((int)Mathf.Pow(2, exponent), 512, 4096);

                if (newValue != contextSize.value)
                {
                    contextSize.SetValueWithoutNotify(newValue);
                }
            });
            contextSize.value = 1024;
            promptSettings.Add(contextSize);

            startGen = new Button(() =>
            {
                OnGenerateClicked();
            })
            {
                text = "Generate"
            };
            startGen.AddToClassList("start-gen");
            promptSettings.Add(startGen);

            LoadSettings();
        }

        private async void OnGenerateClicked()
        {
            SaveSettings();

            descWorld.SetEnabled(false);
            descWorld.value = "Generate description...";
            positivePrompt.SetEnabled(false);
            negativePrompt.SetEnabled(false);
            temperature.SetEnabled(false);
            topP.SetEnabled(false);
            maxTokens.SetEnabled(false);
            freqPenalty.SetEnabled(false);
            presPenalty.SetEnabled(false);
            gpuLayers.SetEnabled(false);
            contextSize.SetEnabled(false);
            startGen.SetEnabled(false);
            generateDesc.SetEnabled(false);
            save.SetEnabled(false);

            try
            {
                string positive = positivePrompt.value;
                string negative = negativePrompt.value;

                string systemMsg = "You are a game world description generator, following instructions strictly." +
                                   "Your task: based on the user's request, create a single, coherent, and complete description of a world." +
                                   "The description must be meaningful, without repetition, and must end with a period." +
                                   "Do not cut off the thought, do not use incomplete sentences, and do not write any code." +
                                   "Begin immediately with the description, without introductory phrases like \"Here is a description of the world:\"." +
                                   "Genre and assumptions are determined by the user's request. If the request does not explicitly mention fantastical or supernatural elements, the world remains strictly realistic." +
                                   "Never add magic, superpowers, or technological wizardry unless it is in the request." +
                                   "Describe only the world itself; do not turn the answer into a plot or story of specific characters." +
                                   "Start writing the world description immediately.";
                string userContent = positivePrompt.value;
                if (!string.IsNullOrEmpty(negative))
                {
                    userContent += $"\n\nNegative requirements (things to avoid): {negative}";
                }

                string fullPrompt = $"System: {systemMsg}\n\n" +
                                    $"User:\n{userContent}\n\n";

                float temp = temperature.value;
                float tP = topP.value;
                int targetMaxTokens = maxTokens.value;
                int bufferTokens = Mathf.Max(10, (int)(targetMaxTokens * 0.2f));
                int generationMaxTokens = targetMaxTokens + bufferTokens;
                float freqPen = freqPenalty.value;
                float presPen = presPenalty.value;
                int gpuL = gpuLayers.value;
                int cntSize = contextSize.value;

                var samplingPipeline = new DefaultSamplingPipeline
                {
                    Temperature = temp,
                    TopP = tP,
                    FrequencyPenalty = freqPen,
                    PresencePenalty = presPen
                };

                string modelPath = ModelSettings.GetSelectedModelPath();
                if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
                {
                    string modelsFolder = Path.Combine(Application.streamingAssetsPath, "Models");
                    if (Directory.Exists(modelsFolder))
                    {
                        string[] models = Directory.GetFiles(modelsFolder, "*.gguf");
                        if (models.Length > 0)
                        {
                            modelPath = models[0];
                        }
                    }
                }
                if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
                {
                    descWorld.value = "Error: model not found. Please select a model first.";
                    return;
                }

                var parameters = new ModelParams(modelPath)
                {
                    ContextSize = (uint)cntSize,
                    GpuLayerCount = gpuL,
                };

                ModelPathProvider.ApplyBackendToLLama(ModelPathProvider.SelectedBackend);
                using var weights = LLamaWeights.LoadFromFile(parameters);
                using var context = weights.CreateContext(parameters);
                var executor = new StatelessExecutor(weights, parameters);

                var inferenceParams = new InferenceParams
                {
                    MaxTokens = generationMaxTokens,
                    SamplingPipeline = samplingPipeline,
                };

                descWorld.value = "";
                string rawResult = "";
                await foreach (var token in executor.InferAsync(fullPrompt, inferenceParams))
                {
                    rawResult += token;
                    descWorld.value += token;
                }

                string finalText = EnsureCompleteSentence(rawResult, targetMaxTokens);

                if (string.IsNullOrWhiteSpace(finalText) || finalText.Length < 10)
                {
                    finalText = "Failed to generate description. Try increasing Max Tokens or modifying the prompts.";
                }
                descWorld.value = finalText;
            }
            catch (Exception ex)
            {
                descWorld.value = $"Error: {ex.Message}";
            }
            finally
            {
                descWorld.SetEnabled(true);
                save.SetEnabled(true);
                positivePrompt.SetEnabled(true);
                negativePrompt.SetEnabled(true);
                temperature.SetEnabled(true);
                topP.SetEnabled(true);
                maxTokens.SetEnabled(true);
                freqPenalty.SetEnabled(true);
                presPenalty.SetEnabled(true);
                gpuLayers.SetEnabled(true);
                contextSize.SetEnabled(true);
                startGen.SetEnabled(true);
                generateDesc.SetEnabled(true);
            }
        }

        private string EnsureCompleteSentence(string text, int maxTokens)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            int maxChars = maxTokens * 5;

            string truncated = text.Length > maxChars ? text[..maxChars] : text;

            int lastDot = truncated.LastIndexOf('.');
            int lastExcl = truncated.LastIndexOf('!');
            int lastQues = truncated.LastIndexOf('?');
            int lastPunct = Math.Max(lastDot, Math.Max(lastExcl, lastQues));

            if (lastPunct >= 0)
            {
                return truncated[..(lastPunct + 1)].Trim();
            }

            string trimmed = truncated.TrimEnd();
            if (trimmed.Length > 0)
            {
                return trimmed + ".";
            }
            return text;
        }

        private void SaveSettings()
        {
            if (positivePrompt != null)
            {
                EditorPrefs.SetString(PREFS_POSITIVE, positivePrompt.value);
                EditorPrefs.SetString(PREFS_NEGATIVE, negativePrompt.value);
                EditorPrefs.SetFloat(PREFS_TEMP, temperature.value);
                EditorPrefs.SetFloat(PREFS_TOPP, topP.value);
                EditorPrefs.SetInt(PREFS_MAXTOKENS, (int)maxTokens.value);
                EditorPrefs.SetFloat(PREFS_FREQPEN, freqPenalty.value);
                if (presPenalty != null)
                {
                    EditorPrefs.SetFloat(PREFS_PRESPEN, presPenalty.value);
                }
                EditorPrefs.SetInt(PREFS_GPULAYERS, (int)gpuLayers.value);
                EditorPrefs.SetInt(PREFS_CONTEXTSIZE, (int)contextSize.value);
            }
        }

        private void OnDisable()
        {
            SaveSettings();
        }

        private void LoadSettings()
        {
            if (positivePrompt != null)
            {
                positivePrompt.value = EditorPrefs.GetString(PREFS_POSITIVE, "");
                negativePrompt.value = EditorPrefs.GetString(PREFS_NEGATIVE, "");
                temperature.value = EditorPrefs.GetFloat(PREFS_TEMP, 0.7f);
                topP.value = EditorPrefs.GetFloat(PREFS_TOPP, 0.9f);
                maxTokens.value = EditorPrefs.GetInt(PREFS_MAXTOKENS, 512);
                freqPenalty.value = EditorPrefs.GetFloat(PREFS_FREQPEN, 0.5f);
                if (presPenalty != null)
                {
                    presPenalty.value = EditorPrefs.GetFloat(PREFS_PRESPEN, 0.5f);
                }
                gpuLayers.value = EditorPrefs.GetInt(PREFS_GPULAYERS, 20);
                contextSize.value = EditorPrefs.GetInt(PREFS_CONTEXTSIZE, 1024);
            }
        }
        private static void SaveWorld(string text)
        {
            string folderPath = Path.Combine(Application.streamingAssetsPath);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            string filePath = Path.Combine(folderPath, "WorldInfo.txt");
            File.WriteAllText(filePath, text);
            AssetDatabase.Refresh();
        }
    }
}
