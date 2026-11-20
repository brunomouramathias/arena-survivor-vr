using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ArenaSurvivor.Utils;

namespace ArenaSurvivor.Editor
{
    // Monta a cena de prova e entra em Play Mode; o PlayerProofRunner faz o resto.
    public static class PlayerProofTest
    {
        public const string SCENE_TEST = "Assets/_Project/Scenes/PlayerTest.unity";

        [MenuItem("Tools/Arena/Run Player Proof Test (Passo 4)", priority = 40)]
        public static void Run()
        {
            Prepare();
        }

        public static void RunProofBatch()
        {
            Prepare();
        }

        private static void Prepare()
        {
            Directory.CreateDirectory("docs/screens/personagem");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Luz
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 1.4f;
            lightGO.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            // Piso neutro
            var floorGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floorGO.name = "Floor_Neutral";
            floorGO.transform.position = Vector3.zero;
            floorGO.transform.localScale = new Vector3(5f, 1f, 5f);
            var floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            floorMat.SetColor("_BaseColor", new Color(0.24f, 0.24f, 0.27f));
            floorGO.GetComponent<Renderer>().sharedMaterial = floorMat;

            // Parede para o teste de impacto
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall_Impact";
            wall.transform.position = new Vector3(0f, 1.2f, 6f);
            wall.transform.localScale = new Vector3(7f, 2.4f, 0.3f);
            var wallMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            wallMat.SetColor("_BaseColor", new Color(0.35f, 0.33f, 0.3f));
            wall.GetComponent<Renderer>().sharedMaterial = wallMat;

            // Câmera
            var camGO = new GameObject("ProofCamera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.fieldOfView = 42f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f);
            camGO.transform.position = new Vector3(0f, 1.35f, 2.75f);
            camGO.transform.LookAt(new Vector3(0f, 0.95f, 0f));

            // Copia o Player configurado da cena Arena
            var arena = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Arena.unity", OpenSceneMode.Additive);
            var arenaPlayer = GameObject.FindGameObjectWithTag("Player");
            if (arenaPlayer == null)
            {
                Debug.LogError("[PROOF] Player não encontrado na Arena.");
                EditorApplication.Exit(3);
                return;
            }

            var player = Object.Instantiate(arenaPlayer);
            player.name = "Player";
            player.transform.position = Vector3.zero;
            player.transform.rotation = Quaternion.identity;
            EditorSceneManager.MoveGameObjectToScene(player, scene);
            EditorSceneManager.CloseScene(arena, true);

            // Salva a cena LIMPA (sem o runner) — o runner só existe na sessão de teste
            EditorSceneManager.SaveScene(scene, SCENE_TEST);

            player.AddComponent<PlayerProofRunner>();
            UnityEditor.SessionState.SetBool("Proof.AutoRun", true);
            Debug.Log("[PROOF] Cena de prova montada. Entrando em Play Mode...");
            EditorApplication.EnterPlaymode();
            float startTime = (float)EditorApplication.timeSinceStartup;
            EditorApplication.update += () =>
            {
                if ((float)EditorApplication.timeSinceStartup - startTime > 150f)
                {
                    Debug.LogError("[PROOF] Watchdog: prova excedeu 150s.");
                    EditorApplication.Exit(4);
                }
            };
        }
    }
}
