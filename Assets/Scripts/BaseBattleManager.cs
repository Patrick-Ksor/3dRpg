using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

public enum BattleState { START, PLAYERTURN, ENEMYTURN, BUSY, WON, LOST }

public enum EnemyAffixType { Armored, Berserker, Swift, Bulwark, Regenerating, Thorns, Vampiric, Enraged }

public class EnemyAffix
{
    public string name;
    public string description;
    public EnemyAffixType type;
    public float value;

    public EnemyAffix(string name, string description, EnemyAffixType type, float value)
    {
        this.name = name; this.description = description; this.type = type; this.value = value;
    }
}

public class BaseBattleManager : MonoBehaviour
{
    public BattleState state;

    [Header("Combatants")]
    public List<GameObject> playerPrefabs = new List<GameObject>();

    [Header("Enemy Pool")]
    [Tooltip("Tier 1 enemies (encounters 1-3): basic enemies like Skeleton, SkeletonMinion.")]
    [UnityEngine.Serialization.FormerlySerializedAs("possibleEnemies")]
    public List<GameObject> tier1Enemies = new List<GameObject>();
    [Tooltip("Tier 2 enemies (encounters 4-6): tougher enemies like Knight.")]
    public List<GameObject> tier2Enemies = new List<GameObject>();
    [Tooltip("Tier 3 enemies (encounters 7+): hardest enemies like Ninja.")]
    public List<GameObject> tier3Enemies = new List<GameObject>();
    [Tooltip("Boss prefabs — used every 10th encounter. Falls back to the highest available tier pool if empty.")]
    public List<GameObject> bossPrefabs = new List<GameObject>();
    [Tooltip("Stat multiplier applied on top of normal encounter scaling for boss enemies.")]
    public float bossStatMultiplier = 1.25f;
    [Tooltip("How many non-boss enemies spawn at the start of each encounter (before scaling adds extras).")]
    [Min(1)] public int baseEnemyCount = 1;

    [Header("VFX & Audio")]
    public GameObject impactVfxPrefab;
    public AudioClip impactSoundEffect;

    [Header("Trait Aura Prefabs")]
    [Tooltip("Persistent aura shown on units with the Vampiric trait.")]
    public GameObject vampiricAuraPrefab;
    [Tooltip("Persistent aura shown on units with the Thorns trait.")]
    public GameObject thornsAuraPrefab;
    [Tooltip("Persistent aura shown on units with the Regenerating trait.")]
    public GameObject regenAuraPrefab;
    [Tooltip("Aura shown on Enraged units when their HP drops below 50%.")]
    public GameObject enragedAuraPrefab;

    public List<Unit> playerTeam = new List<Unit>();
    public List<Unit> enemyTeam = new List<Unit>();

    private List<Unit> _turnQueue = new List<Unit>();
    private Unit _activeUnit;
    private Dictionary<Unit, List<EnemyAffix>> _enemyAffixes = new Dictionary<Unit, List<EnemyAffix>>();
    private Dictionary<GameObject, Queue<GameObject>> _minionPools = new Dictionary<GameObject, Queue<GameObject>>();

    private GameObject GetFromPool(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        if (_minionPools.TryGetValue(prefab, out Queue<GameObject> pool) && pool.Count > 0)
        {
            GameObject obj = pool.Dequeue();
            if (obj != null)
            {
                obj.transform.position = position;
                obj.transform.rotation = rotation;
                obj.SetActive(true);
                return obj;
            }
        }

        return Instantiate(prefab, position, rotation);
    }

    private void ReturnToPool(GameObject prefab, GameObject obj)
    {
        if (obj == null) return;
        obj.SetActive(false);
        if (!_minionPools.ContainsKey(prefab))
        {
            _minionPools[prefab] = new Queue<GameObject>();
        }
        _minionPools[prefab].Enqueue(obj);
    }

    private static readonly List<EnemyAffix> _affixPool = new List<EnemyAffix>
    {
        new EnemyAffix("Armored",      "Defense ×1.5.",                   EnemyAffixType.Armored,      1.5f),
        new EnemyAffix("Berserker",    "Attack ×1.5.",                    EnemyAffixType.Berserker,    1.5f),
        new EnemyAffix("Swift",        "Speed +10.",                      EnemyAffixType.Swift,        10f),
        new EnemyAffix("Bulwark",      "Max HP ×1.4.",                    EnemyAffixType.Bulwark,      1.4f),
        new EnemyAffix("Regenerating", "Restores 8% max HP each turn.",   EnemyAffixType.Regenerating, 0.08f),
        new EnemyAffix("Thorns",       "Reflects 15% of incoming damage.",EnemyAffixType.Thorns,       0.15f),
        new EnemyAffix("Vampiric",     "Heals 25% of damage dealt.",      EnemyAffixType.Vampiric,     0.25f),
        new EnemyAffix("Enraged",      "Doubles attack below 50% HP.",    EnemyAffixType.Enraged,      2f),
    };

    private BattleUIManager _uiManager;
    private bool _isMiniBoss = false;

    public static BaseBattleManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        // Allow hard reset at any time for quick testing
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    public void Initialize(BattleUIManager uiManager)
    {
        _uiManager = uiManager;
        state = BattleState.START;
        StartCoroutine(SetupBattle());
    }

