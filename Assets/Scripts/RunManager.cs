using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum UpgradeType { MaxHealth, Attack, Defense, Speed, MaxMana, Luck, Vampiric, Thorns, Regenerating, Enraged, BonusSummons }

/// <summary>What kind of room the player will face next encounter.</summary>
public enum EncounterType { Combat, Shrine, Merchant, Mystery }

[Serializable]
public class RunUpgrade
{
    public string name;
    public string description;
    public UpgradeType upgradeType;
    public int value;

    public RunUpgrade(string name, string description, UpgradeType upgradeType, int value)
    {
        this.name = name;
        this.description = description;
        this.upgradeType = upgradeType;
        this.value = value;
    }
}

[Serializable]
public class RoomChoice
{
    public EncounterType encounterType;
    public string title;
    public string description;
}

public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    public GameObject SelectedCharacterPrefab { get; set; }

    public int CurrentEncounter { get; private set; } = 0;
    public int Gold { get; private set; } = 0;
    public bool IsRunActive { get; private set; } = false;
    public EncounterType CurrentEncounterType { get; private set; } = EncounterType.Combat;

    private readonly List<RunUpgrade> _appliedUpgrades = new List<RunUpgrade>();
    private readonly List<AttackUpgrade> _appliedAttackUpgrades = new List<AttackUpgrade>();
    private readonly Dictionary<string, LegendaryRelicSO> _equippedRelics = new Dictionary<string, LegendaryRelicSO>();
    public IReadOnlyList<RunUpgrade> AppliedUpgrades => _appliedUpgrades;
    public IReadOnlyList<AttackUpgrade> AppliedAttackUpgrades => _appliedAttackUpgrades;
    public IReadOnlyDictionary<string, LegendaryRelicSO> EquippedRelics => _equippedRelics;
    public RunStatistics CurrentRunStats { get; private set; }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Run lifecycle ──────────────────────────────────────────────────────────

    public void StartRun()
    {
        CurrentEncounter = 1;
        CurrentEncounterType = EncounterType.Combat;
        Gold = 0;
        IsRunActive = true;
        _appliedUpgrades.Clear();
        _appliedAttackUpgrades.Clear();
        _equippedRelics.Clear();

        string charName = SelectedCharacterPrefab != null ? SelectedCharacterPrefab.name : "Unknown Hero";
        CurrentRunStats = new RunStatistics(charName);

        Debug.Log("[RunManager] Run started.");
    }

    public event Action<RoomChoice> OnRoomSelected;

    /// <summary>Generates choices for the upcoming encounter.</summary>
    public List<RoomChoice> GenerateNextRoomChoices()
    {
        var choices = new List<RoomChoice>();
        int nextEncounter = CurrentEncounter + 1;

        // Boss every 10th encounter is always Combat.
        bool isBoss = nextEncounter % 10 == 0;
        if (isBoss)
        {
            choices.Add(new RoomChoice { encounterType = EncounterType.Combat, title = "Boss Battle", description = "A terrifying foe awaits." });
        }
        else if (nextEncounter <= 2)
        {
            choices.Add(new RoomChoice { encounterType = EncounterType.Combat, title = "Enemy Camp", description = "Fight standard enemies." });
        }
        else
        {
            // Always offer at least one combat choice
            choices.Add(new RoomChoice { encounterType = EncounterType.Combat, title = "Enemy Camp", description = "Fight standard enemies." });

            // Roll for an event encounter 1 or 2 extra choices
            int extraChoices = UnityEngine.Random.value < 0.5f ? 2 : 1;
            var generatedTypes = new HashSet<EncounterType>() { EncounterType.Combat };

            for (int i = 0; i < extraChoices; i++)
            {
                float roll = UnityEngine.Random.value;
                EncounterType type;
                if (roll < 0.40f) type = EncounterType.Shrine;
                else if (roll < 0.75f) type = EncounterType.Merchant;
                else type = EncounterType.Mystery;

                if (generatedTypes.Add(type))
                {
                    if (type == EncounterType.Shrine)
                        choices.Add(new RoomChoice { encounterType = type, title = "Ancient Shrine", description = "Rest and receive a blessing." });
                    else if (type == EncounterType.Merchant)
                        choices.Add(new RoomChoice { encounterType = type, title = "Wandering Merchant", description = "Spend your gold on wares." });
                    else if (type == EncounterType.Mystery)
                        choices.Add(new RoomChoice { encounterType = type, title = "Unknown Event", description = "Strange energies." });
                }
            }
        }

        return choices;
    }

    /// <summary>Select the next room, advancing the counter and setting the type.</summary>
    public void SelectNextRoom(RoomChoice choice)
    {
        CurrentEncounter++;
        CurrentEncounterType = choice.encounterType;
        OnRoomSelected?.Invoke(choice);
    }

    public void EndRun()
    {
        IsRunActive = false;
        if (CurrentRunStats != null)
        {
            CurrentRunStats.finalEncounter = CurrentEncounter;
            CurrentRunStats.finalGold = Gold;
        }
        Debug.Log($"[RunManager] Run ended at encounter {CurrentEncounter}. Gold: {Gold}");
    }

    /// <summary>True for encounters that are every 5th but not a boss (E5, E15, E25, ...).</summary>
    public bool IsMiniBossEncounter => IsRunActive && CurrentEncounter % 5 == 0 && CurrentEncounter % 10 != 0;

    // ── Scaling helpers ────────────────────────────────────────────────────────

    // ── Formula-based infinite scaling ─────────────────────────────────────────
    // Each formula is tuned so encounter 1 = baseline and grows each wave.

    /// <summary>Returns 1 (E1-3), 2 (E4-6), or 3 (E7+) used for tiered reward scaling.</summary>
    public static int GetEncounterTier(int encounter)
        => encounter <= 3 ? 1 : encounter <= 6 ? 2 : 3;

    public int GetGoldReward() => 20 + ((CurrentEncounter - 1) * 5);

    private float GetEncounterScalar(float multiplier) => 1f + ((CurrentEncounter - 1) * multiplier);
    public float GetEnemyHpScalar() => GetEncounterScalar(0.15f);
    public float GetEnemyAtkScalar() => GetEncounterScalar(0.15f);
    public float GetEnemyDefScalar() => GetEncounterScalar(0.11f);

    // Speed grows linearly by a flat rate
    public int GetEnemySpeedBonus() => Mathf.RoundToInt((CurrentEncounter - 1) * 1.5f);
    // +1 extra enemy clone every 4 encounters starting at encounter 5
    public int GetExtraEnemyCount() => Mathf.Max(0, (CurrentEncounter - 1) / 4);

    // ── Gold & upgrades ────────────────────────────────────────────────────────

    public void AddGold(int amount)
    {
        Gold += amount;
        Debug.Log($"[RunManager] +{amount} gold. Total: {Gold}");
    }

    public void SpendGold(int amount)
    {
        Gold = Mathf.Max(0, Gold - amount);
        Debug.Log($"[RunManager] -{amount} gold spent. Total: {Gold}");
    }

    public void ApplyUpgrade(RunUpgrade upgrade)
    {
        _appliedUpgrades.Add(upgrade);
        Debug.Log($"[RunManager] Stat upgrade: {upgrade.name}");
    }

    public void ApplyAttackUpgrade(AttackUpgrade upgrade)
    {
        _appliedAttackUpgrades.Add(upgrade);
        Debug.Log($"[RunManager] Attack upgrade: {upgrade.name}");
    }

    /// <summary>Applies all accumulated stat upgrades to a Unit instance (call after fresh instantiation).</summary>
    public void ApplyUpgradesToUnit(Unit unit)
    {
        foreach (var u in _appliedUpgrades)
        {
            switch (u.upgradeType)
            {
                case UpgradeType.MaxHealth: unit.maxHealth = Mathf.Max(1, unit.maxHealth + u.value); break;
                case UpgradeType.Attack: unit.attack = Mathf.Max(0, unit.attack + u.value); break;
                case UpgradeType.Defense: unit.defense = Mathf.Max(0, unit.defense + u.value); break;
                case UpgradeType.Speed: unit.speed = Mathf.Max(0, unit.speed + u.value); break;
                case UpgradeType.MaxMana: unit.maxMana = Mathf.Max(0, unit.maxMana + u.value); break;
                case UpgradeType.Luck: unit.luck = Mathf.Max(0, unit.luck + u.value); break;
                case UpgradeType.Vampiric: unit.hasVampiric = true; break;
                case UpgradeType.Thorns: unit.hasThorns = true; break;
                case UpgradeType.Regenerating: unit.hasRegeneration = true; break;
                case UpgradeType.Enraged: unit.hasEnraged = true; break;
                case UpgradeType.BonusSummons: unit.bonusSummonCount += u.value; break;
            }
        }
    }

    /// <summary>Applies all accumulated attack upgrades to a Unit instance (call after fresh instantiation).</summary>
    public void ApplyAttackUpgradesToUnit(Unit unit)
    {
        foreach (var u in _appliedAttackUpgrades)
        {
            if (u.upgradeType == AttackUpgradeType.UnlockNewAttack)
            {
                if (u.attackToUnlock == null) continue;
                // Skip if the unit already has this attack (by name, to handle clones)
                bool alreadyHas = FindAttackByName(unit, u.attackToUnlock.actionName) != null;
                if (!alreadyHas)
                {
                    AttackAction clone = UnityEngine.Object.Instantiate(u.attackToUnlock);
                    unit.availableAttacks.Add(clone);
                    unit.specialAttacks.Add(clone);
                }
                continue;
            }

            // Find the attack to enhance (basic or available)
            AttackAction target = FindAttackByName(unit, u.targetAttackName);

            if (target == null) continue;

            // Clone once — subsequent upgrades to the same attack in the same encounter
            // will land on the already-cloned instance because we replaced the reference.
            AttackAction enhanced = UnityEngine.Object.Instantiate(target);

            switch (u.upgradeType)
            {
                case AttackUpgradeType.DamageBoost:
                    enhanced.minDamage += u.value;
                    enhanced.maxDamage += u.value;
                    break;
                case AttackUpgradeType.ManaCostReduce:
                    enhanced.manaCost = Mathf.Max(0, enhanced.manaCost - u.value);
                    break;
                case AttackUpgradeType.ProjectileCountBoost:
                    enhanced.projectileCount += u.value;
                    enhanced.projectileSpeed += 15f;
                    break;
                case AttackUpgradeType.AoEUnlock:
                    enhanced.isAoE = true;
                    break;
                case AttackUpgradeType.SpreadAngleBoost:
                    // Fusillade: bonus damage = value × current projectile count (rewards Barrage investment)
                    int fusilladeBonus = u.value * enhanced.projectileCount;
                    enhanced.minDamage += fusilladeBonus;
                    enhanced.maxDamage += fusilladeBonus;
                    break;
                case AttackUpgradeType.HitCountBoost:
                    enhanced.hitCount *= Mathf.Max(1, u.value);
                    break;
                case AttackUpgradeType.SizeMultiply:
                    enhanced.projectileScaleMultiplier += 0.5f * u.value;
                    enhanced.minDamage = (int)(enhanced.minDamage * (1f + 0.5f * u.value));
                    enhanced.maxDamage = (int)(enhanced.maxDamage * (1f + 0.5f * u.value));
                    break;
            }

            // Replace the reference on the unit
            ReplaceAttackByName(unit, u.targetAttackName, enhanced);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private AttackAction FindAttackByName(Unit unit, string attackName)
    {
        if (unit.basicAttack != null && unit.basicAttack.actionName == attackName)
            return unit.basicAttack;
        return unit.availableAttacks.FirstOrDefault(a => a.actionName == attackName);
    }

    private void ReplaceAttackByName(Unit unit, string attackName, AttackAction newAttack)
    {
        if (unit.basicAttack != null && unit.basicAttack.actionName == attackName)
        {
            unit.basicAttack = newAttack;
        }
        else
        {
            int idx = unit.availableAttacks.FindIndex(a => a.actionName == attackName);
            if (idx >= 0) unit.availableAttacks[idx] = newAttack;
            int sidx = unit.specialAttacks.FindIndex(a => a.actionName == attackName);
            if (sidx >= 0) unit.specialAttacks[sidx] = newAttack;
        }
    }

    // ── Legendary relics ───────────────────────────────────────────────────────

    public bool HasRelic(string unitName) => _equippedRelics.ContainsKey(unitName);

    public LegendaryRelicSO GetRelicForUnit(string unitName)
        => _equippedRelics.TryGetValue(unitName, out var r) ? r : null;

    public void EquipRelic(string unitName, LegendaryRelicSO relic)
    {
        if (HasRelic(unitName))
        {
            Debug.LogWarning($"[RunManager] {unitName} already has a legendary relic — ignoring.");
            return;
        }
        _equippedRelics[unitName] = relic;
        Debug.Log($"[RunManager] {unitName} equipped legendary relic: {relic.relicName}");
    }

    /// <summary>Applies the unit's legendary relic stat boosts and grants its unique attack.</summary>
    public void ApplyRelicToUnit(Unit unit)
    {
        LegendaryRelicSO relic = GetRelicForUnit(unit.unitName);
        if (relic == null) return;

        foreach (var boost in relic.statBoosts)
        {
            switch (boost.upgradeType)
            {
                case UpgradeType.MaxHealth: unit.maxHealth += boost.value; break;
                case UpgradeType.Attack: unit.attack += boost.value; break;
                case UpgradeType.Defense: unit.defense += boost.value; break;
                case UpgradeType.Speed: unit.speed += boost.value; break;
                case UpgradeType.MaxMana: unit.maxMana += boost.value; break;
                case UpgradeType.Luck: unit.luck += boost.value; break;
                case UpgradeType.Vampiric: unit.hasVampiric = true; break;
                case UpgradeType.Thorns: unit.hasThorns = true; break;
                case UpgradeType.Regenerating: unit.hasRegeneration = true; break;
                case UpgradeType.Enraged: unit.hasEnraged = true; break;
            }
        }

        if (relic.grantedAttack != null)
        {
            bool alreadyHas = unit.availableAttacks.Any(a => a.actionName == relic.grantedAttack.actionName)
                           || (unit.basicAttack != null && unit.basicAttack.actionName == relic.grantedAttack.actionName);
            if (!alreadyHas)
            {
                AttackAction clone = UnityEngine.Object.Instantiate(relic.grantedAttack);
                unit.availableAttacks.Add(clone);
                unit.specialAttacks.Add(clone);
            }
        }

        Debug.Log($"[RunManager] Applied relic '{relic.relicName}' to {unit.unitName}.");
    }

    // ── Reward pool ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns 3 randomly-selected reward choices from a combined pool of
    /// stat upgrades and context-aware attack upgrades.
    /// </summary>
    public static List<RewardChoice> GenerateRewardChoices(List<Unit> playerUnits, int encounter, int count = 3)
    {
        var pool = new List<RewardChoice>();
        int tier = GetEncounterTier(encounter);

        // ── Stat upgrades ───────────────────────────────────────────────────────
        // Values grow each tier; weaker "small" options are pruned at T2+.
        int hpMain = tier == 1 ? 20 : tier == 2 ? 30 : 40;
        int atkMain = tier == 1 ? 12 : tier == 2 ? 18 : 25;
        int defMain = tier == 1 ? 10 : tier == 2 ? 15 : 22;
        int spdVal = tier == 1 ? 4 : tier == 2 ? 6 : 8;
        int manaVal = tier == 1 ? 10 : tier == 2 ? 15 : 20;

        // Small options only appear at tier 1 (E1-3)
        if (tier == 1)
        {
            pool.Add(RewardChoice.FromStat(new RunUpgrade("+15 Max HP", "+15 maximum health for the rest of the run.", UpgradeType.MaxHealth, 15)));
            pool.Add(RewardChoice.FromStat(new RunUpgrade("+8 Attack", "Your attacks hit harder.", UpgradeType.Attack, 8)));
            pool.Add(RewardChoice.FromStat(new RunUpgrade("+6 Defense", "Reduce all incoming damage.", UpgradeType.Defense, 6)));
        }

        pool.Add(RewardChoice.FromStat(new RunUpgrade($"+{hpMain} Max HP", $"+{hpMain} maximum health for the rest of the run.", UpgradeType.MaxHealth, hpMain)));
        pool.Add(RewardChoice.FromStat(new RunUpgrade($"+{atkMain} Attack", $"Significantly increase attack power.", UpgradeType.Attack, atkMain)));
        pool.Add(RewardChoice.FromStat(new RunUpgrade($"+{defMain} Defense", $"Significantly increase defense.", UpgradeType.Defense, defMain)));
        pool.Add(RewardChoice.FromStat(new RunUpgrade($"+{spdVal} Speed", $"Act earlier in the turn order.", UpgradeType.Speed, spdVal)));
        pool.Add(RewardChoice.FromStat(new RunUpgrade($"+{manaVal} Max Mana", $"More mana for special abilities.", UpgradeType.MaxMana, manaVal)));

        // ── Attack upgrades (context-aware) ────────────────────────────────────
        // Build the set of attacks explicitly unlocked through the reward system so we
        // don't generate enhancement rewards for attacks the player hasn't actually earned yet
        // (e.g. attacks that are in unlockableAttacks but were pre-added to a prefab's
        // availableAttacks in the Inspector by mistake).
        var explicitlyUnlocked = new HashSet<string>();
        if (Instance != null)
            foreach (var u in Instance._appliedAttackUpgrades)
                if (u.upgradeType == AttackUpgradeType.UnlockNewAttack && u.attackToUnlock != null)
                    explicitlyUnlocked.Add(u.attackToUnlock.actionName);

        // Collect all unique attacks across all players, skipping learnable attacks that
        // haven't been chosen as a reward yet.
        var seenAttackNames = new HashSet<string>();
        var allAttacks = new List<(AttackAction attack, Unit owner)>();
        foreach (var unit in playerUnits)
        {
            if (unit == null) continue;

            var unlockableNames = new HashSet<string>();
            if (unit.unlockableAttacks != null)
                foreach (var la in unit.unlockableAttacks)
                    if (la != null) unlockableNames.Add(la.actionName);

            if (unit.basicAttack != null && seenAttackNames.Add(unit.basicAttack.actionName))
                allAttacks.Add((unit.basicAttack, unit));

            var combinedAttacks = new List<AttackAction>();
            if (unit.availableAttacks != null) combinedAttacks.AddRange(unit.availableAttacks);
            if (unit.specialAttacks != null) combinedAttacks.AddRange(unit.specialAttacks);

            foreach (var a in combinedAttacks)
            {
                if (a == null) continue;
                // Skip unlockable attacks that the player hasn't explicitly chosen as a reward
                if (unlockableNames.Contains(a.actionName) && !explicitlyUnlocked.Contains(a.actionName))
                    continue;
                if (seenAttackNames.Add(a.actionName))
                    allAttacks.Add((a, unit));
            }
        }

        // Damage boost scales with tier: T1=+8, T2=+15, T3=+22
        int dmgBoost = tier == 1 ? 8 : tier == 2 ? 15 : 22;

        foreach (var (atk, _) in allAttacks)
        {
            // Damage boost — always applicable
            pool.Add(RewardChoice.FromAttack(new AttackUpgrade(
                $"Empower: {atk.actionName}",
                $"+{dmgBoost} damage on {atk.actionName}.",
                AttackUpgradeType.DamageBoost, atk.actionName, dmgBoost)));

            // Mana cost reduce — only if it costs mana
            if (atk.manaCost > 0)
                pool.Add(RewardChoice.FromAttack(new AttackUpgrade(
                    $"Efficiency: {atk.actionName}",
                    $"Reduce mana cost of {atk.actionName} by 3.",
                    AttackUpgradeType.ManaCostReduce, atk.actionName, 3)));

            // AoE unlock — only if not already AoE
            if (!atk.isAoE)
                pool.Add(RewardChoice.FromAttack(new AttackUpgrade(
                    $"Cleave: {atk.actionName}",
                    $"{atk.actionName} now hits all enemies.",
                    AttackUpgradeType.AoEUnlock, atk.actionName, 0)));

            // Projectile count boost — ranged only
            if (atk.isRanged && !atk.isSummon)
            {
                pool.Add(RewardChoice.FromAttack(new AttackUpgrade(
                    $"Barrage: {atk.actionName}",
                    $"+{dmgBoost} projectiles fired by {atk.actionName} and +15 projectile speed.",
                    AttackUpgradeType.ProjectileCountBoost, atk.actionName, dmgBoost)));

                pool.Add(RewardChoice.FromAttack(new AttackUpgrade(
                    $"Enlarge: {atk.actionName}",
                    $"Drastically increases the size and damage multiplier of {atk.actionName}.",
                    AttackUpgradeType.SizeMultiply, atk.actionName, 1)));

                pool.Add(RewardChoice.FromAttack(new AttackUpgrade(
                    $"Colossal: {atk.actionName}",
                    $"Further push {atk.actionName}'s size and damage multiplier.",
                    AttackUpgradeType.SizeMultiply, atk.actionName, 1)));
            }

            // Fusillade — multi-projectile ranged only; bonus damage scales with current projectile count
            if (atk.isRanged && atk.projectileCount > 1 && !atk.isSummon)
            {
                pool.Add(RewardChoice.FromAttack(new AttackUpgrade(
                    $"Fusillade: {atk.actionName}",
                    $"+3 damage per projectile on {atk.actionName} (currently {atk.projectileCount} projectiles = +{atk.projectileCount * 3} per shot). Scales with Barrage.",
                    AttackUpgradeType.SpreadAngleBoost, atk.actionName, 3)));
            }

            // Hit count boost — melee or summon; T3 upgrades to triple
            if (!atk.isRanged || atk.isSummon)
            {
                string titlePrefix = atk.isSummon ? "Frenzy Minions" : "Double Strike";
                string titlePrefixT3 = atk.isSummon ? "Frenzy Minions x3" : "Triple Strike";
                string descSuffix = atk.isSummon ? "Minions will attack twice before despawning." : "Attack hits twice as many times.";
                string descSuffixT3 = atk.isSummon ? "Minions will attack three times before despawning." : "Attack hits three times as many times.";

                if (tier < 3)
                    pool.Add(RewardChoice.FromAttack(new AttackUpgrade(
                        $"{titlePrefix}: {atk.actionName}",
                        $"{atk.actionName} {descSuffix}",
                        AttackUpgradeType.HitCountBoost, atk.actionName, 2)));
                else
                    pool.Add(RewardChoice.FromAttack(new AttackUpgrade(
                        $"{titlePrefixT3}: {atk.actionName}",
                        $"{atk.actionName} {descSuffixT3}",
                        AttackUpgradeType.HitCountBoost, atk.actionName, 3)));
            }
        }

        // Unlock new specials from each unit's unlockableAttacks list
        foreach (var unit in playerUnits)
        {
            if (unit == null || unit.unlockableAttacks == null) continue;
            foreach (var learnable in unit.unlockableAttacks)
            {
                if (learnable == null) continue;
                bool alreadyKnown = unit.availableAttacks.Any(a => a.actionName == learnable.actionName)
                                 || (unit.basicAttack != null && unit.basicAttack.actionName == learnable.actionName);
                if (!alreadyKnown)
                    pool.Add(RewardChoice.FromAttack(new AttackUpgrade(
                        $"Learn: {learnable.actionName}",
                        $"Permanently unlock {learnable.actionName} for {unit.unitName}.",
                        AttackUpgradeType.UnlockNewAttack, string.Empty, 0, learnable)));
            }
        }

        // ── Passive trait rewards (max 2 per run) ──────────────────────────────
        if (Instance != null)
        {
            if (TraitAcquisitionCount(UpgradeType.Vampiric) < 2)
                pool.Add(RewardChoice.FromStat(new RunUpgrade(
                    "Vampiric",
                    "Your attacks heal you for 25% of damage dealt.",
                    UpgradeType.Vampiric, 0)));
            if (TraitAcquisitionCount(UpgradeType.Thorns) < 2)
                pool.Add(RewardChoice.FromStat(new RunUpgrade(
                    "Thorns",
                    "Reflect 50% of any damage you receive back at the attacker.",
                    UpgradeType.Thorns, 0)));
            if (TraitAcquisitionCount(UpgradeType.Regenerating) < 2)
                pool.Add(RewardChoice.FromStat(new RunUpgrade(
                    "Regenerating",
                    "Heal 8% of your max HP at the start of each of your turns.",
                    UpgradeType.Regenerating, 0)));
            if (TraitAcquisitionCount(UpgradeType.Enraged) < 2)
                pool.Add(RewardChoice.FromStat(new RunUpgrade(
                    "Enraged",
                    "Deal double damage while below 50% HP.",
                    UpgradeType.Enraged, 0)));

            bool hasSpawningAbility = false;
            foreach (var unit in playerUnits)
            {
                if (unit == null) continue;
                if (unit.basicAttack != null && unit.basicAttack.isSummon) hasSpawningAbility = true;
                if (unit.availableAttacks != null && unit.availableAttacks.Any(a => a != null && a.isSummon)) hasSpawningAbility = true;
                if (unit.specialAttacks != null && unit.specialAttacks.Any(a => a != null && a.isSummon)) hasSpawningAbility = true;
                if (unit.unlockableAttacks != null && unit.unlockableAttacks.Any(a => a != null && a.isSummon)) hasSpawningAbility = true;
            }

            if (hasSpawningAbility && TraitAcquisitionCount(UpgradeType.BonusSummons) < 3)
                pool.Add(RewardChoice.FromStat(new RunUpgrade(
                    "More Minions",
                    "Summon one additional skeleton minion whenever you use a summoning attack.",
                    UpgradeType.BonusSummons, 1)));
        }

        // ── Curse cards (T2+) ────────────────────────────────────────────────────
        // Powerful benefit paired with a permanent drawback — risk/reward picks.
        if (tier >= 2)
        {
            pool.Add(RewardChoice.FromCurse(
                "Iron Rage",
                $"+{(tier == 2 ? 30 : 45)} ATK, but −{(tier == 2 ? 20 : 30)} DEF. Power at a cost.",
                new RunUpgrade("Iron Rage (ATK)", "Bonus ATK from Iron Rage.", UpgradeType.Attack, tier == 2 ? 30 : 45),
                new RunUpgrade("Iron Rage (DEF)", "Penalty DEF from Iron Rage.", UpgradeType.Defense, tier == 2 ? -20 : -30)));

            pool.Add(RewardChoice.FromCurse(
                "Frail Colossus",
                $"+{(tier == 2 ? 60 : 90)} Max HP, but −{(tier == 2 ? 15 : 22)} ATK. More bulk, less bite.",
                new RunUpgrade("Frail Colossus (HP)", "Bonus HP from Frail Colossus.", UpgradeType.MaxHealth, tier == 2 ? 60 : 90),
                new RunUpgrade("Frail Colossus (ATK)", "Penalty ATK from Frail Colossus.", UpgradeType.Attack, tier == 2 ? -15 : -22)));

            pool.Add(RewardChoice.FromCurse(
                "Glass Canon",
                $"+{(tier == 2 ? 25 : 38)} ATK, but −{(tier == 2 ? 25 : 38)} Max HP. High risk, high reward.",
                new RunUpgrade("Glass Canon (ATK)", "Bonus ATK from Glass Canon.", UpgradeType.Attack, tier == 2 ? 25 : 38),
                new RunUpgrade("Glass Canon (HP)", "Penalty HP from Glass Canon.", UpgradeType.MaxHealth, tier == 2 ? -25 : -38)));

            pool.Add(RewardChoice.FromCurse(
                "Speed Demon",
                $"+{(tier == 2 ? 20 : 30)} SPD, but −{(tier == 2 ? 18 : 26)} DEF. Strike first, but you're exposed.",
                new RunUpgrade("Speed Demon (SPD)", "Bonus SPD from Speed Demon.", UpgradeType.Speed, tier == 2 ? 20 : 30),
                new RunUpgrade("Speed Demon (DEF)", "Penalty DEF from Speed Demon.", UpgradeType.Defense, tier == 2 ? -18 : -26)));
        }

        // Lucky Find — roll against average team luck before shuffling
        var livingPlayers = playerUnits.Where(u => u != null && !u.isDead).ToList();
        if (livingPlayers.Count > 0)
        {
            int avgLuck = Mathf.RoundToInt((float)livingPlayers.Sum(u => u.luck) / livingPlayers.Count);
            if (UnityEngine.Random.Range(0, 100) < avgLuck)
            {
                int luckGain = tier == 1 ? 3 : tier == 2 ? 4 : 5;
                pool.Add(RewardChoice.FromStat(new RunUpgrade(
                    "Lucky Find!",
                    $"Fortune smiles upon you — +{luckGain} Luck (improves crit chance and the odds of future lucky finds).",
                    UpgradeType.Luck, luckGain)));
            }
        }

        // Boss encounters: offer ONLY relic rewards while any unit still lacks a relic.
        // Once every eligible unit already has a relic, fall through to normal rewards.
        var result = new List<RewardChoice>();
        if (encounter % 10 == 0 && Instance != null)
        {
            var relicChoices = GenerateRelicChoices(playerUnits);
            if (relicChoices.Count > 0)
                return relicChoices;   // relic-only screen
        }

        // Guarantee at least one "Learn" option if available (highly requested)
        var learnOptions = pool.Where(p => p.attackUpgrade != null && p.attackUpgrade.upgradeType == AttackUpgradeType.UnlockNewAttack).ToList();
        if (learnOptions.Count > 0)
        {
            var chosenLearn = learnOptions[UnityEngine.Random.Range(0, learnOptions.Count)];
            result.Add(chosenLearn);
            pool.Remove(chosenLearn);
        }

        // Weighted random selection for remaining pool items
        // Rewards for attacks that the player is already "building" (has upgraded before) get higher weight
        int remaining = count - result.Count;
        while (remaining > 0 && pool.Count > 0)
        {
            var ticketPool = new List<(RewardChoice choice, int weight)>();
            int totalWeight = 0;

            foreach (var choice in pool)
            {
                int w = 10; // Base weight (10 tickets) for attack upgrades without prior investment

                if (choice.attackUpgrade != null && !string.IsNullOrEmpty(choice.attackUpgrade.targetAttackName) && Instance != null)
                {
                    int priorUpgrades = Instance._appliedAttackUpgrades.Count(u => u.targetAttackName == choice.attackUpgrade.targetAttackName);
                    // Add 30 weight (3x base chance) per prior upgrade to that specific move
                    w += priorUpgrades * 30;
                }
                else if (choice.curseEffect != null)
                {
                    // Curses are rare and risky, give them a high base weight so they appear consistently
                    w = 40;
                }
                else if (choice.statUpgrade != null)
                {
                    // Stat upgrades and passives get a higher base weight to compete with the sheer volume of attack upgrades
                    w = 30;
                }

                ticketPool.Add((choice, w));
                totalWeight += w;
            }

            int rnd = UnityEngine.Random.Range(0, totalWeight);
            int cumulative = 0;
            RewardChoice selected = null;

            foreach (var ticket in ticketPool)
            {
                cumulative += ticket.weight;
                if (rnd < cumulative)
                {
                    selected = ticket.choice;
                    break;
                }
            }

            // Fallback in case of logic skipping
            if (selected == null && ticketPool.Count > 0)
                selected = ticketPool[ticketPool.Count - 1].choice;

            if (selected != null)
            {
                result.Add(selected);
                pool.Remove(selected);
                remaining--;
            }
        }
        return result;
    }

    /// <summary>Produces one relic RewardChoice per eligible player (no relic yet), loaded from Resources/Relics.</summary>
    private static List<RewardChoice> GenerateRelicChoices(List<Unit> playerUnits)
    {
        var allRelics = Resources.LoadAll<LegendaryRelicSO>("Relics");
        var choices = new List<RewardChoice>();
        var usedRelics = new HashSet<string>();

        foreach (var unit in playerUnits)
        {
            if (unit == null) continue;
            if (Instance.HasRelic(unit.unitName)) continue;

            // Prefer character-specific relics; fall back to universals
            // Also exclude any relic whose traits would push past the 2-per-run cap.
            var eligible = System.Array.FindAll(allRelics, r =>
                !usedRelics.Contains(r.relicName) &&
                (r.targetCharacterName == unit.unitName || r.targetCharacterName == "Any") &&
                !r.statBoosts.Any(b => IsTraitType(b.upgradeType) && TraitAcquisitionCount(b.upgradeType) >= 2));

            if (eligible.Length == 0) continue;

            LegendaryRelicSO chosen = eligible[UnityEngine.Random.Range(0, eligible.Length)];
            usedRelics.Add(chosen.relicName);
            choices.Add(RewardChoice.FromRelic(chosen, unit.unitName));
        }

        return choices;
    }

    /// <summary>
    /// Generates merchant shop choices with gold costs. Same pool as GenerateRewardChoices but
    /// each card is priced at 50-80% of the current encounter's gold reward.
    /// </summary>
    public static List<RewardChoice> GenerateMerchantChoices(List<Unit> playerUnits, int encounter, int count = 3)
    {
        var choices = GenerateRewardChoices(playerUnits, encounter, count);
        int baseGold = Mathf.RoundToInt(20f * Mathf.Pow(1.35f, encounter - 1));
        foreach (var choice in choices)
            choice.goldCost = Mathf.Max(5, Mathf.RoundToInt(baseGold * UnityEngine.Random.Range(0.5f, 0.85f)));
        return choices;
    }

    /// <summary>How many times a trait has been acquired this run (from upgrades + relic stat boosts).</summary>
    public static int TraitAcquisitionCount(UpgradeType t)
    {
        if (Instance == null) return 0;
        int fromUpgrades = Instance._appliedUpgrades.Count(u => u.upgradeType == t);
        int fromRelics = Instance._equippedRelics.Values
            .Sum(r => r.statBoosts != null ? r.statBoosts.Count(b => b.upgradeType == t) : 0);
        return fromUpgrades + fromRelics;
    }

    private static bool IsTraitType(UpgradeType t) =>
        t == UpgradeType.Vampiric || t == UpgradeType.Thorns ||
        t == UpgradeType.Regenerating || t == UpgradeType.Enraged;
}

[Serializable]
public class RunStatistics
{
    public string characterName;
    public int finalEncounter;
    public int finalGold;
    public int totalDamageDealt;
    public int totalDamageTaken;
    public int healingDone;
    public int enemiesDefeated;
    public int criticalHitsLanded;
    public int turnsSurvived;

    public RunStatistics(string name)
    {
        characterName = string.IsNullOrEmpty(name) ? "Unknown Hero" : name;
    }

    public void AddPlayerDamageDealt(int amount) { totalDamageDealt += amount; }
    public void AddPlayerDamageTaken(int amount) { totalDamageTaken += amount; }
    public void AddPlayerHealing(int amount) { healingDone += amount; }
    public void AddEnemyDefeated() { enemiesDefeated++; }
    public void AddPlayerCrit() { criticalHitsLanded++; }
    public void AddTurnSurvived() { turnsSurvived++; }
}
