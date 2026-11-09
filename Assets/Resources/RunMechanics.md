# Run Mechanics

> Source of truth is the code: `RunManager.cs` (scaling, rewards, rooms), `BaseBattleManager.cs` (combat, spawning, affixes, events) and `Unit.cs` (status effects). Keep this doc in sync when changing numbers there.

## Overview

The game is a roguelike turn-based RPG. A **run** is an endless chain of **encounters**. After each combat the player picks one reward card, then chooses the next **room** (combat, shrine, merchant or mystery). A run ends when the player party is fully defeated. There is currently no meta-progression and no save: every run starts from scratch.

The run is a **single hero** chosen on the main menu (Barbarian, Knight, Necromancer, Ninja, Wizard). The battle manager also supports a list of `playerPrefabs` as a fallback, so multi-unit parties are possible but not used.

**Every encounter re-instantiates all units** and re-applies the run's upgrades, attack upgrades and relic. This means **HP and Mana are fully restored at the start of every encounter**, and status effects do not carry over. Mana does not regenerate during a battle.

---

## Encounter Structure

### Tiers

| Encounters | Tier   | Used for                                        |
| ---------- | ------ | ----------------------------------------------- |
| 1–3        | Tier 1 | Enemy pool, reward values, affix count          |
| 4–6        | Tier 2 | Enemy pool, reward values, affix count, curses  |
| 7+         | Tier 3 | Enemy pool, reward values, affix count          |

Enemy pools are assigned on `BaseBattleManager` in `BattleScene`:

| Pool        | Enemies                          |
| ----------- | -------------------------------- |
| Tier 1      | Skeleton, Skeleton Knight        |
| Tier 2      | Skeleton Minion                  |
| Tier 3      | Skeleton Foot Soldier, Skeleton Knight |
| Boss        | Necromancer (enemy), Dark Knight |

If a tier pool is empty the manager falls back to the next lower pool, then to all pools merged.

### Special Encounters

| Encounter            | Rule                                                                                         |
| -------------------- | -------------------------------------------------------------------------------------------- |
| **Mini-boss**        | Every 5th encounter that is not a boss (E5, E15, E25…).                                      |
| **Boss**             | Every 10th encounter (E10, E20…). Always a combat room.                                      |

**Mini-boss:** one enemy picked from the *next tier up's* pool. Scaled stats ×1.5 and +3 Speed on top of normal scaling. Always has at least 1 affix. Gold ×1.5.

**Boss:** one enemy from the boss pool. Scaled stats ×1.25 (`bossStatMultiplier`) and +5 Speed on top of normal scaling. Always has 2 affixes. Gold ×2. Victory screen offers 4 cards, and it is a **relic-only** screen while any hero still lacks a relic (see Relics).

### Enemy Count (normal combat)

- `baseEnemyCount` (1) + `floor((E−1) / 4)` extra enemies, each a random pick from the tier pool.
- That is +1 enemy at E5, E9, E13…; because E5 is a mini-boss the first extra enemy actually appears at **E9**.
- Maximum 5 enemies.

---

## Rooms

After picking a reward the player chooses the next room. **Rooms count as encounters:** picking any room increments the encounter number (so a shrine at "E6" pushes enemy scaling and tiers forward just like a fight).

| Next encounter            | Choices offered                                                           |
| ------------------------- | ------------------------------------------------------------------------- |
| 1–2                       | Combat only                                                               |
| Every 10th                | Boss combat only                                                          |
| Otherwise                 | Combat + 1 or 2 extra rolls (50/50), duplicates collapse (so 1 or 2 extras) |

Extra room roll: Shrine 40%, Merchant 35%, Mystery 25%.

| Room         | Behaviour                                                                                         |
| ------------ | ------------------------------------------------------------------------------------------------- |
| **Combat**   | Normal fight (or mini-boss / boss), then gold + reward cards.                                     |
| **Shrine**   | No enemies. Heals 40% max HP (see note), then pick 1 of 2 free reward cards.                      |
| **Merchant** | No enemies. 3 reward cards with gold prices. Can be skipped.                                      |
| **Mystery**  | No enemies. 50%: gain `GetGoldReward()` gold and pick 1 of 2 cards. 50%: pick 1 of 3 cards.       |

> **Note:** because HP is restored to full at the start of every encounter (see Overview), the Shrine's heal currently has no practical effect; the blessing card is its real value.

