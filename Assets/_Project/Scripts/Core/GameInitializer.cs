using UnityEngine;
using ArenaSurvivor.Player;
using ArenaSurvivor.Enemies;
using ArenaSurvivor.Core;
using ArenaSurvivor.Waves;
using ArenaSurvivor.UI;

namespace ArenaSurvivor.Core
{
    // Script de inicialização que configura tudo automaticamente no Start
    public class GameInitializer : MonoBehaviour
    {
        [Header("Auto Setup")]
        [SerializeField] private bool autoCreateWaypoints = true;
        [SerializeField] private int defaultWaypointCount = 8;
        [SerializeField] private float waypointRadius = 15f;

        [Header("References")]
        [SerializeField] private GameObject stalkerPrefab;

        private void Awake()
        {
            // Garante que o GameManager exista
            if (GameManager.Instance == null)
            {
                var gm = new GameObject("GameManager").AddComponent<GameManager>();
            }
        }

        private void Start()
        {
            if (autoCreateWaypoints)
            {
                CreateWaypointsIfNeeded();
            }

            SetupCanvasesForVR();
            SetupLightManager();
            RegisterStalkerPrefab();
        }

        private void CreateWaypointsIfNeeded()
        {
            var waypointManager = WaypointManager.Instance;
            if (waypointManager == null)
            {
                var go = new GameObject("WaypointManager");
                waypointManager = go.AddComponent<WaypointManager>();
            }

            // Verifica se já tem waypoints
            if (waypointManager.WaypointCount == 0)
            {
                for (int i = 0; i < defaultWaypointCount; i++)
                {
                    float angle = i * (360f / defaultWaypointCount);
                    Vector3 pos = new Vector3(
                        Mathf.Cos(angle * Mathf.Deg2Rad) * waypointRadius,
                        0f,
                        Mathf.Sin(angle * Mathf.Deg2Rad) * waypointRadius
                    );

                    var wpGo = new GameObject($"Waypoint_{i:00}");
                    wpGo.transform.position = pos;
                    wpGo.AddComponent<Waypoint>();
                    waypointManager.RegisterWaypoint(wpGo.transform);
                }
                
                Debug.Log($"[GameInitializer] Criados {defaultWaypointCount} waypoints automáticos.");
            }
        }

        private void SetupCanvasesForVR()
        {
            // HUD Canvas
            var hudController = FindAnyObjectByType<HUDController>();
            if (hudController != null)
            {
                var canvas = hudController.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
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
                    if (follow == null) follow = canvas.gameObject.AddComponent<CanvasWorldSpaceFollow>();
                    follow.targetCamera = Camera.main;
                    follow.offset = new Vector3(0f, 0.2f, 1.5f);
                }
            }

            // GameOver Canvas
            var gameOverScreen = FindAnyObjectByType<GameOverScreen>();
            if (gameOverScreen != null)
            {
                var canvas = gameOverScreen.GetComponent<Canvas>();
                if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
                {
                    canvas.renderMode = RenderMode.WorldSpace;
                    canvas.transform.SetParent(null);
                    canvas.transform.localScale = Vector3.one * 0.01f;

                    var follow = canvas.GetComponent<CanvasWorldSpaceFollow>();
                    if (follow == null) follow = canvas.gameObject.AddComponent<CanvasWorldSpaceFollow>();
                    follow.targetCamera = Camera.main;
                    follow.offset = new Vector3(0f, 0.3f, 2f);
                }
            }

            // Shop, Pause, Victory panels via UIManager
            var uiManager = FindAnyObjectByType<UIManager>();
            if (uiManager != null)
            {
                // O UIManager já configura isso no Start
            }
        }

        private void SetupLightManager()
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
                mainLight.shadows = LightShadows.Soft;
                mainLight.shadowStrength = 1f;
                mainLight.shadowBias = 0.05f;
                mainLight.shadowNormalBias = 0.4f;
            }
        }

        private void RegisterStalkerPrefab()
        {
            if (stalkerPrefab != null)
            {
                var waveSpawner = FindAnyObjectByType<WaveSpawner>();
                if (waveSpawner != null)
                {
                    var stalkerField = typeof(WaveSpawner).GetField("stalkerPrefab", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (stalkerField != null)
                    {
                        stalkerField.SetValue(waveSpawner, stalkerPrefab);
                        Debug.Log("[GameInitializer] EnemyStalker prefab registrado no WaveSpawner.");
                    }
                }
            }
        }
    }
}