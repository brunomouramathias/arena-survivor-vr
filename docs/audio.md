# Sons do jogador: dano e morte

Prova automática: `docs/screens/audio/sons.txt` (`Tools ▸ Arena ▸ Teste Sons de Dano e Morte`)

## Os dois clipes

| Arquivo | Duração | Pico | Quando toca |
| --- | --- | --- | --- |
| `Assets/_Project/Audio/SFX/Sfx_PlayerDamage.wav` | 0,28 s | −5,4 dBFS | o jogador **leva um golpe** (`PlayerHealth.TakeDamage`) |
| `Assets/_Project/Audio/SFX/Sfx_PlayerDeath.wav` | 1,25 s | −4,0 dBFS | o jogador **cai** (`PlayerHealth.Die`, antes do nocaute) |

Os dois são sintetizados com o conteúdo em **médio-grave (300 Hz – 2 kHz)** — a primeira
versão era grave demais e sumia em caixinha de notebook. Mono 44,1 kHz / 16 bits,
normalizados em −4 dBFS para nunca estourar. São curtos de propósito: som de dano a cada 0,9 s de invulnerabilidade ficaria
repetitivo.

## Nível — "bem baixinho"

O volume é aplicado em código, no `AudioManager`, não no arquivo:

| Campo no Inspector | Valor | Efeito |
| --- | --- | --- |
| `playerDamageVolume` | **0,45** | pico efetivo ≈ −12 dBFS |
| `playerDeathVolume` | **0,50** | pico efetivo ≈ −10 dBFS |
| `damageSoundCooldown` | 0,12 s | evita o som virar metralhadora em dano em área |

Ou seja: o som fica **bem abaixo** da música e dos efeitos do combate — dá para ouvir o
golpe, mas nada salta na frente. Ajuste os dois campos no `AudioManager` da cena `Arena`
se quiser ainda mais baixo (0,2 ≈ quase sussurro).

## Por que o som é disparado pelo `PlayerHealth`

`PlayerDamagedEvent` também é publicado ao **curar** e ao **renascer** — se o som fosse
ligado nesse evento, beber vida ou voltar do nocaute faria barulho de dano. Por isso:

- `PlayerHealth.TakeDamage` → `AudioManager.PlayPlayerDamageSound()`
- `PlayerHealth.Die` → `AudioManager.PlayPlayerDeathSound()`

Ambos passam por `PlayUISound(clip, pitchVariation, volume)`, que toca em 2D (som "na
cabeça do jogador", como todo SFX de interface) usando o pool de `AudioSource` do
`AudioManager`, com pequena variação de pitch para não cansar o ouvido.

## Reinstalar / reimportar

`Tools ▸ Arena ▸ Instalar Sons de Dano e Morte` — reaplica as opções de importação
(PCM, *decompress on load*, mono, preload: zero latência no tiro) e regrava os dois campos
no `AudioManager` da cena `Arena`.

Em batch:

```bash
Unity.exe -batchmode -quit -projectPath <projeto> \
  -executeMethod ArenaSurvivor.Editor.AudioSfxSetupTool.Run
```

## Verificação

`Tools ▸ Arena ▸ Teste Sons de Dano e Morte` (play mode) checa, no jogo rodando:

1. os dois clipes estão ligados no `AudioManager` e têm duração plausível;
2. o pico dos arquivos está abaixo de −3 dBFS (áudio discreto, sem clipping);
3. o golpe toca `Sfx_PlayerDamage` com volume ≤ 0,40;
4. **curar não toca nada**;
5. o nocaute toca `Sfx_PlayerDeath` com volume ≤ 0,40.

Resultado da última execução: **PASS** (5/5).
