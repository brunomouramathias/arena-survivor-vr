using UnityEngine;

namespace ArenaSurvivor.Waves
{
    [CreateAssetMenu(fileName = "WaveConfig", menuName = "ArenaSurvivor/Wave Config")]
    public class WaveConfig : ScriptableObject
    {
        [Header("Configuração Base")]
        public int   baseEnemyCount = 6;
        public float enemyCountFactor = 2.5f;  // inimigos += onda * fator
        public int   maxSimultaneous  = 30;
        public float spawnInterval    = 0.8f;
        public float restDuration     = 8f;

        [Header("Escalonamento")]
        [Range(0f, 1f)] public float shooterRatioPerWave = 0.03f; // proporção atirador cresce
        public float maxShooterRatio = 0.4f;
        
        [Range(0f, 1f)] public float stalkerRatioPerWave = 0.02f; // proporção stalker cresce
        public float maxStalkerRatio = 0.25f;

        [Header("Chefe")]
        public int   bossBossWaveInterval = 3;    // A cada N ondas, chefe
        public float bossExtraHealth      = 150f; // HP extra por chefe além do 1º

        // Calcula quantidade de inimigos para uma onda.
        // Onda 1 e a menor de todas (baseEnemyCount) e cada onda seguinte soma o fator.
        public int GetEnemyCount(int waveNumber)
        {
            int n = Mathf.RoundToInt(baseEnemyCount + Mathf.Max(0, waveNumber - 1) * enemyCountFactor);
            return Mathf.Clamp(n, 1, Mathf.Max(1, maxSimultaneous));
        }

        // Proporção de atiradores nessa onda (0 a maxShooterRatio)
        public float GetShooterRatio(int waveNumber)
        {
            return Mathf.Min(shooterRatioPerWave * waveNumber, maxShooterRatio);
        }

        // Proporção de stalkers nessa onda (0 a maxStalkerRatio)
        public float GetStalkerRatio(int waveNumber)
        {
            if (waveNumber < 3) return 0f; // Stalkers só aparecem a partir da onda 3
            return Mathf.Min(stalkerRatioPerWave * (waveNumber - 2), maxStalkerRatio);
        }

        public bool IsBossWave(int waveNumber)
        {
            // O chefe (bruxo vermelho) entra a cada N ondas (3, 6, 9...).
            // A onda 1 fica sem chefe: ela precisa ser rapida, com inimigos que morrem em ate 3 tiros.
            return waveNumber > 0 && waveNumber % Mathf.Max(1, bossBossWaveInterval) == 0;
        }
    }
}
