using UnityEngine;
using ArenaSurvivor.Enemies;

namespace ArenaSurvivor.Weapons
{
    // Projétil poolado — se move, causa dano ao colidir, retorna ao pool.
    // A colisão é feita por VARREDURA a cada frame (cápsula entre a posição anterior
    // e a nova), então a bala nunca "atravessa" o inimigo entre dois frames.
    public class Projectile : MonoBehaviour
    {
        [Header("Acerto")]
        [Tooltip("Raio efetivo da bala — quanto maior, mais fácil acertar")]
        [SerializeField] private float hitRadius = 0.28f;

        [Tooltip("Distância inicial em que paredes são ignoradas (evita o cano encostado na torre comer o tiro)")]
        [SerializeField] private float wallClearance = 0.7f;

        [SerializeField] private GameObject impactPrefab;

        private float speed;
        private float damage;
        private float range;
        private bool  piercing;
        private bool  isEnemyProjectile;
        private float traveled;
        private bool  returned;

        private static readonly Collider[] buffer = new Collider[24];

        public float HitRadius => hitRadius;

        public void Initialize(float spd, float dmg, float rng, bool pierce, bool enemyProjectile = false)
        {
            speed             = spd;
            damage            = dmg;
            range             = rng;
            piercing          = pierce;
            isEnemyProjectile = enemyProjectile;
            traveled          = 0f;
            returned          = false;
            if (hitRadius < 0.05f) hitRadius = 0.28f;   // seguro contra prefab com valor zerado
        }

        private void Update() => Advance(Time.deltaTime);

        // Passo de simulação (público para os testes chamarem com dt fixo)
        public void Advance(float dt)
        {
            if (returned || dt <= 0f) return;

            float dist = speed * dt;

            Vector3 from = transform.position;
            Vector3 to   = from + transform.forward * dist;
            traveled += dist;

            if (SweepAndHit(from, to)) return;

            transform.position = to;

            if (traveled >= range) ReturnToPool();
        }

        // Varre o trecho percorrido procurando alvo; retorna true se acertou algo
        private bool SweepAndHit(Vector3 from, Vector3 to)
        {
            int n;
            if ((to - from).sqrMagnitude < 0.000001f)
                n = Physics.OverlapSphereNonAlloc(from, hitRadius, buffer, ~0, QueryTriggerInteraction.Collide);
            else
                n = Physics.OverlapCapsuleNonAlloc(from, to, hitRadius, buffer, ~0, QueryTriggerInteraction.Collide);

            if (n == 0) return false;

            EnemyBase  hitEnemy  = null;
            Player.PlayerHealth hitPlayer = null;
            bool blocked = false;

            for (int i = 0; i < n; i++)
            {
                var col = buffer[i];
                if (col == null) continue;
                if (col.transform == transform || col.transform.IsChildOf(transform)) continue;

                if (isEnemyProjectile)
                {
                    // Projétil inimigo: atinge o jogador
                    var ph = col.GetComponentInParent<Player.PlayerHealth>();
                    if (ph != null) { hitPlayer = ph; break; }
                }
                else
                {
                    // Projétil do jogador: ignora o próprio jogador
                    if (col.GetComponentInParent<Player.PlayerHealth>() != null) continue;

                    var enemy = col.GetComponentInParent<EnemyBase>();
                    if (enemy != null && !enemy.IsDead) { hitEnemy = enemy; break; }

                    // ignora parede/torre nos primeiros centímetros (cano encostado nela)
                    if ((col.CompareTag("Obstacle") || col.CompareTag("Wall")) && traveled > wallClearance)
                    { blocked = true; break; }
                }
            }

            if (hitPlayer != null)
            {
                hitPlayer.TakeDamage((int)damage);
                ReturnToPool();
                return true;
            }

            if (hitEnemy != null)
            {
                hitEnemy.TakeDamage((int)damage);

                if (piercing) return false;    // continua voando
                ReturnToPool();
                return true;
            }

            if (blocked)
            {
                ReturnToPool();
                return true;
            }

            return false;
        }

        // Mantém o caminho de gatilho como reforço (balas lentas / alvos grandes)
        private void OnTriggerEnter(Collider other)
        {
            if (returned) return;

            if (isEnemyProjectile)
            {
                if (other.CompareTag("Player"))
                {
                    other.GetComponentInParent<Player.PlayerHealth>()?.TakeDamage((int)damage);
                    ReturnToPool();
                }
                return;
            }

            if (other.CompareTag("Enemy"))
            {
                var enemy = other.GetComponentInParent<EnemyBase>();
                if (enemy == null || enemy.IsDead) return;
                enemy.TakeDamage((int)damage);
                if (!piercing) ReturnToPool();
            }
            else if (other.CompareTag("Obstacle") || other.CompareTag("Wall"))
            {
                ReturnToPool();
            }
        }

        private void ReturnToPool()
        {
            if (returned) return;
            returned = true;

            if (impactPrefab != null)
            {
                var fx = Instantiate(impactPrefab, transform.position, Quaternion.LookRotation(-transform.forward));
                fx.transform.localScale = Vector3.one * 2.2f;   // faíscas visíveis no tamanho do jogo
                Destroy(fx, 1.2f);
            }

            gameObject.SetActive(false);
        }
    }
}