    private IEnumerator SetupBattle()
    {
        RunManager rm = RunManager.Instance;
        _enemyAffixes.Clear();
        _isMiniBoss = false;
        int encounterTier = (rm != null && rm.IsRunActive) ? RunManager.GetEncounterTier(rm.CurrentEncounter) : 1;
        bool isBossEncounter = rm != null && rm.IsRunActive && rm.CurrentEncounter % 10 == 0;

        // Re-tint the arena (torches, fog, sun) for this encounter's tier / boss status
        ArenaController.Instance?.Apply(
            rm != null && rm.IsRunActive ? rm.CurrentEncounter : 1,
            isBossEncounter,
            rm != null && rm.IsMiniBossEncounter);

        // Decide which prefabs to use
        var activePlayerPrefabs = new List<GameObject>();
        if (rm != null && rm.SelectedCharacterPrefab != null)
        {
            activePlayerPrefabs.Add(rm.SelectedCharacterPrefab);
        }
        else
        {
            activePlayerPrefabs.AddRange(playerPrefabs);
        }

        // Setup Players based on assigned prefabs in the Inspector or RunManager
        for (int i = 0; i < activePlayerPrefabs.Count; i++)
        {
            if (activePlayerPrefabs[i] == null) continue;
            GameObject playerGO = Instantiate(activePlayerPrefabs[i]);
            playerGO.name = activePlayerPrefabs[i].name + "_" + i;

            Unit pUnit = playerGO.GetComponent<Unit>();
            if (pUnit == null) pUnit = playerGO.AddComponent<Unit>();

            pUnit.unitName = activePlayerPrefabs[i].name;
            pUnit.isPlayer = true;
            if (pUnit.speed == 0) pUnit.speed = Random.Range(10, 20);

            // Apply all in-run permanent upgrades to the fresh instance
            if (rm != null && rm.IsRunActive)
            {
                rm.ApplyUpgradesToUnit(pUnit);
                rm.ApplyAttackUpgradesToUnit(pUnit);
                rm.ApplyRelicToUnit(pUnit);
                ApplyRelicVisuals(playerGO, pUnit.unitName);
            }
            // Use RunManager counts so a 2× acquired trait spawns two stacked aura instances
            RunManager prm = RunManager.Instance;
            ApplyTraitAuras(pUnit,
                prm != null ? RunManager.TraitAcquisitionCount(UpgradeType.Vampiric) : (pUnit.hasVampiric ? 1 : 0),
                prm != null ? RunManager.TraitAcquisitionCount(UpgradeType.Thorns) : (pUnit.hasThorns ? 1 : 0),
                prm != null ? RunManager.TraitAcquisitionCount(UpgradeType.Regenerating) : (pUnit.hasRegeneration ? 1 : 0),
                prm != null ? RunManager.TraitAcquisitionCount(UpgradeType.Enraged) : (pUnit.hasEnraged ? 1 : 0));

            // Restore to (possibly upgraded) maximums
            pUnit.currentHealth = pUnit.maxHealth;
            pUnit.currentMana = pUnit.maxMana;

            playerGO.transform.position = new Vector3(-2.5f - (i * 0.5f), 0, -1.5f + (i * 1.5f));
            playerGO.transform.rotation = Quaternion.Euler(0, 90, 0);
            playerGO.AddComponent<FloatingHealthBar>().Init(pUnit, true);
            playerTeam.Add(pUnit);
        }

        // Before a boss encounter, fully restore the player team
        if (isBossEncounter)
        {
            foreach (var p in playerTeam)
            {
                p.currentHealth = p.maxHealth;
                p.currentMana = p.maxMana;
            }
            _uiManager.SetMessage("A deadly presence looms ahead… Your party braces themselves.");
            yield return new WaitForSeconds(1.5f);
        }

        // Event encounters skip enemy spawning entirely
        bool isEventEncounter = !isBossEncounter && rm != null && rm.IsRunActive
                                && rm.CurrentEncounterType != EncounterType.Combat;
        if (isEventEncounter)
        {
            _uiManager.SetupHealthBars(playerTeam);
            _uiManager.UpdateRunHud(rm.CurrentEncounter, rm.Gold);
            yield return StartCoroutine(StartEventEncounter());
            yield break;
        }

        // Spawn enemies — tiered pool, boss every 10th encounter
        if (isBossEncounter)
        {
            // Boss encounter: one powerful boss with 3 forced affixes
            List<GameObject> bossPool = bossPrefabs.Count > 0 ? bossPrefabs : GetEnemyPoolForTier(encounterTier);
            if (bossPool.Count > 0)
            {
                GameObject srcPrefab = bossPool[Random.Range(0, bossPool.Count)];
                GameObject bossGO = Instantiate(srcPrefab);
                bossGO.name = "Enemy_Boss";

                Unit bUnit = bossGO.GetComponent<Unit>();
                if (bUnit == null) bUnit = bossGO.AddComponent<Unit>();
                bUnit.isPlayer = false;
                if (bUnit.speed == 0) bUnit.speed = Random.Range(8, 18);

                bUnit.maxHealth = Mathf.RoundToInt(bUnit.maxHealth * rm.GetEnemyHpScalar() * bossStatMultiplier);
                bUnit.attack = Mathf.RoundToInt(bUnit.attack * rm.GetEnemyAtkScalar() * bossStatMultiplier);
                bUnit.defense = Mathf.RoundToInt(bUnit.defense * rm.GetEnemyDefScalar() * bossStatMultiplier);
                bUnit.speed += rm.GetEnemySpeedBonus() + 5;
                bUnit.currentHealth = bUnit.maxHealth;

                var affixes = RollAffixes(encounterTier, false, true);
                ApplyAffixesToUnit(bUnit, affixes);
                _enemyAffixes[bUnit] = affixes;
                ApplyTraitAuras(bUnit,
                    affixes.Exists(a => a.type == EnemyAffixType.Vampiric) ? 1 : 0,
                    affixes.Exists(a => a.type == EnemyAffixType.Thorns) ? 1 : 0,
                    affixes.Exists(a => a.type == EnemyAffixType.Regenerating) ? 1 : 0,
                    affixes.Exists(a => a.type == EnemyAffixType.Enraged) ? 1 : 0);

                bUnit.unitName = $"☆ BOSS {srcPrefab.name}";

                bossGO.transform.position = new Vector3(2.5f, 0, 0f);
                Vector3 bossFaceDir = new Vector3(-2.5f, 0, -1.5f) - bossGO.transform.position; bossFaceDir.y = 0;
                bossGO.transform.rotation = Quaternion.LookRotation(bossFaceDir);
                var bossHpBar = bossGO.AddComponent<FloatingHealthBar>();
                bossHpBar.Init(bUnit, false);
                bossHpBar.SetAffixes(affixes);
                enemyTeam.Add(bUnit);
            }
        }
        else
        {
            // Check for mini-boss (E5, E15, E25... every 5th that isn't a boss)
            bool isMiniBossEncounter = rm != null && rm.IsMiniBossEncounter;

            if (isMiniBossEncounter)
            {
                _isMiniBoss = true;
                // Pick from the next tier up to make it feel more threatening
                int miniBossTier = Mathf.Min(encounterTier + 1, 3);
                List<GameObject> mbPool = GetEnemyPoolForTier(miniBossTier);
                if (mbPool.Count == 0) mbPool = GetEnemyPoolForTier(encounterTier);

                if (mbPool.Count > 0)
                {
                    GameObject srcPrefab = mbPool[Random.Range(0, mbPool.Count)];
                    GameObject mbGO = Instantiate(srcPrefab);
                    mbGO.name = "Enemy_MiniBoss";

                    Unit mbUnit = mbGO.GetComponent<Unit>();
                    if (mbUnit == null) mbUnit = mbGO.AddComponent<Unit>();
                    mbUnit.isPlayer = false;
                    if (mbUnit.speed == 0) mbUnit.speed = Random.Range(8, 18);

                    if (rm != null && rm.IsRunActive)
                    {
                        mbUnit.maxHealth = Mathf.RoundToInt(mbUnit.maxHealth * rm.GetEnemyHpScalar() * 1.5f);
                        mbUnit.attack = Mathf.RoundToInt(mbUnit.attack * rm.GetEnemyAtkScalar() * 1.5f);
                        mbUnit.defense = Mathf.RoundToInt(mbUnit.defense * rm.GetEnemyDefScalar() * 1.5f);
                        mbUnit.speed += rm.GetEnemySpeedBonus() + 3;
                    }
                    mbUnit.currentHealth = mbUnit.maxHealth;

                    // Guaranteed 1 random affix
                    var miniBossAffixes = RollAffixes(encounterTier, false, false);
                    if (miniBossAffixes.Count == 0)
                        miniBossAffixes.Add(_affixPool[Random.Range(0, _affixPool.Count)]);
                    ApplyAffixesToUnit(mbUnit, miniBossAffixes);
                    _enemyAffixes[mbUnit] = miniBossAffixes;
                    ApplyTraitAuras(mbUnit,
                        miniBossAffixes.Exists(a => a.type == EnemyAffixType.Vampiric) ? 1 : 0,
                        miniBossAffixes.Exists(a => a.type == EnemyAffixType.Thorns) ? 1 : 0,
                        miniBossAffixes.Exists(a => a.type == EnemyAffixType.Regenerating) ? 1 : 0,
                        miniBossAffixes.Exists(a => a.type == EnemyAffixType.Enraged) ? 1 : 0);

                    mbUnit.unitName = $"\u26a1 MINI-BOSS {srcPrefab.name}";

                    mbGO.transform.position = new Vector3(2.5f, 0, 0f);
                    Vector3 faceDir = new Vector3(-2.5f, 0, -1.5f) - mbGO.transform.position; faceDir.y = 0;
                    mbGO.transform.rotation = Quaternion.LookRotation(faceDir);
                    var mbHpBar = mbGO.AddComponent<FloatingHealthBar>();
                    mbHpBar.Init(mbUnit, false);
                    mbHpBar.SetAffixes(miniBossAffixes);
                    enemyTeam.Add(mbUnit);
                }

                _uiManager.SetMessage("A powerful guardian blocks your path!");
                yield return new WaitForSeconds(1.2f);
            }
            else
            {
                // Normal encounter: tiered pool + elite rolls + affixes
                List<GameObject> pool = GetEnemyPoolForTier(encounterTier);

                for (int i = 0; i < baseEnemyCount; i++)
                {
                    if (pool.Count == 0) break;
                    SpawnSingleEnemy(pool[Random.Range(0, pool.Count)], encounterTier, rm);
                }

                if (rm != null && rm.IsRunActive)
                {
                    int extras = rm.GetExtraEnemyCount();
                    for (int c = 0; c < extras; c++)
                    {
                        if (pool.Count == 0) break;
                        SpawnSingleEnemy(pool[Random.Range(0, pool.Count)], encounterTier, rm);
                    }
                }

                // Cap total enemies at 5
                const int MaxEnemies = 5;
                while (enemyTeam.Count > MaxEnemies)
                {
                    int last = enemyTeam.Count - 1;
                    Destroy(enemyTeam[last].gameObject);
                    enemyTeam.RemoveAt(last);
                }
            } // end normal encounter
        }

        // Wait for units to initialize
        yield return null;

        List<Unit> allUnits = new List<Unit>();
        allUnits.AddRange(playerTeam);
        allUnits.AddRange(enemyTeam);

        _uiManager.SetupHealthBars(allUnits);

        if (rm != null && rm.IsRunActive)
        {
            _uiManager.UpdateRunHud(rm.CurrentEncounter, rm.Gold);
            _uiManager.SetMessage($"Encounter {rm.CurrentEncounter} — Fight!");
        }

        Debug.Log($"Battle setup complete. {playerTeam.Count}v{enemyTeam.Count} Brawl!");
        yield return new WaitForSeconds(1f);

        BuildTurnQueue();
        ProcessNextTurn();
    }

