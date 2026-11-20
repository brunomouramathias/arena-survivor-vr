using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ArenaSurvivor.Editor
{
    [InitializeOnLoad]
    public static class SmokeTest
    {
        public const string SCENE_ARENA = "Assets/_Project/Scenes/Arena.unity";
        public const string LOG_PATH    = "docs/smoke_log.txt";
        public const string SCREEN_DIR  = "docs/screens";

        private const string KEY_RUNNING    = "SmokeTest_Running";
        private const string KEY_SCREENSHOT = "SmokeTest_Screenshot";
        private const string KEY_BATCH      = "SmokeTest_Batch";

        private static double startTime;
        private static int frameCount;
        private static List<string> errors = new List<string>();
        private static Vector3 playerStartPos;
        private static bool hasRecordedStart;

        static SmokeTest()
        {
            if (SessionState.GetBool(KEY_RUNNING, false))
            {
                EditorApplication.update += OnEditorUpdate;
                Application.logMessageReceived += OnLogMessage;
                startTime = EditorApplication.timeSinceStartup;
                frameCount = 0;
                hasRecordedStart = false;
                errors.Clear();
            }
        }

        [MenuItem("Tools/Arena/Smoke Test", priority = 0)]
        public static void RunSmokeTestFromMenu()
        {
            SetupAndStartSmokeTest("smoke_tarefa1.png", false);
        }

        public static void RunSmokeTestBatch()
        {
            SetupAndStartSmokeTest("smoke_tarefa1.png", true);
        }

        public static void RunSmokeTestTarefa2Batch()
        {
            SetupAndStartSmokeTest("smoke_tarefa2.png", true);
        }

        public static void SetupAndStartSmokeTest(string filename, bool batch)
        {
            var scene = EditorSceneManager.OpenScene(SCENE_ARENA, OpenSceneMode.Single);

            // Garantir que o HUD está montado por código
            HUDBuilder.BuildHUD();

            // Desativar BOSS_MAGO_TESTE se presente
            var boss = GameObject.Find("BOSS_MAGO_TESTE");
            if (boss != null)
            {
                boss.SetActive(false);
                Debug.Log("[SmokeTest] BOSS_MAGO_TESTE desativado na cena.");
            }

            // Remover qualquer objeto runner antigo na cena
            var oldRunner = GameObject.Find("--- SmokeTestRunner ---");
            if (oldRunner != null) UnityEngine.Object.DestroyImmediate(oldRunner);

            SessionState.SetBool(KEY_RUNNING, true);
            SessionState.SetString(KEY_SCREENSHOT, filename);
            SessionState.SetBool(KEY_BATCH, batch);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EditorApplication.isPlaying = true;
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                errors.Add($"[{type}] {condition}\n{stackTrace}");
            }
        }

        private static void OnEditorUpdate()
        {
            if (!SessionState.GetBool(KEY_RUNNING, false)) return;
            if (!EditorApplication.isPlaying) return;

            frameCount++;
            var player = GameObject.FindGameObjectWithTag("Player");

            if (!hasRecordedStart && player != null)
            {
                playerStartPos = player.transform.position;
                hasRecordedStart = true;
                startTime = EditorApplication.timeSinceStartup;
            }

            double elapsed = EditorApplication.timeSinceStartup - startTime;
            if (hasRecordedStart && elapsed >= 5.0)
            {
                SessionState.SetBool(KEY_RUNNING, false);
                EditorApplication.update -= OnEditorUpdate;
                Application.logMessageReceived -= OnLogMessage;

                string screenshotFile = SessionState.GetString(KEY_SCREENSHOT, "smoke_tarefa1.png");
                bool isBatch = SessionState.GetBool(KEY_BATCH, false);

                FinishTest(player, (float)elapsed, screenshotFile, isBatch);
            }
        }

        private static void FinishTest(GameObject player, float elapsed, string screenshotFilename, bool isBatch)
        {
            Directory.CreateDirectory(SCREEN_DIR);
            string screenPath = Path.Combine(SCREEN_DIR, screenshotFilename);

            // 1. Capturar imagem da câmera principal
            CaptureScreenshot(screenPath);

            // 2. Medir dados do jogador com foco na malha esquelética real (Beta_Surface)
            Vector3 playerEndPos = player != null ? player.transform.position : Vector3.zero;
            float playerHeight = 0f;
            bool isVisible = false;
            bool cameraSeesPlayer = false;

            if (player != null)
            {
                var smr = player.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null)
                {
                    isVisible = smr.enabled && smr.gameObject.activeInHierarchy;
                    playerHeight = smr.bounds.size.y;

                    var cam = Camera.main;
                    if (cam != null)
                    {
                        var planes = GeometryUtility.CalculateFrustumPlanes(cam);
                        cameraSeesPlayer = GeometryUtility.TestPlanesAABB(planes, smr.bounds);
                    }
                }
                else
                {
                    var renderers = player.GetComponentsInChildren<Renderer>();
                    if (renderers.Length > 0)
                    {
                        isVisible = true;
                        Bounds combined = renderers[0].bounds;
                        for (int i = 1; i < renderers.Length; i++)
                            combined.Encapsulate(renderers[i].bounds);

                        playerHeight = combined.size.y;

                        var cam = Camera.main;
                        if (cam != null)
                        {
                            var planes = GeometryUtility.CalculateFrustumPlanes(cam);
                            cameraSeesPlayer = GeometryUtility.TestPlanesAABB(planes, combined);
                        }
                    }
                }
            }

            // 3. Contar inimigos vivos
            int aliveEnemies = GameObject.FindGameObjectsWithTag("Enemy").Length;

            // 4. Calcular FPS médio
            float avgFps = frameCount / Mathf.Max(0.1f, elapsed);

            // 5. Escrever docs/smoke_log.txt
            var sb = new StringBuilder();
            sb.AppendLine("==================================================");
            sb.AppendLine("           ARENA SURVIVOR - SMOKE TEST LOG        ");
            sb.AppendLine("==================================================");
            sb.AppendLine($"Data/Hora:           {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Tempo de Simulação:  {elapsed:F2}s ({frameCount} frames)");
            sb.AppendLine($"FPS Médio:           {avgFps:F1}");
            sb.AppendLine($"Status Geral:        {(errors.Count == 0 ? "PASS" : "FAIL")}");
            sb.AppendLine("--------------------------------------------------");
            sb.AppendLine("CHECAGENS DO JOGADOR:");
            sb.AppendLine($"  Posição Inicial:    {playerStartPos}");
            sb.AppendLine($"  Posição Final:      {playerEndPos}");
            sb.AppendLine($"  Renderer Visível:   {(isVisible ? "SIM" : "NÃO")}");
            sb.AppendLine($"  Altura Medida (m):  {playerHeight:F3}m");
            sb.AppendLine($"  Câmera Enxerga:     {(cameraSeesPlayer ? "SIM" : "NÃO")}");
            sb.AppendLine("--------------------------------------------------");
            sb.AppendLine("CHECAGENS DE JOGO:");
            sb.AppendLine($"  Inimigos Vivos:     {aliveEnemies}");
            sb.AppendLine($"  Captura de Tela:    {screenPath}");
            sb.AppendLine("--------------------------------------------------");
            sb.AppendLine($"ERROS E EXCEÇÕES ({errors.Count}):");

            if (errors.Count == 0)
            {
                sb.AppendLine("  [Nenhum erro ou exceção registrado no Console!]");
            }
            else
            {
                foreach (var err in errors)
                    sb.AppendLine($"  {err}");
            }

            sb.AppendLine("==================================================");

            File.WriteAllText(LOG_PATH, sb.ToString(), Encoding.UTF8);
            Debug.Log($"[SmokeTest] Concluído! Log gravado em {LOG_PATH}. Erros: {errors.Count}");

#if UNITY_EDITOR
            if (isBatch)
            {
                EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
            }
            else
            {
                EditorApplication.isPlaying = false;
            }
#endif
        }

        private static void CaptureScreenshot(string outputPath)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[SmokeTest] Camera.main não encontrada para captura!");
                return;
            }

            int width = 1920;
            int height = 1080;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var prevRT = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            cam.targetTexture = prevRT;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);

            byte[] bytes = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            File.WriteAllBytes(outputPath, bytes);
            Debug.Log($"[SmokeTest] Captura de tela salva em: {outputPath}");
        }
    }
}