**Merchant prices:** `max(5, round(floor(20 × 1.35^(E−1)) × rand(0.5, 0.85)))` gold per card. Cards the player cannot afford are disabled.

Event rooms grant no gold and no Victory reward screen.

---

## Enemy Scaling

Scaling **compounds** per encounter number `E` (it was linear before; player upgrades multiply, so linear enemies could never keep up):

| Stat    | Formula                                       |
| ------- | --------------------------------------------- |
| Max HP  | `base × 1.12^(E−1)`                           |
| Attack  | `base × 1.12^(E−1)`                           |
| Defense | `base × 1.09^(E−1)`                           |
| Speed   | `base + round(1.5 × (E−1))` (additive)        |

Base stats come from each enemy prefab's `Unit` component (e.g. Skeleton 80 HP / 10 ATK / 10 DEF / 12 SPD).

### Elites

Normal enemies roll to be **Elite**: T2 15%, T3 25%, T1 never. Elites have all stats ×1.5, +5 Speed, 2 affixes, and a ★ name prefix.

---

## Enemy Affixes

| Enemy type    | T1 | T2 | T3         |
| ------------- | -- | -- | ---------- |
| Normal        | 0  | 1  | 1 (50%) or 2 (50%) |
| Elite         | 2  | 2  | 2          |
| Mini-boss     | 1+ | 1+ | 1–2        |
| Boss          | 2  | 2  | 2          |

Berserker and Enraged are never kept together (combined ~3× ATK); Enraged is removed if both roll.

| Affix            | Effect                                               |
| ---------------- | ---------------------------------------------------- |
| **Armored**      | Defense ×1.5                                         |
| **Berserker**    | Attack ×1.5                                          |
| **Swift**        | Speed +10                                            |
| **Bulwark**      | Max HP ×1.4                                          |
| **Regenerating** | Restores 8% max HP at the start of its turn          |
| **Thorns**       | Reflects 15% of damage taken back to the attacker (enemy does not flinch) |
| **Vampiric**     | Heals 25% of damage dealt                            |
| **Enraged**      | Damage ×2 while below 50% HP                         |

Affixes show as icons on the enemy's floating health bar and as aura VFX where aura prefabs are assigned.

---

## Combat

### Turn Order

Each round all living units are sorted by **Speed (highest first)** and act once. The queue is rebuilt when empty. A "turn survived" is counted per round.

### Player Actions

Attack (basic attack, then a target), Special (attacks with mana cost), Item, Defend.

- **Defend:** halves incoming damage until that unit's next turn starts.
- **Items:** `BattleItem` (Health / Mana potion) in a unit's `inventory`. No reward or shop currently grants items, so inventories are only filled in the prefab.

### Enemy AI

20% chance to Defend. Otherwise picks a random attack it can afford and a random living player (AoE hits all). Enemy mana is never regenerated.

### Damage Formula

```
base   = random(minDamage, maxDamage)
damage = max(1, base × attacker.ATK / target.DEF)
× 0.5   if target is defending            (min 1)
× 1.5   if critical hit
× 2     if attacker is Enraged (< 50% HP)
× (1 − 0.25)  if attacker is Weakened
× (1 − 0.25)  if target is Fortified
```

Multi-hit, multi-projectile and AoE attacks apply this formula **per hit / per target**. Crit is rolled once per attack action.

### Luck

| Effect         | Formula                    | Cap |
| -------------- | -------------------------- | --- |
| Critical hit   | `Luck`% chance             | 75% |
| **Evasion**    | `Luck`% chance to fully dodge a hit ("MISS") | 70% |
| Lucky Find     | Average team Luck = % chance the card appears | – |

Evasion applies to both sides, rolled per hit. High Luck is very strong (the Barbarian starts at 40 Luck; it was 70, which meant a 70% dodge from the first fight).

### Status Effects

Applied by attacks (`AttackAction.appliesStatus`, with chance and duration). Ticked at the **start of the affected unit's turn**; durations tick down each turn. Re-applying refreshes to the longer duration.

| Status      | Effect                                               | Target             |
| ----------- | ---------------------------------------------------- | ------------------ |
| **Burn**    | Takes 8% max HP per turn                             | Enemy / target     |
| **Stun**    | Loses its next turn                                  | Target             |
| **Weaken**  | Deals 25% less damage                                | Target             |
| **Fortify** | Takes 25% less damage                                | **Applied to the attacker (self-buff)** |

### Summons

