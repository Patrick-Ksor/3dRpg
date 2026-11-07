using UnityEngine;

[CreateAssetMenu(fileName = "NewAttack", menuName = "RPG/Attack Action")]
public class AttackAction : ScriptableObject
{
    public string actionName;
    public int minDamage = 15;
    public int maxDamage = 30;
    public int manaCost = 0;
    public bool isRanged = false;
    [Tooltip("If true, the attack targets all alive enemies in the battle")]
    public bool isAoE = false;

    [Header("Animation")]
    public string animationName;
    public float hitDelay;
    public float finishDelay;

    [Header("Visual Effects")]
    public GameObject chargeVfxPrefab;
    public GameObject hitVfxPrefab;
    public GameObject projectilePrefab;
    [Tooltip("For instant ranged attacks (no projectile), delay between spawning the hit VFX and dealing damage.")]
    public float hitVfxDamageDelay = 0f;

    [Header("Audio")]
    public AudioClip chargeSoundEffect;
    public AudioClip gruntSound;
    [Tooltip("Delay in seconds from the start of the attack animation before the grunt plays")]
    public float gruntSoundDelay = 0f;
    public AudioClip hitSoundEffect;

    [Header("Projectile")]
    public float projectileSpeed = 15f;
    public float projectileScaleMultiplier = 1f;
    [Tooltip("If true, the projectile graphics wrapper spins rapidly on its Y axis.")]
    public bool spinProjectile = true;
    [Tooltip("If true, the projectile faces the target exactly when spawned. If false, uses default identity rotation.")]
    public bool rotateToFaceTarget = true;
    [Tooltip("If true, the projectile flies out to the target then visually flies back to the caster before being destroyed.")]
    public bool returnsToCaster = false;
    [Tooltip("Delay in seconds from the start of the attack animation until the projectile is fired")]
    public float projectileFireDelay = 0.4f;

    [Header("Multi-Projectile (Storm Settings)")]
    [Tooltip("Number of projectiles to fire simultaneously or in sequence")]
    public int projectileCount = 1;
    [Tooltip("Total spread angle in degrees (e.g. 45 for a fan)")]
    public float projectileSpreadAngle = 0f;
    [Tooltip("Delay between each projectile (0 = shotgun blast)")]
    public float delayBetweenProjectiles = 0f;

    [Header("Multi-Hit (Melee)")]
    [Tooltip("Number of times damage is applied per attack (melee only)")]
    public int hitCount = 1;
    [Tooltip("Delay in seconds between each hit when hitCount > 1")]
    public float delayBetweenHits = 0.3f;

    [Header("Status Effect")]
    public StatusEffectType appliesStatus = StatusEffectType.None;
    [Tooltip("How many turns the status effect lasts")]
    public int statusDuration = 2;
    [Tooltip("Magnitude of the effect (e.g. 0.08 = 8% max HP per turn for Burn)")]
    public float statusValue = 0.08f;
    [Tooltip("Probability (0–1) that the status effect is applied on hit. 1 = always.")]
    [Range(0f, 1f)]
    public float statusChance = 1f;
    [Header("Summon")]
    [Tooltip("If true, this attack spawns a skeleton minion that rushes the target instead of projectile/melee.")]
    public bool isSummon;
    [Tooltip("Base number of minions to summon per attack.")]
    public int summonCount = 1;
    [Tooltip("Prefab to spawn as the minion (e.g. SkeletonMinion.prefab).")]
    public GameObject summonPrefab;
    [Tooltip("VFX played on the ground as the minion rises.")]
    public GameObject summonSpawnVfxPrefab;
    [Tooltip("SFX played as the minion rises.")]
    public AudioClip summonSpawnSoundEffect;
    [Tooltip("Delay in seconds after the minion starts swinging before it applies damage.")]
    public float summonHitDelay = 0.35f;

    [Header("Life Steal")]
    [Tooltip("Fraction of damage dealt restored to the attacker as HP (0 = none, 0.5 = 50%).")]
    [Range(0f, 1f)]
    public float lifeStealPercent;
}
