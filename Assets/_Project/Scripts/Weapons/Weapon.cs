using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ArenaSurvivor.Core;
using ArenaSurvivor.Player;
using ArenaSurvivor.Utils;

namespace ArenaSurvivor.Weapons
{
    // Runtime de uma arma equipada — dispara, recarrega, controla munição
    public class Weapon : MonoBehaviour
    {
        [SerializeField] private WeaponData data;
        [SerializeField] private Transform  muzzlePoint;
        [SerializeField] private AudioSource audioSource;

        private int   currentClip;
        private int   currentReserve;
        private bool  isReloading;
        private float nextFireTime;
        private bool  initialized;        // munição/estado já inicializados
        private bool  unlockRequested;    // Unlock() pedido antes do Awake (arma desativada)

        // Pool de projéteis
        private ObjectPool<Projectile> projectilePool;

        public WeaponData Data => data;
        public int   CurrentClip    => currentClip;
        public int   CurrentReserve => currentReserve;
        public bool  IsReloading    => isReloading;
        public bool  IsUnlocked     { get; private set; }

        // Sem pente e sem reserva: a arma não atira mais (a pistola tem reserva infinita, nunca fica seca)
        public bool  IsDry          => currentClip <= 0 && currentReserve <= 0 && data != null && data.reserveAmmo >= 0;

        // Usado pelos testes automatizados para simular munição esgotada
        public void DebugDrainAmmo() { currentClip = 0; currentReserve = 0; OnAmmoChanged?.Invoke(); }

        public event System.Action OnAmmoChanged;

        private void Awake()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            if (audioSource != null) audioSource.playOnAwake = false;

            EnsureInitialized();
        }

        // Inicializa munição, estado e pool. Idempotente e seguro ANTES do Awake:
        // a escopeta / arma de energia começam com o GameObject desativado, então o Awake
        // só roda quando o jogador equipa — e NÃO pode apagar o desbloqueio feito pela loja
        // (era o bug: comprar a escopeta e não conseguir mais atirar).
        public void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

            if (data == null) return;

            currentClip    = data.clipSize;
            currentReserve = data.reserveAmmo;
            IsUnlocked     = data.startUnlocked || unlockRequested;

