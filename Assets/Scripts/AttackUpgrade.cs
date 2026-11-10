using System;
using System.Collections.Generic;
using UnityEngine;

public enum AttackUpgradeType
{
    DamageBoost,          // +min/max damage on an existing attack
    ManaCostReduce,       // reduce mana cost of an existing attack
    ProjectileCountBoost, // +1 projectile (ranged attacks only)
    AoEUnlock,            // convert a single-target attack to AoE
    SpreadAngleBoost,     // Fusillade: +value damage per current projectile count (ranged multi-projectile only; scales with Barrage)
    UnlockNewAttack,      // add a new AttackAction to the unit's available attacks
    HitCountBoost,        // ×N hits per swing (melee only); T1-2 = ×2, T3 = ×3
    SizeMultiply,         // increases projectile size and damage
}

[Serializable]
public class AttackUpgrade
{
    public string name;
    public string description;
    public AttackUpgradeType upgradeType;

    /// <summary>Matches AttackAction.actionName for enhancement upgrades. Empty for UnlockNewAttack.</summary>
    public string targetAttackName;

    /// <summary>Numeric delta used by DamageBoost, ManaCostReduce, ProjectileCountBoost, SpreadAngleBoost.</summary>
    public int value;

    /// <summary>The AttackAction asset to clone and add for UnlockNewAttack rewards.</summary>
    public AttackAction attackToUnlock;

    public AttackUpgrade(string name, string description, AttackUpgradeType upgradeType,
                         string targetAttackName, int value, AttackAction attackToUnlock = null)
    {
        this.name = name;
        this.description = description;
        this.upgradeType = upgradeType;
        this.targetAttackName = targetAttackName;
        this.value = value;
        this.attackToUnlock = attackToUnlock;
    }
}

/// <summary>
/// Unified reward choice passed to the intermission UI. Exactly one of
/// <see cref="statUpgrade"/> or <see cref="attackUpgrade"/> is non-null.
/// </summary>
public class RewardChoice
{
    public string name;
    public string description;
    public RunUpgrade statUpgrade;
    public AttackUpgrade attackUpgrade;
    /// <summary>Non-null on curse cards: applied in addition to statUpgrade. Always has a negative value.</summary>
    public RunUpgrade curseEffect;

    /// <summary>Non-null for legendary relic rewards. statUpgrade and attackUpgrade will both be null.</summary>
    public LegendaryRelicSO legendaryRelic;
    /// <summary>The unit.unitName this relic is offered to. Used by RunManager.EquipRelic.</summary>
    public string targetUnitName;

    public static RewardChoice FromStat(RunUpgrade upgrade) =>
        new RewardChoice
        {
            name = upgrade.name,
            description = upgrade.description,
            statUpgrade = upgrade,
            attackUpgrade = null,
        };

    public static RewardChoice FromAttack(AttackUpgrade upgrade) =>
        new RewardChoice
        {
            name = upgrade.name,
            description = upgrade.description,
            statUpgrade = null,
            attackUpgrade = upgrade,
        };

    /// <summary>Creates a curse card: <paramref name="mainUpgrade"/> is the benefit,
    /// <paramref name="curse"/> is the drawback (negative value).</summary>
    public static RewardChoice FromCurse(string name, string description, RunUpgrade mainUpgrade, RunUpgrade curse) =>
        new RewardChoice
        {
            name = name,
            description = description,
            statUpgrade = mainUpgrade,
            attackUpgrade = null,
            curseEffect = curse,
        };

    /// <summary>Non-null for supply rewards: adds <see cref="itemCount"/> copies of this potion to the run's pack.</summary>
    public BattleItem battleItem;
    public int itemCount = 1;

    public static RewardChoice FromItem(BattleItem item, int count) =>
        new RewardChoice
        {
            name = $"{count}x {item.itemName}",
            description = item.itemType == ItemType.HealthPotion
                ? $"Restores {item.potency} HP when used from the Item command. Potions are kept until you use them."
                : $"Restores {item.potency} MP when used from the Item command. Potions are kept until you use them.",
            battleItem = item,
            itemCount = count,
        };

    /// <summary>Gold cost for merchant purchases. Zero means free (standard reward).</summary>
    public int goldCost = 0;

    /// <summary>Creates a legendary relic reward card for the given unit.</summary>
    public static RewardChoice FromRelic(LegendaryRelicSO relic, string unitName) =>
        new RewardChoice
        {
            name = relic.relicName,
            description = relic.description,
            legendaryRelic = relic,
            targetUnitName = unitName,
        };
}