    private void BuildTurnQueue()
    {
        _turnQueue.Clear();
        RunManager.Instance?.CurrentRunStats?.AddTurnSurvived();
        var allAlive = playerTeam.Concat(enemyTeam).Where(u => !u.isDead).ToList();
        _turnQueue = allAlive.OrderByDescending(u => u.speed).ToList();
    }

    private void ProcessNextTurn()
    {
        if (CheckBattleOver()) return;

        if (_turnQueue.Count == 0)
        {
            BuildTurnQueue();
        }

        if (_turnQueue.Count == 0) return; // Should be handled by CheckBattleOver but just in case

        _activeUnit = _turnQueue[0];
        _turnQueue.RemoveAt(0);

        if (_activeUnit.isDead)
        {
            ProcessNextTurn();
            return;
        }

        if (_activeUnit.isPlayer)
        {
            state = BattleState.PLAYERTURN;
            StartCoroutine(PlayerTurnStart());
        }
        else
        {
            state = BattleState.ENEMYTURN;
            StartCoroutine(EnemyTurn());
        }
    }

    private IEnumerator PlayerTurnStart()
    {
        _activeUnit.isDefending = false;

        // Tick status effects at the start of this unit's turn
        var ticks = _activeUnit.TickStatusEffects();
        foreach (var tick in ticks)
        {
            _uiManager.SetMessage(tick.message);
            yield return new WaitForSeconds(0.4f);
            if (tick.type == StatusEffectType.Burn && tick.burnDamage > 0)
            {
                _activeUnit.TakeDamage(tick.burnDamage, silent: true);
                DamagePopSpawner.Show(_activeUnit.transform, tick.burnDamage, PopType.Burn);
                _uiManager.UpdateHealth(_activeUnit);
                if (_activeUnit.isDead) { CheckBattleOver(); yield break; }
            }
            else if (tick.type == StatusEffectType.Stun)
            {
                yield return new WaitForSeconds(0.4f);
                ProcessNextTurn();
                yield break;
            }
        }

        if (_activeUnit.hasRegeneration)
        {
            int regenAmt = Mathf.Max(1, Mathf.RoundToInt(_activeUnit.maxHealth * 0.08f));
            int actualRegen = Mathf.Min(_activeUnit.maxHealth - _activeUnit.currentHealth, regenAmt);
            _activeUnit.currentHealth += actualRegen;
            if (_activeUnit.isPlayer) RunManager.Instance?.CurrentRunStats?.AddPlayerHealing(actualRegen);
            _uiManager.UpdateHealth(_activeUnit);
            _activeUnit.GetComponent<TraitAuraController>()?.RefreshDynamicAuras();
            _uiManager.SetMessage($"{_activeUnit.unitName} regenerates {regenAmt} HP!");
            yield return new WaitForSeconds(0.6f);
        }
        _uiManager.SetMessage($"{_activeUnit.unitName}'s turn!");
        _uiManager.EnableAttackButton(true);
    }

    public bool IsBattleOver()
    {
        bool playerAlive = playerTeam.Any(u => !u.isDead);
        bool enemyAlive = enemyTeam.Any(u => !u.isDead);
        return !playerAlive || !enemyAlive;
    }

    private bool CheckBattleOver()
    {
        bool playerAlive = playerTeam.Any(u => !u.isDead);
        bool enemyAlive = enemyTeam.Any(u => !u.isDead);

        if (!playerAlive)
        {
            state = BattleState.LOST;
            EndBattle();
            return true;
        }
        else if (!enemyAlive)
        {
            state = BattleState.WON;
            EndBattle();
            return true;
        }
        return false;
    }

    // Called by UI Managers attack button
    public void OnAttackButton()
    {
        if (state != BattleState.PLAYERTURN)
            return;
    }

    public Unit GetActiveUnit()
    {
        return _activeUnit;
    }

    public List<Unit> GetAliveEnemies()
    {
        return enemyTeam.Where(u => !u.isDead).ToList();
    }

    public void PlayerSelectTarget(Unit target, AttackAction action)
    {
        if (state != BattleState.PLAYERTURN) return;
        state = BattleState.BUSY;
        StartCoroutine(PlayerAttack(target, action));
    }

    public void PlayerDefend()
    {
        if (state != BattleState.PLAYERTURN) return;
        state = BattleState.BUSY;
        _activeUnit.isDefending = true;
        _uiManager.SetMessage($"{_activeUnit.unitName} is defending!");
        _uiManager.EnableAttackButton(false);
        StartCoroutine(DelayThenNextTurn(1f));
    }

    public void PlayerUseItem(BattleItem item)
    {
        if (state != BattleState.PLAYERTURN) return;
        state = BattleState.BUSY;

        _activeUnit.inventory.Remove(item);

        switch (item.itemType)
        {
            case ItemType.HealthPotion:
                int healAmt = Mathf.Min(item.potency, _activeUnit.maxHealth - _activeUnit.currentHealth);
                _activeUnit.currentHealth += healAmt;
                RunManager.Instance?.CurrentRunStats?.AddPlayerHealing(healAmt);
                _uiManager.SetMessage($"{_activeUnit.unitName} used {item.itemName}! Restored {item.potency} HP.");
                break;
            case ItemType.ManaPotion:
                _activeUnit.currentMana = Mathf.Min(_activeUnit.currentMana + item.potency, _activeUnit.maxMana);
                _uiManager.SetMessage($"{_activeUnit.unitName} used {item.itemName}! Restored {item.potency} MP.");
                break;
        }

        _uiManager.UpdateHealth(_activeUnit);
        _uiManager.EnableAttackButton(false);
        StartCoroutine(DelayThenNextTurn(1f));
    }

    private IEnumerator DelayThenNextTurn(float delay)
    {
        yield return new WaitForSeconds(delay);
        ProcessNextTurn();
    }

    private void SpawnImpactEffect(Vector3 position, GameObject customVfx = null, AudioClip customSfx = null)
    {
        GameObject prefabToUse = customVfx != null ? customVfx : impactVfxPrefab;
        if (prefabToUse != null)
        {
            // Spawn slightly above the ground, using the prefab's internal rotation
            GameObject vfx = Instantiate(prefabToUse, position + Vector3.up * 1f, prefabToUse.transform.rotation);

            // Unity Particle Systems that use Hierarchy Scaling need all their child modules' scaling modes to be Local or Hierarchy.
            // To make it look "0.3" times its normal self, we multiply its original scale by 0.3
            vfx.transform.localScale = prefabToUse.transform.localScale * 0.3f;

            Destroy(vfx, 2f); // Cleanup after 2 seconds
        }

        AudioClip soundToUse = customSfx != null ? customSfx : impactSoundEffect;
        if (soundToUse != null)
        {
            // Create a temporary audio source with pitch variation for multi-hit enhancement
            GameObject audioObj = new GameObject("TempHitAudio");
            audioObj.transform.position = position;
            AudioSource audioSource = audioObj.AddComponent<AudioSource>();
            audioSource.clip = soundToUse;
            audioSource.spatialBlend = 0.5f; // Partially 3D
            audioSource.volume = 0.8f;
            audioSource.pitch = Random.Range(0.85f, 1.15f); // Randomize pitch slightly
            audioSource.Play();

            Destroy(audioObj, soundToUse.length + 0.1f); // Cleanup after sound finishes
        }
    }

