# Bruxos inimigos (Fire Wizard & Air Wizard)

Dois inimigos mágicos do pack **Wizard_Free** substituem os antigos modelos de zumbi nas
classes mais fortes do jogo.

| Inimigo | Papel no jogo | Modelo | Altura | Poder |
|---|---|---|---|---|
| `Enemy_Boss` | Chefe (ondas de chefe) | `Fire_Wizard.fbx` (mago vermelho) | 2,05 m | AOE de fogo + leque de projéteis + invoca lacaios |
| `Enemy_Sorcerer` | Atirador de elite | `Air_Wizard.fbx` (mago azul) | 1,85 m | Projétil mágico a distância + orbe de energia |

## Como aparecem nas ondas

`WaveSpawner` está ligado em `Assets/_Project/Scenes/Arena.unity`:

- `bossPrefab` → `Enemy_Boss.prefab`
- `shooterPrefab` → `Enemy_Sorcerer.prefab`

Regras de aparição (`WaveConfig_Default`):

- **Bruxo azul**: nasce **desde a onda 1**, com mínimo de **2 por onda** (limitado a 1/3 da onda
  para não dominar o combate).
- **Bruxo vermelho (chefe)**: **onda 1** e depois a cada **3 ondas** — 1, 3, 6, 9, 12, 15, 18
  (vitória na 20).

Resultado na onda 1 (medido em jogo — `onda1.txt`):

```
t=5,0  inimigos=1 (chefe/bruxo vermelho=1, bruxo azul=0, outros=0)
t=7,0  inimigos=2 (chefe/bruxo vermelho=1, bruxo azul=1, outros=0)
t=9,0  inimigos=4 (chefe/bruxo vermelho=1, bruxo azul=2, outros=1)
t=11,0 inimigos=5 (chefe/bruxo vermelho=1, bruxo azul=2, outros=2)
```

Imagens da onda 1: `30_onda1_chefe.png`, `31_onda1_bruxo_azul.png`, `32_onda1_grupo.png`.

## Animação — procedural, sem Animator

Os FBX do pack vêm em **T-pose** e sem rig humanoide compatível com o Mixamo. O retarget
automático deformava a pose (o mago "tombava") e deixava partes do corpo em T-pose.
A solução final é o componente **`WizardAnim`** (`Assets/_Project/Scripts/Enemies/WizardAnim.cs`),
que gera a animação por código sobre os próprios ossos do modelo:

- **Idle**: respiração sutil na coluna + cabeça olhando ao redor.
- **Andar**: passada com pernas, balanço oposto dos braços e "bob" do tronco,
  escala de velocidade 1,7 → 7,0 conforme a velocidade do NavMeshAgent.
- **Conjuração/Ataque**: braços erguidos à frente, antebraços flexionados,
  tronco inclinado e **orbe de energia** pulsante na mão (com luz pontual).
  Cor do orbe: laranja (fogo) no chefe e ciano (ar) no bruxo azul.

Nenhum prefab de bruxo possui componente `Animator` — a animação é 100% procedural e
determinística. `BossController` e `EnemyShooter` chamam `WizardAnim.PlayCast()` /
`PlayAttack()` ao executar seus poderes.

### Ossos usados (20 de 20 encontrados nos dois modelos)

`spine`, `spine.001`…`spine.005`, `shoulder.L/R`, `upper_arm.L/R`, `forearm.L/R`,
`hand.L/R`, `thigh.L/R`, `shin.L/R`, `foot.L/R`.

## Provas

### Em jogo (playmode, Arena real)

Log: [`playtest.txt`](playtest.txt)

```
[ANIM] Enemy_Boss(Clone) WizardAnim=True animator=False renderers=6 escala=0,901
[ANIM] Enemy_Sorcerer(Clone) WizardAnim=True animator=False renderers=8 escala=0,698
t=0,5 chefe:    pos=(-2,1,4,2) vel=2,5m/s speed=2,50 cast=0,00 glow=off
t=0,5 atirador: pos=(2,5,4,5) vel=0,0m/s speed=0,00 cast=0,99 glow=ON
t=3,5 chefe:    pos=(-0,8,1,8) vel=0,0m/s speed=0,00 cast=0,97 glow=ON
t=3,5 atirador: pos=(2,5,4,5) vel=0,0m/s speed=0,00 cast=0,99 glow=ON
```

- O chefe **anda de verdade** (2,5 m/s) com a animação acompanhando a velocidade.
- Ao chegar perto do jogador ele para e **conjura** (`cast=0,97`, orbe ativo).
- O bruxo azul mantém distância e **conjura** repetidamente.

Imagens:

| Arquivo | O que mostra |
|---|---|
| `10_boss_andando.png` | Chefe andando na Arena, escala e pose corretas |
| `11_chefe_conjurando.png` | Chefe conjurando com o círculo de AOE vermelho |
| `12_bruxo_azul_conjurando.png` | Bruxo azul com orbe ciano iluminando o cenário |
| `30_onda1_chefe.png` | Onda 1 do jogo: chefe vermelho em campo |
| `31_onda1_bruxo_azul.png` | Onda 1 com os dois magos lado a lado |
| `32_onda1_grupo.png` | Onda 1 com magos + monstros |

### Em edit mode (poses determinísticas)

Log: [`proof.txt`](proof.txt) — gerado por `Tools/Arena/Wizards: Prova Visual`.

| Arquivo | Pose |
|---|---|
| `Fire_Wizard_00_Idle.png` | parado, braços ao lado do corpo |
| `Fire_Wizard_01_Walk.png` | passada (perna/braço opostos) |
| `Fire_Wizard_02_Walk2.png` | meio-ciclo da passada |
| `Fire_Wizard_03_Cast.png` | conjurando (braços à frente) |
| `Fire_Wizard_04_Poder.png` | conjurando com o orbe visível |
| `Air_Wizard_*.png` | mesmas cinco poses no bruxo azul |

Escala e medidas conferidas: chefe 2,05 m, bruxo azul 1,85 m, mão em conjuração a
0,22 m à frente do corpo e 1,09 m de altura.

## Como regenerar tudo

```
Unity -batchmode -projectPath <projeto> \
  -executeMethod ArenaSurvivor.Editor.WizardRigSetup.Run    # rig, escala, orbe
  -executeMethod ArenaSurvivor.Editor.WizardProof.Run       # imagens e medidas
  -executeMethod ArenaSurvivor.Editor.WizardPlayTest.Run    # teste em jogo
```

Os scripts ficam em `Assets/_Project/Scripts/Editor/` (`WizardRigSetup`, `WizardProof`,
`WizardPlayTest`, `WizardPackInstaller`, `WizardAnimate`).
