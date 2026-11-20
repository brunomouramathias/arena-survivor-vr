using System.Collections.Generic;
using UnityEngine;
using ArenaSurvivor.Core;
using ArenaSurvivor.Player;
using ArenaSurvivor.Weapons;

namespace ArenaSurvivor.Shop
{
    // Gerencia a loja entre ondas: upgrades, desbloqueio de armas, UI
    public class ShopManager : MonoBehaviour
    {
        public static ShopManager Instance { get; private set; }

        [Header("Upgrades disponíveis")]
        [SerializeField] private List<UpgradeData> upgrades;

        [Header("Armas desbloqueáveis")]
        [SerializeField] private int   shotgunCost     = 150;
        [SerializeField] private int   energyGunCost   = 350;
        [SerializeField] private bool  shotgunUnlocked = false;
        [SerializeField] private bool  energyGunUnlocked = false;

        private Dictionary<UpgradeType, int> upgradeLevels = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Inicializa níveis
            if (upgrades != null)
                foreach (var u in upgrades)
                    if (u != null) upgradeLevels[u.type] = 0;
        }

        // Fecha a loja e retorna ao combate
        public void CloseShop()
        {
            GameManager.Instance?.SetState(GameState.Playing);
        }

        // Tenta comprar um upgrade
        public bool TryBuyUpgrade(UpgradeType type)
        {
            var upgradeData = upgrades.Find(u => u.type == type);
            if (upgradeData == null) return false;

            int level = upgradeLevels.GetValueOrDefault(type, 0);
            if (level >= upgradeData.maxLevel) return false;

            int cost = upgradeData.GetCost(level);
            if (!ScoreService.Instance.CanAfford(cost)) return false;

            ScoreService.Instance.SpendCurrency(cost);
            upgradeLevels[type] = level + 1;
            ApplyUpgrade(type);
            return true;
        }

        private void ApplyUpgrade(UpgradeType type)
        {
            var stats = PlayerStats.Instance;
            if (stats == null) return;

            switch (type)
            {
                case UpgradeType.Damage:
                    stats.UpgradeDamage();
                    break;
                case UpgradeType.MaxHealth:
                    stats.UpgradeMaxHp();
                    break;
                case UpgradeType.ReloadSpeed:
                    stats.UpgradeReload();
                    break;
            }
        }

        // Tenta desbloquear escopeta
        public bool TryUnlockShotgun()
        {
            if (shotgunUnlocked) return false;
            if (!ScoreService.Instance.CanAfford(shotgunCost)) return false;

            ScoreService.Instance.SpendCurrency(shotgunCost);
            shotgunUnlocked = true;

            var shooter = FindAnyObjectByType<PlayerShooter>();
            shooter?.UnlockWeapon(1);
            return true;
        }

        // Tenta desbloquear arma de energia
        public bool TryUnlockEnergyGun()
        {
            if (energyGunUnlocked) return false;
            if (!ScoreService.Instance.CanAfford(energyGunCost)) return false;

            ScoreService.Instance.SpendCurrency(energyGunCost);
            energyGunUnlocked = true;

            var shooter = FindAnyObjectByType<PlayerShooter>();
            shooter?.UnlockWeapon(2);
            return true;
        }

        public int GetUpgradeLevel(UpgradeType type) => upgradeLevels.GetValueOrDefault(type, 0);

        // Custo do próximo nível do upgrade (usado pela tela da loja)
        public int GetUpgradeCost(UpgradeType type)
        {
            var upgradeData = upgrades.Find(u => u.type == type);
            if (upgradeData == null) return 0;
            return upgradeData.GetCost(GetUpgradeLevel(type));
        }

        public int GetUpgradeMaxLevel(UpgradeType type)
        {
            var upgradeData = upgrades.Find(u => u.type == type);
            return upgradeData != null ? upgradeData.maxLevel : 0;
        }

        public bool IsShotgunUnlocked  => shotgunUnlocked;
        public bool IsEnergyGunUnlocked => energyGunUnlocked;
        public int  ShotgunCost  => shotgunCost;
        public int  EnergyGunCost => energyGunCost;
    }
}
