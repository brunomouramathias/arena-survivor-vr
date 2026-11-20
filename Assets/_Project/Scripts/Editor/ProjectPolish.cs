using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

namespace ArenaSurvivor.Editor
{
    // Corrige os avisos do Console: AudioListener ausente e TextMesh Pro Essentials faltando.
    public static class ProjectPolish
    {
        const string ARENA = "Assets/_Project/Scenes/Arena.unity";
        const string TMP_PKG = "Library/PackageCache/com.unity.ugui@1c4864518158/Package Resources/TMP Essential Resources.unitypackage";
        const string TMP_FONT = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        [MenuItem("Tools/Arena/Polish Project (Audio + TMP)")]
        public static void Run() { RunInternal(false); }

        public static void RunBatch() { RunInternal(true); }

        private static void RunInternal(bool isBatch)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== [POLISH] " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
            try
            {
                // 1) TMP Essential Resources
                if (!Directory.Exists("Assets/TextMesh Pro"))
                {
                    string pkg = TMP_PKG;
                    if (!File.Exists(pkg))
                    {
                        var alt = Directory.GetFiles("Library/PackageCache", "TMP Essential Resources.unitypackage", SearchOption.AllDirectories).FirstOrDefault();
                        if (alt != null) pkg = alt.Replace('\\', '/');
                    }
                    if (File.Exists(pkg))
                    {
                        AssetDatabase.ImportPackage(pkg, false);
                        AssetDatabase.Refresh();
                        sb.AppendLine("TMP Essential Resources importado de: " + pkg);
                    }
                    else sb.AppendLine("AVISO: pacote TMP Essential Resources não encontrado.");
                }
                else sb.AppendLine("TMP Essential Resources: já presente.");

                // 2) Cena Arena
                var scene = EditorSceneManager.OpenScene(ARENA, OpenSceneMode.Single);

                // 2.1 AudioListener (evita "There are no audio listeners in the scene")
                var listener = UnityEngine.Object.FindAnyObjectByType<AudioListener>();
                if (listener == null)
                {
                    var cam = Camera.main;
                    if (cam == null) cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
                    if (cam != null)
                    {
                        cam.gameObject.AddComponent<AudioListener>();
                        sb.AppendLine($"AudioListener adicionado em '{cam.name}'.");
                    }
                    else sb.AppendLine("AVISO: nenhuma câmera para adicionar AudioListener.");
                }
                else sb.AppendLine($"AudioListener já presente em '{listener.gameObject.name}'.");

                // 2.2 Fonte padrão nos textos TMP (HUD)
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TMP_FONT);
                if (font == null)
                {
                    try { font = TMP_Settings.defaultFontAsset; }
                    catch { font = null; }
                }
                int fixedFonts = 0;
                if (font != null)
                {
                    foreach (var t in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (t.font == null)
                        {
                            t.font = font;
                            EditorUtility.SetDirty(t);
                            fixedFonts++;
                        }
                    }
                    sb.AppendLine($"Textos TMP com fonte atribuída: {fixedFonts} (fonte: {font.name}).");
                }
                else sb.AppendLine("AVISO: fonte TMP padrão não encontrada.");

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();

                File.WriteAllText("docs/polish_log.txt", sb.ToString(), Encoding.UTF8);
                Debug.Log(sb.ToString());
                if (isBatch) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                sb.AppendLine("EXCEÇÃO: " + e);
                File.WriteAllText("docs/polish_log.txt", sb.ToString(), Encoding.UTF8);
                Debug.LogError(e);
                if (isBatch) EditorApplication.Exit(1);
            }
        }
    }
}
