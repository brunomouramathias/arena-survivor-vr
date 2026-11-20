# RELATÓRIO TÉCNICO — ARENA SURVIVOR

**Disciplina:** Realidade Virtual (Bacharelado em Ciência da Computação, UTFPR)  
**Equipe:** Bruno Moura e João Pedro Domingues  
**Tema:** Jogo de Sobrevivência em Arena (Realidade Virtual Não-Imersiva)  
**Versão da Engine:** Unity 6 LTS (`6000.5.9f1`) | Universal Render Pipeline (URP)  

---

## 1. Introdução e Contextualização
O projeto consiste em uma aplicação 3D interativa de Realidade Virtual Não-Imersiva (Desktop VR), onde a sensação de presença espacial e tridimensionalidade é transmitida através de renderização em tempo real, perspectiva aérea isométrica/top-down, iluminação dinâmica, pistas visuais de profundidade (neblina atmosférica, sombras suaves e partículas) e resposta contínua aos inputs do usuário.

A dinâmica central baseia-se em um modelo *arena survival* com progressão por ondas de dificuldade escalar, interrupções táticas para gerenciamento de recursos em uma loja de melhorias e batalhas contra chefes com múltiplos padrões de ataque.

---

## 2. Decisões Arquiteturais e Padrões de Projeto

### 2.1 Separação de Responsabilidades e Desacoplamento
Para evitar referências circulares e acoplamento rígido entre a interface gráfica (UI) e a lógica de combate:
- **`EventBus` Estático e Tipado:** Toda notificação de morte de inimigos (`EnemyDiedEvent`), dano ao jogador (`PlayerDamagedEvent`), início de onda (`WaveStartedEvent`) e conclusão de onda (`WaveClearedEvent`) é propagada via structs imutáveis no barramento central.
- **Singletons Especializados:** Serviços de infraestrutura (`GameManager`, `InputService`, `ScoreService`, `PoolService`) possuem instâncias únicas com controle de ciclo de vida (`DontDestroyOnLoad`).

### 2.2 Gerenciamento de Memória e Otimização em Tempo de Execução
Em jogos com alta cadência de disparo e dezenas de inimigos simultâneos, a invocação indiscriminada de `Instantiate` e `Destroy` causa picos de coleta de lixo (GC spikes), resultando em travamentos de taxa de quadros (micro-stutters). Para garantir 60 FPS estáveis:
- **`ObjectPool<T>` Genérico:** Implementado com uma fila (`Queue<T>`) e pré-aquecimento. Projéteis do jogador e inimigos são reciclados imediatamente ao expirar ou colidir.
- **`MaterialPropertyBlock`:** Aplicado para os efeitos de *hit flash* em inimigos e no jogador, alterando as propriedades de cor do shader URP/Lit diretamente na GPU sem instanciar novos materiais na memória RAM.

### 2.3 Mecânica de Mira e Movimentação Espacial
- **Mira Precisa com `Plane.Raycast`:** A mira do jogador projeta um raio a partir da posição da tela da câmera ortogonal/perspectiva contra um plano matemático infinito (`new Plane(Vector3.up, player.position)`). Esse método é superior e imune a falhas em relação a raycasts de colisor físico de terreno, garantindo rotação suave e mira precisa mesmo sobre degraus ou obstáculos.
- **Esquiva Tática com Invulnerabilidade (I-Frames):** A esquiva é calculada aplicando aceleração linear momentânea com travamento temporário do dano (`isInvincible = true`), recompensando reflexos rápidos do jogador.

### 2.4 Inteligência Artificial e Navegação
- **Mapeamento de Navegação (`NavMesh`):** Os obstáculos e muros possuem flags de navegação estática assinaladas pelo script de automação (`ProjectBuilder`), e a malha de navegação foi assada via `NavMeshBuilder`.
- **Inimigos Perseguidores (`EnemyChaser`):** Utilizam `NavMeshAgent` para encontrar o caminho mais curto até o jogador, recalculando o trajeto dinamicamente.
- **Inimigos Atiradores (`EnemyShooter`):** Mantêm uma distância de segurança preferencial entre 6 e 9 unidades do jogador, alternando entre perseguição, recuo e manobras de flanqueamento lateral (*strafe*).
- **Chefe da Arena (`BossController`):** Máquina de estados baseada em patamares de vida (100%-60%, 60%-30%, <30%), combinando ataque em área telegrafado com área de perigo no chão, rajada em leque de projéteis e invocação de servos de suporte.

---

## 3. Direção de Arte e Ambientação
- **Paleta e Atmosfera:** Iluminação quente crepuscular (luz direcional a 40° em tom dourado/alaranjado) combinada com iluminação ambiente trilight em tons de roxo e azul-escuro.
- **Contraste de Silhueta:** Inimigos escuros com pontos focais emissivos (olhos amarelos e roxos brilhantes) para legibilidade imediata em meio ao cenário.
- **Pontos de Luz Coloridos:** Tochas e cristais em tons de ciano, laranja e púrpura ao redor dos monólitos de pedra.

---

## 4. Conclusão e Testabilidade
O projeto foi estruturado com foco em automação total: a qualquer momento, o comando `Tools > Arena > Build Everything` é capaz de reconstruir do zero as camadas, materiais, prefabs, ScriptableObjects e cenas do jogo com validação automatizada de integridade (zero referências nulas e zero shaders com erro).

