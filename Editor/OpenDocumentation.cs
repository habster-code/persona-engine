using UnityEditor;
using UnityEngine;

namespace PersonaEngine
{
    public static class OpenDocumentation
    {
        [MenuItem("Tools/Persona Engine/Documentation")]
        public static void OpenDoc()
        {
            Application.OpenURL("https://github.com/habster-code/persona-engine/wiki");
        }
    }
}
