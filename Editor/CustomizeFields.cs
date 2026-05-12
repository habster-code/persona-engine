using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PersonaEngine
{
    public class CustomizeFields : EditorWindow
    {
        private List<CustomFieldDef> fields;
        private Action<List<CustomFieldDef>> onSchemaChanged;
        private ScrollView listContainer;

        public static void ShowExample(List<CustomFieldDef> currentFields, Action<List<CustomFieldDef>> onSchemaChanged)
        {
            var wnd = GetWindow<CustomizeFields>(true, "Customize Character Fields");
            wnd.minSize = new Vector2(600, 450);
            wnd.maxSize = new Vector2(600, 700);
            wnd.fields = currentFields.Select(CloneField).ToList();
            wnd.onSchemaChanged = onSchemaChanged;
            wnd.RedrawFields();
            wnd.Repaint();
        }

        private void CreateGUI()
        {
            fields ??= new List<CustomFieldDef>();

            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Column;
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/PersonaEngine/Editor/UI/CustomizeFields.uss");
            root.styleSheets.Add(styleSheet);

            listContainer = new ScrollView();
            listContainer.style.flexGrow = 1;
            listContainer.style.maxHeight = 400;
            root.Add(listContainer);

            var buttonRow = new VisualElement() { style = { flexDirection = FlexDirection.Row, marginTop = 10 } };

            var addBtn = new Button(AddField)
            {
                text = "Add Field"
            };
            addBtn.AddToClassList("add-btn");

            var applyBtn = new Button(SaveAndClose)
            {
                text = "Apply & Close"
            };
            applyBtn.AddToClassList("apply-btn");

            buttonRow.Add(addBtn);
            buttonRow.Add(applyBtn);
            root.Add(buttonRow);
            RedrawFields();
        }

        private void RedrawFields()
        {
            if (fields == null)
            {
                return;
            }
            listContainer.Clear();

            for (int i = 0; i < fields.Count; i++)
            {
                var def = fields[i];
                var row = new VisualElement
                {
                    style =
                {
                    flexDirection = FlexDirection.Row,
                    marginBottom = 5,
                    alignItems = Align.Center
                }
                };

                var nameLabel = new Label("Name");
                nameLabel.AddToClassList("name-label");

                var nameInput = new TextField { value = def.fieldName };
                nameInput.AddToClassList("name-input");
                nameInput.RegisterValueChangedCallback(evt => def.fieldName = evt.newValue);
                row.Add(nameLabel);
                row.Add(nameInput);

                var typeLabel = new Label("Type");
                typeLabel.AddToClassList("type-label");
                var typeNames = Enum.GetNames(typeof(CustomFieldType)).ToList();
                int idx = typeNames.IndexOf(def.fieldType.ToString());
                if (idx < 0)
                {
                    idx = 0;
                }
                var typePopup = new PopupField<string>(typeNames, idx);
                typePopup.AddToClassList("type-popup");
                typePopup.RegisterValueChangedCallback(evt =>
                {
                    if (Enum.TryParse(evt.newValue, out CustomFieldType t))
                    {
                        def.fieldType = t;
                        RedrawFields();
                    }
                });
                row.Add(typeLabel);
                row.Add(typePopup);

                if (def.fieldType == CustomFieldType.Dropdown || def.fieldType == CustomFieldType.RadioButton)
                {
                    var optsLabel = new Label(def.fieldType == CustomFieldType.Dropdown ? "Options" : "Buttons");
                    optsLabel.AddToClassList("opts-label");
                    var optsInput = new TextField
                    {
                        value = def.fieldType == CustomFieldType.Dropdown ? def.dropdownOptions : def.radioBtnOptions
                    };
                    optsInput.AddToClassList("opts-input");
                    optsInput.RegisterValueChangedCallback(evt =>
                    {
                        if (def.fieldType == CustomFieldType.Dropdown)
                        {
                            def.dropdownOptions = evt.newValue;
                        }
                        else
                        {
                            def.radioBtnOptions = evt.newValue;
                        }
                    });
                    row.Add(optsLabel);
                    row.Add(optsInput);
                }

                var delBtn = new Button(() => { fields.Remove(def); RedrawFields(); }) { text = "X", style = { width = 30, marginTop = 20, marginLeft = 10, color = Color.red } };
                row.Add(delBtn);

                listContainer.Add(row);
            }
        }

        private void AddField()
        {
            fields.Add(new CustomFieldDef { fieldName = "New Field", fieldType = CustomFieldType.TextLine });
            RedrawFields();
        }

        private void SaveAndClose()
        {
            fields.RemoveAll(f => string.IsNullOrWhiteSpace(f.fieldName));
            onSchemaChanged?.Invoke(fields);
            Close();
        }

        private static CustomFieldDef CloneField(CustomFieldDef source) => new()
        {
            fieldName = source.fieldName,
            fieldType = source.fieldType,
            defaultValue = source.defaultValue,
            dropdownOptions = source.dropdownOptions,
            radioBtnOptions = source.radioBtnOptions
        };
    }
}