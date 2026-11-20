using UnityEngine;

namespace ArenaSurvivor.Player
{
    // Multiplicadores de atributo (poderes das ondas + upgrades da loja)
    public class PlayerStats : MonoBehaviour
    {
        public static PlayerStats Instance { get; private set; }

        [Header("Multiplicadores (upgrades da loja / poderes)")]
        public float DamageMultiplier  = 1f;
        public float ReloadMultiplier  = 1f;   // <1 = mais rápido
        public float SpeedMultiplier   = 1f;
        public float FireRateMultiplier = 1f;  // >1 = atira mais rápido

        [Header("Níveis atuais")]
        public int DamageLevel  = 0;
        public int ReloadLevel  = 0;
        public int MaxHpLevel   = 0;
        public int SpeedLevel   = 0;
        public int FireRateLevel = 0;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // Chamado pela loja ao comprar upgrade de dano
        public void UpgradeDamage()
        {
            DamageLevel++;
            DamageMultiplier = 1f + 0.15f * DamageLevel;
        }

        // Poder das ondas: arma mais forte (+X% de dano direto)
        public void UpgradeDamagePercent(float percent)
        {
            DamageLevel++;
            DamageMultiplier += percent;
        }

        // Chamado pela loja ao comprar upgrade de recarga
        public void UpgradeReload()
        {
            ReloadLevel++;
            ReloadMultiplier = Mathf.Max(0.3f, 1f - 0.12f * ReloadLevel);
        }

        // Poder das ondas: cadência de tiro
        public void UpgradeFireRate(float percent)
        {
            FireRateLevel++;
            FireRateMultiplier += percent;
        }

        // Poder das ondas / loja: velocidade de movimento
        public void UpgradeSpeed(float percent = 0.15f)
        {
            SpeedLevel++;
            SpeedMultiplier += percent;
        }

        // Chamado pela loja ao comprar upgrade de vida (delega para PlayerHealth)
        public void UpgradeMaxHp()
        {
            MaxHpLevel++;
            var health = FindAnyObjectByType<PlayerHealth>();
            if (health != null) health.SetMaxHealth(100 + MaxHpLevel * 20);
        }
    }
}
