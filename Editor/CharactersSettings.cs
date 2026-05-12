using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PersonaEngine
{
    public class CharactersSettings : EditorWindow
    {
        [MenuItem("Tools/Persona Engine/Characters Settings", false, 2)]
        public static void ShowExample()
        {
            var wnd = GetWindow<CharactersSettings>("Characters Settings");
            wnd.minSize = new Vector2(600, 730);
            wnd.maxSize = new Vector2(600, 730);
        }

        private ScrollView character;
        private List<CustomFieldDef> currentSchema;

        [Serializable]
        public class Trigger
        {
            public string keyword;
            public string reaction;
        }

        [Serializable]
        public class CustomFieldValue
        {
            public string fieldName;
            public string value;
        }

        [Serializable]
        public class CharacterData
        {
            public string name;
            public string description;
            public List<Trigger> triggers = new();
            public List<CustomFieldValue> customFields = new();
        }

        private void LoadSchema() => currentSchema = CharacterFieldSchema.LoadSchema();
        private void SaveSchema() => CharacterFieldSchema.SaveSchema(currentSchema);

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.minWidth = 600;
            root.style.minHeight = 730;
            root.style.maxWidth = 600;
            root.style.maxHeight = 730;
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/PersonaEngine/Editor/UI/CharactersSettings.uxml");
            visualTree.CloneTree(root);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/PersonaEngine/Editor/UI/CharactersSettings.uss");
            root.styleSheets.Add(styleSheet);

            var customizeBtn = new Button(() =>
            {
                SaveAllCharacters();
                CustomizeFields.ShowExample(currentSchema, OnSchemaUpdated);
            })
            {
                text = "Customize Fields"
            };
            customizeBtn.AddToClassList("cust-btn");
            root.Insert(1, customizeBtn);

            var addCharacter = root.Q<Button>("add-character");
            character = root.Q<ScrollView>("character");
            var save = root.Q<Button>("save");

            if (addCharacter != null && character != null)
            {
                addCharacter.clicked += () => CreateCharacter(character, null);
            }

            if (save != null)
            {
                save.clicked += () => SaveAllCharacters();
            }

            character?.Clear();

            LoadSchema();
            LoadCharacters();
        }

        private void OnSchemaUpdated(List<CustomFieldDef> newSchema)
        {
            currentSchema = newSchema;
            SaveSchema();
            var scroll = rootVisualElement.Q<ScrollView>("character");
            scroll?.Clear();
            LoadCharacters();
        }

        private void CreateCharacter(VisualElement parent, CharacterData existingData)
        {
            var block = new VisualElement();
            block.AddToClassList("block-char");
            block.userData = existingData ?? new CharacterData
            {
                name = "NewCharacter",
                description = "",
                triggers = new List<Trigger>(),
                customFields = new List<CustomFieldValue>()
            };

            var nameField = new TextField("Name")
            {
                value = existingData?.name ?? "",
                multiline = false
            };
            nameField.AddToClassList("name-char");
            block.Add(nameField);

            var descField = new TextField("Character Description")
            {
                value = existingData?.description ?? "",
                multiline = true
            };
            descField.AddToClassList("character-description");
            block.Add(descField);

            var customContainer = new VisualElement();
            customContainer.AddToClassList("custom-fields-container");
            if (currentSchema != null)
            {
                var savedFields = existingData?.customFields ?? new List<CustomFieldValue>();
                foreach (var def in currentSchema)
                {
                    var row = new VisualElement();
                    row.AddToClassList("custom-fields");
                    row.userData = def.fieldType;

                    var label = new Label(def.fieldName);
                    label.AddToClassList("custom-label");
                    row.Add(label);

                    var saved = savedFields.Find(s => s.fieldName == def.fieldName);
                    string initValue = saved?.value ?? def.defaultValue;
                    VisualElement input = null;

                    switch (def.fieldType)
                    {
                        case CustomFieldType.TextLine:
                            input = new TextField { value = initValue };
                            input.AddToClassList("text-line");
                            break;
                        case CustomFieldType.TextArea:
                            input = new TextField { value = initValue, multiline = true };
                            input.AddToClassList("text-area");
                            break;
                        case CustomFieldType.Integer:
                            var intField = new IntegerField();
                            if (int.TryParse(initValue, out int iv))
                            {
                                intField.value = iv;
                            }
                            input = intField;
                            input.AddToClassList("integer");
                            break;
                        case CustomFieldType.Float:
                            var floatField = new FloatField();
                            if (float.TryParse(initValue, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out float fv))
                            {
                                floatField.value = fv;
                            }
                            input = floatField;
                            input.AddToClassList("float");
                            break;
                        case CustomFieldType.Toggle:
                            var tog = new Toggle();
                            bool.TryParse(initValue, out bool bv);
                            tog.value = bv;
                            input = tog;
                            input.AddToClassList("toggle");
                            break;
                        case CustomFieldType.Dropdown:
                            var opts = def.dropdownOptions.Split(',').Select(s => s.Trim()).ToList();
                            var popup = new PopupField<string>(opts, 0);
                            int idx = opts.IndexOf(initValue);
                            if (idx >= 0)
                            {
                                popup.index = idx;
                            }
                            input = popup;
                            input.AddToClassList("dropdown");
                            break;
                        case CustomFieldType.RadioButton:
                            var radioOpts = def.radioBtnOptions.Split(',').Select(s => s.Trim()).ToList();
                            var radioGroup = new RadioButtonGroup(def.fieldName, radioOpts)
                            {
                                label = ""
                            };
                            int ridx = radioOpts.IndexOf(initValue);
                            if (ridx >= 0)
                            {
                                radioGroup.value = ridx;
                            }
                            input = radioGroup;
                            break;
                    }

                    if (input != null)
                    {
                        input.style.flexGrow = 1;
                        input.name = "custom-input";
                        row.Add(input);
                    }
                    customContainer.Add(row);
                }
            }
            block.Add(customContainer);

            var triggers = existingData?.triggers ?? new List<Trigger>();
            var triggersFoldout = new Foldout
            {
                text = "Triggers",
                value = false
            };
            triggersFoldout.AddToClassList("triggers-foldout");

            var triggersScroll = new ScrollView { style = { maxHeight = 200 } };
            var triggersContainer = new VisualElement();
            triggersContainer.AddToClassList("triggers-container");
            triggersScroll.Add(triggersContainer);

            void RefreshTriggers()
            {
                triggersContainer.Clear();
                foreach (var trig in triggers)
                {
                    var row = CreateTriggerRow(trig, () =>
                    {
                        triggers.Remove(trig);
                        RefreshTriggers();
                    });
                    triggersContainer.Add(row);
                }
            }
            RefreshTriggers();
            triggersFoldout.Add(triggersScroll);

            var addTriggerBtn = new Button(() =>
            {
                triggers.Add(new Trigger());
                RefreshTriggers();
            })
            {
                text = "Add Trigger"
            };
            addTriggerBtn.AddToClassList("add-trigger");
            triggersFoldout.Add(addTriggerBtn);
            block.Add(triggersFoldout);

            var removeBtn = new Button(() =>
            {
                var originalData = block.userData as CharacterData;
                string originalName = originalData?.name;
                DeleteCharacter(originalName ?? nameField.value);
                parent.Remove(block);
            })
            {
                text = "Remove Character"
            };
            removeBtn.AddToClassList("remove-char");
            block.Add(removeBtn);

            parent.Add(block);
        }

        private VisualElement CreateTriggerRow(Trigger trigger, Action onRemove)
        {
            var row = new VisualElement();
            row.AddToClassList("trigger-row");

            var keywordField = new TextField("Keyword") { name = "keyword", value = trigger.keyword };
            keywordField.AddToClassList("key-word");
            keywordField.RegisterValueChangedCallback(evt => trigger.keyword = evt.newValue);
            row.Add(keywordField);

            var reactionField = new TextField("Reaction") { name = "reaction", value = trigger.reaction, multiline = true };
            reactionField.AddToClassList("reaction");
            reactionField.RegisterValueChangedCallback(evt => trigger.reaction = evt.newValue);
            row.Add(reactionField);

            var removeBtn = new Button(() => onRemove?.Invoke()) { text = "Delete Trigger" };
            removeBtn.AddToClassList("remove-trg");
            row.Add(removeBtn);
            return row;
        }

        private void SaveAllCharacters()
        {
            try
            {
                var scroll = rootVisualElement.Q<ScrollView>("character");
                if (scroll == null)
                {
                    return;
                }

                foreach (var block in scroll.Children())
                {
                    if (!block.ClassListContains("block-char"))
                    {
                        continue;
                    }
                    if (block.userData is not CharacterData originalData)
                    {
                        continue;
                    }
                    string id = originalData.name;
                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }

                    var nameField = block.Q<TextField>(className: "name-char");
                    var descField = block.Q<TextField>(className: "character-description");

                    var triggers = new List<Trigger>();
                    var triggersFoldout = block.Q<Foldout>(className: "triggers-foldout");
                    var triggersContainer = triggersFoldout?.Q<VisualElement>(className: "triggers-container");
                    if (triggersContainer != null)
                    {
                        foreach (var tRow in triggersContainer.Children())
                        {
                            var keyField = tRow.Q<TextField>("keyword");
                            var reactionField = tRow.Q<TextField>("reaction");
                            if (keyField != null && reactionField != null)
                            {
                                triggers.Add(new Trigger { keyword = keyField.value, reaction = reactionField.value });
                            }
                        }
                    }

                    var customFields = new List<CustomFieldValue>();
                    var customContainer = block.Q<VisualElement>(className: "custom-fields-container");
                    if (customContainer != null)
                    {
                        foreach (var row in customContainer.Children())
                        {
                            var label = row.Q<Label>();
                            if (label == null)
                            {
                                continue;
                            }
                            var typeObj = row.userData;
                            if (typeObj is not CustomFieldType fieldType)
                            {
                                continue;
                            }
                            var input = row.Q<VisualElement>("custom-input");
                            if (input == null)
                            {
                                continue;
                            }

                            string value = "";
                            switch (fieldType)
                            {
                                case CustomFieldType.TextLine:
                                case CustomFieldType.TextArea:
                                    if (input is TextField tf) value = tf.value; break;
                                case CustomFieldType.Integer:
                                    if (input is IntegerField intf) { value = intf.value.ToString(); }
                                    break;
                                case CustomFieldType.Float:
                                    if (input is FloatField ff) { value = ff.value.ToString(System.Globalization.CultureInfo.InvariantCulture); }
                                    break;
                                case CustomFieldType.Toggle:
                                    if (input is Toggle tog) { value = tog.value.ToString(); }
                                    break;
                                case CustomFieldType.Dropdown:
                                    if (input is PopupField<string> popup) value = popup.value; break;
                                case CustomFieldType.RadioButton:
                                    if (input is RadioButtonGroup radio && radio.choices != null)
                                    {
                                        var list = radio.choices.ToList();
                                        if (radio.value >= 0 && radio.value < list.Count) value = list[radio.value];
                                    }
                                    break;
                            }
                            customFields.Add(new CustomFieldValue { fieldName = label.text, value = value });
                        }
                    }

                    string newName = nameField?.value?.Trim();
                    string newDesc = descField?.value?.Trim();

                    var data = new CharacterData
                    {
                        name = !string.IsNullOrWhiteSpace(newName) ? newName : (originalData?.name ?? "Unnamed"),
                        description = !string.IsNullOrWhiteSpace(newDesc) ? newDesc : originalData?.description,
                        triggers = triggers,
                        customFields = customFields
                    };

                    SaveCharacter(data);
                }
                AssetDatabase.Refresh();
            }
            catch (Exception) { }
        }

        private static void SaveCharacter(CharacterData data)
        {
            string folder = Path.Combine(Application.streamingAssetsPath, "CharactersData", data.name);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"{data.name}.json");
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
        }

        private void LoadCharacters()
        {
            string baseFolder = Path.Combine(Application.streamingAssetsPath, "CharactersData");
            if (!Directory.Exists(baseFolder)) return;

            foreach (string subDir in Directory.GetDirectories(baseFolder))
            {
                string characterName = Path.GetFileName(subDir);
                string jsonFile = Path.Combine(subDir, $"{characterName}.json");
                if (File.Exists(jsonFile))
                {
                    string json = File.ReadAllText(jsonFile);
                    var data = JsonUtility.FromJson<CharacterData>(json);
                    CreateCharacter(character, data);
                }
            }
        }

        private static void DeleteCharacter(string name)
        {
            string folder = Path.Combine(Application.streamingAssetsPath, "CharactersData", name);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
                File.Delete(folder + ".meta");
            }
            AssetDatabase.Refresh();
        }

        private void OnDestroy()
        {
            SaveAllCharacters();
        }
    }
}