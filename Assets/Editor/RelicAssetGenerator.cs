#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot editor utility that creates the 6 legendary relic attack assets and
/// the 6 legendary relic ScriptableObjects. Run via RPG > Generate Relic Assets.
/// Existing assets at the target paths are skipped (never overwritten).
/// After generation, assign visual prefab slots on each relic SO in the Inspector.
/// </summary>
public static class RelicAssetGenerator
{
    [MenuItem("RPG/Generate Relic Assets")]
    public static void GenerateAll()
    {
        EnsureFolder("Assets/Attacks");
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/Relics");

        // ── Attack Actions ────────────────────────────────────────────────────
        // args: fileName, actionName, minDmg, maxDmg, mana, isRanged, isAoE, hitCount, delayBetweenHits
        AttackAction royalEdict   = CreateAttackAction("Relic_Knight_RoyalEdict",       "Royal Edict",    40, 65, 8, false, false, 1, 1.0f);
        AttackAction sacredWrath  = CreateAttackAction("Relic_Knight_SacredWrath",      "Sacred Wrath",   25, 40, 5, false, true,  1, 1.0f);
        AttackAction deathSpiral  = CreateAttackAction("Relic_Ninja_DeathsSpiral",      "Death's Spiral", 12, 22, 6, false, true,  5, 0.2f);
        AttackAction shadowGate   = CreateAttackAction("Relic_Ninja_ShadowGate",        "Shadow Gate",    50, 75, 8, true,  false, 1, 1.0f);
        AttackAction chaosBurst   = CreateAttackAction("Relic_Universal_ChaosBurst",    "Chaos Burst",    20, 50, 5, false, true,  1, 1.0f);
        AttackAction undyingWrath = CreateAttackAction("Relic_Universal_UndyingWrath",  "Undying Wrath",  35, 55, 4, false, false, 3, 0.25f);

        AssetDatabase.SaveAssets();

        // ── Relic ScriptableObjects ───────────────────────────────────────────
        CreateRelicSO(
            "Knight_BladeOfTheFallenKing",
            "Blade of the Fallen King",
            "Knight",
            "An ancient sword radiating crimson energy. Those who wield it are consumed by battle-lust.",
            "\"The king fell. The blade did not.\"",
            new List<RunUpgrade>
            {
                new RunUpgrade("+20 Attack",  "+20 Attack.",          UpgradeType.Attack,    20),
                new RunUpgrade("+15 Max HP",  "+15 Max HP.",          UpgradeType.MaxHealth, 15),
                new RunUpgrade("Enraged",     "Deal double damage below 50% HP.", UpgradeType.Enraged, 0),
            },
            royalEdict);

        CreateRelicSO(
            "Knight_InquisitorsAegis",
            "Inquisitor's Aegis",
            "Knight",
            "A holy shield of golden alloy. Enemies who strike it are scorched, and its bearer mends each turn.",
            "\"Faith made manifest. Impenetrable.\"",
            new List<RunUpgrade>
            {
                new RunUpgrade("+25 Defense",  "+25 Defense.",      UpgradeType.Defense,      25),
                new RunUpgrade("+30 Max HP",   "+30 Max HP.",       UpgradeType.MaxHealth,    30),
                new RunUpgrade("Thorns",       "Reflect 50% of incoming damage.", UpgradeType.Thorns,       0),
                new RunUpgrade("Regeneration", "Heal 8% Max HP per turn.",        UpgradeType.Regenerating, 0),
            },
            sacredWrath);

        CreateRelicSO(
            "Ninja_PhantomKusarigama",
            "Phantom Kusarigama",
            "Ninja",
            "A spirit-forged chain-sickle that drinks the life-force of every foe it lacerates.",
            "\"Strike fast. Drink deep.\"",
            new List<RunUpgrade>
            {
                new RunUpgrade("+20 Attack", "+20 Attack.", UpgradeType.Attack,   20),
                new RunUpgrade("+15 Speed",  "+15 Speed.",  UpgradeType.Speed,    15),
                new RunUpgrade("Vampiric",   "Heal 25% of damage dealt.", UpgradeType.Vampiric, 0),
            },
            deathSpiral);

        CreateRelicSO(
            "Ninja_OniMaskOfTheVoid",
            "Oni Mask of the Void",
            "Ninja",
            "The mask of a defeated shadow demon. Through it, the wearer glimpses fate itself.",
            "\"The oni stares. Luck bends.\"",
            new List<RunUpgrade>
            {
                new RunUpgrade("+10 Attack", "+10 Attack.", UpgradeType.Attack,  10),
                new RunUpgrade("+20 Speed",  "+20 Speed.",  UpgradeType.Speed,   20),
                new RunUpgrade("+10 Luck",   "+10 Luck.",   UpgradeType.Luck,    10),
            },
            shadowGate);

        CreateRelicSO(
            "Universal_HeartOfChaos",
            "Heart of Chaos",
            "Any",
            "A crystallised fragment of pure entropy. It grants everything — and demands everything.",
            "\"Power without order. Order without power. Both.\"",
            new List<RunUpgrade>
            {
                new RunUpgrade("+10 Attack",   "+10 Attack.",   UpgradeType.Attack,       10),
                new RunUpgrade("+10 Defense",  "+10 Defense.",  UpgradeType.Defense,      10),
                new RunUpgrade("+10 Speed",    "+10 Speed.",    UpgradeType.Speed,        10),
                new RunUpgrade("+20 Max HP",   "+20 Max HP.",   UpgradeType.MaxHealth,    20),
                new RunUpgrade("+5 Max Mana",  "+5 Max Mana.",  UpgradeType.MaxMana,       5),
                new RunUpgrade("Vampiric",     "Heal 25% of damage dealt.",    UpgradeType.Vampiric,     0),
                new RunUpgrade("Thorns",       "Reflect 50% incoming damage.", UpgradeType.Thorns,       0),
                new RunUpgrade("Regeneration", "Heal 8% Max HP per turn.",     UpgradeType.Regenerating, 0),
                new RunUpgrade("Enraged",      "Double damage below 50% HP.",  UpgradeType.Enraged,      0),
            },
            chaosBurst);

        CreateRelicSO(
            "Universal_CrownOfTheUndying",
            "Crown of the Undying",
            "Any",
            "A tarnished crown wrested from a death-god. The wearer cannot be felled easily.",
            "\"Death tried. It failed.\"",
            new List<RunUpgrade>
            {
                new RunUpgrade("+50 Max HP",   "+50 Max HP.",   UpgradeType.MaxHealth,    50),
                new RunUpgrade("+15 Defense",  "+15 Defense.",  UpgradeType.Defense,      15),
                new RunUpgrade("Regeneration", "Heal 8% Max HP per turn.", UpgradeType.Regenerating, 0),
            },
            undyingWrath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[RelicAssetGenerator] All relic assets generated successfully.");
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static AttackAction CreateAttackAction(
        string fileName, string actionName,
        int minDmg, int maxDmg, int manaCost,
        bool isRanged, bool isAoE, int hitCount, float delayBetweenHits)
    {
        string path = $"Assets/Attacks/{fileName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<AttackAction>(path);
        if (existing != null)
        {
            Debug.Log($"[RelicAssetGenerator] Attack already exists, skipping: {path}");
            return existing;
        }

        var attack = ScriptableObject.CreateInstance<AttackAction>();
        attack.actionName       = actionName;
        attack.minDamage        = minDmg;
        attack.maxDamage        = maxDmg;
        attack.manaCost         = manaCost;
        attack.isRanged         = isRanged;
        attack.isAoE            = isAoE;
        attack.hitCount         = hitCount;
        attack.delayBetweenHits = delayBetweenHits;
        attack.hitDelay         = 0.3f;
        attack.finishDelay      = 0.5f;
        // animationName left empty — assign in Inspector to match your character's animator triggers.

        AssetDatabase.CreateAsset(attack, path);
        Debug.Log($"[RelicAssetGenerator] Created attack: {path}");
        return attack;
    }

    private static void CreateRelicSO(
        string fileName, string relicName, string targetCharacter,
        string description, string flavorText,
        List<RunUpgrade> statBoosts, AttackAction grantedAttack)
    {
        string path = $"Assets/Resources/Relics/{fileName}.asset";
        if (AssetDatabase.LoadAssetAtPath<LegendaryRelicSO>(path) != null)
        {
            Debug.Log($"[RelicAssetGenerator] Relic already exists, skipping: {path}");
            return;
        }

        var relic = ScriptableObject.CreateInstance<LegendaryRelicSO>();
        relic.relicName           = relicName;
        relic.targetCharacterName = targetCharacter;
        relic.description         = description;
        relic.flavorText          = flavorText;
        relic.statBoosts          = statBoosts;
        relic.grantedAttack       = grantedAttack;
        // relic.visuals and relic.icon left empty — assign weapon/armour prefabs in the Inspector.

        AssetDatabase.CreateAsset(relic, path);
        Debug.Log($"[RelicAssetGenerator] Created relic: {path}");
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
            string folder = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
#endif