    private IEnumerator PlayerAttack(Unit target, AttackAction action)
    {
        _activeUnit.currentMana -= action.manaCost;
        _uiManager.UpdateHealth(_activeUnit);

        // Determine targets based on AoE flag
        List<Unit> targetedUnits = action.isAoE ? enemyTeam.Where(u => !u.isDead).ToList() : new List<Unit>() { target };
        List<Transform> targetTransforms = targetedUnits.Select(u => u.transform).ToList();

        // For AoE melee, lunge to the centroid of all target positions so the attacker
        // visually sweeps through the middle of the group instead of just the first enemy.
        Vector3? meleeCentroid = null;
        if (action.isAoE && !action.isRanged && targetTransforms.Count > 1)
        {
            Vector3 sum = Vector3.zero;
            foreach (var t in targetTransforms) sum += t.position;
            meleeCentroid = sum / targetTransforms.Count;
        }

        // Base critical hit determination for the attack
        bool isCrit = Random.Range(0, 100) < Mathf.Min(_activeUnit.luck, 75);
        string critTag = isCrit ? " CRITICAL HIT!" : "";
        string targetName = action.isAoE ? "All Enemies" : target.unitName;

        _uiManager.SetMessage($"{_activeUnit.unitName} uses {action.actionName} on {targetName}!{critTag}");

        // Player animation and callback when hit lands
        Unit attackingUnit = _activeUnit;
        yield return _activeUnit.PerformAttackLunge(target.transform, action, () =>
        {
            if (action.isSummon) return; // Minions deal the damage in SpawnMinionAttack
            foreach (var u in targetedUnits)
            {
                if (u.isDead) continue;

                int baseDamage = Random.Range(action.minDamage, action.maxDamage + 1);
                int damage = Mathf.Max(1, (baseDamage * attackingUnit.attack) / Mathf.Max(1, u.defense));
                if (u.isDefending) damage = Mathf.Max(1, damage / 2);
                if (isCrit) damage = Mathf.RoundToInt(damage * 1.5f);
                // Enraged perk: double damage below 50% HP
                if (attackingUnit.hasEnraged && attackingUnit.currentHealth < attackingUnit.maxHealth * 0.5f)
                    damage = Mathf.RoundToInt(damage * 2f);

                // Weaken: attacker deals less damage
                var weakenSe = attackingUnit.activeStatusEffects.Find(e => e.type == StatusEffectType.Weaken);
                if (weakenSe != null) damage = Mathf.Max(1, Mathf.RoundToInt(damage * (1f - weakenSe.value)));
                // Fortify: target takes less damage
                var fortifySe = u.activeStatusEffects.Find(e => e.type == StatusEffectType.Fortify);
                if (fortifySe != null) damage = Mathf.Max(1, Mathf.RoundToInt(damage * (1f - fortifySe.value)));

                if (CalculateEvasion(u))
                {
                    DamagePopSpawner.Show(u.transform, 0, PopType.Miss);
                    _uiManager.SetMessage($"{attackingUnit.unitName} missed {u.unitName}!");
                    continue;
                }

                // Thorns: enemy thorns affix reflects damage to attacker (no hit reaction on the enemy)
                var thornAffix = GetAffix(u, EnemyAffixType.Thorns);
                u.TakeDamage(damage, silent: thornAffix != null);
                RunManager.Instance?.CurrentRunStats?.AddPlayerDamageDealt(damage);
                if (isCrit) RunManager.Instance?.CurrentRunStats?.AddPlayerCrit();
                if (u.isDead) RunManager.Instance?.CurrentRunStats?.AddEnemyDefeated();
                DamagePopSpawner.Show(u.transform, damage, isCrit ? PopType.Crit : PopType.Normal);
                _uiManager.UpdateHealth(u);
                u.GetComponent<TraitAuraController>()?.RefreshDynamicAuras();

                if (thornAffix != null && !attackingUnit.isDead)
                {
                    int reflectDmg = Mathf.Max(1, Mathf.RoundToInt(damage * thornAffix.value));
                    attackingUnit.TakeDamage(reflectDmg, silent: true);
                    RunManager.Instance?.CurrentRunStats?.AddPlayerDamageTaken(reflectDmg);
                    DamagePopSpawner.Show(attackingUnit.transform, reflectDmg, PopType.Thorns);
                    _uiManager.UpdateHealth(attackingUnit);
                    attackingUnit.GetComponent<TraitAuraController>()?.RefreshDynamicAuras();
                }

                // Vampiric perk: heal attacker for 25% of damage dealt
                if (attackingUnit.hasVampiric && !attackingUnit.isDead)
                {
                    int healAmt = Mathf.Max(1, Mathf.RoundToInt(damage * 0.25f));
                    int actualHealed = Mathf.Min(attackingUnit.maxHealth - attackingUnit.currentHealth, healAmt);
                    attackingUnit.currentHealth += actualHealed;
                    RunManager.Instance?.CurrentRunStats?.AddPlayerHealing(actualHealed);
                }

                // Life steal: action-specific heal based on damage dealt
                if (action.lifeStealPercent > 0f && !attackingUnit.isDead)
                {
                    int lifeSteal = Mathf.Max(1, Mathf.RoundToInt(damage * action.lifeStealPercent));
                    int actualHealed = Mathf.Min(attackingUnit.maxHealth - attackingUnit.currentHealth, lifeSteal);
                    attackingUnit.currentHealth += actualHealed;
                    RunManager.Instance?.CurrentRunStats?.AddPlayerHealing(actualHealed);
                    DamagePopSpawner.Show(attackingUnit.transform, lifeSteal, PopType.Heal);
                    _uiManager.UpdateHealth(attackingUnit);
                    attackingUnit.GetComponent<TraitAuraController>()?.RefreshDynamicAuras();
                }
            }
        }, (hitTransform) =>
        {
            // apply VFX and stutter to the exact transform that got hit
            if (action.isSummon) return; // Impact handled in SpawnMinionAttack
            Unit u = hitTransform != null ? hitTransform.GetComponent<Unit>() : target;
            if (u != null)
            {
                if (GetAffix(u, EnemyAffixType.Thorns) == null)
                    u.TriggerHitAnimation(); // Trigger the stutter hit
            }
        }, (hitTransform) =>
        {
            if (action.isSummon) return;
            Unit u = hitTransform != null ? hitTransform.GetComponent<Unit>() : target;
            if (u != null) SpawnImpactEffect(u.transform.position, action.hitVfxPrefab, action.hitSoundEffect);
        }, targetTransforms, meleeCentroid);

        // Summon: spawn minions that rush the target and deal damage on arrival
        if (action.isSummon && action.summonPrefab != null && !attackingUnit.isDead)
            yield return StartCoroutine(SpawnMinionAttack(attackingUnit, targetedUnits, action, isCrit));

        // Apply status effect from the attack action
        if (action.appliesStatus != StatusEffectType.None && Random.value <= action.statusChance)
        {
            StatusEffect se = CreateStatusEffect(action);
            if (action.appliesStatus == StatusEffectType.Fortify)
            {
                // Fortify buffs the attacker, not the target
                attackingUnit.ApplyStatusEffect(se);
                _uiManager.SetMessage($"{attackingUnit.unitName} is now {se.displayName}!");
            }
            else
            {
                foreach (var u in targetedUnits)
                {
                    if (!u.isDead)
                    {
                        u.ApplyStatusEffect(se);
                        _uiManager.SetMessage($"{u.unitName} is now {se.displayName}!");
                    }
                }
            }
            yield return new WaitForSeconds(0.5f);
        }

        yield return new WaitForSeconds(0.5f);

        ProcessNextTurn();
    }

