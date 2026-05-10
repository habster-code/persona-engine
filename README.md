# 🧠Persona Engine

Full documentation on [Wiki](https://github.com/habster-code/persona-engine/wiki)

**Local LLM-powered dialogue and world generation for Unity.**

Generate rich world descriptions, create customisable characters, and hold real-time
conversations — all running locally on yours machine. No internet, no API keys.

---

## ✨ Key Features

- World Generation from a text prompt
- Character Editor with custom fields and triggers
- Dialogue System with history and typewriter effect
- CPU / CUDA / Vulkan backend switching
- Automatic build optimisation

---

## ⚙️ System Requirements

- Unity 2022.3 LTS, Unity 6
- Windows x64
- Any GGUF model (demo model included: `TinyLlama-1.1B-Chat-Q4_K_M.gguf`, ~670 MB)

---

## 🚀 Quick Start

1. Download the `.unitypackage` from [Releases](https://github.com/habster-code/persona-engine/releases).
2. Import into your project.
3. Install **NuGetForUnity** and the required NuGet packages (see the [Wiki](https://github.com/habster-code/persona-engine/wiki/Get-Started#installation-nugetforunity)).
4. Open the demo scene (`Assets/PersonaEngine/Demo/Scenes/DialogueScene.unity`), press Play, and click a button.

---

## 📜 Licenses

MIT License — see [LICENSE](LICENSE).  
Third-party components (LlamaSharp, llama.cpp, TinyLlama, NuGetForUnity) are used under their respective
open‑source licenses. Full texts are included in the package.
