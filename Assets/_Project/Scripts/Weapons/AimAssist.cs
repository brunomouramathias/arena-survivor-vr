using UnityEngine;
using ArenaSurvivor.Enemies;

namespace ArenaSurvivor.Weapons
{
    // Ponto de mira no inimigo (peito). A bala do jogador sempre sai reta da mira.
    public static class AimAssist
    {
        // O tiro do jogador NÃO é desviado: a bala sai reta e continua reta (pedido do jogo:
        // "faça ele ir reto e não dar curva"). A mira assistida antiga ficou desligada — o que
        // sobrou aqui é só o ponto de mira no peito do inimigo, usado pelo HUD/testes.

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
