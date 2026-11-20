using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ArenaSurvivor.Core;
using ArenaSurvivor.Shop;

namespace ArenaSurvivor.UI
{
    // Interface da loja — conecta os botões ao ShopManager
    public class ShopUI : MonoBehaviour
    {
        [Header("Moeda")]
        [SerializeField] private TextMeshProUGUI currencyText;

        [Header("Botões de Upgrade")]
        [SerializeField] private Button damageUpgradeBtn;
        [SerializeField] private Button reloadUpgradeBtn;
        [SerializeField] private Button healthUpgradeBtn;

        [Header("Textos de Custo")]
        [SerializeField] private TextMeshProUGUI damageCostText;
        [SerializeField] private TextMeshProUGUI reloadCostText;
        [SerializeField] private TextMeshProUGUI healthCostText;

        [Header("Botões de Arma")]
        [SerializeField] private Button shotgunBtn;
        [SerializeField] private Button energyGunBtn;
        [SerializeField] private TextMeshProUGUI shotgunBtnText;
        [SerializeField] private TextMeshProUGUI energyGunBtnText;

        private void OnEnable()
        {
            ScoreService.OnCurrencyChanged += OnCurrencyChanged;
            Refresh();
        }

        private void OnDisable()
        {
            ScoreService.OnCurrencyChanged -= OnCurrencyChanged;
        }

        private void Refresh()
        {
            if (ScoreService.Instance == null || ShopManager.Instance == null) return;

            int currency = ScoreService.Instance.Currency;
            if (currencyText != null) currencyText.text = $"Pontos: {currency}";

            var shop = ShopManager.Instance;

            // Atualiza botões de upgrade
            RefreshUpgradeButton(damageUpgradeBtn, damageCostText, UpgradeType.Damage);
            RefreshUpgradeButton(reloadUpgradeBtn, reloadCostText, UpgradeType.ReloadSpeed);
            RefreshUpgradeButton(healthUpgradeBtn, healthCostText, UpgradeType.MaxHealth);

            // Armas
            if (shotgunBtn != null)
            {
                bool unlocked = shop.IsShotgunUnlocked;
                shotgunBtn.interactable = !unlocked && ScoreService.Instance.CanAfford(shop.ShotgunCost);
                if (shotgunBtnText != null)
                    shotgunBtnText.text = unlocked ? "Desbloqueada" : $"Escopeta\n{shop.ShotgunCost} pts";
            }

            if (energyGunBtn != null)
            {
                bool unlocked = shop.IsEnergyGunUnlocked;
                energyGunBtn.interactable = !unlocked && ScoreService.Instance.CanAfford(shop.EnergyGunCost);
                if (energyGunBtnText != null)
                    energyGunBtnText.text = unlocked ? "Desbloqueada" : $"Arma de Energia\n{shop.EnergyGunCost} pts";
            }
        }

        private void RefreshUpgradeButton(Button btn, TextMeshProUGUI costText, UpgradeType type)
        {
            if (btn == null || ShopManager.Instance == null) return;
            int level = ShopManager.Instance.GetUpgradeLevel(type);
            btn.interactable = true;
            if (costText != null) costText.text = $"Nível {level}";
        }

        private void OnCurrencyChanged(int currency)
        {
            Refresh();
        }

        public void OnBuyDamage()   => PurchaseAndRefresh(() => ShopManager.Instance?.TryBuyUpgrade(UpgradeType.Damage));
        public void OnBuyReload()   => PurchaseAndRefresh(() => ShopManager.Instance?.TryBuyUpgrade(UpgradeType.ReloadSpeed));
        public void OnBuyHealth()   => PurchaseAndRefresh(() => ShopManager.Instance?.TryBuyUpgrade(UpgradeType.MaxHealth));
        public void OnBuyShotgun()  => PurchaseAndRefresh(() => ShopManager.Instance?.TryUnlockShotgun());
        public void OnBuyEnergy()   => PurchaseAndRefresh(() => ShopManager.Instance?.TryUnlockEnergyGun());

        public void OnNextWave()    => ShopManager.Instance?.CloseShop();

        private void PurchaseAndRefresh(System.Func<bool?> action)
        {
            action?.Invoke();
            Refresh();
        }
    }
}

