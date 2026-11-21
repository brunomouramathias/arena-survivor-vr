# Progresso — Skins texturizadas + Mago azul (17/11/2026)

## O que mudou
1. **Skins de verdade (texturas, não cor sólida)** — criadas pintando diretamente
   sobre as UVs reais da malha (bake por osso: cabeça, trono, braços, mãos,
   pernas, pés), com manchas, veias e grão de pele:
   - `Assets/_Project/Textures/Skins/monster_red.png` → Chaser (Bruto vermelho)
   - `Assets/_Project/Textures/Skins/monster_orange.png` → Shooter (Cuspidor laranja)
   - `Assets/_Project/Textures/Skins/monster_purple.png` → Stalker (Espectro roxo)
   - `Assets/_Project/Textures/Skins/mage_robe.png` → Boss (Mago: manto azul-noite,
     colar/cinto dourados, barra do manto com brilho azul arcano)
   - Técnica: segmentação por osso via `bindposes` (imune à pose do FBX), rasterização
     triângulo a triâmtulo em textura 1024² e dilatação para costuras.
   - Re-executável pelo menu `Tools/Arena/Bake Character Skins`.

2. **Mago solta poder AZUL de curta distância**
   - `Mat_ProjMagicBoss` agora é azul emissivo (leque de projéteis azul).
   - Nova de curta distância (raio 4) com aviso azul: `Mat_Warning_Blue` +
     `SpawnWarning_Blue.prefab` ligados ao Boss (a aviso vermelho continua nos
     inimigos comuns).

3. **Movimento mais natural**
   - NavMeshAgent de todos: `acceleration=14`, `angularSpeed=480°/s` (curvas suaves).
   - Animador: Walk 1.1x / Run 1.15x (passada mais viva, menos deslize).
   - Animações Walk/Run/Attack do Mixamo continuam (blend por velocidade).

## Provas
- `docs/screens/diag/20_Enemy_*.png` — closes texturizados de cada personagem.
- `docs/screens/cenario/23_torres_em_jogo.png` — jogo real com monstros texturizados.
- `docs/scenario_play.txt` — playmode: onda 1 com 8 inimigos, dano nas torres,
  game over validado, zero erros de console.

---

## Bruxos inimigos com poderes (Fire Wizard / Air Wizard)

O chefe e o atirador de elite agora são **bruxos do pack Wizard_Free**, com poderes e
movimento natural. Detalhes completos em [`docs/wizards.md`](wizards.md).

### O que mudou
1. **Modelos trocados**
   - `Enemy_Boss` → `Fire_Wizard.fbx` (mago vermelho, 2,05 m)
   - `Enemy_Sorcerer` → `Air_Wizard.fbx` (mago azul, 1,85 m)
   - Materiais `Mat_Wizard_Fire` / `Mat_Wizard_Air`, olhos emissivos preservados.

2. **Animação procedural (`WizardAnim`)**
   - Mixamo retarget deformava a pose (T-pose parcial, mago inclinado), então os
     clipes foram substituídos por animação gerada por código sobre os 20 ossos do
     próprio modelo: idle com respiração, passada com braços em oposição e "bob"
     do tronco, e conjuração com braços à frente.
   - Nenhum `Animator` nos prefabs dos bruxos; `BossController` e `EnemyShooter`
     chamam `WizardAnim.PlayCast()` / `PlayAttack()`.

3. **Poderes visíveis**
   - Orbe de energia pulsante na mão durante conjuração, com luz pontual:
     laranja no chefe (fogo) e ciano no bruxo azul (ar).
   - Chefe: AOE de fogo (círculo vermelho no chão), leque de projéteis e invocação
     de lacaios. Bruxo azul: projétil mágico a distância.

4. **Aparição garantida nas ondas**
   - Bruxo azul nasce **desde a onda 1** (mínimo 2 por onda, máximo 1/3).
   - Chefe na **onda 1** e depois a cada **3 ondas** (3, 6, 9, 12, 15, 18).
   - Materiais com tint branco para mostrar as texturas originais do pack.