Summon attacks (`isSummon`) spawn pooled minion prefabs that rise, run to the target and hit using the **caster's** ATK and the attack's damage values. `summonCount` minions per target. The Necromancer's *Raise Skeleton* summons 3.

---

## Gold

- Earned after winning each combat: `20 + 5 × (E−1)`.
- Boss ×2. Mini-boss ×1.5.
- Spent only at Merchants.
- Mystery rooms can grant one extra payout of the same base amount.

---

## Rewards

After a combat victory the player is shown **3 cards (4 on boss encounters)**, picks one, and may **Reroll once** per victory screen. At least one **Learn** card is guaranteed if any is available. Remaining cards are drawn by weighted random from a contextual pool:

| Card type                          | Weight                                              |
| ---------------------------------- | --------------------------------------------------- |
| Attack upgrade                     | 10, **+30 per prior upgrade on that same attack**   |
| Stat upgrade / passive trait / Lucky Find | 30                                           |
| Curse (T2+)                        | 40                                                  |

### Stat Upgrades

Permanent for the run.

| Upgrade  | T1        | T2  | T3  |
| -------- | --------- | --- | --- |
| Max HP   | +15 / +20 | +30 | +40 |
| Attack   | +8 / +12  | +18 | +25 |
| Defense  | +6 / +10  | +15 | +22 |
| Speed    | +4        | +6  | +8  |
| Max Mana | +10       | +15 | +20 |

Smaller values (the first number) only appear at Tier 1.

### Attack Upgrades

Each attack the player owns generates its own set of cards. Upgrades stack.

| Upgrade                | Condition                       | Effect                                                              |
| ---------------------- | ------------------------------- | ------------------------------------------------------------------- |
| **Empower**            | Any attack                      | +8 / +15 / +22 min & max damage (T1/T2/T3), divided by √(hits × projectiles) for multi-hit attacks so heroes stay comparable |
| **Efficiency**         | Attack has a mana cost          | −3 mana cost                                                        |
| **Cleave**             | Not already AoE                 | Attack hits all living enemies                                      |
| **Barrage**            | Ranged, not a summon            | +1 projectile and +15 projectile speed                              |
| **Enlarge / Colossal** | Ranged, not a summon            | +0.5 projectile scale and ×1.5 damage per pick                      |
| **Fusillade**          | Ranged with more than 1 projectile | +3 damage × current projectile count                             |
| **Double Strike**      | Melee or summon, T1–T2          | Hits per swing ×2 (named *Frenzy Minions* for summons)              |
| **Triple Strike**      | Melee or summon, T3             | Hits per swing ×3 (*Frenzy Minions x3* for summons)                 |
| **Learn: \<Attack\>**  | Unlockable attack not yet known | Permanently adds a new attack                                       |

Learnable attacks come from each hero prefab's `unlockableAttacks` list; their own upgrades only appear after being learned.

### Passive Traits

Each trait can be taken **up to 2 times per run** (counting relic-granted copies).

| Trait            | Effect                                                                  |
| ---------------- | ----------------------------------------------------------------------- |
| **Vampiric**     | Heal for 25% of damage dealt                                            |
| **Thorns**       | Take 50% of incoming damage; the other 50% is dealt to the attacker     |
| **Regenerating** | Restore 8% max HP at the start of your turn                             |
| **Enraged**      | Deal double damage while below 50% HP                                   |
| **More Minions** | Only if the hero has a summon attack; up to 3×. Each pick **doubles** the minions per summon (`summonCount << picks`) |

> A second copy of Vampiric/Thorns/Regenerating/Enraged currently only adds a stacked aura visual; the effect is a boolean and does not increase.

### Curses (Tier 2+)

A strong benefit paired with a permanent drawback.

| Curse            | T2                | T3                |
| ---------------- | ----------------- | ----------------- |
| **Iron Rage**    | +30 ATK, −20 DEF  | +45 ATK, −30 DEF  |
| **Frail Colossus** | +60 Max HP, −15 ATK | +90 Max HP, −22 ATK |
| **Glass Canon**  | +25 ATK, −25 Max HP | +38 ATK, −38 Max HP |
| **Speed Demon**  | +20 SPD, −18 DEF  | +30 SPD, −26 DEF  |

### Lucky Find

Bonus card granting Luck (+3 / +4 / +5 by tier). Appears with probability equal to the team's average Luck (in %).

---

## Legendary Relics