    private IEnumerator EnemyTurn()
    {
        _uiManager.SetMessage($"{_activeUnit.unitName}'s turn...");

        yield return new WaitForSeconds(0.5f);

        // Tick status effects at the start of this unit's turn
        var ticks = _activeUnit.TickStatusEffects();
        foreach (var tick in ticks)
        {
            _uiManager.SetMessage(tick.message);
            yield return new WaitForSeconds(0.4f);
            if (tick.type == StatusEffectType.Burn && tick.burnDamage > 0)
            {
                _activeUnit.TakeDamage(tick.burnDamage, silent: true);
                DamagePopSpawner.Show(_activeUnit.transform, tick.burnDamage, PopType.Burn);
                _uiManager.UpdateHealth(_activeUnit);
                if (_activeUnit.isDead) { CheckBattleOver(); yield break; }
            }
            else if (tick.type == StatusEffectType.Stun)
            {
                yield return new WaitForSeconds(0.4f);
                ProcessNextTurn();
                yield break;
            }
        }

        // Regenerating: restore HP at the start of this unit's turn
        var regenAffix = GetAffix(_activeUnit, EnemyAffixType.Regenerating);
        if (regenAffix != null)
        {
            int regenAmt = Mathf.Max(1, Mathf.RoundToInt(_activeUnit.maxHealth * regenAffix.value));
            _activeUnit.currentHealth = Mathf.Min(_activeUnit.maxHealth, _activeUnit.currentHealth + regenAmt);
            _uiManager.UpdateHealth(_activeUnit);
            _activeUnit.GetComponent<TraitAuraController>()?.RefreshDynamicAuras();
            _uiManager.SetMessage($"{_activeUnit.unitName} regenerates {regenAmt} HP!");
            yield return new WaitForSeconds(0.6f);
        }

        // Enemy AI: 20% chance to defend instead of attacking
        if (Random.value < 0.2f)
        {
            _activeUnit.isDefending = true;
            _uiManager.SetMessage($"{_activeUnit.unitName} takes a defensive stance!");
            yield return new WaitForSeconds(1f);
            ProcessNextTurn();
            yield break;
        }

        // Enemy AI: pick random available attack that we have mana for
        AttackAction chosenAttack = null;
        if (_activeUnit.availableAttacks != null)
        {
            var affordableAttacks = _activeUnit.availableAttacks.Where(a => _activeUnit.currentMana >= a.manaCost).ToList();
            if (affordableAttacks.Count > 0)
            {
                chosenAttack = affordableAttacks[Random.Range(0, affordableAttacks.Count)];
            }
        }

        if (chosenAttack == null)
        {
            Debug.LogError($"{_activeUnit.unitName} has no affordable attack actions!");
            ProcessNextTurn();
            yield break;
        }

        _activeUnit.currentMana -= chosenAttack.manaCost;
        _uiManager.UpdateHealth(_activeUnit);

        // Enemy AI: pick random alive player
        var alivePlayers = playerTeam.Where(u => !u.isDead).ToList();
        if (alivePlayers.Count == 0)
        {
            ProcessNextTurn();
            yield break;
        }

        Unit target = alivePlayers[Random.Range(0, alivePlayers.Count)];

        List<Unit> targetedUnits = chosenAttack.isAoE ? alivePlayers : new List<Unit>() { target };
        List<Transform> targetTransforms = targetedUnits.Select(u => u.transform).ToList();

        // For AoE melee, lunge to the centroid of all target positions.
        Vector3? meleeCentroid = null;
        if (chosenAttack.isAoE && !chosenAttack.isRanged && targetTransforms.Count > 1)
        {
            Vector3 sum = Vector3.zero;
            foreach (var t in targetTransforms) sum += t.position;
            meleeCentroid = sum / targetTransforms.Count;
        }

        // Base critical hit determination
        bool isCrit = Random.Range(0, 100) < Mathf.Min(_activeUnit.luck, 75);
        string critMsg = isCrit ? " CRITICAL HIT!" : "";
        string targetName = chosenAttack.isAoE ? "All Players" : target.unitName;

        // Enraged: check before the attack message so we can annotate it
        var enragedAffix = GetAffix(_activeUnit, EnemyAffixType.Enraged);
        bool isEnraged = enragedAffix != null && _activeUnit.currentHealth < _activeUnit.maxHealth * 0.5f;
        string enrageTag = isEnraged ? " [ENRAGED!]" : "";

        _uiManager.SetMessage($"{_activeUnit.unitName} uses {chosenAttack.actionName} on {targetName}!{critMsg}{enrageTag}");

        // Vampiric affix: captured before the callback closure
        var vampAffix = GetAffix(_activeUnit, EnemyAffixType.Vampiric);

        // Enemy animation and callback when hit lands
        yield return _activeUnit.PerformAttackLunge(target.transform, chosenAttack, () =>
        {
            if (chosenAttack.isSummon) return; // Minions deal the damage in SpawnMinionAttack
            foreach (var u in targetedUnits)
            {
                if (u.isDead) continue;

                int baseDamage = Random.Range(chosenAttack.minDamage, chosenAttack.maxDamage + 1);
                int damage = Mathf.Max(1, (baseDamage * _activeUnit.attack) / Mathf.Max(1, u.defense));
                if (u.isDefending) damage = Mathf.Max(1, damage / 2);
                if (isCrit) damage = Mathf.RoundToInt(damage * 1.5f);
                if (isEnraged) damage = Mathf.RoundToInt(damage * 2f);

                // Weaken: attacker deals less damage
                var weakenSe = _activeUnit.activeStatusEffects.Find(e => e.type == StatusEffectType.Weaken);
                if (weakenSe != null) damage = Mathf.Max(1, Mathf.RoundToInt(damage * (1f - weakenSe.value)));
                // Fortify: target takes less damage
                var fortifySe = u.activeStatusEffects.Find(e => e.type == StatusEffectType.Fortify);
                if (fortifySe != null) damage = Mathf.Max(1, Mathf.RoundToInt(damage * (1f - fortifySe.value)));

                if (CalculateEvasion(u))
                {
                    DamagePopSpawner.Show(u.transform, 0, PopType.Miss);
                    _uiManager.SetMessage($"{_activeUnit.unitName} missed {u.unitName}!");
                    continue;
                }

                // Thorns perk: redirect 50% of incoming damage to the attacker
                int playerDamage = (u.hasThorns && !_activeUnit.isDead) ? Mathf.Max(1, damage / 2) : damage;
                u.TakeDamage(playerDamage);
                RunManager.Instance?.CurrentRunStats?.AddPlayerDamageTaken(playerDamage);
                DamagePopSpawner.Show(u.transform, playerDamage, isCrit ? PopType.Crit : PopType.Normal);
                _uiManager.UpdateHealth(u);
                u.GetComponent<TraitAuraController>()?.RefreshDynamicAuras();

                // Vampiric affix: heal the attacker for 25% of damage dealt
                if (vampAffix != null)
                {
                    int healAmt = Mathf.Max(1, Mathf.RoundToInt(damage * vampAffix.value));
                    _activeUnit.currentHealth = Mathf.Min(_activeUnit.maxHealth, _activeUnit.currentHealth + healAmt);
                    DamagePopSpawner.Show(_activeUnit.transform, healAmt, PopType.Heal);
                    _uiManager.UpdateHealth(_activeUnit);
                    _activeUnit.GetComponent<TraitAuraController>()?.RefreshDynamicAuras();
                }

                // Thorns perk: redirect the other 50% to the attacker (no hit animation or sound)
                if (u.hasThorns && !_activeUnit.isDead)
                {
                    int reflectDmg = Mathf.Max(1, damage - playerDamage);
                    _activeUnit.TakeDamage(reflectDmg, silent: true);
                    RunManager.Instance?.CurrentRunStats?.AddPlayerDamageDealt(reflectDmg);
                    if (_activeUnit.isDead) RunManager.Instance?.CurrentRunStats?.AddEnemyDefeated();
                    DamagePopSpawner.Show(_activeUnit.transform, reflectDmg, PopType.Thorns);
                    _uiManager.UpdateHealth(_activeUnit);
                    _activeUnit.GetComponent<TraitAuraController>()?.RefreshDynamicAuras();
                }
            }
        }, (hitTransform) =>
        {
            if (chosenAttack.isSummon) return; // Impact handled in SpawnMinionAttack
            Unit u = hitTransform != null ? hitTransform.GetComponent<Unit>() : target;
            if (u != null)
            {
                u.TriggerHitAnimation(); // Trigger the stutter hit
            }
        }, (hitTransform) =>
        {
            if (chosenAttack.isSummon) return;
            Unit u = hitTransform != null ? hitTransform.GetComponent<Unit>() : target;
            if (u != null) SpawnImpactEffect(u.transform.position, chosenAttack.hitVfxPrefab, chosenAttack.hitSoundEffect);
        }, targetTransforms, meleeCentroid);

        // Summon: spawn minions that rush the target and deal damage on arrival
        if (chosenAttack.isSummon && chosenAttack.summonPrefab != null && !_activeUnit.isDead)
            yield return StartCoroutine(SpawnMinionAttack(_activeUnit, targetedUnits, chosenAttack, isCrit));

        // Apply status effect from the attack action
        if (chosenAttack.appliesStatus != StatusEffectType.None && Random.value <= chosenAttack.statusChance)
        {
            StatusEffect se = CreateStatusEffect(chosenAttack);
            if (chosenAttack.appliesStatus == StatusEffectType.Fortify)
            {
                // Fortify buffs the attacker
                _activeUnit.ApplyStatusEffect(se);
                _uiManager.SetMessage($"{_activeUnit.unitName} is now {se.displayName}!");
            }
            else
            {
                foreach (var u in targetedUnits)
                {
                    if (!u.isDead)
                    {
                        u.ApplyStatusEffect(se);
                        _uiManager.SetMessage($"{u.unitName} is now {se.displayName}!");
                    }
                }
            }
        }

        yield return new WaitForSeconds(0.5f);

        ProcessNextTurn();
    }

    // ── Enemy variety helpers ──────────────────────────────────────────────────

