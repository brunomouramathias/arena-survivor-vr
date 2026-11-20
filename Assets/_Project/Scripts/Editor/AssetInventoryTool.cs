using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ArenaSurvivor.Editor
{
    public static class AssetInventoryTool
    {
        private const string InboxPath = "Assets/_Project/ThirdParty/_INBOX";
        private const string OutputDoc = "docs/INVENTARIO_ASSETS.md";

        [MenuItem("Tools/Arena/Inventory INBOX")]
        public static void InventoryInbox()
        {
            Debug.Log("=== [INVENTORY] Iniciando inventário da pasta _INBOX ===");

            if (!Directory.Exists(InboxPath))
            {
                Directory.CreateDirectory(InboxPath);
                Debug.LogWarning($"[INVENTORY] A pasta {InboxPath} foi criada, mas está vazia no momento.");
            }

            var files = Directory.GetFiles(InboxPath, "*.*", SearchOption.AllDirectories);
            var sb = new StringBuilder();

            sb.AppendLine("# INVENTÁRIO DE ASSETS — ARENA SURVIVOR");
            sb.AppendLine($"Data do inventário: {System.DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            sb.AppendLine($"Total de arquivos encontrados em `{InboxPath}`: {files.Length}");
            sb.AppendLine();
            sb.AppendLine("## 1. Classificação por Papel");
            sb.AppendLine("| Arquivo | Tipo / Ext | Tamanho (KB) | Papel Identificado |");
            sb.AppendLine("| :--- | :--- | :--- | :--- |");

            int modelsCount = 0, texturesCount = 0, audioCount = 0, fontsCount = 0, zipsCount = 0;

            foreach (var f in files)
            {
                if (f.EndsWith(".meta")) continue;

                var fi = new FileInfo(f);
                string ext = fi.Extension.ToLower();
                string name = fi.Name;
                long kb = fi.Length / 1024;
                string role = DetectRole(name, ext);

                if (ext == ".fbx" || ext == ".glb" || ext == ".gltf" || ext == ".obj") modelsCount++;
                else if (ext == ".png" || ext == ".jpg" || ext == ".tga") texturesCount++;
                else if (ext == ".wav" || ext == ".mp3" || ext == ".ogg") audioCount++;
                else if (ext == ".ttf" || ext == ".otf") fontsCount++;
                else if (ext == ".zip") zipsCount++;

                sb.AppendLine($"| `{name}` | `{ext}` | {kb} | {role} |");
            }

            sb.AppendLine();
            sb.AppendLine("## 2. Totais por Categoria");
            sb.AppendLine($"- **Modelos 3D (.fbx, .glb, .obj):** {modelsCount}");
            sb.AppendLine($"- **Texturas/Imagens (.png, .jpg):** {texturesCount}");
            sb.AppendLine($"- **Áudio (.wav, .mp3, .ogg):** {audioCount}");
            sb.AppendLine($"- **Fontes (.ttf, .otf):** {fontsCount}");
            sb.AppendLine($"- **Arquivos ZIP compactados:** {zipsCount}");

            string projectDir = Directory.GetParent(Application.dataPath).FullName;
            string docFullPath = Path.Combine(projectDir, OutputDoc);
            Directory.CreateDirectory(Path.GetDirectoryName(docFullPath));
            File.WriteAllText(docFullPath, sb.ToString(), Encoding.UTF8);

            AssetDatabase.Refresh();
            Debug.Log($"=== [INVENTORY] Concluído! Relatório salvo em: {docFullPath} ===");

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Inventário de Assets",
                    $"Inventário concluído!\nTotal de arquivos: {files.Length}\nModelos: {modelsCount}\nÁudio: {audioCount}\nZIPs: {zipsCount}\n\nConsulte docs/INVENTARIO_ASSETS.md",
                    "OK");
            }
        }

        private static string DetectRole(string name, string ext)
        {
            string lower = name.ToLower();
            if (lower.Contains("player") || lower.Contains("soldier") || lower.Contains("character") || lower.Contains("hero")) return "player";
            if (lower.Contains("chaser") || lower.Contains("zombie") || lower.Contains("crawler") || lower.Contains("basic")) return "enemy_basic";
            if (lower.Contains("shooter") || lower.Contains("ranger") || lower.Contains("spitter")) return "enemy_shooter";
            if (lower.Contains("boss") || lower.Contains("titan") || lower.Contains("golem")) return "enemy_boss";
            if (lower.Contains("pistol") || lower.Contains("gun")) return "weapon_pistol";
            if (lower.Contains("shotgun")) return "weapon_shotgun";
            if (lower.Contains("energy") || lower.Contains("laser") || lower.Contains("plasma")) return "weapon_energy";
            if (lower.Contains("tree") || lower.Contains("plant") || lower.Contains("palm")) return "env_tree";
            if (lower.Contains("rock") || lower.Contains("stone") || lower.Contains("boulder")) return "env_rock";
            if (lower.Contains("wall") || lower.Contains("fence") || lower.Contains("pillar") || lower.Contains("column")) return "env_wall";
            if (lower.Contains("floor") || lower.Contains("tile") || lower.Contains("ground")) return "env_ground";
            if (ext == ".ttf" || ext == ".otf") return "font";
            if (ext == ".wav" || ext == ".mp3" || ext == ".ogg") return lower.Contains("music") || lower.Contains("theme") ? "music" : "sfx";
            if (lower.Contains("ui") || lower.Contains("panel") || lower.Contains("button") || lower.Contains("border")) return "ui";

            return "env_decor / outros";
        }
    }
}

