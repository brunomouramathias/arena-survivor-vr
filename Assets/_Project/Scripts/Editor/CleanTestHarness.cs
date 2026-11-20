using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ArenaSurvivor.Utils;

namespace ArenaSurvivor.Editor
{
    // Remove qualquer harness de teste das cenas do jogo e apaga a cena de prova antiga.
    public static class CleanTestHarness
    {
        [MenuItem("Tools/Arena/Clean Test Harness From Scenes")]
        public static void Run() { RunInternal(false); }

        public static void RunBatch() { RunInternal(true); }

        private static void RunInternal(bool isBatch)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== [CLEAN HARNESS] " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");

            try
            {
                string[] scenes = { "Assets/_Project/Scenes/Arena.unity", "Assets/_Project/Scenes/MainMenu.unity", "Assets/_Project/Scenes/Sandbox.unity" };
                foreach (var s in scenes)
                {
                    if (!File.Exists(s)) continue;
                    var scene = EditorSceneManager.OpenScene(s, OpenSceneMode.Single);
                    int removed = 0;
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var r in root.GetComponentsInChildren<PlayerProofRunner>(true))
                        {
                            UnityEngine.Object.DestroyImmediate(r);
                            removed++;
                        }
                        foreach (var r in root.GetComponentsInChildren<FreezeGuardTestRunner>(true))
                        {
                            UnityEngine.Object.DestroyImmediate(r);
                            removed++;
                        }
                    }
                    if (removed > 0)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                    sb.AppendLine($"{s}: componentes de teste removidos = {removed}");
                }

                // Cena de prova antiga (podia ser aberta por engano e fechar o Editor)
                const string testScene = "Assets/_Project/Scenes/PlayerTest.unity";
                if (File.Exists(testScene))
                {
                    AssetDatabase.DeleteAsset(testScene);
                    sb.AppendLine($"{testScene}: removida (é recriada automaticamente pelo teste de prova).");
                }

                // Abre a cena do jogo ao final
                EditorSceneManager.OpenScene("Assets/_Project/Scenes/Arena.unity", OpenSceneMode.Single);

                AssetDatabase.SaveAssets();
                File.WriteAllText("docs/clean_harness.txt", sb.ToString(), Encoding.UTF8);
                Debug.Log(sb.ToString());
                if (isBatch) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                sb.AppendLine("EXCEÇÃO: " + e);
                File.WriteAllText("docs/clean_harness.txt", sb.ToString(), Encoding.UTF8);
                Debug.LogError(e);
                if (isBatch) EditorApplication.Exit(1);
            }
        }
    }
}