    private List<GameObject> GetEnemyPoolForTier(int tier)
    {
        if (tier >= 3 && tier3Enemies.Count > 0) return tier3Enemies;
        if (tier >= 2 && tier2Enemies.Count > 0) return tier2Enemies;
        if (tier1Enemies.Count > 0) return tier1Enemies;
        // Final fallback: merge all non-empty pools
        var all = new List<GameObject>();
        all.AddRange(tier1Enemies);
        all.AddRange(tier2Enemies);
        all.AddRange(tier3Enemies);
        return all;
    }

    private void SpawnSingleEnemy(GameObject srcPrefab, int tier, RunManager rm)
    {
        int slotIndex = enemyTeam.Count;
        GameObject enemyGO = Instantiate(srcPrefab);
        enemyGO.name = $"Enemy_{srcPrefab.name}_{slotIndex}";

        Unit eUnit = enemyGO.GetComponent<Unit>();
        if (eUnit == null) eUnit = enemyGO.AddComponent<Unit>();
        eUnit.isPlayer = false;
        if (eUnit.speed == 0) eUnit.speed = Random.Range(8, 18);

        // Base encounter scaling
        if (rm != null && rm.IsRunActive)
        {
            eUnit.maxHealth = Mathf.RoundToInt(eUnit.maxHealth * rm.GetEnemyHpScalar());
            eUnit.attack = Mathf.RoundToInt(eUnit.attack * rm.GetEnemyAtkScalar());
            eUnit.defense = Mathf.RoundToInt(eUnit.defense * rm.GetEnemyDefScalar());
            eUnit.speed += rm.GetEnemySpeedBonus();
            eUnit.currentHealth = eUnit.maxHealth;
        }

        // Elite roll: T2 = 15%, T3 = 25%
        float eliteChance = tier == 2 ? 0.15f : tier >= 3 ? 0.25f : 0f;
        bool isElite = Random.value < eliteChance;
        if (isElite)
        {
            const float EliteBonus = 1.5f;
            eUnit.maxHealth = Mathf.RoundToInt(eUnit.maxHealth * EliteBonus);
            eUnit.attack = Mathf.RoundToInt(eUnit.attack * EliteBonus);
            eUnit.defense = Mathf.RoundToInt(eUnit.defense * EliteBonus);
            eUnit.speed += 5;
            eUnit.currentHealth = eUnit.maxHealth;
        }

        var affixes = RollAffixes(tier, isElite, false);
        ApplyAffixesToUnit(eUnit, affixes);
        _enemyAffixes[eUnit] = affixes;
        ApplyTraitAuras(eUnit,
            affixes.Exists(a => a.type == EnemyAffixType.Vampiric) ? 1 : 0,
            affixes.Exists(a => a.type == EnemyAffixType.Thorns) ? 1 : 0,
            affixes.Exists(a => a.type == EnemyAffixType.Regenerating) ? 1 : 0,
            affixes.Exists(a => a.type == EnemyAffixType.Enraged) ? 1 : 0);

        string baseName = $"{srcPrefab.name} {slotIndex + 1}";
        eUnit.unitName = isElite ? $"★ {baseName}" : baseName;

        enemyGO.transform.position = new Vector3(2.5f + (slotIndex * 0.3f), 0, -3.0f + (slotIndex * 2.2f));
        Vector3 faceDir = new Vector3(-2.5f, 0, -1.5f) - enemyGO.transform.position; faceDir.y = 0;
        enemyGO.transform.rotation = Quaternion.LookRotation(faceDir);
        var enemyHpBar = enemyGO.AddComponent<FloatingHealthBar>();
        enemyHpBar.Init(eUnit, false);
        enemyHpBar.SetAffixes(affixes);
        enemyTeam.Add(eUnit);
    }

    private List<EnemyAffix> RollAffixes(int tier, bool isElite, bool isBoss)
    {
        var available = new List<EnemyAffix>(_affixPool);
        var result = new List<EnemyAffix>();

        int count;
        if (isBoss) count = 2;
        else if (isElite) count = 2;
        else if (tier >= 3) count = Random.Range(0, 100) < 50 ? 2 : 1;
        else if (tier == 2) count = 1;
        else count = 0;

        for (int i = 0; i < count && available.Count > 0; i++)
        {
            int idx = Random.Range(0, available.Count);
            result.Add(available[idx]);
            available.RemoveAt(idx);
        }
        // Prevent Berserker + Enraged stacking (combined ~3× ATK is too punishing)
        if (result.Any(a => a.type == EnemyAffixType.Berserker) && result.Any(a => a.type == EnemyAffixType.Enraged))
            result.RemoveAll(a => a.type == EnemyAffixType.Enraged);
        return result;
    }

    private void ApplyAffixesToUnit(Unit unit, List<EnemyAffix> affixes)
    {
        foreach (var a in affixes)
        {
            switch (a.type)
            {
                case EnemyAffixType.Armored:
                    unit.defense = Mathf.RoundToInt(unit.defense * a.value);
                    break;
                case EnemyAffixType.Berserker:
                    unit.attack = Mathf.RoundToInt(unit.attack * a.value);
                    break;
                case EnemyAffixType.Swift:
                    unit.speed += Mathf.RoundToInt(a.value);
                    break;
                case EnemyAffixType.Bulwark:
                    unit.maxHealth = Mathf.RoundToInt(unit.maxHealth * a.value);
                    unit.currentHealth = unit.maxHealth;
                    break;
                    // Behavioral affixes (Regenerating, Thorns, Vampiric, Enraged) are handled at runtime
            }
        }
    }

    private EnemyAffix GetAffix(Unit unit, EnemyAffixType type)
    {
        if (!_enemyAffixes.TryGetValue(unit, out var list)) return null;
        return list.FirstOrDefault(a => a.type == type);
    }

    public IReadOnlyList<EnemyAffix> GetAffixes(Unit unit)
    {
        if (_enemyAffixes.TryGetValue(unit, out var list)) return list;
        return System.Array.Empty<EnemyAffix>();
    }

    private StatusEffect CreateStatusEffect(AttackAction action)
    {
        StatusEffect se;
        switch (action.appliesStatus)
        {
            case StatusEffectType.Burn: se = StatusEffect.Burn(action.statusDuration); break;
            case StatusEffectType.Stun: se = StatusEffect.Stun(action.statusDuration); break;
            case StatusEffectType.Weaken: se = StatusEffect.Weaken(action.statusDuration); break;
            case StatusEffectType.Fortify: se = StatusEffect.Fortify(action.statusDuration); break;
            default: return new StatusEffect { type = StatusEffectType.None, duration = 0, value = 0f, displayName = "None" };
        }
        if (action.statusValue > 0f) se.value = action.statusValue;
        return se;
    }

    private void EndBattle()
    {
        _uiManager.EnableAttackButton(false);
        RunManager rm = RunManager.Instance;

        if (state == BattleState.WON)
        {
            if (rm != null && rm.IsRunActive)
            {
                int goldEarned = rm.GetGoldReward();
                if (rm.CurrentEncounter % 10 == 0) goldEarned *= 2; // Boss encounters award double gold
                if (_isMiniBoss) goldEarned = Mathf.RoundToInt(goldEarned * 1.5f); // Mini-boss bonus
                rm.AddGold(goldEarned);
                _uiManager.UpdateRunHud(rm.CurrentEncounter, rm.Gold);

                _uiManager.SetMessage("Victory!");
                int choiceCount = rm.CurrentEncounter % 10 == 0 ? 4 : 3;
                var choices = RunManager.GenerateRewardChoices(playerTeam, rm.CurrentEncounter, choiceCount);
                int clearedEncounter = rm.CurrentEncounter;

                Action<RewardChoice> applyChoice = null;
                applyChoice = (choice) =>
                {
                    if (choice.legendaryRelic != null)
                        rm.EquipRelic(choice.targetUnitName, choice.legendaryRelic);
                    else if (choice.statUpgrade != null)
                        rm.ApplyUpgrade(choice.statUpgrade);
                    else
                        rm.ApplyAttackUpgrade(choice.attackUpgrade);

                    if (choice.curseEffect != null)
                        rm.ApplyUpgrade(choice.curseEffect);

                    var roomChoices = rm.GenerateNextRoomChoices();
                    _uiManager.ShowRoomChoices(rm.CurrentEncounter, roomChoices, (room) =>
                    {
                        rm.SelectNextRoom(room);
                        StartNextEncounter();
                    });
                };

                Action onReroll = () =>
                {
                    var newChoices = RunManager.GenerateRewardChoices(playerTeam, clearedEncounter, choiceCount);
                    _uiManager.ShowIntermission(clearedEncounter, goldEarned, newChoices, applyChoice, null);
                };

                _uiManager.ShowIntermission(clearedEncounter, goldEarned, choices, applyChoice, onReroll);
            }
            else
            {
                _uiManager.SetMessage("You Won the Battle!");
                _uiManager.ShowRestartMenu();
            }
        }
        else if (state == BattleState.LOST)
        {
            int reached = rm != null ? rm.CurrentEncounter : 1;
            int gold = rm != null ? rm.Gold : 0;
            rm?.EndRun();
            _uiManager.SetMessage("Defeated...");
            _uiManager.ShowRunSummary(false, reached, gold);
        }
    }

