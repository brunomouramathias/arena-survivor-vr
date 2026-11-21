# Tela de abertura e modos de jogo

Prova visual: `docs/screens/menu/01_menu.png` … `04_como_jogar.png`
Prova automática: `docs/screens/menu/menu_probe.txt` e `docs/screens/menu/dificuldades.txt`

## O que o jogador vê

A cena `MainMenu.unity` tem uma única câmera e o canvas `UI_MainMenu` (`MainMenuScreen`).
Toda a tela é construída **por código** — nada de prefab gigante, nada de layout arrastado
à mão que quebra ao abrir em outra máquina:

| Bloco | Conteúdo |
|---|---|
| Fundo | arte do próprio jogo (`Resources/Menu_Backdrop.png`) com zoom lento (Ken Burns) e três degradês de leitura |
| Topo | marca `ARENA SURVIVOR` + `UTFPR · REALIDADE VIRTUAL · v1.0` + fio dourado |
| Herói | etiqueta "DEFENDA AS TORRES · SOBREVIVA ÀS ONDAS", título grande, parágrafo e **recorde pessoal** |
| Painel | seletor de **modo de jogo** (3 dificuldades), ficha do modo com 5 números, `INICIAR PARTIDA », COMO JOGAR` e `SAIR` |
| Rodapé | os controles de combate resumidos numa linha |

O painel da direita é o único elemento "de interface": fundo escuro translúcido, contorno
dourado fino e texto claro — a mesma paleta da loja e do HUD, para o jogo parecer um produto
só.

## Modos de jogo

| | Fácil | Médio | Difícil |
|---|---|---|---|
| Vida dos inimigos | ×0,80 | padrão | ×1,30 |
| Velocidade | ×0,85 | padrão | ×1,15 |
| Inimigos por onda | ×0,85 | padrão | ×1,20 |
| Pontos iniciais | 150 | 0 | 0 |
| Pontuação | ×0,85 | padrão | ×1,30 |

- **Médio é o balanceamento oficial** (o mesmo dos testes de ritmo: 2 tiros por inimigo na
  primeira onda, até 8 nas últimas).
- A escolha é lembrada entre sessões (`PlayerPrefs` chave `arena.difficulty`) e aplicada no
  `EnemyBase` (vida/velocidade), no `WaveManager` (quantidade) e no `ScoreService`
  (pontos iniciais e multiplicador **só da pontuação** — a moeda da loja continua 1:1).
- O HUD de combate mostra o modo atual na linha de status (`TEMPO 00:05 · TORRES 4/4 · DIFÍCIL`).

Implementação: `Core/Difficulty.cs` (`GameDifficulty`, `DifficultyProfile`, `Difficulty`).

## Teclas da tela de abertura

| Tecla | Ação |
|---|---|
| ← / A e → / D | troca o modo de jogo |
| 1 / 2 / 3 | escolhe Fácil / Médio / Difícil direto |
| ENTER | inicia a partida |
| ESC | fecha a janela "Como jogar" |

## "Como jogar" (o que o jogador precisa apertar)

O botão **COMO JOGAR** abre um painel com tudo o que é preciso saber, em três colunas:

1. **CONTROLES** — `W A S D` mover · `MOUSE` mirar · `CLIQUE ESQ.` atirar · `R` recarregar ·
   `SHIFT / ESPAÇO` esquiva · `1 2 3` trocar de arma · `CLIQUE` comprar na loja ·
   `ESPAÇO / ENTER` fechar a loja e começar a próxima onda · `ESC` sair da loja/fechar janelas.
2. **ESTRATÉGIA** — seis dicas curtas: ficar entre a horda e as torres, pegar o ícone de poder
   antes de entrar na loja, priorizar DANO, usar a mira assistida andando, guardar a esquiva
   para sair do cerco e o recarregamento atrás das torres (sem munição a arma volta sozinha
   para a pistola).
3. **O CICLO DE UMA ONDA** — `LIMPAR A ONDA » PEGAR O PODER » GASTAR NA LOJA » PRÓXIMA ONDA`.

## Como refazer / testar

| Ferramenta | O que faz |
|---|---|
| `Tools ▸ Arena ▸ Rebuild Menu Principal` | reimporta a arte e recria o canvas `UI_MainMenu` na cena `MainMenu` |
| `Tools ▸ Arena ▸ Verificar Dificuldades` | instancia os prefabs reais e confere vida/velocidade/quantidade de cada modo (16 OK) |
| `Tools ▸ Arena ▸ Teste Tela de Abertura` | play mode: estrutura da tela, troca de modos, "Como jogar", clique em INICIAR e aplicação do modo na partida |
| `Tools ▸ Arena ▸ Verificar Fonte do Menu` | confere quais símbolos (`·`, `×`, `»`, `—` …) a fonte `LiberationSans SDF` tem |

Último resultado: **PASS** (etapas 1 a 5, 0 erros) —
menu com 8 botões, os três modos exibindo a ficha correta, "Como jogar" com 7 teclas e 6 dicas,
clique real por raycast em INICIAR levando à Arena em **DIFÍCIL** (vida 47 = 36 × 1,30,
pontuação ×1,30, HUD marcando DIFÍCIL) e, na segunda partida, **FÁCIL** começando com 150 pontos
e vida 29 (36 × 0,80).
