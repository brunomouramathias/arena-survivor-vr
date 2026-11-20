using UnityEngine;
using UnityEditor;
using ArenaSurvivor.Player;
using ArenaSurvivor.UI;
using ArenaSurvivor.Core;

namespace ArenaSurvivor.Editor
{
    public class VRSceneSetup : EditorWindow
    {
        [MenuItem("ArenaSurvivor/Setup VR Scene")]
        public static void ShowWindow()
        {
            GetWindow<VRSceneSetup>("VR Scene Setup");
        }

        private void OnGUI()
        {
            GUILayout.Label("Configuração da Cena VR", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (GUILayout.Button("Criar Waypoints Padrão", GUILayout.Height(30)))
            {
                CreateDefaultWaypoints();
            }

            if (GUILayout.Button("Configurar Canvas HUD para VR", GUILayout.Height(30)))
            {
                SetupHUDCanvasForVR();
            }

            if (GUILayout.Button("Configurar GameOver Screen para VR", GUILayout.Height(30)))
            {
                SetupGameOverForVR();
            }

            if (GUILayout.Button("Configurar Light Manager", GUILayout.Height(30)))
            {
                SetupLightManager();
            }

            if (GUILayout.Button("Configuração Completa (Tudo)", GUILayout.Height(40)))
            {
                CreateDefaultWaypoints();
                SetupHUDCanvasForVR();
                SetupGameOverForVR();
                SetupLightManager();
                Debug.Log("Configuração VR completa!");
            }

            EditorGUILayout.Space();
            GUILayout.Label("Instruções:", EditorStyles.boldLabel);
            GUILayout.Label("1. Execute 'Configuração Completa' para configurar tudo automaticamente");
            GUILayout.Label("2. Ajuste os waypoints na cena conforme necessário");
            GUILayout.Label("3. Atribua os sprites de coração no HUDController");
            GUILayout.Label("4. Adicione AudioClips nos EnemyAudio e AudioManager");
            GUILayout.Label("5. Configure o EnemyStalker prefab no WaveSpawner");
        }

        private static void CreateDefaultWaypoints()
        {
            var waypointManager = FindAnyObjectByType<WaypointManager>();
            if (waypointManager == null)
            {
                var go = new GameObject("WaypointManager");
                waypointManager = go.AddComponent<WaypointManager>();
            }

            // Cria waypoints em círculo ao redor da arena
            int waypointCount = 8;
            float radius = 15f;
            float centerY = 0f;

            for (int i = 0; i < waypointCount; i++)
            {
                float angle = i * (360f / waypointCount);
                Vector3 pos = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                    centerY,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * radius
                );

                var wpGo = new GameObject($"Waypoint_{i:00}");
                wpGo.transform.position = pos;
                var wp = wpGo.AddComponent<Waypoint>();
                
                // Registra no manager
                waypointManager.RegisterWaypoint(wpGo.transform);
            }

            Debug.Log($"Criados {waypointCount} waypoints padrão.");
        }

        private static void SetupHUDCanvasForVR()
        {
            var hudController = FindAnyObjectByType<HUDController>();
            if (hudController == null)
            {
                Debug.LogError("HUDController não encontrado na cena!");
                return;
            }

            var canvas = hudController.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.transform.SetParent(null);
                canvas.transform.localScale = Vector3.one * 0.01f;

                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    canvas.transform.position = player.transform.position + player.transform.forward * 2f + Vector3.up * 1.5f;
                    canvas.transform.rotation = Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f);
                }

                var follow = canvas.GetComponent<CanvasWorldSpaceFollow>();
                if (follow == null)
                    follow = canvas.gameObject.AddComponent<CanvasWorldSpaceFollow>();
                
                follow.targetCamera = Camera.main;
                follow.offset = new Vector3(0f, 0.2f, 1.5f);
                follow.followPosition = true;
                follow.followRotation = true;
                follow.onlyYawRotation = true;

                Debug.Log("HUD Canvas configurado para VR (World Space).");
            }
        }

        private static void SetupGameOverForVR()
        {
            var gameOverScreen = FindAnyObjectByType<GameOverScreen>();
            if (gameOverScreen == null)
            {
                Debug.LogError("GameOverScreen não encontrado na cena!");
                return;
            }

            var canvas = gameOverScreen.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.transform.SetParent(null);
                canvas.transform.localScale = Vector3.one * 0.01f;

                var follow = canvas.GetComponent<CanvasWorldSpaceFollow>();
                if (follow == null)
                    follow = canvas.gameObject.AddComponent<CanvasWorldSpaceFollow>();
                
                follow.targetCamera = Camera.main;
                follow.offset = new Vector3(0f, 0.3f, 2f);
                follow.followPosition = true;
                follow.followRotation = true;
                follow.onlyYawRotation = true;

                Debug.Log("GameOver Screen configurado para VR (World Space).");
            }
        }

        private static void SetupLightManager()
        {
            var lightManager = FindAnyObjectByType<LightManager>();
            if (lightManager == null)
            {
                var go = new GameObject("LightManager");
                lightManager = go.AddComponent<LightManager>();
            }

            var mainLight = FindAnyObjectByType<Light>();
            if (mainLight != null && mainLight.type == LightType.Directional)
            {
                // Configura a luz principal para não piscar
                mainLight.shadows = LightShadows.Soft;
                mainLight.shadowStrength = 1f;
                mainLight.shadowBias = 0.05f;
                mainLight.shadowNormalBias = 0.4f;
                
                Debug.Log("Light Manager configurado - luz principal estabilizada.");
            }
        }
    }
}