            if (data.projectilePrefab != null)
            {
                var prefabProjectile = data.projectilePrefab.GetComponent<Projectile>();
                if (prefabProjectile != null)
                    projectilePool = new ObjectPool<Projectile>(prefabProjectile, 30, ProjectilePoolRoot());
            }
        }

        // As balas ficam num objeto fixo na origem do mundo — NUNCA embaixo do jogador:
        // dentro do jogador elas herdavam a rotação do corpo (que gira para a mira) e o tiro
        // saía curvando conforme o personagem virava.
        private static Transform sharedPoolRoot;

        private static Transform ProjectilePoolRoot()
        {
            if (sharedPoolRoot != null) return sharedPoolRoot;

            var go = GameObject.Find("Projectiles_Pool");
            if (go == null) go = new GameObject("Projectiles_Pool");

            sharedPoolRoot = go.transform;
            sharedPoolRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            sharedPoolRoot.localScale = Vector3.one;
            return sharedPoolRoot;
        }

        public void Unlock()
        {
            unlockRequested = true;
            EnsureInitialized();
            IsUnlocked = true;
        }

        // Chamado pelo PlayerShooter a cada frame quando o botão está pressionado
        public void TryFire(Vector3 targetPoint)
        {
            EnsureInitialized();
            if (data == null || !IsUnlocked)   return;
            if (isReloading)                   return;
            if (Time.time < nextFireTime)      return;

            if (currentClip <= 0)
            {
                if (data.emptySFX != null && audioSource != null) audioSource.PlayOneShot(data.emptySFX);
                TryReload();
                return;
            }

            Fire(targetPoint);
        }

        private void Fire(Vector3 targetPoint)
        {
            // Cadência com o multiplicador de poder (arma evoluída atira mais rápido)
            float fireMult = Player.PlayerStats.Instance != null
                ? Mathf.Max(0.2f, Player.PlayerStats.Instance.FireRateMultiplier)
                : 1f;
            nextFireTime = Time.time + 1f / Mathf.Max(0.05f, data.fireRate * fireMult);
            currentClip--;
            OnAmmoChanged?.Invoke();

            // Direção do tiro = exatamente onde a mira está. Sem desvio, sem curva:
            // a bala sai reta e continua reta até o fim do alcance.
            Vector3 baseDir = AimDirection(targetPoint);

            for (int i = 0; i < data.pellets; i++)
                SpawnProjectile(muzzlePoint.position, DirectionForPellet(i, baseDir));

            // Muzzle flash
            if (data.muzzleFlashPrefab != null)
            {
                var flash = Instantiate(data.muzzleFlashPrefab, muzzlePoint.position, muzzlePoint.rotation);
                Destroy(flash, 0.1f);
            }

            // SFX
            if (data.shootSFX != null && audioSource != null) audioSource.PlayOneShot(data.shootSFX);

            // Camera shake
            var cam = FindAnyObjectByType<CameraController>();
            cam?.Shake(data.shakeMagnitude);

            // Aplica multiplicador de dano via PlayerStats
            // (já aplicado no Projectile.Initialize via PlayerShooter)
        }

        // Direção da mira, projetada na altura do cano (a bala não "entra no chão")
        public Vector3 AimDirection(Vector3 targetPoint)
        {
            Vector3 aimTarget = targetPoint;
            aimTarget.y = muzzlePoint != null ? muzzlePoint.position.y : transform.position.y;
            Vector3 origin = muzzlePoint != null ? muzzlePoint.position : transform.position;
            Vector3 dir = aimTarget - origin;
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
        }

        // Dispersão SIMÉTRICA em torno da mira: os bagos se abrem igualmente para os dois
        // lados, então o centro do tiro continua exatamente onde o jogador apontou.
        public Vector3 DirectionForPellet(int index, Vector3 baseDir)
        {
            if (data.spread <= 0f || data.pellets <= 0) return baseDir;

            // -1 .. +1, com passo constante (bago 0 no centro quando a quantidade é ímpar)
            float t = data.pellets <= 1 ? 0f : Mathf.Clamp(2f * index / (data.pellets - 1) - 1f, -1f, 1f);
            float angle = t * data.spread;
            return Quaternion.Euler(0f, angle, 0f) * baseDir;
        }

        private void SpawnProjectile(Vector3 origin, Vector3 dir)
        {
            if (projectilePool == null) return;

            var proj = projectilePool.Get(origin, Quaternion.LookRotation(dir));
            proj.IsPooled = true;
            float dmg = data.damage;

            // Aplica multiplicador de dano
            var stats = Player.PlayerStats.Instance;
            if (stats != null) dmg *= stats.DamageMultiplier;

            proj.Initialize(data.projectileSpeed, dmg, data.projectileRange, data.piercing);
        }

        public void TryReload()
        {
            EnsureInitialized();
            if (isReloading)      return;
            if (currentClip >= data.clipSize) return;
            if (currentReserve == 0)         return;  // Sem reserva (nunca para pistola)

            StartCoroutine(ReloadCoroutine());
        }

        private IEnumerator ReloadCoroutine()
        {
            isReloading = true;
            if (data.reloadSFX != null && audioSource != null) audioSource.PlayOneShot(data.reloadSFX);

            // Aplica multiplicador de recarga via PlayerStats
            float stats_mult = Player.PlayerStats.Instance?.ReloadMultiplier ?? 1f;
            yield return new WaitForSeconds(data.reloadTime * stats_mult);

            int needed = data.clipSize - currentClip;
            if (data.reserveAmmo < 0)
            {
                // Reserva infinita (pistola)
                currentClip = data.clipSize;
            }
            else
            {
                int take   = Mathf.Min(needed, currentReserve);
                currentClip    += take;
                currentReserve -= take;
            }

            isReloading = false;
            OnAmmoChanged?.Invoke();
        }

        public void AddAmmo(int amount)
        {
            EnsureInitialized();
            if (data == null || data.reserveAmmo < 0) return;
            currentReserve += amount;
            OnAmmoChanged?.Invoke();
        }
    }
}
