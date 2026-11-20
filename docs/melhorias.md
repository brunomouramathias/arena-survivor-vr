# Melhorias de combate, ondas e pontuação

Pacote de melhorias implementado e **verificado em jogo** (16 verificações automáticas, 0 falhas).
Relatório bruto: `docs/screens/melhorias/relatorio.txt`.

## Checklist

| # | Pedido | Como ficou | Verificação |
|---|--------|-----------|-------------|
| 1 | Vida em cima de cada inimigo | Barra flutuante criada em runtime para **todos** os inimigos | `Barra de vida criada em chaser, bruxo azul e chefe — 3/3` |
| 2 | Onda 1: no máximo 3 tiros por inimigo | HP da onda 1 limitado a **54** (3 × 18 da pistola) | `chasers=45HP, bruxo azul=34HP` + `morrem em até 3 tiros` |
| 3 | Menos inimigos na 1ª onda, crescendo depois | 5 → 7 → 8 → 10 → 12 → 14 → … → 30 (teto) | `o1:5 … o10:20 o20:30` |
| 4 | Jogo segue até a **última torre** cair | Jogador nocauteado **renasce**; derrota só com 0 torres | `estado=Playing` no nocaute, `GameOver` só com 0 torres |
| 5 | Pontos por tempo, torres vivas e vida das torres | +5/s em jogo + bônus final (tempo×10, torre×250, 1% HP×5) | `tempo(+200) torres(+1000) vida(+1735)` |

## 1. Barra de vida em cima dos inimigos

`Assets/_Project/Scripts/Enemies/EnemyHealthBar.cs` — reescrita como barra **procedural**:

- Dois quads (`BG` + `Fill`) com material `Universal Render Pipeline/Unlit` → não depende de
  Canvas/Slider configurado no prefab (funcionava em uns e não em outros).
- Criada automaticamente pelo `EnemyBase.Awake()` se o prefab não tiver uma.
- Ancorada no **osso da cabeça** do rig (`spine.005` nos bruxos, `head`/`mixamorig:Head` nos
  Mixamo) + 0,32 m; se não houver osso, usa o topo do *bounding box* + 0,15 m.
- **Billboard**: copia a rotação da câmera todo `LateUpdate` (sempre de frente para o jogador).
- Cor vai de **vermelho → verde** conforme a vida; some quando o inimigo morre e volta no `Initialize`.
- Escala compensada (`1/lossyScale`) para ficar sempre com o mesmo tamanho em metros (0,95 m).
- Barras antigas de Canvas/Slider nos prefabs são destruídas em runtime (`RemoveLegacyUi`).

Provas: `10_barra_monstro.png`, `11_barra_bruxo_azul.png`, `12_barra_chefe.png`.

## 2. Onda 1 morre em até 3 tiros

`EnemyBase.FirstWaveMaxHealth = 54` (3 × 18 da pistola). No `Initialize`:

```csharp
if (waveNumber <= 1) maxHealth = Mathf.Min(maxHealth, FirstWaveMaxHealth);
```

O chefe (800 HP) **não entra mais na onda 1** — ele aparece nas ondas 3, 6, 9, 12, 15, 18
(`WaveConfig.IsBossWave`). Assim toda a primeira onda cai em ≤ 3 tiros.

## 3. Escalonamento de inimigos

`WaveConfig.GetEnemyCount(wave) = base + (wave - 1) * fator`, com `base = 5`, `fator = 1,7`
e teto `maxSimultaneous = 30`.

| Onda | 1 | 2 | 3 | 5 | 6 | 10 | 15 | 16+ |
|------|---|---|---|---|---|----|----|-----|
| Inimigos | 5 | 7 | 8 | 12 | 14 | 20 | 29 | 30 |

Composição: no mínimo **2 bruxos azuis** por onda (até 1/3 da onda); stalkers a partir da onda 3.

## 4. Fim de jogo = última torre

- `PlayerHealth.Die()` **não** encerra mais o jogo: o jogador fica **nocauteado 1,6 s**
  (controle desligado), perde 50 pontos, volta ao ponto inicial com 3 corações e
  2,5 s de invulnerabilidade.
- A derrota vem de `TowerHealth.DestroyTower()`: quando `TowerHealth.All` não tem nenhuma torre
  viva, `GameState.GameOver`.
- HUD mostra `TORRES 4/4 (99%)` em tempo real (4 torres na Arena).

## 5. Pontuação

`Core/ScoreService.cs`:

- **Em jogo**: +5 pontos por segundo (`pointsPerSecond`), só no estado `Playing`
  (loja/menu/pausa não contam).
- **No fim** (`GameOver` ou `Victory`), bônus somados uma única vez:

| Bônus | Fórmula | Exemplo do teste |
|-------|---------|------------------|
| Tempo | segundos sobrevividos × 10 | 20 s → **+200** |
| Torres de pé | torres vivas × 250 | 4 torres → **+1000** |
| Vida das torres | (soma de 1% de vida restante) × 5 | 3,47 torres cheias → **+1735** |

A tela de Game Over mostra o detalhamento (`Sobreviveu 21s (+210) · Torres de pé (+1000) ·
Vida das torres (+1735)`) e o HUD mostra `PONTOS`, `TEMPO mm:ss` e `TORRES n/4 (%)`.

## Como reproduzir a verificação

No Unity: **Tools ▸ Arena ▸ Testar Melhorias (checklist)**.

O teste entra em playmode na Arena e valida, escrevendo `docs/screens/melhorias/relatorio.txt`
e as capturas:

- contagem de inimigos por onda (1..20);
- HP e morte em 3 tiros na onda 1 (chaser e bruxo azul);
- barra de vida existente/visível/acima da cabeça nos três tipos + close-ups;
- pontos por segundo;
- nocaute do jogador sem fim de jogo e retorno com vida cheia;
- bônus de tempo/torres/vida das torres;
- derrota somente quando a última torre cai.

Resultado da última execução: **16 OK / 0 FALHAS**.
