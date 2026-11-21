using UnityEngine;

namespace ArenaSurvivor.Core
{
    // Modos de jogo escolhidos na tela de abertura.
    public enum GameDifficulty { Easy, Medium, Hard }

    // Perfil de um modo: o que muda no jogo e como o modo se apresenta ao jogador.
    // MÉDIO é o balanceamento oficial — todos os multiplicadores valem 1.
    public readonly struct DifficultyProfile
    {
        public readonly string Label;         // "FÁCIL"
        public readonly string Headline;      // uma linha, exibida ao lado do seletor
        public readonly string Description;   // explicação curta do que muda
        public readonly float  EnemyHealth;
        public readonly float  EnemySpeed;
        public readonly float  EnemyCount;
        public readonly int    StartPoints;   // pontos (e moeda de loja) no início da partida
        public readonly float  ScoreMultiplier;

        public DifficultyProfile(string label, string headline, string description,
                                 float enemyHealth, float enemySpeed, float enemyCount,
                                 int startPoints, float scoreMultiplier)
        {
            Label           = label;
            Headline        = headline;
            Description     = description;
            EnemyHealth     = enemyHealth;
            EnemySpeed      = enemySpeed;
            EnemyCount      = enemyCount;
            StartPoints     = startPoints;
            ScoreMultiplier = scoreMultiplier;
        }

        // Vida dos inimigos formatada para a UI: "×0,80" / "padrão"
        public string HealthText => EnemyHealth >= 0.999f && EnemyHealth <= 1.001f ? "padrão" : $"×{EnemyHealth:0.00}";
        public string SpeedText  => EnemySpeed  >= 0.999f && EnemySpeed  <= 1.001f ? "padrão" : $"×{EnemySpeed:0.00}";
        public string CountText  => EnemyCount  >= 0.999f && EnemyCount  <= 1.001f ? "padrão" : $"×{EnemyCount:0.00}";
        public string ScoreText  => ScoreMultiplier >= 0.999f && ScoreMultiplier <= 1.001f ? "padrão" : $"×{ScoreMultiplier:0.00}";
    }

    // Seleção do modo atual — fica em memória (troca de cena) e é lembrada entre sessões.
    public static class Difficulty
    {
        private const string PrefsKey = "arena.difficulty";

        private static bool prefsLoaded;
        private static GameDifficulty current = GameDifficulty.Medium;

        public static GameDifficulty Current
        {
            get
            {
                if (!prefsLoaded) LoadPreference();
                return current;
            }
        }

        public static DifficultyProfile Profile => Get(Current);

        public static DifficultyProfile Get(GameDifficulty difficulty)
        {
            switch (difficulty)
            {
                case GameDifficulty.Easy:
                    return new DifficultyProfile(
                        "FÁCIL",
                        "Aprenda o ritmo sem pressão",
                        "Inimigos mais fracos, lentos e em menor número. Você começa com pontos para a primeira loja.",
                        0.80f, 0.85f, 0.85f, 150, 0.85f);

                case GameDifficulty.Hard:
                    return new DifficultyProfile(
                        "DIFÍCIL",
                        "Cada erro pesa",
                        "Inimigos mais resistentes, rápidos e numerosos. Sem pontos de partida e pontuação valendo 30% a mais.",
                        1.30f, 1.15f, 1.20f, 0, 1.30f);

                default:
                    return new DifficultyProfile(
                        "MÉDIO",
                        "O balanceamento oficial",
                        "O ritmo planejado do jogo: 2 tiros por inimigo na primeira onda e evolução constante.",
                        1.00f, 1.00f, 1.00f, 0, 1.00f);
            }
        }

        // Troca o modo e guarda a preferência
        public static void Select(GameDifficulty difficulty)
        {
            current = difficulty;
            prefsLoaded = true;
            PlayerPrefs.SetString(PrefsKey, difficulty.ToString());
            PlayerPrefs.Save();
        }

        // Usado por testes/ferramentas: troca sem tocar na preferência salva
        public static void SelectForSession(GameDifficulty difficulty)
        {
            current = difficulty;
            prefsLoaded = true;
        }

        public static void LoadPreference()
        {
            prefsLoaded = true;
            string saved = PlayerPrefs.GetString(PrefsKey, GameDifficulty.Medium.ToString());
            current = System.Enum.TryParse(saved, out GameDifficulty parsed) ? parsed : GameDifficulty.Medium;
        }

        public static int Index => (int)Current;
        public static int Count => 3;
        public static GameDifficulty AtIndex(int index) =>
            (GameDifficulty)Mathf.Clamp(index, 0, Count - 1);
    }
}
