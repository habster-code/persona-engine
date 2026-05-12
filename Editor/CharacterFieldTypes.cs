using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PersonaEngine
{
    public enum CustomFieldType
    {
        TextLine,
        TextArea,
        Integer,
        Float,
        Toggle,
        Dropdown,
        RadioButton
    }

    [Serializable]
    public class CustomFieldDef
    {
        public string fieldName;
        public CustomFieldType fieldType;
        public string defaultValue = "";
        public string dropdownOptions = "";
        public string radioBtnOptions = "";
    }

    public static class CharacterFieldSchema
    {
        private const string PREFS_KEY = "CharacterCustomFields_Schema";

        public static List<CustomFieldDef> LoadSchema()
        {
            string json = EditorPrefs.GetString(PREFS_KEY, "[]");
            try
            {
                var wrapper = JsonUtility.FromJson<SchemaWrapper>(json);
                return wrapper?.fields ?? new List<CustomFieldDef>();
            }
            catch
            {
                return new List<CustomFieldDef>();
            }
        }

        public static void SaveSchema(List<CustomFieldDef> fields)
        {
            var wrapper = new SchemaWrapper { fields = fields };
            EditorPrefs.SetString(PREFS_KEY, JsonUtility.ToJson(wrapper));
        }

        [Serializable]
        private class SchemaWrapper
        {
            public List<CustomFieldDef> fields;
        }
    }
}