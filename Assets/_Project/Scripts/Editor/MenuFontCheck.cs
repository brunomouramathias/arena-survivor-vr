#if UNITY_EDITOR
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace ArenaSurvivor.Editor
{
    // Confere quais caracteres especiais existem na fonte do TMP usada pela interface.
    public static class MenuFontCheck
    {
        [MenuItem("Tools/Arena/Verificar Fonte do Menu")]
        public static void Run()
        {
            var sb = new StringBuilder();
            var font = TMP_Settings.defaultFontAsset;

            if (font == null)
            {
                // fallback: fonte do HUD antigo
                font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            }

            sb.AppendLine($"fonte={font?.name}");

            string chars = "▶◀←→↑↓·×÷—–…°→▶«»✓★•|/<>+-=%$#@!?()[]{}";
            foreach (char c in chars)
            {
                bool has = font != null && font.HasCharacter(c, searchFallbacks: true, tryAddCharacter: false);
                sb.AppendLine($"  U+{(int)c:X4} '{c}' = {(has ? "OK" : "FALTA")}");
            }

            Debug.Log(sb.ToString());
            System.IO.File.WriteAllText("docs/screens/menu/fonte.txt", sb.ToString(), Encoding.UTF8);
        }
    }
}
#endif
