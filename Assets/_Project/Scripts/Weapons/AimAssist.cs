using UnityEngine;
using ArenaSurvivor.Enemies;

namespace ArenaSurvivor.Weapons
{
    // Mira assistida: se o tiro passa perto de um inimigo, a bala é levemente
    // desviada para o centro dele. Não "cola" na parede nem em aliado.
    public static class AimAssist
    {
        // Raio da "bolha" de ajuda em volta da linha de tiro (metros)
        public const float Radius = 1.1f;
        // Ângulo máximo de desvio, para não virar aimbot
        public const float MaxAngle = 14f;

        static readonly RaycastHit[] hits = new RaycastHit[24];

        // Retorna a direção já corrigida (ou a original, se nada por perto)
        public static Vector3 Apply(Vector3 origin, Vector3 direction, float range = 45f)
        {
            if (direction.sqrMagnitude < 0.0001f) return direction;
            direction.Normalize();

            int n = Physics.SphereCastNonAlloc(origin, Radius, direction, hits, range,
                                               ~0, QueryTriggerInteraction.Collide);
            if (n == 0) return direction;

            EnemyBase bestEnemy = null;
            float      bestDist  = float.MaxValue;
            float      wallDist  = float.MaxValue;

            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider == null) continue;

                // ignora o próprio jogador (e o que estiver acima dele)
                if (h.collider.GetComponentInParent<Player.PlayerHealth>() != null) continue;

                var enemy = h.collider.GetComponentInParent<EnemyBase>();
                if (enemy != null)
                {
                    if (enemy.IsDead) continue;
                    if (h.distance < bestDist) { bestDist = h.distance; bestEnemy = enemy; }
                    continue;
                }

                // parede/obstáculo mais perto que o inimigo bloqueia a ajuda
                if (h.collider.CompareTag("Obstacle") || h.collider.CompareTag("Wall"))
                    if (h.distance < wallDist) wallDist = h.distance;
            }

            if (bestEnemy == null || wallDist < bestDist) return direction;

            Vector3 chest = ChestPoint(bestEnemy);
            Vector3 wanted = (chest - origin);
            if (wanted.sqrMagnitude < 0.01f) return direction;
            wanted.Normalize();

            float angle = Vector3.Angle(direction, wanted);
            if (angle > MaxAngle) return direction;

            // desvio proporcional: quanto mais perto do alvo, mais "puxa"
            float t = Mathf.Clamp01(1f - angle / MaxAngle);
            return Vector3.Slerp(direction, wanted, Mathf.Lerp(0.35f, 1f, t)).normalized;
        }

        // Ponto de mira no inimigo (peito/altura do cano, não nos pés)
        public static Vector3 ChestPoint(EnemyBase enemy)
        {
            Vector3 p = enemy.transform.position;

            // usa a altura do próprio colisor quando existir
            var col = enemy.GetComponent<Collider>();
            if (col != null)
            {
                var b = col.bounds;
                return new Vector3(b.center.x, Mathf.Lerp(b.min.y, b.max.y, 0.62f), b.center.z);
            }
            return p + Vector3.up * 1.1f;
        }
    }
}
