# Guia de Configuração do Jogo VR - ArenaSurvivor

## Resumo das Implementações

### 1. Sistema de Waypoints (Movimento VR) ✅
- **WaypointManager.cs**: Gerencia waypoints e teleporte do jogador
- **Waypoint.cs**: Componente para marcar waypoints na cena
- **PlayerController.cs**: Atualizado para suporte a movimento por waypoints
- **EnemyBase.cs**: Atualizado para receber eventos de teleporte e atualizar NavMeshAgent

### 2. HUD com Corações (3 vidas) ✅
- **HUDController.cs**: Atualizado com sistema de 3 corações no canto superior direito
- Sprites necessários: `heartFullSprite`, `heartHalfSprite`, `heartEmptySprite`
- Canvas configurado automaticamente para World Space

### 3. UI para VR (World Space) ✅
- **CanvasWorldSpaceFollow.cs**: Faz o canvas seguir a câmera do jogador
- **HUDController.cs**: Configura canvas automaticamente para World Space
- **GameOverScreen.cs**: Configurado para World Space com follow de câmera
- **UIManager.cs**: Configura todos os painéis para World Space

### 4. Novo Inimigo: Stalker ✅
- **EnemyStalker.cs**: Inimigo com comportamento único:
  - Persegue furtivamente (stalk)
  - Teleporta atrás do jogador
  - Animações: idle, walk, run, attack, teleport, death
  - Sons integrados via EnemyAudio

### 5. Sistema de Áudio Completo ✅
- **AudioManager.cs**: Expandido com:
  - Música em camadas (normal, boss, intenso) com crossfade
  - Pool de SFX 2D (UI) e 3D (mundo)
  - Eventos: PlayerDamaged, PlayerDied, WaveStarted, EnemyDied
- **EnemyAudio.cs**: Componente para sons dos inimigos:
  - Idle, walk, attack, hit, death, teleport
  - Variação de pitch e volume
  - Spatial blend 3D

### 6. Correção de Inimigos Fantasma ✅
- **EnemyVisibilityMonitor.cs**: Monitora e corrige automaticamente:
  - Renderers desativados
  - Colliders desativados
  - NavMeshAgent desativado
  - GameObject inativo
- Integrado no **EnemyBase.cs** para todos os inimigos

### 7. Estabilização de Iluminação ✅
- **LightManager.cs**: Evita flickering indesejado
  - Desabilita ReflectionProbes em tempo real
  - Configura sombras estáveis
  - Opcional: ciclo dia/noite controlado

### 8. Balanceamento para 3 Corações ✅
- **PlayerHealth.cs**: MaxHealth = 3
- **EnemyData.cs**: contactDamage = 1
- **EnemyShooter.cs**: shootDamage = 1
- **BossController.cs**: aoeDamage = 1, fanDamage = 1

---

## Como Configurar na Unity

### Passo 1: Execute o Setup Automático
1. Abra o menu **ArenaSurvivor > Setup VR Scene**
2. Clique em **"Configuração Completa (Tudo)"**
3. Isso criará waypoints, configurará canvases e LightManager

### Passo 2: Configure os Sprites de Coração
1. Selecione o objeto com **HUDController**
2. No Inspector, atribua:
   - **Heart Images**: 3 Image components (filhos do HUD)
   - **Heart Full Sprite**: Sprite de coração cheio
   - **Heart Half Sprite**: Sprite de meio coração
   - **Heart Empty Sprite**: Sprite de coração vazio

### Passo 3: Configure os Áudios
1. Abra **ArenaSurvivor > Setup Audio**
2. Adicione **AudioManager** à cena se não existir
3. No **AudioManager**, atribua:
   - **Music Tracks**: 3 clipes (normal, boss, intenso)
   - **SFX Globais**: playerDamage, playerDeath, weaponFire, etc.
4. Em cada prefab de inimigo (Chaser, Shooter, Stalker, Boss):
   - Adicione componente **EnemyAudio**
   - Atribua clipes: idle, walk, attack, hit, death, teleport

### Passo 4: Configure o EnemyStalker
1. Crie um prefab do Stalker com:
   - Modelo com Animator (idle, walk, run, attack, teleport, death)
   - NavMeshAgent
   - EnemyAudio
   - EnemyVisibilityMonitor (adicionado automaticamente)
2. No **WaveSpawner**, atribua o **Stalker Prefab**

### Passo 5: Ajuste os Waypoints
1. Na cena, mova os waypoints criados para posições estratégicas
2. Adicione mais waypoints se necessário (clique com botão direito > ArenaSurvivor > Create Waypoint)