### Provas
- `docs/screens/wizards/playtest.txt` — playmode na Arena: chefe andando a 2,5 m/s,
  ambos conjurando (`cast≈0,97`, `glow=ON`), zero erros.
- `docs/screens/wizards/10_boss_andando.png` — chefe andando na Arena.
- `docs/screens/wizards/11_chefe_conjurando.png` — chefe com o AOE vermelho ativo.
- `docs/screens/wizards/12_bruxo_azul_conjurando.png` — mago azul com orbe ciano.
- `docs/screens/wizards/Fire_Wizard_0*.png` / `Air_Wizard_0*.png` — poses
  idle/andar/conjurar/poder com medidas (`proof.txt`).

## Melhorias de combate, ondas e pontuação (20/11)

Implementadas e verificadas em jogo (relatório: `docs/screens/melhorias/relatorio.txt`, doc: `docs/melhorias.md`).

- **Barra de vida em todos os inimigos**: `EnemyHealthBar` reescrita como barra procedural
  (quads URP Unlit) ancorada no osso da cabeça, billboard para a câmera, verde→vermelho,
  escala em metros. Criada automaticamente pelo `EnemyBase`; barras antigas de Canvas/Slider
  dos prefabs são destruídas em runtime.
- **Onda 1 em até 3 tiros**: `EnemyBase.FirstWaveMaxHealth = 54` (3 × 18 da pistola);
  o chefe saiu da onda 1 (volta nas ondas 3, 6, 9...).
- **Escalonamento suave**: `GetEnemyCount = 5 + (onda-1) × 1,7` (5, 7, 8, 10, 12, 14 … 30).
- **Fim de jogo só com a última torre**: jogador nocauteado renasce (1,6 s, −50 pontos,
  3 corações, 2,5 s invulnerável); `GameOver` apenas quando `TowerHealth.All` fica sem torres vivas.
- **Pontuação por sobrevivência**: +5/s em jogo e bônus final (tempo×10, torre viva×250,
  1% de vida das torres×5), com detalhamento na tela de Game Over e HUD com
  `PONTOS`, `TEMPO mm:ss` e `TORRES n/4 (%)`.
- Teste automatizado: **Tools ▸ Arena ▸ Testar Melhorias (checklist)** → 16 OK / 0 falhas.

## Ritmo das ondas, poderes e nocaute (20/11 — parte 2)

Doc: `docs/poderes.md`. Verificações: `docs/screens/poderes/`.

- **Bug corrigido — jogador ficava deitado**: `PlayerAnimation.Revive()` (`ResetTrigger(Die)` +
  `Rebind()` + `Update(0)`) chamado no `PlayerHealth.RespawnRoutine`. Em jogo o clipe volta para
  `Idle_Gun` e o jogador levanta.
- **Tiros para matar por onda**: `Waves/WaveTuning.cs` → 2 tiros na onda 1, 3 na 2, 4 na 3,
  até 8 (vida = tiros × 18). Chefe fora da regra (mantém vida própria).
- **Inimigos mais rápidos por onda**: fator 0,60 → 1,60 aplicado no `NavMeshAgent`
  (1,92 m/s na onda 1 → 5,12 m/s na onda 20).
- **Poder ao limpar a onda**: `Items/PowerUpPickup.cs` brota no local do último inimigo morto,
  gira/brilha, é atraído e aplica a evolução: velocidade → arma mais forte → cadência → dano → vida.
  O `WaveManager` espera a coleta (14 s máx.) antes de abrir a loja.
- **PlayerStats** ganhou `UpgradeSpeed`, `UpgradeFireRate`, `UpgradeDamagePercent`; `Weapon` usa
  `FireRateMultiplier`; `PlayerController` usa `EffectiveMoveSpeed` (= base × SpeedMultiplier).
- Verificação: **Tools ▸ Arena ▸ Verificar Ritmo e Poderes (edit mode)** → 16 OK / 0 falhas.

## Tiro nos inimigos (20/11 — parte 3)

Doc: `docs/tiro.md`.

