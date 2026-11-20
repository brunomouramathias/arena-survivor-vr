using UnityEngine;

namespace ArenaSurvivor.Weapons
{
    // Dados configuráveis de uma arma — edite os valores sem tocar em código
    [CreateAssetMenu(fileName = "WeaponData", menuName = "ArenaSurvivor/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        [Header("Identificação")]
        public string weaponName  = "Pistola";
        public Sprite weaponIcon;

        [Header("Dano e Cadência")]
        public float damage       = 18f;
        public float fireRate     = 4f;      // Tiros por segundo
        public int   pellets      = 1;       // Pellets por tiro (escopeta > 1)
        public float spread       = 0f;      // Dispersão em graus

        [Header("Projétil")]
        public GameObject projectilePrefab;
        public float projectileSpeed  = 25f;
        public float projectileRange  = 30f;
        public bool  piercing         = false;  // Perfura inimigos (arma de energia)

        [Header("Munição")]
        public int   clipSize       = 12;
        public int   reserveAmmo    = -1;   // -1 = infinito
        public float reloadTime     = 1.1f;

        [Header("Loja")]
        public int   unlockCost     = 0;    // 0 = começa desbloqueada
        public bool  startUnlocked  = true;

        [Header("Efeitos")]
        public GameObject muzzleFlashPrefab;
        public AudioClip  shootSFX;
        public AudioClip  reloadSFX;
        public AudioClip  emptySFX;

        [Header("Camera Shake")]
        public float shakeMagnitude = 0.1f;
    }
}
