using UnityEngine;
using ArenaSurvivor.Core;
using ArenaSurvivor.Player;

namespace ArenaSurvivor.Utils
{
    // Overlay de debug (F1) mostrando telemetria em tempo real
    public class DebugOverlay : MonoBehaviour
    {
        private bool showOverlay = true; // Habilitado por padrão para diagnóstico rápido
        private float deltaTime = 0.0f;
        private PlayerController player;
        private InputService inputService;

        private void Start()
        {
            player = FindAnyObjectByType<PlayerController>();
            inputService = InputService.Instance ?? FindAnyObjectByType<InputService>();
        }

        private void Update()
        {
            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

            if (Input.GetKeyDown(KeyCode.F1))
                showOverlay = !showOverlay;
        }

        private void OnGUI()
        {
            if (!showOverlay) return;

            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (inputService == null) inputService = InputService.Instance ?? FindAnyObjectByType<InputService>();

            float msec = deltaTime * 1000.0f;
            float fps = 1.0f / (deltaTime > 0.0001f ? deltaTime : 0.016f);

            Vector2 moveInput = inputService != null ? inputService.MoveInput : Vector2.zero;
            Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
            string stateStr = GameManager.Instance != null ? GameManager.Instance.CurrentState.ToString() : "No GameManager";
            float timeScale = Time.timeScale;

            int w = Screen.width;
            GUIStyle style = new GUIStyle();
            style.alignment = TextAnchor.UpperLeft;
            style.fontSize = 16;
            style.normal.textColor = Color.yellow;

            GUI.Box(new Rect(10, 10, 320, 160), "ARENA SURVIVOR - DEBUG (F1)");

            string text = string.Format(
                "\n FPS: {0:0.} ({1:0.0} ms)\n State: {2} | TimeScale: {3:0.0}\n MoveInput: {4}\n PlayerPos: {5}\n AimPoint: {6}\n DodgeCooldown: {7:0.0}s",
                fps, msec, stateStr, timeScale, moveInput, playerPos, player != null ? player.AimPoint.ToString() : "N/A",
                player != null ? player.DodgeCooldownNormalized : 0f
            );

            GUI.Label(new Rect(20, 25, 300, 140), text, style);
        }
    }
}