- **Bug**: tiros não acertavam os bruxos. Causa: `Projectile_Player` com colisor de raio **0,08**,
  Rigidbody kinematic + detecção Discrete e movimento por `transform.Translate` a 25 m/s
  → a bala atravessava o alvo entre frames (tunneling) e o `OnTriggerEnter` nunca disparava.
- **Correção**: `Projectile` agora faz **varredura por frame** (`OverlapCapsuleNonAlloc`, raio 0,28 m)
  entre a posição anterior e a nova; pega `EnemyBase` no parent, ignora o jogador, bloqueia em
  parede após 0,7 m de folga do cano. `Advance(dt)` público para testes.
- **Mira assistida** (`Weapons/AimAssist.cs`): bolha de 1,1 m e desvio máximo de 14° puxando para
  o peito do inimigo; cancelada se houver parede na frente. Aplicada no `Weapon.Fire`.
- Verificação edit mode: **19 OK / 0 falhas** — bruxo de 36 HP morre em 2 tiros; mira com 6° de
  erro cai para 1,7°; tiro apontado longe não sofre desvio.

## Loja entre ondas: o "travamento" ao pegar o ícone (20/11 — parte 4)

Doc: `docs/loja.md`. Prova: `docs/screens/loja/fluxo.txt` + `docs/screens/loja/01_loja_onda.png`.

- **Sintoma**: encostar no ícone de poder que brota quando o último inimigo da onda morre
  "travava" o jogo — tela do jogo parada, sem nada para clicar, para sempre.
- **Causa raiz** (medida em play mode): pegar o ícone termina a onda e o `WaveManager` chama
  `GameState.Shop`, que zera o `Time.timeScale`. Só que a loja era o `Shop_Panel` do `Canvas_HUD`
  legado — e esse canvas está **desativado na cena Arena** (`m_IsActive: 0`), com o painel
  **vazio** (sem filhos). Resultado: `timeScale = 0` + nenhuma UI = jogo congelado sem saída.
  A coleta em si custa ~6 ms de spawn e ~23 ms no pior frame — não era engasgo de performance.
- **Correção**: nova `UI/ShopScreen.cs`, construída por código (não depende de prefab nem do canvas
  legado): fundo escuro, pontos, DANO/RECARGA/VIDA MÁXIMA com nível e custo, ESCOPETA e ARMA DE
  ENERGIA, botão **PRÓXIMA ONDA** e atalhos ESPAÇO/ENTER/N/ESC. Aparece em `GameState.Shop`,
  esconde o HUD de combate e devolve o cursor ao mouse.
- **Travas de segurança** (`GameManager`): loja/pausa com `timeScale 0` e **sem tela visível**
  volta ao combate em 1,5 s; vitória agora mostra painel "VITÓRIA!" (antes também ficava em
  `timeScale 0` sem UI).
- **Verificação**: `Tools ▸ Arena ▸ Teste Loja (ícone -> loja -> onda 2)` → **PASS**
  (7/7): onda limpa → ícone brota → jogador encosta → loja visível com 6 botões → clique real
  por raycast compra DANO (Lv 0→1, 188→108 pontos) → PRÓXIMA ONDA volta ao combate → onda 2
  nasce com inimigos → loja sem tela não congela → vitória tem saída. 0 erros no console.

## Armas: "não consigo mais atirar" depois de comprar upgrade (20/11 — parte 5)

Prova: `docs/screens/armas/tiro_armas.txt` (play mode, 5/5 PASS).

- **Sintoma**: na 3ª onda, depois de gastar pontos na loja, o jogador não atirava mais.
- **Causa raiz**: a escopeta e a arma de energia começam com o GameObject **desativado**
  (`m_IsActive: 0` na cena) — logo o `Awake()` delas não roda no início. A loja
  (`TryUnlockShotgun → PlayerShooter.UnlockWeapon → Weapon.Unlock()`) marcava
  `IsUnlocked = true` na arma desativada; quando o jogador equipava, o `SetActive(true)`
  disparava o `Awake()`, que fazia `IsUnlocked = data.startUnlocked` (**false**) e
  **apagava o desbloqueio**. `TryFire` saía na primeira linha (`if (!IsUnlocked) return;`)
  e o jogador ficava sem arma — o mesmo acontecia com o poder "ARMA MAIS FORTE", que
  também re-desbloqueava a escopeta em vez de passar para a próxima arma.
