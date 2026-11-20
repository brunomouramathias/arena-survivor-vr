# Loja entre ondas — o travamento ao pegar o ícone de poder

Correção de 20/11. Prova em jogo: `docs/screens/loja/fluxo.txt` (7/7 PASS) e
`docs/screens/loja/01_loja_onda.png`.

## Sintoma

Quando o último inimigo da onda morre, brota no chão o ícone de poder. Ao **encostar nele o
jogo travava**: a cena ficava parada, sem nenhuma tela, e não havia como continuar.

## Causa raiz

O caminho do código era:

```
PowerUpPickup.Collect()          (jogador encosta no ícone)
  -> WaveManager.SpawnAndWaitPowerUp() termina
  -> WaveManager.WaveLoop(): GameManager.SetState(GameState.Shop)
  -> Time.timeScale = 0            (GameManager.SetState)
```

A loja deveria ser mostrada pelo `UIManager`, que liga o `Shop_Panel`. Mas na cena
`Arena.unity` o nó `Canvas_HUD` — que contém `UIManager`, `HUDController`, `HUD_Panel`,
`Shop_Panel`, `Pause_Panel` — está **desativado** (`m_IsActive: 0`), e o `Shop_Panel` está
**vazio** (`m_Children: []`, 100×100 px). Ou seja: nada aparecia, o `timeScale` ficava em 0 e
o jogo congelava para sempre.

Medições em play mode (sonda temporária):

| Etapa | Custo |
|---|---|
| `PowerUpPickup.SpawnNext` (cria malha + luz) | ~6 ms, 1 frame de 9,8 ms |
| pior frame na coleta | 23 ms |
| `timeScale` depois da coleta | **0** e nada na tela |

Ou seja: não era falta de desempenho — era estado do jogo sem interface.

## Correção

1. **`UI/ShopScreen.cs`** (novo) — tela de loja construída por código, sem prefab:
   - fundo escuro, título `ONDA N CONCLUÍDA — LOJA`, pontos;
   - `DANO +10%`, `RECARGA +15%`, `VIDA MÁXIMA +20` com nível/máximo/custo e botão;
   - `ESCOPETA` e `ARMA DE ENERGIA` com custo (ficam apagados sem pontos);
   - botão **PRÓXIMA ONDA ▶** e atalhos **ESPAÇO / ENTER / N / ESC**;
   - escurece/esconde o HUD de combate enquanto está aberta e devolve o cursor ao mouse.
   - Nasce sozinha em qualquer cena `Arena` via `[RuntimeInitializeOnLoadMethod]`.
2. **`ShopManager`** ganhou `GetUpgradeCost(type)` e `GetUpgradeMaxLevel(type)` para a tela
   mostrar custo/nível.
3. **`GameManager`** — travas anti-congelamento:
   - `Shop`/`Paused` com `timeScale 0` e **sem tela visível** → volta a `Playing` em 1,5 s
     (`Debug.LogWarning` explicando);
   - `Playing` com `timeScale 0` já voltava para 1 (trava antiga, mantida).
4. **`GameHUD`** — o estado `Victory` (onda 20) também ficava em `timeScale 0` sem UI; agora
   mostra o painel com título **VITÓRIA!** e os mesmos atalhos de reinício.

O `Canvas_HUD` legado continua desativado (a UI que funciona é a procedural: `GameHUD` +
`ShopScreen`).

## Como testar

`Tools ▸ Arena ▸ Teste Loja (ícone -> loja -> onda 2)` (play mode, batch):

```
[1] onda limpa? state=Cleared inimigos=0
[2] ícone de poder brotou: True | ícone coletado: True
[3] estado=Shop timeScale=0 telaVisivel=True botões=6 visíveis=6 => PASS
    'ONDA 1 CONCLUÍDA  —  LOJA'   'PONTOS: 188'
    'DANO +10% · Lv 0/5 · 80 pts'  'RECARGA +15% · Lv 0/5 · 80 pts'
    'VIDA MÁXIMA +20 · Lv 0/5 · 80 pts'  'ESCOPETA — 150 pts'
    'ARMA DE ENERGIA — 350 pts'   'PRÓXIMA ONDA ▶'
[3b] clique no 1º botão: raycast=True nível DANO 0->1 pontos 188->108 => PASS
[4] após continuar: estado=Playing timeScale=1 => PASS
[5] onda=2/Spawning inimigos=1 timeScale=1 => PASS
[6] loja sem tela: estado=Playing timeScale=1 => PASS
[7] Victory: painel de vitória visível=True => PASS
RESULTADO: PASS
```

Em jogo: mate a onda 1 inteira, encoste no ícone → a loja abre, clique num upgrade
(ou aperte ESPAÇO) → a onda 2 começa normalmente.
