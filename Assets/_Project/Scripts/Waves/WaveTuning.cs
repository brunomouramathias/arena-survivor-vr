using UnityEngine;

namespace ArenaSurvivor.Waves
{
    // Regras de escalonamento dos inimigos por onda.
    // Ficam juntas aqui para o balanceamento ser fácil de ajustar.
    public static class WaveTuning
    {
        // Dano de um tiro da pistola inicial — base do cálculo de "quantos tiros para matar".
        public const int PistolDamage = 18;

        // Onda 1 = 2 tiros, onda 2 = 3 tiros, onda 3 = 4 tiros... até o teto.
        public const int MinShots = 2;
        public const int MaxShots = 8;

        public static int ShotsToKill(int waveNumber)
        {
            int w = Mathf.Max(1, waveNumber);
            return Mathf.Clamp(MinShots + (w - 1), MinShots, MaxShots);
        }

        public static int HealthForWave(int waveNumber)
        {
            return ShotsToKill(waveNumber) * PistolDamage;
        }

        // Inimigos lentos na primeira onda e progressivamente mais rápidos.
        // onda 1 = 60% da velocidade, +8% por onda, teto de 160%.
        public const float BaseSpeedFactor = 0.60f;
        public const float SpeedFactorPerWave = 0.08f;
        public const float MaxSpeedFactor = 1.60f;

        public static float SpeedFactor(int waveNumber)
        {
            int w = Mathf.Max(1, waveNumber);
            return Mathf.Clamp(BaseSpeedFactor + (w - 1) * SpeedFactorPerWave,
                               BaseSpeedFactor, MaxSpeedFactor);
        }
    }
}