- **Correção** (`Weapons/Weapon.cs`): `EnsureInitialized()` idempotente, chamado pelo
  `Awake()` e por `Unlock()`/`TryFire()`/`TryReload()`/`AddAmmo()`. O `Awake` não
  sobrescreve mais um desbloqueio feito antes da ativação (`unlockRequested`), e a
  munição/pool passam a ser inicializados mesmo na arma desativada.
- **Rede de segurança** (`PlayerShooter`): se a arma equipada ficar **seca** (pente e
  reserva zerados) o jogo volta sozinho para a pistola (reserva infinita) e mostra
  "SEM MUNIÇÃO — PISTOLA", então nunca dá para ficar sem atirar.
- **API**: `PlayerShooter.EquipSlot(int)` agora é público e devolve `bool`;
  `Weapon.IsDry` expõe o estado de munição esgotada.
- **Verificação**: `Tools ▸ Arena ▸ Teste Tiro das Armas` → pistola, escopeta (caminho da
  loja) e arma de energia (caminho do poder) atiram; troca de volta para a pistola atira;
  escopeta seca volta para a pistola. **5/5 PASS, 0 erros**.

## Tela de abertura: modos de jogo e "como jogar" (21/11 — parte 6)

Prova: `docs/screens/menu/menu_probe.txt` (play mode, 5/5 PASS) + `01_menu.png` … `04_como_jogar.png`
Documentação: `docs/menu.md`

- **Antes**: a cena `MainMenu` tinha o script `MainMenuController`, mas o canvas `Canvas_MainMenu`
  estava **sem filhos** — abrir o jogo dava uma tela praticamente vazia.
- **Agora**: `UI/MainMenuScreen.cs` constrói a tela inteira por código sobre a arte do próprio
  jogo (`Resources/Menu_Backdrop.png`, capturada da arena com a `Tools ▸ Arena ▸ Capturar Arte do Menu`):
  topo com marca/instituição, herói com título e recorde pessoal, painel de modo de jogo,
  rodapé com os controles e a janela **COMO JOGAR** (9 teclas + 6 dicas de estratégia + o ciclo
  de uma onda). Paleta, contorno dourado e tipografia iguais às do HUD/loja.
- **Modos** (`Core/Difficulty.cs`): FÁCIL (vida ×0,80, velocidade ×0,85, quantidade ×0,85,
  150 pontos iniciais, pontuação ×0,85), MÉDIO (balanceamento oficial, tudo ×1) e DIFÍCIL
  (×1,30 / ×1,15 / ×1,20, sem pontos iniciais, pontuação ×1,30). Aplicado em `EnemyBase`
  (vida/velocidade), `WaveManager` (quantidade) e `ScoreService` (pontos iniciais e
  multiplicador da pontuação). A escolha fica salva e aparece no HUD.
- **Verificação**: `Tools ▸ Arena ▸ Teste Tela de Abertura` → estrutura (8 botões, os três modos,
  INICIAR/COMO JOGAR/SAIR e a arte), troca de modo conferindo a ficha de cada perfil, "Como jogar"
  abrindo e fechando por clique real (raycast), INICIAR na partida em DIFÍCIL (vida 47 = 36 × 1,30,
  +100 pontos → +130, HUD "DIFÍCIL") e, recarregando o menu, FÁCIL com 150 pontos iniciais e
  vida 29 (36 × 0,80). **PASS, 0 erros**.
- **Efeito colateral tratado**: a preferência de dificuldade sobrevive entre execuções, então as
  verificações de balanceamento (`ArenaTuningVerify`, `ShopFlowTest`, `WeaponShootTest`) agora
  forçam **MÉDIO** no início — antes elas mediam o modo que tinha ficado salvo pelo jogador.
  Regressão: 19 OK / 0 FALHAS, loja PASS, tiro PASS.

