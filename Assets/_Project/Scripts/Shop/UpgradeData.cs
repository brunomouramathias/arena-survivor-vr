using UnityEngine;

namespace ArenaSurvivor.Shop
{
    public enum UpgradeType { Damage, MaxHealth, ReloadSpeed, MoveSpeed }

    [CreateAssetMenu(fileName = "UpgradeData", menuName = "ArenaSurvivor/Upgrade Data")]
    public class UpgradeData : ScriptableObject
    {
        [Header("Identificação")]
        public string upgradeName = "Upgrade";
        public string description = "Descrição do upgrade";
        public Sprite icon;

        [Header("Tipo e Custo")]
        public UpgradeType type;
        public int   baseCost     = 80;
        public float costMultiplier = 1.5f;  // Custo aumenta por nível
        public int   maxLevel     = 5;

        public int GetCost(int currentLevel)
        {
            return Mathf.RoundToInt(baseCost * Mathf.Pow(costMultiplier, currentLevel));
        }
    }
}
