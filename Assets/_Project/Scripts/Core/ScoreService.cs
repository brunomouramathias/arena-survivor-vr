using System;
using UnityEngine;
using ArenaSurvivor.World;

namespace ArenaSurvivor.Core
{
    // Serviço de pontuação — pontos atuais, moeda da loja, recorde
    // e pontuação por sobrevivência (tempo + torres de pé + vida das torres).
    public class ScoreService : MonoBehaviour
    {
        public static ScoreService Instance { get; private set; }

        private const string HighScoreKey = "HighScore";

        [Header("Sobrevivência — pontos por segundo em jogo")]
        [SerializeField] private int pointsPerSecond = 5;

        [Header("Bônus final (fim de jogo)")]
        [SerializeField] private int pointsPerSecondSurvived = 10;   // x segundos vividos
        [SerializeField] private int pointsPerTowerAlive     = 250;  // x torres ainda de pé
        [SerializeField] private int pointsPerTowerPercent   = 5;    // x 1% de vida restante nas torres

        private int   currentScore;
        private int   currency;    // Pontos usados na loja (mesma moeda que score)
        private int   highScore;

        private float playSeconds;
        private float timeAccumulator;
        private bool  endBonusApplied;

        // Resultado do bônus final (usado pelo HUD/tela de fim)
        public int TimeBonus        { get; private set; }
        public int TowerBonus       { get; private set; }
        public int TowerHealthBonus { get; private set; }

        public int   CurrentScore => currentScore;
        public int   Currency => currency;
        public int   HighScore => highScore;
        public float PlaySeconds => playSeconds;

        public static event Action<int> OnScoreChanged;
        public static event Action<int> OnCurrencyChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            highScore = PlayerPrefs.GetInt(HighScoreKey, 0);

            // Dificuldade: pontos (e moeda de loja) iniciais do modo escolhido
            int start = Difficulty.Profile.StartPoints;
            if (start > 0)
            {
                currentScore += start;
                currency     += start;
            }
        }

        private void OnEnable()  { GameManager.OnGameStateChanged += HandleGameStateChanged; }
        private void OnDisable() { GameManager.OnGameStateChanged -= HandleGameStateChanged; }

        // Acumula pontos enquanto o jogo está de fato rolando (não na loja/menu/pausa)
        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.CurrentState != GameState.Playing) return;

            playSeconds += Time.unscaledDeltaTime;
            timeAccumulator += pointsPerSecond * Time.unscaledDeltaTime;

            int whole = Mathf.FloorToInt(timeAccumulator);
            if (whole > 0)
            {
                timeAccumulator -= whole;
                AddScore(whole);
            }
        }

        private void HandleGameStateChanged(GameState state)
        {
            if (state == GameState.GameOver || state == GameState.Victory)
                AwardEndBonus();
        }

        // Bônus de fim de jogo: mais tempo vivo, mais torres de pé e mais vida nelas.
        public void AwardEndBonus()
        {
            if (endBonusApplied) return;
            endBonusApplied = true;

            int seconds = Mathf.RoundToInt(playSeconds);
            TimeBonus = seconds * pointsPerSecondSurvived;

            int aliveTowers = 0;
            float hpSum = 0f;
            foreach (var t in TowerHealth.All)
            {
                if (t == null || t.Destroyed) continue;
                aliveTowers++;
                hpSum += t.Normalized;
            }

            TowerBonus       = aliveTowers * pointsPerTowerAlive;
            TowerHealthBonus = Mathf.RoundToInt(hpSum * 100f) * pointsPerTowerPercent;

            int total = TimeBonus + TowerBonus + TowerHealthBonus;
            AddScore(total);
            SaveHighScore();

            Debug.Log($"[SCORE] Fim de jogo — tempo={seconds}s (+{TimeBonus}) | " +
                      $"torres de pé={aliveTowers} (+{TowerBonus}) | " +
                      $"vida das torres={hpSum * 100f:0}% (+{TowerHealthBonus}) | total=+{total}");
        }

        // Adiciona pontos (também vira moeda da loja)
        public void AddScore(int amount)
        {
            // O multiplicador do modo vale só para a pontuação (a moeda da loja segue 1:1,
            // senão o modo difícil ficaria mais rico que o fácil).
            int scoreGain = amount > 0
                ? Mathf.RoundToInt(amount * Difficulty.Profile.ScoreMultiplier)
                : amount;

            currentScore = Mathf.Max(0, currentScore + scoreGain);
            currency     = Mathf.Max(0, currency + amount);
            OnScoreChanged?.Invoke(currentScore);
            OnCurrencyChanged?.Invoke(currency);
        }

        // Perda de pontos (ex.: jogador nocauteado)
        public void PenalizeScore(int amount)
        {
            if (amount <= 0) return;
            AddScore(-Mathf.Min(amount, currentScore));
        }

        // Gasta moeda da loja
        public bool SpendCurrency(int amount)
        {
            if (currency < amount) return false;
            currency -= amount;
            OnCurrencyChanged?.Invoke(currency);
            return true;
        }

        public bool CanAfford(int amount) => currency >= amount;

        // Salva recorde se necessário
        public void SaveHighScore()
        {
            if (currentScore > highScore)
            {
                highScore = currentScore;
                PlayerPrefs.SetInt(HighScoreKey, highScore);
                PlayerPrefs.Save();
            }
        }

        public void ResetSession()
        {
            currentScore = 0;
            currency = 0;
            playSeconds = 0f;
            timeAccumulator = 0f;
            endBonusApplied = false;
            TimeBonus = TowerBonus = TowerHealthBonus = 0;
            OnScoreChanged?.Invoke(0);
            OnCurrencyChanged?.Invoke(0);
        }
    }
}