## Texto nítido na tela de abertura + publicação (21/11 — parte 7)

- **Sintoma**: as letras do menu apareciam embaçadas e difíceis de ler.
- **Causa**: o canvas é `ScreenSpaceCamera`, então o texto passava pelo pós-processamento da
  câmera (bloom + tonemap + vinheta) e pelo antialiasing de tela cheia; e o `FontStyles.Bold`
  do TMP em fonte sem face negrito dobra o glifo (halo de "negrito falso").
- **Correção** (`UI/MainMenuScreen.cs`): `renderPostProcessing = false` e
  `antialiasing = None` na câmera do menu; negrito por dilatação do SDF (`_FaceDilate` em
  material compartilhado, sem duplicar glifo); textos secundários mais claros e painel mais
  opaco para contraste. Nenhuma mudança de layout ou de paleta.
- **Verificação**: `Teste Tela de Abertura` **PASS** (5/5, 0 erros) e comparação de capturas
  em 1920×1080 antes/depois; regressão completa: ritmo 19 OK / 0 falhas, dificuldades 16 OK,
  loja PASS, tiro PASS. `Verificar Dificuldades` agora encerra sozinho no modo batch.
- **Publicado**: `https://github.com/brunomouramathias/arena-survivor-vr` (público) atualizado
  com a tela nova, `docs/menu.md`, três imagens novas no README e os relatórios de teste.
  A versão reduzida foi validada abrindo uma cópia limpa no Unity: 19 OK na verificação de
  ritmo, tela de abertura PASS e loja PASS dentro do próprio repositório.

## Sons de dano e morte do jogador (21/11 — parte 8)

Prova: `docs/screens/audio/sons.txt` (play mode, 5/5 PASS). Documentação: `docs/audio.md`.

- **Novos SFX**: `Sfx_PlayerDamage.wav` (0,30 s, impacto grave com estalo) e
  `Sfx_PlayerDeath.wav` (1,30 s, queda descendente com sub-grave), sintetizados, mono 44,1 kHz,
  normalizados em −6 dBFS — nada de pico estourado.
- **Volume baixo em código** (`AudioMixer` não é usado na cena): `playerDamageVolume = 0,28`
  e `playerDeathVolume = 0,32`, o que deixa o pico efetivo em torno de −17 dBFS, bem abaixo da
  música e do resto do combate. `damageSoundCooldown = 0,12 s` evita repetição em dano em área.
- **Disparo no lugar certo**: `PlayerHealth.TakeDamage` → `PlayPlayerDamageSound()` e
  `PlayerHealth.Die` → `PlayPlayerDeathSound()`. O `PlayerDamagedEvent` **não** serve para isso:
  ele também dispara ao curar e ao renascer (o som de dano tocaria ao beber vida).
- **Instalação/importação**: `Tools ▸ Arena ▸ Instalar Sons de Dano e Morte` (PCM,
  *decompress on load*, mono, preload) e gravação dos campos no `AudioManager` da cena `Arena`.
- **Verificação**: `Tools ▸ Arena ▸ Teste Sons de Dano e Morte` — clipes ligados, pico dos
  arquivos ≤ −3 dBFS, golpe tocando a 0,28, cura silenciosa e nocaute tocando a 0,32. **5/5 PASS**.
  Regressão: ritmo 19 OK / 0 falhas, loja PASS, tiro PASS, tela de abertura PASS.

## Tiro reto, aviso de torre e som de dano audível (21/11 — parte 9)

- **Tiro ia para outra direção**: `Weapon.Fire` chamava `AimAssist.Apply`, que puxava a bala
  até 14° para o inimigo mais próximo — o disparo saía fora da mira e parecia "curvar".
  Removido: agora `AimDirection()` usa exatamente a mira (projetada na altura do cano),
  `DirectionForPellet()` espalha os bagos da escopeta de forma **simétrica** (centro = mira) e
  a arma de energia ficou com `spread = 0`. O projétil continua andando só no `forward`, então
  a trajetória é reta do começo ao fim. O texto da dica no "Como jogar" foi atualizado junto.