    private void StartNextEncounter()
    {
        // Prevent ghost encounters from carrying overlapping turn logic!
        StopAllCoroutines();

        // Immediately purge all HP bar containers from the shared canvas so they
        // don't linger while the new encounter's bars are being created.
        FloatingHealthBar.ClearAll();

        foreach (var u in playerTeam) if (u != null) Destroy(u.gameObject);
        foreach (var u in enemyTeam) if (u != null) Destroy(u.gameObject);
        playerTeam.Clear();
        enemyTeam.Clear();
        _turnQueue.Clear();
        _activeUnit = null;
        state = BattleState.START;

        // Wait 1 frame to ensure coroutines actually wiped fully before restarting
        StartCoroutine(DelayThenSetup(0.1f));
    }

    private IEnumerator DelayThenSetup(float delay)
    {
        yield return new WaitForSeconds(delay);
        _uiManager.HideOverlays();
        _uiManager.ClearBattleLog();
        StartCoroutine(SetupBattle());
    }

    /// <summary>
    /// Instantiates the visual prefabs from the unit's equipped relic onto the character's
    /// RelicAttachmentPoints sockets. Call after the unit's prefab is instantiated.
    /// </summary>
    /// <summary>
    /// Instantiates aura prefabs onto the unit and attaches a TraitAuraController to manage them.
    /// Any prefab slot left null is simply skipped — safe to call before prefabs are assigned.
    /// </summary>
    private void ApplyTraitAuras(Unit unit, int vampiricCount, int thornsCount, int regenCount, int enragedCount)
    {
        if (vampiricCount == 0 && thornsCount == 0 && regenCount == 0 && enragedCount == 0) return;

        List<GameObject> SpawnAll(GameObject prefab, int count)
        {
            var list = new List<GameObject>();
            if (prefab == null) return list;
            GameObject auraContainer = new GameObject("Auras_" + prefab.name);
            auraContainer.transform.SetParent(unit.transform, false);

            Vector3 worldScale = unit.transform.lossyScale;
            auraContainer.transform.localScale = new Vector3(
                1f / Mathf.Max(0.001f, worldScale.x),
                1f / Mathf.Max(0.001f, worldScale.y),
                1f / Mathf.Max(0.001f, worldScale.z)
            );

            for (int i = 0; i < count; i++)
            {
                var go = Instantiate(prefab, auraContainer.transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                if (i == 1) // second stack: slightly larger for visual distinction
                {
                    go.transform.localScale *= 1.2f;
                }
                list.Add(go);
            }
            return list;
        }

        var ctrl = unit.gameObject.AddComponent<TraitAuraController>();
        ctrl.Init(unit,
            SpawnAll(vampiricAuraPrefab, vampiricCount),
            SpawnAll(thornsAuraPrefab, thornsCount),
            SpawnAll(regenAuraPrefab, regenCount),
            SpawnAll(enragedAuraPrefab, enragedCount));
    }

    private void ApplyRelicVisuals(GameObject go, string unitName)
    {
        LegendaryRelicSO relic = RunManager.Instance?.GetRelicForUnit(unitName);
        if (relic == null) return;

        RelicAttachmentPoints pts = go.GetComponent<RelicAttachmentPoints>();
        if (pts == null) return;

        foreach (var v in relic.visuals)
        {
            if (v.prefab == null) continue;
            Transform socket = pts.GetSocket(v.socket);
            if (socket == null) continue;
            Instantiate(v.prefab, socket.position, socket.rotation, socket);
        }
    }

    private IEnumerator StartEventEncounter()
    {
        RunManager rm = RunManager.Instance;
        EncounterType type = rm != null ? rm.CurrentEncounterType : EncounterType.Mystery;

        string title;
        string subtitle;
        int choiceCount = 2;

        _uiManager.SetMessage("An encounter of a different kind...");
        yield return new WaitForSeconds(0.8f);

        switch (type)
        {
            case EncounterType.Shrine:
                foreach (var p in playerTeam)
                    if (!p.isDead)
                        p.currentHealth = Mathf.Min(p.maxHealth, p.currentHealth + Mathf.RoundToInt(p.maxHealth * 0.40f));
                _uiManager.SetupHealthBars(playerTeam);
                _uiManager.SetMessage("A sacred shrine heals your wounds...");
                yield return new WaitForSeconds(1.2f);
                title = "\u26E9 Ancient Shrine";
                subtitle = "Restored 40% HP. Choose a blessing.";
                break;

            case EncounterType.Merchant:
                title = "\u2696 Wandering Merchant";
                subtitle = "\"Fine wares for the weary traveler...\"";
                choiceCount = 3;
                break;

            default: // Mystery
                bool goldBonus = UnityEngine.Random.value < 0.5f;
                if (goldBonus && rm != null)
                {
                    int bonus = rm.GetGoldReward();
                    rm.AddGold(bonus);
                    _uiManager.UpdateRunHud(rm.CurrentEncounter, rm.Gold);
                    subtitle = $"Strange energies crystallise into gold! +{bonus} Gold";
                }
                else
                {
                    subtitle = "The air crackles with unknown power.";
                    choiceCount = 3;
                }
                title = "\u2726 Strange Energies";
                break;
        }

        bool isMerchant = type == EncounterType.Merchant;
        var choices = isMerchant
            ? RunManager.GenerateMerchantChoices(playerTeam, rm != null ? rm.CurrentEncounter : 1, choiceCount)
            : RunManager.GenerateRewardChoices(playerTeam, rm != null ? rm.CurrentEncounter : 1, choiceCount);
        int goldForOverlay = (isMerchant && rm != null) ? rm.Gold : -1;

        // If merchant and the player can't afford anything, update the subtitle
        if (isMerchant && rm != null && choices.TrueForAll(c => c.goldCost > rm.Gold))
            subtitle = "\"Nothing here is within your means...\"";

        Action onSkip = isMerchant ? () =>
        {
            if (rm != null)
            {
                var roomChoices = rm.GenerateNextRoomChoices();
                _uiManager.ShowRoomChoices(rm.CurrentEncounter, roomChoices, (room) =>
                {
                    rm.SelectNextRoom(room);
                    StartNextEncounter();
                });
            }
            else
            {
                StartNextEncounter();
            }
        }
        : (Action)null;

        _uiManager.ShowEventOverlay(title, subtitle, choices, (choice) =>
        {
            if (isMerchant && rm != null && choice.goldCost > 0)
                rm.SpendGold(choice.goldCost);

            if (choice.legendaryRelic != null)
                rm.EquipRelic(choice.targetUnitName, choice.legendaryRelic);
            else if (choice.statUpgrade != null)
                rm.ApplyUpgrade(choice.statUpgrade);
            else
                rm.ApplyAttackUpgrade(choice.attackUpgrade);

            if (choice.curseEffect != null)
                rm.ApplyUpgrade(choice.curseEffect);

            if (rm != null)
            {
                var roomChoices = rm.GenerateNextRoomChoices();
                _uiManager.ShowRoomChoices(rm.CurrentEncounter, roomChoices, (room) =>
                {
                    rm.SelectNextRoom(room);
                    StartNextEncounter();
                });
            }
            else
            {
                StartNextEncounter();
            }
        }, goldForOverlay, onSkip);
    }

    private IEnumerator SpawnMinionAttack(Unit caster, List<Unit> targets, AttackAction action, bool isCrit)
    {
        int minionCount = action.summonCount << caster.bonusSummonCount; // Starts at base and doubles
        int activeSkeletons = 0;
        bool isFirstSpawn = true;

        foreach (var target in targets)
        {
            if (target.isDead) continue;
            for (int i = 0; i < minionCount; i++)
            {
                activeSkeletons++;
                StartCoroutine(MinionRoutine(caster, target, action, i, minionCount, isCrit, isFirstSpawn, () => activeSkeletons--));
                isFirstSpawn = false;

                // Stagger the spawning of each minion unless it's an AoE attack
                if (!action.isAoE)
                {
                    yield return new WaitForSeconds(0.2f);
                }
            }
        }

        while (activeSkeletons > 0)
        {
            yield return null;
        }
    }

    private IEnumerator MinionRoutine(Unit caster, Unit target, AttackAction action, int minionIndex, int minionCount, bool isCrit, bool isFirstSpawn, System.Action onComplete)
    {
        Vector3 spawnOffset = new Vector3((minionIndex - minionCount * 0.5f + 0.5f) * 1.2f, 0f, Random.Range(-0.5f, 0.5f));
        Vector3 surfacePos = caster.transform.position + spawnOffset;
        Vector3 spawnPos = surfacePos + Vector3.down * 1.5f;

        GameObject minionGO = GetFromPool(
            action.summonPrefab,
            spawnPos,
            Quaternion.identity);

        if (isFirstSpawn && action.summonSpawnVfxPrefab != null)
        {
            GameObject vfx = Instantiate(action.summonSpawnVfxPrefab, caster.transform.position, Quaternion.identity);
            Destroy(vfx, 2f);
        }

        if (isFirstSpawn && action.summonSpawnSoundEffect != null)
        {
            GameObject audioObj = new GameObject("TempSpawnAudio");
            audioObj.transform.position = caster.transform.position;
            AudioSource audioSource = audioObj.AddComponent<AudioSource>();
            audioSource.clip = action.summonSpawnSoundEffect;
            audioSource.spatialBlend = 0.5f;
            audioSource.Play();
            Destroy(audioObj, action.summonSpawnSoundEffect.length + 0.1f);
        }

        // Make minion face target early
        Vector3 destination = target.transform.position;
        Vector3 dir = (destination - surfacePos).normalized; // Use surfacePos since minion is below ground right now
        if (dir != Vector3.zero) minionGO.transform.rotation = Quaternion.LookRotation(dir);

        float riseDuration = 0.5f;
        float riseElapsed = 0f;
        while (riseElapsed < riseDuration)
        {
            if (minionGO == null) yield break;
            riseElapsed += Time.deltaTime;
            minionGO.transform.position = Vector3.Lerp(spawnPos, surfacePos, riseElapsed / riseDuration);
            yield return null;
        }

        if (minionGO == null) yield break;
        minionGO.transform.position = surfacePos;

        Animator minionAnim = minionGO.GetComponentInChildren<Animator>();
        if (minionAnim != null)
        {
            minionAnim.SetBool("isRunning", true);
        }

        while (!target.isDead && minionGO != null && Vector3.Distance(minionGO.transform.position, destination) > 0.8f)
        {
            minionGO.transform.position = Vector3.MoveTowards(
                minionGO.transform.position, destination, 8f * Time.deltaTime);
            yield return null;
        }

        if (minionGO == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        if (!target.isDead)
        {
            if (minionAnim != null)
            {
                minionAnim.SetBool("isRunning", false);
                minionAnim.SetTrigger("Attack"); // Main attack
                minionAnim.SetTrigger("Attack1"); // Fallback attack param
            }

            // Wait for the visual swing to actually connect before applying damage
            yield return new WaitForSeconds(action.summonHitDelay);

            int hitCount = Mathf.Max(1, action.hitCount);
            for (int hit = 0; hit < hitCount; hit++)
            {
                if (target.isDead) break;

                int baseDamage = Random.Range(action.minDamage, action.maxDamage + 1);
                int damage = Mathf.Max(1, (baseDamage * caster.attack) / Mathf.Max(1, target.defense));
                if (target.isDefending) damage = Mathf.Max(1, damage / 2);
                if (isCrit) damage = Mathf.RoundToInt(damage * 1.5f);
                if (caster.hasEnraged && caster.currentHealth < caster.maxHealth * 0.5f)
                    damage = Mathf.RoundToInt(damage * 2f);

                var weakenSe = caster.activeStatusEffects.Find(e => e.type == StatusEffectType.Weaken);
                if (weakenSe != null) damage = Mathf.Max(1, Mathf.RoundToInt(damage * (1f - weakenSe.value)));
                var fortifySe = target.activeStatusEffects.Find(e => e.type == StatusEffectType.Fortify);
                if (fortifySe != null) damage = Mathf.Max(1, Mathf.RoundToInt(damage * (1f - fortifySe.value)));

                if (CalculateEvasion(target))
                {
                    DamagePopSpawner.Show(target.transform, 0, PopType.Miss);
                    _uiManager.SetMessage($"{caster.unitName} missed {target.unitName}!");
                    continue;
                }

                target.TakeDamage(damage, silent: false);
                DamagePopSpawner.Show(target.transform, damage, isCrit ? PopType.Crit : PopType.Normal);
                _uiManager.UpdateHealth(target);
                target.TriggerHitAnimation();
                SpawnImpactEffect(target.transform.position, action.hitVfxPrefab, action.hitSoundEffect);

                // Vampiric perk: heal attacker for 25% of damage dealt
                if (caster.hasVampiric && !caster.isDead)
                {
                    int healAmt = Mathf.Max(1, Mathf.RoundToInt(damage * 0.25f));
                    caster.currentHealth = Mathf.Min(caster.maxHealth, caster.currentHealth + healAmt);
                    DamagePopSpawner.Show(caster.transform, healAmt, PopType.Heal);
                    _uiManager.UpdateHealth(caster);
                    caster.GetComponent<TraitAuraController>()?.RefreshDynamicAuras();
                }

                // Life steal: action-specific heal based on damage dealt
                if (action.lifeStealPercent > 0f && !caster.isDead)
                {
                    int lifeSteal = Mathf.Max(1, Mathf.RoundToInt(damage * action.lifeStealPercent));
                    caster.currentHealth = Mathf.Min(caster.maxHealth, caster.currentHealth + lifeSteal);
                    DamagePopSpawner.Show(caster.transform, lifeSteal, PopType.Heal);
                    _uiManager.UpdateHealth(caster);
                    caster.GetComponent<TraitAuraController>()?.RefreshDynamicAuras();
                }

                // Apply status effect if any
                if (action.appliesStatus != StatusEffectType.None && Random.value <= action.statusChance)
                {
                    StatusEffect se = new StatusEffect { type = action.appliesStatus, duration = action.statusDuration, value = action.statusValue, displayName = action.appliesStatus.ToString() };
                    target.ApplyStatusEffect(se);
                    _uiManager.UpdateHealth(target);
                }

                // Wait for the swing to reset before the next strike (if there is one) or before dying
                if (hit < hitCount - 1)
                {
                    float delay = action.delayBetweenHits > 0 ? action.delayBetweenHits : 0.5f;
                    yield return new WaitForSeconds(delay);
                }
                else
                {
                    yield return new WaitForSeconds(0.9f);
                }
            }
        }

        if (minionGO != null)
        {
            if (minionAnim != null) minionAnim.SetBool("isDead", true);

            yield return new WaitForSeconds(1f); // Wait for the bones to collapse

            // Sink into the ground smoothly
            float sinkDuration = 0.5f;
            float elapsed = 0f;
            Vector3 startPos = minionGO.transform.position;
            Vector3 endPos = startPos + Vector3.down * 2f;

            while (elapsed < sinkDuration && minionGO != null)
            {
                elapsed += Time.deltaTime;
                minionGO.transform.position = Vector3.Lerp(startPos, endPos, elapsed / sinkDuration);
                yield return null;
            }

            if (minionGO != null)
            {
                if (minionAnim != null)
                {
                    minionAnim.SetBool("isDead", false);
                    minionAnim.SetBool("isRunning", false);
                    // Force the animator back to the default state, assuming "Idle" is default, 
                    // or just resetting the isDead bool is usually enough to let the transition occur.
                    minionAnim.Rebind();
                    minionAnim.Update(0f);
                }
                ReturnToPool(action.summonPrefab, minionGO);
            }
        }

        onComplete?.Invoke();
    }

    private bool CalculateEvasion(Unit target)
    {
        // 1 Luck = 1% dodge chance, hard capped at 70% evasion
        float evasionChance = Mathf.Min(target.luck, 70f);
        return Random.Range(0f, 100f) < evasionChance;
    }
}


