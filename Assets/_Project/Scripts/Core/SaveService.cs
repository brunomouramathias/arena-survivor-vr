using UnityEngine;

namespace ArenaSurvivor.Core
{
    // Serviço de salvamento — wraps PlayerPrefs com chaves tipadas
    public static class SaveService
    {
        private const string HighScoreKey     = "HighScore";
        private const string BestWaveKey      = "BestWave";

        public static void SaveHighScore(int score)
        {
            if (score > LoadHighScore())
            {
                PlayerPrefs.SetInt(HighScoreKey, score);
                PlayerPrefs.Save();
            }
        }

        public static int LoadHighScore() => PlayerPrefs.GetInt(HighScoreKey, 0);

        public static void SaveBestWave(int wave)
        {
            if (wave > LoadBestWave())
            {
                PlayerPrefs.SetInt(BestWaveKey, wave);
                PlayerPrefs.Save();
            }
        }

        public static int LoadBestWave() => PlayerPrefs.GetInt(BestWaveKey, 0);

        public static void ClearAll()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }
    }
}
