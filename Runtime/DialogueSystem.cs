using LLama;
using LLama.Common;
using LLama.Sampling;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace PersonaEngine
{

    public class DialogueSystem : MonoBehaviour
    {
        [Header("UI Element Names")]
        [SerializeField] private string dialogueRootName = "dialogue-root";
        [SerializeField] private string chatHistoryName = "chat-history";
        [SerializeField] private string chatInputName = "chat-input";
        [SerializeField] private string sendButtonName = "send-button";
        [SerializeField] private string backButtonName = "back-button";

        [Header("Typewriter")]
        [SerializeField] private float typewriterDelay = 0.02f;

        [Header("Style")]
        [SerializeField] private StyleSheet styleSheet;

        [SerializeField] private string characterPanelName = "character-panel";

        private LLamaWeights weights;
        private LLamaContext context;
        private InteractiveExecutor executor;
        private ChatSession chatSession;
        private string currentCharacterName;
        private string currentCharacterJson;

        private VisualElement dialogueRoot;
        private ScrollView messageContainer;
        private TextField inputField;
        private Button sendButton;
        private Button backButton;
        private VisualElement characterPanel;

        private List<ChatMessageSave> loadedHistory = new();
        private bool isGenerating = false;
        private Coroutine typewriterCoroutine;

        private void Awake()
        {
            ModelPathProvider.LoadPreferences();
        }

        private void OnEnable()
        {
            WorldInfoLoader.LoadOnce();
            ModelPathProvider.ApplyBackendToLLama(ModelPathProvider.SelectedBackend);

            if (!TryGetComponent<UIDocument>(out var uiDoc))
            {
                return;
            }

            var root = uiDoc.rootVisualElement;

            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            dialogueRoot = root.Q<VisualElement>(dialogueRootName);
            messageContainer = root.Q<ScrollView>(chatHistoryName);
            inputField = root.Q<TextField>(chatInputName);
            sendButton = root.Q<Button>(sendButtonName);
            backButton = root.Q<Button>(backButtonName);
            characterPanel = root.Q<VisualElement>(characterPanelName);

            var characterButton = root.Q<Button>("character-dialogue");
            if (characterButton != null)
            {
                characterButton.clicked += () => StartDialogue("Ben");
            }

            if (dialogueRoot != null)
            {
                dialogueRoot.style.display = DisplayStyle.None;
            }

            if (sendButton != null)
            {
                sendButton.clicked += OnSendClicked;
            }

            inputField?.RegisterCallback<KeyDownEvent>(OnKeyDown);

            if (backButton != null)
            {
                backButton.clicked += OnBackClicked;
            }
        }

        private void OnDisable()
        {
            if (sendButton != null)
            {
                sendButton.clicked -= OnSendClicked;
            }

            inputField?.UnregisterCallback<KeyDownEvent>(OnKeyDown);

            if (backButton != null)
            {
                backButton.clicked -= OnBackClicked;
            }

            ReleaseModel();
        }

        public async void StartDialogue(string characterName)
        {
            if (string.IsNullOrEmpty(characterName))
            {
                return;
            }

            if (!string.IsNullOrEmpty(currentCharacterName))
            {
                await EndDialogue();
            }

            currentCharacterName = characterName;

            if (characterPanel != null)
            {
                characterPanel.style.display = DisplayStyle.None;
            }

            if (dialogueRoot != null)
            {
                dialogueRoot.style.display = DisplayStyle.Flex;
            }

            string jsonPath = Path.Combine(Application.streamingAssetsPath, "CharactersData", characterName, $"{characterName}.json");
            if (!File.Exists(jsonPath))
            {
                return;
            }

            currentCharacterJson = File.ReadAllText(jsonPath);

            string historyPath = Path.Combine(Application.streamingAssetsPath, "CharactersData", characterName, $"{characterName}_history.json");
            loadedHistory.Clear();
            if (File.Exists(historyPath))
            {
                try
                {
                    string histJson = File.ReadAllText(historyPath);
                    var wrapper = JsonUtility.FromJson<HistoryWrapper>(histJson);
                    if (wrapper?.messages != null)
                    {
                        loadedHistory = wrapper.messages;
                    }
                }
                catch (Exception) { }

                foreach (var msg in loadedHistory)
                {
                    if (msg.role != "User")
                    {
                        msg.message = CleanResponse(msg.message, characterName);
                    }
                }
            }

            string systemPrompt = BuildSystemPrompt(currentCharacterJson, loadedHistory);

            messageContainer?.Clear();
            foreach (var msg in loadedHistory)
            {
                AddMessageToUI(msg.role, msg.message);
            }

            inputField?.SetEnabled(false);
            sendButton?.SetEnabled(false);

            await InitializeModelAsync(systemPrompt);

            foreach (var msg in loadedHistory)
            {
                AuthorRole role = (msg.role == "User") ? AuthorRole.User : AuthorRole.Assistant;
                chatSession.History.AddMessage(role, msg.message);
            }

            if (chatSession != null)
            {
                inputField?.SetEnabled(true);
                sendButton?.SetEnabled(true);
                inputField?.Focus();
            }
            else
            {
                sendButton?.SetEnabled(false);
                inputField?.SetEnabled(false);
            }
        }

        private async Task EndDialogue()
        {
            if (isGenerating)
            {
                if (typewriterCoroutine != null)
                {
                    StopCoroutine(typewriterCoroutine);
                    typewriterCoroutine = null;
                }
                isGenerating = false;
                sendButton?.SetEnabled(true);
                backButton?.SetEnabled(true);
            }

            await SaveHistory();

            if (dialogueRoot != null)
            {
                dialogueRoot.style.display = DisplayStyle.None;
            }

            if (characterPanel != null)
            {
                characterPanel.style.display = DisplayStyle.Flex;
            }

            currentCharacterName = null;
            loadedHistory.Clear();
            ReleaseModel();
        }

        private async void OnBackClicked()
        {
            await EndDialogue();
        }

        private string BuildSystemPrompt(string characterJson, List<ChatMessageSave> history)
        {
            StringBuilder sb = new();
            sb.AppendLine($"World context:\n{WorldInfoLoader.WorldDescription}\n");
            sb.AppendLine("You are a character in this world. Your entire identity and knowledge are defined by the following JSON data. Embody this character completely and never break character.");
            sb.AppendLine("Character JSON:");
            sb.AppendLine(characterJson);

            sb.AppendLine("\n=== ROLEPLAY RULES ===");
            sb.AppendLine("- You speak as this character. Your response can optionally start with the character's name and a colon, but the important part is the spoken words.");
            sb.AppendLine("- NEVER output meta‑text like \"Response:\", \"END OF USER REPLY\", or any similar markers.");
            sb.AppendLine("- After you finish your reply, stop immediately. Do NOT continue generating text for the user.");
            sb.AppendLine("- Always reply in the same language the user uses.");
            sb.AppendLine("- Keep responses concise and natural, like real conversation.");

            if (history != null && history.Count > 0)
            {
                sb.AppendLine("\nPrevious conversation (for context, do not repeat greetings if already done):");
                foreach (var msg in history)
                {
                    sb.AppendLine($"{msg.role}: {msg.message}");
                }
            }

            sb.AppendLine("\nNow, as the character, respond to the user's next message following all the rules above.");

            return sb.ToString();
        }

        private async Task InitializeModelAsync(string systemPrompt)
        {
            ReleaseModel();

            string modelPath = ModelPathProvider.GetModelPath();
            if (string.IsNullOrEmpty(modelPath))
            {
                AddMessageToUI("System", "No AI model found. Please add a model in Model Settings.");
                return;
            }

            var parameters = new ModelParams(modelPath)
            {
                ContextSize = (uint)ModelPathProvider.ContextSize,
                GpuLayerCount = ModelPathProvider.GpuLayerCount
            };

            try
            {
                await Task.Run(() =>
                {
                    weights = LLamaWeights.LoadFromFile(parameters);
                    uint nativeContext = (uint)weights.ContextSize;

                    if (ModelPathProvider.ContextSizeAuto)
                    {
                        parameters.ContextSize = nativeContext;
                    }
                    else
                    {
                        parameters.ContextSize = (uint)ModelPathProvider.ContextSize;
                    }

                    if (ModelPathProvider.GpuLayerCountAuto)
                    {
                        parameters.GpuLayerCount = -1;
                    }
                    else
                    {
                        parameters.GpuLayerCount = ModelPathProvider.GpuLayerCount;
                    }

                    context = weights.CreateContext(parameters);
                    executor = new InteractiveExecutor(context);
                });

                chatSession = new ChatSession(executor);
                chatSession.History.AddMessage(AuthorRole.System, systemPrompt);
            }
            catch (Exception)
            {

            }

            if (chatSession == null)
            {
                AddMessageToUI("System", "Failed to load model. Check model path or settings.");
                sendButton?.SetEnabled(false);
            }
        }

        private void ReleaseModel()
        {
            chatSession = null;
            context?.Dispose();
            weights?.Dispose();
            executor = null;
        }

        private async Task SaveHistory()
        {
            if (chatSession == null || string.IsNullOrEmpty(currentCharacterName))
            {
                return;
            }

            var messagesToSave = new List<ChatMessageSave>();
            foreach (var msg in chatSession.History.Messages)
            {
                if (msg.AuthorRole == AuthorRole.System)
                {
                    continue;
                }

                string role = (msg.AuthorRole == AuthorRole.User) ? "User" : currentCharacterName;
                messagesToSave.Add(new ChatMessageSave { role = role, message = msg.Content });
            }

            var wrapper = new HistoryWrapper { messages = messagesToSave };
            string json = JsonUtility.ToJson(wrapper, true);
            string dir = Path.Combine(Application.streamingAssetsPath, "CharactersData", currentCharacterName);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"{currentCharacterName}_history.json");
            await File.WriteAllTextAsync(path, json);
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
        }

        private async void OnSendClicked() => await SendMessageAsync();

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                evt.StopPropagation();
                _ = SendMessageAsync();
            }
        }

        private async Task SendMessageAsync()
        {
            if (chatSession == null || executor == null || isGenerating)
            {
                AddMessageToUI("System", "Model not loaded. Please check model settings and try again.");
                return;
            }

            string userMessage = inputField.text.Trim();
            if (string.IsNullOrEmpty(userMessage))
            {
                return;
            }

            AddMessageToUI("User", userMessage);
            inputField.value = "";
            isGenerating = true;
            sendButton?.SetEnabled(false);
            backButton?.SetEnabled(false);

            string reply;
            try
            {
                var userMsg = new ChatHistory.Message(AuthorRole.User, userMessage);
                var samplingPipeline = new DefaultSamplingPipeline
                {
                    Temperature = ModelPathProvider.Temperature,
                    TopP = ModelPathProvider.TopP,
                    FrequencyPenalty = ModelPathProvider.FrequencyPenalty,
                    PresencePenalty = ModelPathProvider.PresencePenalty
                };

                var antiPrompts = new List<string> { "\nUser:", "\nPlayer:" };

                if (!string.IsNullOrEmpty(currentCharacterName))
                {
                    antiPrompts.Add($"\n{currentCharacterName}:");
                    antiPrompts.Add($"{currentCharacterName}:");
                }

                var inferenceParams = new InferenceParams
                {
                    MaxTokens = ModelPathProvider.MaxTokens,
                    SamplingPipeline = samplingPipeline,
                    AntiPrompts = antiPrompts
                };

                var responseBuilder = new StringBuilder();
                await foreach (var token in chatSession.ChatAsync(userMsg, inferenceParams))
                {
                    responseBuilder.Append(token);
                }

                reply = responseBuilder.ToString().Trim();
                reply = CleanResponse(reply, currentCharacterName);
                var lastAssistantMsg = chatSession.History.Messages
                    .LastOrDefault(m => m.AuthorRole == AuthorRole.Assistant);
                if (lastAssistantMsg != null)
                {
                    lastAssistantMsg.Content = reply;
                }
            }
            catch (Exception)
            {
                reply = "Error generating response.";
            }

            await SaveHistory();

            if (!string.IsNullOrEmpty(reply))
            {
                typewriterCoroutine = StartCoroutine(TypeResponse(reply, currentCharacterName));
            }
            else
            {
                isGenerating = false;
                sendButton?.SetEnabled(true);
                backButton?.SetEnabled(true);
            }
        }

        private string CleanResponse(string rawResponse, string characterName)
        {
            if (string.IsNullOrEmpty(rawResponse))
            {
                return rawResponse;
            }

            string[] prefixesToRemove = { "Character:", "Response:", "AI:", "Assistant:", "Bot:", "Char:" };
            if (!string.IsNullOrEmpty(characterName))
            {
                var namePrefix = characterName + ":";
                prefixesToRemove = new[] { namePrefix }.Concat(prefixesToRemove).ToArray();
            }

            foreach (var prefix in prefixesToRemove)
            {
                if (rawResponse.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    rawResponse = rawResponse[prefix.Length..].TrimStart();
                    break;
                }
            }

            string[] stopSuffixes = { "User:", "Player:", "Assistant:", "Character:", "Response:" };
            foreach (var suffix in stopSuffixes)
            {
                if (rawResponse.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    rawResponse = rawResponse[..^suffix.Length].TrimEnd();
                }
            }

            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                return "(character did not respond)";
            }

            return rawResponse.Trim();
        }

        private IEnumerator TypeResponse(string fullText, string sender)
        {
            var label = new Label($"{sender}: ") { style = { whiteSpace = WhiteSpace.Normal, marginBottom = 5, paddingLeft = 5, paddingRight = 5 } };
            messageContainer?.Add(label);

            string typed = "";
            foreach (char c in fullText)
            {
                typed += c;
                label.text = $"{sender}: {typed}";
                messageContainer.schedule.Execute(() =>
                {
                    messageContainer.verticalScroller.value = messageContainer.verticalScroller.highValue;
                }).StartingIn(0);
                yield return new WaitForSeconds(typewriterDelay);
            }

            isGenerating = false;
            sendButton?.SetEnabled(true);
            backButton?.SetEnabled(true);
            typewriterCoroutine = null;
        }

        private void AddMessageToUI(string sender, string message)
        {
            if (messageContainer == null)
            {
                return;
            }

            var label = new Label($"{sender}: {message}")
            {
                style =
            {
                whiteSpace = WhiteSpace.Normal,
                marginBottom = 5,
                paddingLeft = 5,
                paddingRight = 5
            }
            };
            messageContainer.Add(label);
            messageContainer.schedule.Execute(() =>
            {
                messageContainer.verticalScroller.value = messageContainer.verticalScroller.highValue;
            }).StartingIn(0);
        }

        [Serializable] public class ChatMessageSave { public string role; public string message; }
        [Serializable] public class HistoryWrapper { public List<ChatMessageSave> messages; }
    }
}