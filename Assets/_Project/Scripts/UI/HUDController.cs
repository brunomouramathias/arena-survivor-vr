using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ArenaSurvivor.Core;
using ArenaSurvivor.Player;
using ArenaSurvivor.Weapons;
using ArenaSurvivor.Waves;

namespace ArenaSurvivor.UI
{
    // Controla o HUD em combate: vida, munição, onda, pontuação, esquiva
    public class HUDController : MonoBehaviour
    {
        [Header("Vida - Corações")]
        [SerializeField] private Image[] heartImages; // 3 corações
        [SerializeField] private Sprite heartFullSprite;
        [SerializeField] private Sprite heartHalfSprite;
        [SerializeField] private Sprite heartEmptySprite;

        [Header("Vida - Barra (Legado)")]
        [SerializeField] private Slider       healthSlider;
        [SerializeField] private Image        healthFill;
        [SerializeField] private TextMeshProUGUI healthText;

        [Header("Munição")]
        [SerializeField] private TextMeshProUGUI ammoText;
        [SerializeField] private Image           weaponIcon;

        [Header("Onda e Pontuação")]
        [SerializeField] private TextMeshProUGUI waveText;
        [SerializeField] private TextMeshProUGUI scoreText;

        [Header("Esquiva")]
        [SerializeField] private Image dodgeCooldownFill;

        [Header("Dano na borda")]
        [SerializeField] private Image damageVignette;

        [Header("Mira customizada")]
        [SerializeField] private RectTransform reticle;

        private PlayerController playerController;
        private PlayerShooter    playerShooter;
        private PlayerHealth     playerHealth;
        private Canvas           canvas;

        private float vignetteAlpha;

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerController = player.GetComponent<PlayerController>();
                playerShooter    = player.GetComponent<PlayerShooter>();
                playerHealth     = player.GetComponent<PlayerHealth>();
            }

            canvas = GetComponentInParent<Canvas>();

            // Configurar canvas para World Space se for VR
            SetupCanvasForVR();

            // Esconde cursor padrão
            Cursor.visible   = false;
            Cursor.lockState = CursorLockMode.None;

            // Inscreve em eventos
            EventBus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
            EventBus.Subscribe<WaveStartedEvent>(OnWaveStarted);
            ScoreService.OnScoreChanged   += OnScoreChanged;

            // Inicializa corações
            UpdateHearts();
        }

        private void SetupCanvasForVR()
        {
            if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.transform.SetParent(null);
                
                // Escala apropriada para World Space (0.01 para ficar legível)
                canvas.transform.localScale = Vector3.one * 0.01f;
                
                // Posiciona o canvas à frente do jogador
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    canvas.transform.position = player.transform.position + player.transform.forward * 2f + Vector3.up * 1.5f;
                    canvas.transform.rotation = Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f);
                }

                // Adiciona componente para seguir a cabeça/câmera em VR
                var canvasFollow = canvas.gameObject.AddComponent<CanvasWorldSpaceFollow>();
                canvasFollow.targetCamera = Camera.main;
                canvasFollow.offset = new Vector3(0f, 0.2f, 1.5f);
            }
        }

        private void UpdateHearts()
        {
            if (heartImages == null || heartImages.Length == 0 || playerHealth == null) return;

            float healthPercent = (float)playerHealth.CurrentHealth / playerHealth.MaxHealth;
            int heartsToShow = Mathf.CeilToInt(healthPercent * heartImages.Length);

            for (int i = 0; i < heartImages.Length; i++)
            {
                if (heartImages[i] == null) continue;

                if (i < heartsToShow)
                {
                    // Verifica se é meio coração
                    float heartThreshold = (i + 1) / (float)heartImages.Length;
                    float prevThreshold = i / (float)heartImages.Length;
                    
                    if (healthPercent >= heartThreshold)
                    {
                        heartImages[i].sprite = heartFullSprite;
                        heartImages[i].color = Color.white;
                    }
                    else if (healthPercent > prevThreshold)
                    {
                        heartImages[i].sprite = heartHalfSprite;
                        heartImages[i].color = Color.white;
                    }
                    else
                    {
                        heartImages[i].sprite = heartEmptySprite;
                        heartImages[i].color = new Color(1f, 1f, 1f, 0.3f);
                    }
                }
                else
                {
                    heartImages[i].sprite = heartEmptySprite;
                    heartImages[i].color = new Color(1f, 1f, 1f, 0.3f);
                }
            }
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
            EventBus.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
            ScoreService.OnScoreChanged   -= OnScoreChanged;
            Cursor.visible = true;
        }

        private void Update()
        {
            UpdateAmmo();
            UpdateDodge();
            UpdateReticle();
            UpdateDamageVignette();
            UpdateHearts();
        }

        private void UpdateAmmo()
        {
            if (playerShooter == null || ammoText == null) return;

            var weapon = playerShooter.CurrentWeapon;
            if (weapon == null) return;

            string clip    = weapon.CurrentClip.ToString();
            string reserve = weapon.Data.reserveAmmo < 0 ? "∞" : weapon.CurrentReserve.ToString();
            string status  = weapon.IsReloading ? " (RECARREGANDO)" : "";

            ammoText.text = $"{clip} / {reserve}{status}";

            if (weaponIcon != null && weapon.Data.weaponIcon != null)
                weaponIcon.sprite = weapon.Data.weaponIcon;
        }

        private void UpdateDodge()
        {
            if (playerController == null || dodgeCooldownFill == null) return;
            dodgeCooldownFill.fillAmount = 1f - playerController.DodgeCooldownNormalized;
        }

        private void UpdateReticle()
        {
            if (reticle == null || InputService.Instance == null) return;
            reticle.position = InputService.Instance.MouseScreenPosition;
        }

        private void UpdateDamageVignette()
        {
            if (damageVignette == null) return;

            vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, 0f, Time.deltaTime * 3f);
            var c = damageVignette.color;
            damageVignette.color = new Color(c.r, c.g, c.b, vignetteAlpha);
        }

        private void OnPlayerDamaged(PlayerDamagedEvent e)
        {
            // Atualiza barra de vida (legado)
            if (healthSlider != null)
                healthSlider.value = (float)e.CurrentHealth / e.MaxHealth;

            if (healthText != null)
                healthText.text = $"{e.CurrentHealth} / {e.MaxHealth}";

            // Cor da barra: verde -> amarelo -> vermelho
            if (healthFill != null)
                healthFill.color = Color.Lerp(Color.red, Color.green, (float)e.CurrentHealth / e.MaxHealth);

            // Flash de dano na borda
            vignetteAlpha = 0.6f;

            // Atualiza corações
            UpdateHearts();
        }

        private void OnWaveStarted(WaveStartedEvent e)
        {
            if (waveText != null)
                waveText.text = e.IsBossWave
                    ? $"⚠ CHEFE — ONDA {e.WaveNumber} ⚠"
                    : $"ONDA {e.WaveNumber}";
        }

        private void OnScoreChanged(int score)
        {
            if (scoreText != null)
                scoreText.text = $"Pontos: {score}";
        }
    }
}
