using TMPro;
using UnityEngine;
using ArenaSurvivor.Core;
using ArenaSurvivor.Waves;

namespace ArenaSurvivor.UI
{
    // Tela de Game Over com pontuação, recorde e botão de reiniciar
    public class GameOverScreen : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI highScoreText;
        [SerializeField] private TextMeshProUGUI waveText;
        [SerializeField] private TextMeshProUGUI newRecordText;

        private Canvas canvas;
        private CanvasWorldSpaceFollow canvasFollow;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            canvasFollow = GetComponent<CanvasWorldSpaceFollow>();
        }

        private void OnEnable()
        {
            // Configurar para World Space se necessário
            SetupForVR();

            // Salva recorde
            int score = ScoreService.Instance?.CurrentScore ?? 0;
            int best  = SaveService.LoadHighScore();
            SaveService.SaveHighScore(score);

            int wave = WaveManager.Instance?.CurrentWave ?? 0;
            SaveService.SaveBestWave(wave);

            if (scoreText     != null) scoreText.text     = $"Pontuação: {score}";
            if (highScoreText != null) highScoreText.text = $"Recorde: {Mathf.Max(score, best)}";
            if (waveText      != null) waveText.text      = $"Onda Alcançada: {wave}";
            if (newRecordText != null) newRecordText.gameObject.SetActive(score > best);

            // Posicionar na frente do jogador
            PositionInFrontOfPlayer();
        }

        private void SetupForVR()
        {
            if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.transform.SetParent(null);
                canvas.transform.localScale = Vector3.one * 0.01f;
            }

            if (canvasFollow == null)
            {
                canvasFollow = gameObject.AddComponent<CanvasWorldSpaceFollow>();
            }
            
            canvasFollow.targetCamera = Camera.main;
            canvasFollow.offset = new Vector3(0f, 0.3f, 2f);
            canvasFollow.followPosition = true;
            canvasFollow.followRotation = true;
            canvasFollow.onlyYawRotation = true;
        }

        private void PositionInFrontOfPlayer()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && canvas != null)
            {
                Vector3 pos = player.transform.position + player.transform.forward * 2f + Vector3.up * 1.5f;
                canvas.transform.position = pos;
                canvas.transform.rotation = Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f);
            }
        }

        public void OnRestartClicked()
        {
            GameManager.Instance?.RestartGame();
        }

        public void OnMenuClicked()
        {
            GameManager.Instance?.GoToMenu();
        }
    }
}