- Offered on **boss encounters only** (E10, E20…) as a relic-only card screen: one card per hero without a relic, chosen from `Resources/Relics` where `targetCharacterName` matches the hero or is `Any`.
- Relics whose trait would push a trait past the 2-per-run cap are excluded.
- One relic per hero per run. Once every hero has one, bosses fall back to normal reward cards.
- A relic applies its stat boosts, clones its `grantedAttack` into the hero's attacks, and spawns its visual prefabs on the hero's `RelicAttachmentPoints` sockets (right hand, left hand, head, back).

| Relic                        | Hero        | Boosts                                  | Grants attack |
| ---------------------------- | ----------- | --------------------------------------- | ------------- |
| Blade of the Fallen King     | Knight      | +20 ATK, +15 HP, Enraged                | yes           |
| Inquisitor's Aegis           | Knight      | +25 DEF, +30 HP, Thorns, Regenerating   | –             |
| Dark Staff                   | Necromancer | +10 Summons*, +15 ATK, +15 Mana         | yes           |
| Oni Mask of the Void         | Ninja       | +10 ATK, +20 SPD, +10 Luck              | yes           |
| Phantom Kusarigama           | Ninja       | +20 ATK, +15 SPD, Vampiric              | yes           |
| Crown of the Undying         | Any         | +50 HP, +15 DEF, Regenerating           | yes           |
| Heart of Chaos               | Any         | +10 ATK / DEF / SPD, +20 HP             | yes           |

\* See Known Issues.

---

## Player Characters (base stats)

| Hero        | HP  | Mana | ATK | DEF | SPD | Luck |
| ----------- | --- | ---- | --- | --- | --- | ---- |
| Barbarian   | 100 | 20   | 30  | 10  | 10  | 40   |
| Knight      | 100 | 20   | 15  | 15  | 15  | 5    |
| Wizard      | 100 | 20   | 30  | 5   | 15  | 0    |
| Ninja       | 100 | 20   | 15  | 5   | 15  | 15   |
| Necromancer | 100 | 20   | 25  | 5   | 15  | 5    |

---

## Attack Properties

Each `AttackAction` ScriptableObject has these tunable properties:

| Property                  | Description                                                  |
| ------------------------- | ------------------------------------------------------------ |
| `minDamage` / `maxDamage` | Damage range before stat scaling                             |
| `manaCost`                | Mana consumed when used                                      |
| `isRanged`                | Fires projectiles (or instant VFX hits); does not lunge      |
| `isAoE`                   | Hits all alive targets                                       |
| `projectileCount`         | Projectiles (or instant strikes) per use                     |
| `projectileSpreadAngle`   | Fan spread in degrees                                        |
| `delayBetweenProjectiles` | Seconds between projectiles (0 = all at once)                |
| `hitCount`                | Damage applications per swing (melee / summon)               |
| `delayBetweenHits`        | Seconds between hits (default 0.3s)                          |
| `appliesStatus` / `statusDuration` / `statusValue` / `statusChance` | Status effect on hit |
| `isSummon` / `summonCount` / `summonPrefab` | Spawns minions instead of a normal hit     |
| `lifeStealPercent`        | Fraction of damage healed to the attacker (Soul Drain 50%)   |
| `returnsToCaster`, `hitVfxDamageDelay`, VFX/SFX fields | Presentation and timing        |

Melee AoE attacks lunge to the **centre** of all living targets.

---

## Run End

The run ends when all player units reach 0 HP. The run summary shows the encounter reached and total gold. `RunStatistics` also tracks damage dealt/taken, healing, kills, crits and rounds survived.

---

## Known Issues (found while documenting)

1. **Dark Staff's "+10 Summons" does nothing.** `ApplyRelicToUnit` has no `BonusSummons` case. (If it were added as-is, `summonCount << 10` would spawn ×1024 minions, so the shift needs a cap.)
2. **Shrine heal is moot** because each encounter restores full HP.
3. **Second trait copy has no mechanical effect**, only extra auras.
4. **Relic grants:** Dark Staff (Necromancer) and Crown of the Undying both grant *Undying Wrath*, a melee ×3 attack. Verify that is intended for the Necromancer.
5. **Boss affix comment mismatch:** a code comment says "3 forced affixes" but `RollAffixes` gives 2.
6. **Footstep SFX and BGM only play in the Editor** (loaded via `AssetDatabase` under `#if UNITY_EDITOR`).
