using System.Collections;
using UnityEngine;
using ArenaSurvivor.Core;

namespace ArenaSurvivor.Player
{
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Configuração")]
        [SerializeField] private int maxHealth = 3; // 3 corações

        [Header("Nocaute (o jogo continua até a última torre cair)")]
        [SerializeField] private float respawnDelay = 1.6f;
        [SerializeField] private float respawnInvulnerability = 2.5f;
        [SerializeField] private int   respawnScorePenalty = 50;

        private int currentHealth;
        private bool isInvincible;
        private bool isDead;

        private Vector3    spawnPosition;
        private Quaternion spawnRotation;

        private Renderer[] renderers;
        private MaterialPropertyBlock mpb;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsDead => isDead;
        public bool IsInvincible => isInvincible;

        private void Awake()
        {
            currentHealth = maxHealth;
            renderers     = GetComponentsInChildren<Renderer>();
            mpb           = new MaterialPropertyBlock();

            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        public void SetSpawnPoint(Vector3 position, Quaternion rotation)
        {
            spawnPosition = position;
            spawnRotation = rotation;
        }

        public void SetMaxHealth(int newMax)
        {
            int diff  = newMax - maxHealth;
            maxHealth = newMax;
            currentHealth = Mathf.Min(currentHealth + diff, maxHealth);
            EventBus.Publish(new PlayerDamagedEvent { CurrentHealth = currentHealth, MaxHealth = maxHealth });
        }

        public void TakeDamage(int amount)
        {
            if (isInvincible || isDead) return;

            // Com 3 corações, qualquer golpe tira exatamente 1 coração
            int dmg = maxHealth <= 3 ? 1 : amount;
            currentHealth = Mathf.Max(0, currentHealth - dmg);
            EventBus.Publish(new PlayerDamagedEvent { CurrentHealth = currentHealth, MaxHealth = maxHealth });
            StartCoroutine(HitFlash());
            StartCoroutine(Invulnerability(0.9f));

            if (currentHealth <= 0) Die();
        }

        public void Heal(int amount)
        {
            if (isDead) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            EventBus.Publish(new PlayerDamagedEvent { CurrentHealth = currentHealth, MaxHealth = maxHealth });
        }

        public void SetInvincible(bool value) => isInvincible = value;

        private void Die()
        {
            if (isDead) return;
            isDead = true;
            EventBus.Publish(new PlayerDiedEvent());

            // A derrota NÃO acontece aqui: o jogo segue enquanto houver torre de pé.
            // O jogador fica nocauteado alguns segundos e volta para a arena.
            StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            // Congela o controle durante o nocaute
            var controller = GetComponent<PlayerController>();
            var shooter    = GetComponent<PlayerShooter>();
            var anim       = GetComponent<PlayerAnimation>();
            if (controller != null) controller.enabled = false;
            if (shooter != null)    shooter.enabled    = false;

            var cc = GetComponent<CharacterController>();

            ScoreService.Instance?.PenalizeScore(respawnScorePenalty);
            PoolService.Instance?.ShowFloatingText(
                transform.position + Vector3.up * 2.2f,
                "NOCAUTEADO -",
                new Color(1f, 0.35f, 0.3f));

            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, respawnDelay));

            // Volta ao ponto inicial da arena
            if (cc != null) cc.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            if (cc != null) cc.enabled = true;

            currentHealth = maxHealth;
            EventBus.Publish(new PlayerDamagedEvent { CurrentHealth = currentHealth, MaxHealth = maxHealth });

            if (controller != null) controller.enabled = true;
            if (shooter != null)    shooter.enabled    = true;

            // Sair da animação de morte (o Animator ficava travado deitado)
            if (anim != null) anim.Revive();

            isDead = false;
            StartCoroutine(HitFlash());
            StartCoroutine(Invulnerability(Mathf.Max(0.5f, respawnInvulnerability)));

            PoolService.Instance?.ShowFloatingText(
                transform.position + Vector3.up * 2.2f,
                "DE VOLTA",
                new Color(0.35f, 1f, 0.45f));
        }

        private IEnumerator HitFlash()
        {
            // Flash vermelho via MaterialPropertyBlock (não altera o material compartilhado)
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", new Color(1f, 0.45f, 0.4f));
                r.SetPropertyBlock(block);
            }

            yield return new WaitForSecondsRealtime(0.12f);

            // Limpar o block devolve a cor ORIGINAL do material (não força branco)
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.SetPropertyBlock(null);
            }
        }

        private IEnumerator Invulnerability(float seconds)
        {
            isInvincible = true;
            yield return new WaitForSecondsRealtime(seconds);
            isInvincible = false;
        }
    }
}
