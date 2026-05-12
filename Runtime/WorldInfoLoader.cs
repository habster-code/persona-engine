using System.IO;
using UnityEngine;

namespace PersonaEngine
{
    public static class WorldInfoLoader
    {
        private static string worldDescription = null;
        private static bool loaded = false;
        public static void LoadOnce()
        {
            if (!loaded)
            {
                string path = Path.Combine(Application.streamingAssetsPath, "WorldInfo.txt");
                if (File.Exists(path))
                {
                    worldDescription = File.ReadAllText(path);
                }
                else
                {
                    worldDescription = "A typical fantasy world.";
                }
                loaded = true;
            }
        }
        public static string WorldDescription
        {
            get
            {
                if (!loaded)
                {
                    LoadOnce();
                }
                return worldDescription;
            }
        }
    }
}
