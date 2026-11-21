# Tiro nos inimigos — por que não morriam e o que mudou

## Causa raiz (encontrada nos prefabs)

`Assets/_Project/Prefabs/Projectile_Player.prefab`:

| Componente | Valor antigo | Problema |
|---|---|---|
| CapsuleCollider | `isTrigger: 1`, raio **0,08** | bala muito fina |
| Rigidbody | `isKinematic: 1`, `collisionDetection: 0` (Discrete) | física não varre o caminho |
| Movimento | `transform.Translate` a **25 m/s** | anda ~0,42 m por frame |

Com isso a bala **atravessava** o inimigo entre dois frames (tunneling): o evento
`OnTriggerEnter` simplesmente não acontecia. Pior nos bruxos, que têm o corpo estreito
(cápsula de raio 0,42). Resultado: dava para atirar à vontade e a vida não caía.

## Correções

### 1. Colisão por varredura (`Weapons/Projectile.cs`)

O projétil não depende mais de `OnTriggerEnter`. A cada frame ele varre o trecho percorrido:

```csharp
Physics.OverlapCapsuleNonAlloc(from, to, hitRadius, ...)   // raio 0,28 m
```

- pega `EnemyBase` no `GetComponentInParent` (funciona com colisor no filho),
- **nunca** atravessa o alvo, mesmo a 25–60 m/s,
- ignora o próprio jogador,
- parede/obstáculo ainda bloqueia — mas só depois de 0,7 m
  (`wallClearance`), para o cano encostado numa torre não "comer" o tiro,
- mantém o `OnTriggerEnter` como reforço e um guarda (`returned`) contra dano duplo.

`Advance(dt)` é público, então os testes simulam o voo com passo fixo.

### 2. Bala mais "gorda"

`hitRadius = 0,28 m` (antes o colisor tinha 0,08 m). O raio efetivo de acerto é
~3,5× maior, sem virar um tiro teleguiado.

### 3. Tiro reto (sem mira assistida)

**A bala vai reta: não curva e não é desviada.** A mira assistida antiga (que puxava o tiro
para o inimigo mais próximo, até 14°) ficou **desligada** — ela fazia o disparo sair numa
direção diferente da mira, o que atrapalhava na hora de acertar de longe.

O que existe hoje:

- `Weapon.AimDirection(targetPoint)` — direção da mira, projetada na altura do cano (a bala
  não "entra no chão" quando o cursor está perto);
- `Weapon.DirectionForPellet(i, baseDir)` — dispersão **simétrica**: os bagos da escopeta se
  abrem igualmente para os dois lados (12°), então o centro do tiro continua exatamente onde
  o jogador apontou. Pistola e arma de energia têm `spread = 0` (tiro 100% reto);
- `Projectile` só anda no próprio `forward` (sem gravidade, sem perseguição).

`AimAssist` sobrou apenas como `ChestPoint` (ponto de mira no peito do inimigo, usado nos
testes), sem alterar nenhuma direção de tiro.

## Verificação

`Tools ▸ Arena ▸ Verificar Ritmo e Poderes (edit mode)` → `docs/screens/poderes/verificacao.txt`:

```
[TIRO] 2 disparos mataram o bruxo? True (vida inicial 36)
[OK]   Tiro do jogador acerta e mata o bruxo azul — 2 disparos para matar 36HP
[TIRO] desvio máximo do caminho: 0,03°
[OK]   Tiro vai reto: a bala não desvia nem curva depois de sair — pior desvio = 0,03°
[TIRO] escopeta: centro da dispersão=0,00° maior desvio=12,0°
[OK]   Escopeta espalha os bagos em torno da mira (centro continua no alvo)
=== RESULTADO: 19 OK / 0 FALHAS ===
```

O teste usa os prefabs reais (`Projectile_Player` + `Enemy_Sorcerer`) e a arma real
(`WeaponData_Pistol`, 18 de dano), simulando o voo a 60 fps — é o mesmo caminho de código do jogo.

Também existe `Tools ▸ Arena ▸ Testar Tiro nos Inimigos (em jogo)` (playmode): dispara com a
arma do jogador em um bruxo a 9 m e registra cada acerto em `docs/screens/tiro/relatorio.txt`.

## Se quiser ajustar o "quão fácil" é acertar

- `Projectile.hitRadius` (prefab `Projectile_Player`) — 0,28 → 0,4 deixa bem mais fácil.
- `AimAssist.Radius` / `AimAssist.MaxAngle` em `Weapons/AimAssist.cs` — 1,1 m / 14°.
- `WeaponData.damage` da pistola (`WeaponData_Pistol.asset`) — hoje 18.

---

## Correção: arma desbloqueada que não atirava (20/11)

**Sintoma.** Depois de comprar o upgrade de dano na loja (e desbloquear a escopeta), o
jogador parava de atirar na onda seguinte.

**Causa.** `Weapon_Shotgun` e `Weapon_Energy` começam desativadas na cena, então o
`Awake()` delas só roda quando o jogador equipa. A loja chamava `Weapon.Unlock()`
(`IsUnlocked = true`) na arma ainda desativada; no `SetActive(true)`, o `Awake()` executava
`IsUnlocked = data.startUnlocked` — que é `false` para as armas compradas — e **apagava o
desbloqueio**. Como `TryFire` começa com `if (!IsUnlocked) return;`, a arma simplesmente
não disparava. O poder "ARMA MAIS FORTE" caía no mesmo buraco: re-desbloqueava a escopeta
em vez de liberar a arma de energia.

**Correção.**

```csharp
public void Unlock()
{
    unlockRequested = true;      // sobrevive ao Awake da arma desativada
    EnsureInitialized();         // munição e pool já ficam prontos
    IsUnlocked = true;
}

public void EnsureInitialized()
{
    if (initialized) return;
    initialized = true;
    if (data == null) return;
    currentClip    = data.clipSize;
    currentReserve = data.reserveAmmo;
    IsUnlocked     = data.startUnlocked || unlockRequested;
    // ... pool de projéteis
}
```

`EnsureInitialized()` é chamado no `Awake()` e no começo de `TryFire()`, `TryReload()` e
`AddAmmo()`, então nenhum caminho depende mais da ordem de ativação do GameObject.

**Rede de segurança.** `Weapon.IsDry` (pente e reserva zerados) faz o `PlayerShooter`
voltar para a pistola — que tem reserva infinita — e avisar na tela
("SEM MUNIÇÃO — PISTOLA"). Assim nenhuma arma pode deixar o jogador sem atirar.

**Prova.** `Tools ▸ Arena ▸ Teste Tiro das Armas` (play mode): pistola 12→11, escopeta
comprada na loja 6→5, arma de energia pelo poder 30→29, volta para a pistola 11→10 e
escopeta seca trocando sozinha para a pistola. 5/5 PASS, 0 erros
(`docs/screens/armas/tiro_armas.txt`).