### Passo 6: Configure a Luz Principal
1. Certifique-se de ter uma **Directional Light** principal
2. O **LightManager** a configurará automaticamente
3. Se quiser ciclo dia/noite, ative `enableTimeOfDay` no LightManager

---

## Estrutura de Pastas dos Scripts

```
Assets/_Project/Scripts/
├── Audio/
│   └── AudioManager.cs
├── Core/
│   ├── EventBus.cs
│   ├── GameInitializer.cs
│   ├── GameManager.cs
│   ├── InputService.cs
│   ├── LightManager.cs
│   ├── PoolService.cs
│   ├── SaveService.cs
│   └── ScoreService.cs
├── Editor/
│   ├── ArenaSetupWindow.cs
│   ├── AudioSetupWindow.cs
│   ├── AssetInventoryTool.cs
│   ├── HUDBuilder.cs
│   ├── PlayerDiagnosticTool.cs
│   ├── PlayerMovementTest.cs
│   ├── PlayerProofTest.cs
│   ├── PlayerSetupTool.cs
│   ├── PlayerWeaponOverhaul.cs
│   ├── ProjectBuilder.cs
│   ├── SmokeTest.cs
│   ├── VRSceneSetup.cs
│   └── VisualAssetsMasterBuilder.cs
├── Enemies/
│   ├── BossController.cs
│   ├── EnemyAudio.cs
│   ├── EnemyBase.cs
│   ├── EnemyChaser.cs
│   ├── EnemyData.cs
│   ├── EnemyHealthBar.cs
│   ├── EnemyShooter.cs
│   ├── EnemyStalker.cs
│   └── EnemyVisibilityMonitor.cs
├── Items/
│   ├── DropManager.cs
│   └── Pickup.cs
├── Player/
│   ├── CameraController.cs
│   ├── PlayerAnimation.cs
│   ├── PlayerController.cs
│   ├── PlayerHealth.cs
│   ├── PlayerShooter.cs
│   ├── PlayerStats.cs
│   ├── Waypoint.cs
│   └── WaypointManager.cs
├── Shop/
│   ├── ShopManager.cs
│   └── UpgradeData.cs
├── UI/
│   ├── CanvasWorldSpaceFollow.cs
│   ├── GameOverScreen.cs
│   ├── HUDController.cs
│   ├── MainMenuController.cs
│   ├── ShopUI.cs
│   └── UIManager.cs
├── Utils/
│   ├── DebugOverlay.cs
│   ├── FloatingText.cs
│   └── ObjectPool.cs
├── Waves/
│   ├── WaveConfig.cs
│   ├── WaveManager.cs
│   └── WaveSpawner.cs
└── Weapons/
    ├── Projectile.cs
    ├── Weapon.cs
    └── WeaponData.cs
```

---

## Testes Recomendados

1. **Movimento por Waypoints**: 
   - Inicie o jogo na cena Arena
   - Use W/S para mover entre waypoints
   - Verifique se inimigos atualizam destino

2. **Sistema de Vida**:
   - Leve dano de inimigos
   - Verifique corações diminuindo
   - Teste cura (pickup de vida)

3. **Game Over**:
   - Perca todos os corações
   - Verifique tela de Game Over em World Space
   - Teste botões Restart/Menu

4. **Inimigos**:
   - Verifique Chaser, Shooter, Stalker, Boss
   - Teste sons de idle, hit, death
   - Verifique se nenhum inimigo fica invisível

5. **Iluminação**:
   - Jogue por alguns minutos
   - Verifique se não há flickering
   - Sombras estáveis

---

## Problemas Conhecidos e Soluções

| Problema | Solução |
|----------|---------|
| Canvas não aparece em VR | Verifique se `CanvasWorldSpaceFollow` está no canvas e targetCamera está atribuído |
| Inimigos não spawnam | Verifique NavMesh baked e prefabs atribuídos no WaveSpawner |
| Waypoints não funcionam | Verifique se WaypointManager existe e waypoints registrados |
| Sons não tocam | Verifique AudioManager na cena e AudioClips atribuídos |
| Luz pisca | Verifique LightManager e desative ReflectionProbes realtime |

---

## Próximos Passos Sugeridos

1. Adicionar mais tipos de inimigos (ex: explodidor, tanque)
2. Implementar sistema de upgrades de armas mais complexo
3. Adicionar efeitos visuais (partículas, shaders)
4. Implementar multiplayer cooperativo
5. Adicionar mais fases/arenas
6. Sistema de conquistas/estatísticas