# Ritmo das ondas, poderes e correção do nocaute

Implementado e verificado. Documento irmão: `docs/melhorias.md` (barras de vida, pontuação, torres).

## Checklist

| # | Pedido | Como ficou | Verificação |
|---|--------|-----------|-------------|
| 1 | Jogador ficava deitado depois de voltar | `PlayerAnimation.Revive()` faz `Rebind()` ao renascer | em jogo: `clipe atual = Idle_Gun`, jogador de pé |
| 2 | 2 tiros na 1ª onda, 3 na 2ª, 4 na 3ª | vida = tiros × 18 (dano da pistola) | medido em jogo: 36 / 54 / 72 HP → cai em 2 / 3 / 4 tiros |
| 3 | Inimigos lentos na 1ª e mais rápidos depois | 60% da velocidade na onda 1, +8% por onda, teto 160% | medido: 1,92 → 3,20 → 4,74 m/s |
| 4 | Poder brota ao matar o último inimigo | `WaveManager` solta o ícone na posição da última morte | ícone criado e ativo; poder de velocidade aplicado (+25%) |
| 5 | Evolução: velocidade → arma → … | sequência fixa que cicla e fica mais forte | `Speed → Weapon → FireRate → Damage → Health` |

## 1. Nocaute: por que ele ficava deitado

`PlayerAnimation` disparava o trigger `Die` no evento `PlayerDiedEvent`, mas nada voltava o Animator
para o estado normal — ele ficava travado no último frame do clipe de morte.

Correção (`PlayerAnimation.cs` + `PlayerHealth.cs`):

```csharp
public void Revive()
{
    animator.ResetTrigger(DieHash);   // limpa o trigger
    animator.Rebind();                // volta ao estado padrão (Idle)
    animator.Update(0f);
    CacheParameterList();
}
```

`RespawnRoutine()` chama `anim.Revive()` junto com: teleporte ao ponto inicial, 3 corações,
2,5 s de invulnerabilidade, −50 pontos e novos controles.

## 2. Tiros para matar por onda

`Waves/WaveTuning.cs` (novo, centraliza o balanceamento):

| Onda | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8+ |
|------|---|---|---|---|---|---|---|-----|
| Tiros na pistola | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 8 |
| Vida | 36 | 54 | 72 | 90 | 108 | 126 | 144 | 144 |

`EnemyData.useWaveShotRule` liga a regra; **o chefe tem a flag desligada** (mantém 800 HP e
cresce com a onda — na onda 3 fica com 1088 HP).

## 3. Velocidade progressiva

Ainda em `WaveTuning`:

```csharp
SpeedFactor(onda) = clamp(0.60 + (onda-1) * 0.08, 0.60, 1.60)
```

O `EnemyBase.Initialize` guarda o fator e expõe `MoveSpeed`; chaser, bruxo azul, stalker e chefe
usam esse valor no `NavMeshAgent`. Na prática (monstro, 3,2 m/s base):

| Onda | 1 | 4 | 9 | 20 |
|------|---|---|---|----|
| Velocidade | 1,92 m/s | 2,69 m/s | 3,97 m/s | 5,12 m/s |

## 4. Poder que brota ao limpar a onda

`Items/PowerUpPickup.cs` (novo). Ao zerar os inimigos, `WaveManager` solta o ícone **no lugar onde
o último inimigo morreu** e espera o jogador pegá-lo (até 14 s; se ninguém buscar, aplica sozinho
para a onda não travar). Ao pegar, o jogo segue para a loja.

O ícone é construído em runtime: cubo girando em 45°, brilho em cruz, anel no chão e luz pontual
na cor do poder — nada de prefab para configurar. Ele é atraído quando o jogador chega perto.

Sequência de evolução (`PowerUpPickup.Order`), repetindo mais forte a cada ciclo:

| Limpeza | Poder | Efeito |
|---------|-------|--------|
| onda 1 → 2 | **Velocidade** | `SpeedMultiplier` +25% (7,5 m/s com a base 6) |
| onda 2 → 3 | **Arma mais forte** | libera e equipa a próxima arma; se todas já estão liberadas, +35% de dano |
| onda 3 → 4 | **Cadência** | +20% na velocidade de tiro |
| onda 4 → 5 | **Dano** | +30% de dano |
| onda 5 → 6 | **Vida** | cura cheia |
| onda 6 → 7 | volta para velocidade, +10% por ciclo |  |

## Como verificar

- **Rápido (edit mode, ~10 s):** menu **Tools ▸ Arena ▸ Verificar Ritmo e Poderes (edit mode)**.
  Resultado atual: **16 OK / 0 falhas** → `docs/screens/poderes/verificacao.txt`.
- **Em jogo:** menu **Tools ▸ Arena ▸ Testar Poderes e Ritmo (checklist 2)** — entra em playmode,
  mede tiros/velocidade, cria o ícone (screenshot), aplica poderes, força o nocaute e confere que o
  jogador volta de pé. Escreve `docs/screens/poderes/relatorio.txt`.

Medições já obtidas em playmode:

```
Onda 1: inimigo cai em exatamente 2 tiros (36 HP)
Onda 2: inimigo cai em exatamente 3 tiros (54 HP)
Onda 3: inimigo cai em exatamente 4 tiros (72 HP)
Inimigos lentos na onda 1 e mais rápidos depois — onda1=1,92 | onda6=3,20 | onda12=4,74 m/s
Poder de VELOCIDADE deixa o jogador mais rápido — speed 1,00 -> 1,25 (7,5 m/s em jogo)
Jogador volta de pé depois do nocaute — revives=1 vida=3/3
Animator não está mais no clipe de morte — clipe=Idle_Gun
```