- **Aviso de torre caindo**: `GameHUD` assina `TowerHealth.DestroyedEvent` e mostra uma linha
  pequena embaixo do número da onda — `TORRE NORDESTE DESTRUÍDA · 3/4 DE PÉ` (ou
  `ÚLTIMA TORRE DESTRUÍDA`) — que desvanece em 3,2 s. O nome sai do lado da arena
  (os quatro cantos).
- **Som de dano não aparecia**: a primeira versão tinha quase só graves (60–130 Hz), sumindo em
  caixinha de notebook. Os dois clipes foram refeitos com conteúdo em médio-grave
  (300 Hz – 2 kHz) e o volume subiu de 0,28/0,32 para **0,45/0,50** (pico efetivo ≈ −12 dBFS,
  ainda discreto). Nocaute usa 0,50.
- **Verificação**: ritmo **19 OK / 0 falhas** (novos testes: tiro reto com desvio de 0,03° e
  escopeta com centro de dispersão 0,00°), sons **5/5 PASS** (`docs/screens/audio/sons.txt`),
  aviso de torre **4/4 PASS** (`docs/screens/hud/torre.txt`), loja PASS, tiro PASS, menu PASS.

## Tempo de sobrevivência em minutos no fim de jogo (21/11 — parte 10)

- **Antes**: a tela de fim mostrava `Sobreviveu 227s` — número cru, difícil de ler depois de
  partidas longas.
- **Agora**: `GameHUD.FormatDuration()` formata o tempo do `ScoreService.PlaySeconds`:
  abaixo de 60 s continua em segundos (`48 s`); a partir de 1 minuto vira `3 min 47 s`
  (e `1 min` quando é redondo). A linha ficou: `Sobreviveu 1 min 12 s (+720)  Torres de pé (+0)  Vida das torres (+0)`.
- **Verificação**: `Verificar Ritmo e Poderes` ganhou a checagem de formato (0 s / 48 s / 59 s /
  1 min / 3 min 47 s / 10 min 05 s) → **20 OK / 0 falhas**. Captura real do fim de jogo em
  `docs/screens/hud/gameover_tempo.png` (`Tools ▸ Arena ▸ Capturar Tela de Fim de Jogo`).

## Tiro curvando: a causa era o pool dentro do jogador (21/11 — parte 11)

- **Sintoma**: mesmo sem mira assistida, o tiro "às vezes" saía curvado.
- **Causa 1 (a principal)**: `ObjectPool<Projectile>(prefab, 30, transform.root)` — as balas
  nasciam como **filhas do jogador**, e o `PlayerController` gira o corpo para encarar a mira
  a cada frame. A bala herdava essa rotação e o voo virava arco; como o desvio depende de o
  jogador estar girando naquele instante, parecia intermitente.
- **Causa 2**: `Advance` usava `transform.forward` (que muda com o pai) em vez de uma direção
  congelada no disparo.
- **Causa 3**: o tremor de câmera (chamado a cada disparo) deslocava a câmera usada por
  `Camera.ScreenPointToRay`, então o **ponto de mira no chão andava metros** entre um tiro e
  outro — mesmo com a bala reta, ela ia para outro lugar.
- **Correções**: pool em `Projectiles_Pool` (fixo na origem); `Projectile` se solta de
  qualquer pai no `Initialize` e passa a andar em `flyingDirection` congelada; rastro limpo a
  cada reuso; `CameraController` separa pose limpa (`StablePosition`/`StableRotation`) e
  `ScreenRay()` dá o raio da mira sem tremor (shake de 0,88 m → mira desviando 0,000 m).
  Balas de inimigo (instanciadas) agora são destruídas ao fim do voo em vez de ficar
  inativas na cena.
- **Verificação**: `Tiro continua reto mesmo com o pai da bala girando` (pai girando 9°/frame
  por 90 passos → desvio 0,00°), `[6]` no teste de armas (girar o jogador 90° depois do
  disparo → desvio 0,00°) e `[7]` (tremor não move a mira). Ritmo **21 OK / 0 falhas**;
  armas PASS (7/7); loja PASS.
