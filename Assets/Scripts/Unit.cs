using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Animator))]
public class Unit : MonoBehaviour
{
    [Header("Base Stats")]
    public string unitName;
    public Sprite portrait;
    public bool isPlayer;
    public int attack = 10;
    public int defense = 10;
    public int speed = 10;
    public int luck = 5;
    public int maxHealth = 100;
    public int currentHealth;
    public int maxMana = 20;
    public int currentMana;
    public bool isDead = false;

    [Header("Animation States")]
    public string runAnimatorState = "Run";
    public string idleAnimatorState = "Idle";

    [Header("Audio")]
    public AudioClip hitSound;

    [Header("Attack Actions")]
    public AttackAction basicAttack;
    public List<AttackAction> specialAttacks = new List<AttackAction>();
    public List<AttackAction> availableAttacks = new List<AttackAction>();

    [Header("Learnable Attacks")]
    public List<AttackAction> unlockableAttacks = new List<AttackAction>();

    [Header("Inventory")]
    public List<BattleItem> inventory = new List<BattleItem>();

    [HideInInspector] public bool isDefending;
    [HideInInspector] public bool hasVampiric;
    [HideInInspector] public bool hasThorns;
    [HideInInspector] public bool hasRegeneration;
    [HideInInspector] public bool hasEnraged;
    [HideInInspector] public int bonusSummonCount;

    [HideInInspector] public List<StatusEffect> activeStatusEffects = new List<StatusEffect>();

    private Animator _animator;

    private Vector3 _homePosition;
    private Quaternion _homeRotation;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        currentHealth = maxHealth;
        currentMana = maxMana;

        // Add AudioSource if it doesn't have one
        if (GetComponent<AudioSource>() == null)
        {
            gameObject.AddComponent<AudioSource>();
        }
    }

    private void Start()
    {
        // Store exact spawn position to ensure we always return to the same spot
        _homePosition = transform.position;
        _homeRotation = transform.rotation;
    }

    public void TakeDamage(int damage, bool silent = false)
    {
        currentHealth -= damage;

        if (!silent)
        {
            if (hitSound != null)
            {
                if (TryGetComponent<AudioSource>(out var audio))
                {
                    audio.PlayOneShot(hitSound);
                }
            }

            // Trigger hit animation via boolean
            TriggerHitAnimation();
        }

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            isDead = true;
            Die();
        }
    }

    public void TriggerHitAnimation()
    {
        // If currently in Hit state, rewind it to the beginning so it "stutters" repeatedly
        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName("Hit") || stateInfo.IsName("GetHit") || stateInfo.IsName("Damage"))
        {
            _animator.Play(stateInfo.fullPathHash, -1, 0f);
        }

        StartCoroutine(HandleHitAnimation());
    }

    private IEnumerator HandleHitAnimation()
    {
        _animator.SetBool("isHit", true);
        // Wait a tiny bit just to ensure the state machine picks up the transition
        yield return new WaitForSeconds(0.2f);
        _animator.SetBool("isHit", false);
    }

    public IEnumerator PerformAttackLunge(Transform target, AttackAction action, System.Action onHit, System.Action<Transform> onExtraHit = null, System.Action<Transform> onSpawnVfx = null, List<Transform> aoeTargets = null, Vector3? overrideLungePos = null)
    {
        GameObject chargeVfxInstance = null;
        if (action.chargeVfxPrefab != null)
        {
            GameObject container = new GameObject("ChargeVFXContainer");
            container.transform.SetParent(transform, false);
            Vector3 lossy = transform.lossyScale;
            container.transform.localScale = new Vector3(
                1f / Mathf.Max(0.001f, lossy.x),
                1f / Mathf.Max(0.001f, lossy.y),
                1f / Mathf.Max(0.001f, lossy.z)
            );
            chargeVfxInstance = Instantiate(action.chargeVfxPrefab, container.transform, false);
            chargeVfxInstance.transform.localPosition = Vector3.zero;
            chargeVfxInstance.transform.localRotation = Quaternion.identity;
        }

        if (action.chargeSoundEffect != null)
        {
            GameObject audioObj = new GameObject("TempChargeAudio");
            audioObj.transform.position = transform.position;
            AudioSource audioSource = audioObj.AddComponent<AudioSource>();
            audioSource.clip = action.chargeSoundEffect;
            audioSource.spatialBlend = 0.5f; // Partially 3D
            audioSource.volume = 1.0f;
            audioSource.Play();
            Destroy(audioObj, action.chargeSoundEffect.length + 0.1f);
        }

        // Use true _homePosition rather than current position, in case they drifted
        Vector3 startPos = _homePosition;

        // For AoE melee, overrideLungePos is the centroid of all targets so the attacker
        // lunges to the middle of the group rather than just the first enemy.
        Vector3 lungeAnchor = overrideLungePos ?? target.position;

        // Move towards target, getting very close before stopping
        Vector3 endPos = lungeAnchor + (_homePosition - lungeAnchor).normalized * 0.75f;

        // Face the target
        Vector3 lookTarget = new Vector3(lungeAnchor.x, transform.position.y, lungeAnchor.z);
        transform.LookAt(lookTarget);

        float moveDuration = .5f;
        float elapsed = 0f;
        Coroutine footstepRoutine = null;

        if (!action.isRanged)
        {
            // Run forward
            _animator.SetBool("isRunning", true);

            // Start playing footstep sounds
            footstepRoutine = StartCoroutine(PlayFootstepSounds());

            // Lunge forward
            while (elapsed < moveDuration)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / moveDuration);
                transform.position = Vector3.Lerp(startPos, endPos, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.position = endPos;
            // The attack animation will instantly crossfade from the run here
            if (footstepRoutine != null) StopCoroutine(footstepRoutine);

            _animator.SetBool("isRunning", false);
        }

        // Perform Attack Animation (Use CrossFade for a smooth blend)
        _animator.CrossFadeInFixedTime(action.animationName, 0.15f);

        if (action.gruntSound != null)
        {
            if (action.gruntSoundDelay > 0f)
                StartCoroutine(PlayGruntDelayed(action.gruntSound, action.gruntSoundDelay));
            else if (TryGetComponent<AudioSource>(out var audioSource))
                audioSource.PlayOneShot(action.gruntSound);
        }

        if (action.isRanged)
        {
            if (action.projectilePrefab != null)
            {
                // For ranged, we wait until the specified fire delay before spawning the projectile
                yield return new WaitForSeconds(action.projectileFireDelay);
                Vector3 targetPos = target.position + Vector3.up * 1f; // Aim at chest height
                yield return StartCoroutine(AnimateProjectiles(targetPos, action, onHit, onExtraHit, onSpawnVfx, aoeTargets));
            }
            else
            {
                // Instant Ranged VFX (e.g. lightning strike from above directly on target)
                yield return new WaitForSeconds(action.projectileFireDelay);

                // Trigger Hits based on projectile count (like a barrage of instant strikes)
                int count = Mathf.Max(1, action.projectileCount);
                for (int i = 0; i < count; i++)
                {
                    if (BaseBattleManager.Instance != null && BaseBattleManager.Instance.IsBattleOver()) break;

                    // Trigger the VFX spawn across all targets
                    if (aoeTargets != null && aoeTargets.Count > 0)
                        foreach (var t in aoeTargets) onSpawnVfx?.Invoke(t);
                    else
                        onSpawnVfx?.Invoke(target);

                    // Start a coroutine to handle the damage delay without blocking the rapid barrage
                    if (action.hitVfxDamageDelay > 0f)
                    {
                        StartCoroutine(DelayedInstantHit(action.hitVfxDamageDelay, onHit, onExtraHit, target, aoeTargets));
                    }
                    else
                    {
                        onHit?.Invoke();
                        if (aoeTargets != null && aoeTargets.Count > 0)
                            foreach (var t in aoeTargets) onExtraHit?.Invoke(t);
                        else
                            onExtraHit?.Invoke(target);
                    }

                    // Delay before the next strike in the barrage
                    if (i < count - 1)
                    {
                        float delay = action.delayBetweenProjectiles > 0f ? action.delayBetweenProjectiles : 0.2f;
                        yield return new WaitForSeconds(delay);
                    }
                }
            }

            // Post-impact pause before the turn advances
            float remainingAnimTime = action.finishDelay;
            yield return new WaitForSeconds(remainingAnimTime);
        }
        else
        {
            // Melee execution string
            // Wait for the hit frame to land.
            yield return new WaitForSeconds(action.hitDelay);

            // Trigger damage and VFX once per hit; repeat for multi-hit attacks
            int hitCount = Mathf.Max(1, action.hitCount);
            for (int i = 0; i < hitCount; i++)
            {
                if (BaseBattleManager.Instance != null && BaseBattleManager.Instance.IsBattleOver()) break;

                if (aoeTargets != null && aoeTargets.Count > 0)
                {
                    foreach (var t in aoeTargets) onSpawnVfx?.Invoke(t);
                }
                else
                {
                    onSpawnVfx?.Invoke(target);
                }

                if (action.hitVfxDamageDelay > 0f)
                    yield return new WaitForSeconds(action.hitVfxDamageDelay);

                onHit?.Invoke();

                // Trigger visual and sound effects for each hit target
                if (aoeTargets != null && aoeTargets.Count > 0)
                {
                    foreach (var t in aoeTargets)
                    {
                        onExtraHit?.Invoke(t);
                    }
                }
                else
                {
                    onExtraHit?.Invoke(target);
                }

                // Pause between hits (skip after the final hit)
                if (i < hitCount - 1)
                    yield return new WaitForSeconds(action.delayBetweenHits);
            }

            // Wait for the rest of the attack animation
            yield return new WaitForSeconds(action.finishDelay);
        }

        if (!action.isRanged)
        {
            // Run back
            _animator.SetBool("isRunning", true);
            footstepRoutine = StartCoroutine(PlayFootstepSounds());

            // Turn around to run back
            Vector3 lookHome = new Vector3(_homePosition.x, transform.position.y, _homePosition.z);
            transform.LookAt(lookHome);

            elapsed = 0f;
            while (elapsed < moveDuration)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / moveDuration);
                transform.position = Vector3.Lerp(endPos, _homePosition, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.position = _homePosition;
            transform.rotation = _homeRotation; // Reset to face opponent perfectly

            _animator.SetBool("isRunning", false);
        }
        else
        {
            // Blend to idle and simultaneously slide position/rotation smoothly back to home
            _animator.CrossFade(idleAnimatorState, 0.3f);

            Vector3 driftedPos = transform.position;
            Quaternion driftedRot = transform.rotation;
            float returnDuration = 0.35f;
            float returnElapsed = 0f;
            while (returnElapsed < returnDuration)
            {
                float t = Mathf.SmoothStep(0f, 1f, returnElapsed / returnDuration);
                transform.position = Vector3.Lerp(driftedPos, _homePosition, t);
                transform.rotation = Quaternion.Slerp(driftedRot, _homeRotation, t);
                returnElapsed += Time.deltaTime;
                yield return null;
            }
            transform.position = _homePosition;
            transform.rotation = _homeRotation;
        }

        _animator.CrossFade(idleAnimatorState, 0.2f);
        if (footstepRoutine != null) StopCoroutine(footstepRoutine);

        if (chargeVfxInstance != null)
        {
            GameObject parentContainer = chargeVfxInstance.transform.parent != transform ? chargeVfxInstance.transform.parent.gameObject : null;
            Destroy(chargeVfxInstance);
            if (parentContainer != null) Destroy(parentContainer);
        }
    }

    private IEnumerator AnimateProjectiles(Vector3 defaultTargetPos, AttackAction action, System.Action onHit, System.Action<Transform> onExtraHit = null, System.Action<Transform> onSpawnVfx = null, List<Transform> aoeTargets = null)
    {
        // Start roughly at hand/chest height, and slightly forward so it spawns in front of the character
        Vector3 forwardOffset = transform.forward * 0.5f;
        Vector3 startPos = transform.position + Vector3.up * 1f + forwardOffset;

        int count = Mathf.Max(1, action.projectileCount);
        float startAngle = -action.projectileSpreadAngle / 2f;
        float angleStep = count > 1 ? action.projectileSpreadAngle / (count - 1) : 0f;

        int targetMultiplier = (aoeTargets != null && aoeTargets.Count > 0) ? aoeTargets.Count : 1;
        int pending = count * targetMultiplier;

        for (int i = 0; i < count; i++)
        {
            if (BaseBattleManager.Instance != null && BaseBattleManager.Instance.IsBattleOver())
            {
                int remaining = (count - i) * targetMultiplier;
                pending -= remaining;
                break;
            }

            float currentAngle = startAngle + (angleStep * i);
            bool[] hitCalledHolder = new bool[] { false };

            if (aoeTargets != null && aoeTargets.Count > 0)
            {
                foreach (var t in aoeTargets)
                {
                    Vector3 currentTargetPos = t.position + Vector3.up * 1f;
                    Quaternion baseRotation = Quaternion.LookRotation(currentTargetPos - startPos);
                    Quaternion angledRotation = baseRotation * Quaternion.Euler(0, currentAngle, 0);

                    float distanceToTarget = Vector3.Distance(startPos, currentTargetPos);
                    Vector3 direction = angledRotation * Vector3.forward;
                    Vector3 finalTargetPos = startPos + direction * distanceToTarget;

                    System.Action projectileHitCb = () =>
                    {
                        onSpawnVfx?.Invoke(t);
                        if (action.hitVfxDamageDelay > 0f)
                        {
                            System.Action passedOnHit = hitCalledHolder[0] ? null : onHit;
                            hitCalledHolder[0] = true;
                            StartCoroutine(DelayedProjectileHit(action, passedOnHit, onExtraHit, t, () => pending--));
                        }
                        else
                        {
                            if (!hitCalledHolder[0])
                            {
                                onHit?.Invoke();
                                hitCalledHolder[0] = true;
                            }
                            onExtraHit?.Invoke(t);
                            pending--;
                        }
                    };

                    StartCoroutine(MoveSingleProjectile(startPos, finalTargetPos, action, projectileHitCb));
                }
            }
            else
            {
                Vector3 currentTargetPos = defaultTargetPos;
                Quaternion baseRotation = Quaternion.LookRotation(currentTargetPos - startPos);
                Quaternion angledRotation = baseRotation * Quaternion.Euler(0, currentAngle, 0);

                float distanceToTarget = Vector3.Distance(startPos, currentTargetPos);
                Vector3 direction = angledRotation * Vector3.forward;
                Vector3 finalTargetPos = startPos + direction * distanceToTarget;

                System.Action projectileHitCb = () =>
                {
                    onSpawnVfx?.Invoke(null);
                    if (action.hitVfxDamageDelay > 0f)
                    {
                        StartCoroutine(DelayedProjectileHit(action, onHit, onExtraHit, null, () => pending--));
                    }
                    else
                    {
                        onHit?.Invoke();
                        onExtraHit?.Invoke(null);
                        pending--;
                    }
                };

                StartCoroutine(MoveSingleProjectile(startPos, finalTargetPos, action, projectileHitCb));
            }

            if (action.delayBetweenProjectiles > 0)
            {
                yield return new WaitForSeconds(action.delayBetweenProjectiles);
            }
        }

        // Block until the last projectile has landed
        yield return new WaitUntil(() => pending <= 0);
    }

    private IEnumerator DelayedInstantHit(float delay, System.Action onHit, System.Action<Transform> onExtraHit, Transform mainTarget, List<Transform> aoeTargets)
    {
        yield return new WaitForSeconds(delay);
        onHit?.Invoke();

        if (aoeTargets != null && aoeTargets.Count > 0)
        {
            foreach (var t in aoeTargets) onExtraHit?.Invoke(t);
        }
        else
        {
            onExtraHit?.Invoke(mainTarget);
        }
    }

    private IEnumerator DelayedProjectileHit(AttackAction action, System.Action onHit, System.Action<Transform> onExtraHit, Transform target, System.Action onComplete)
    {
        yield return new WaitForSeconds(action.hitVfxDamageDelay);
        onHit?.Invoke();
        onExtraHit?.Invoke(target);
        onComplete?.Invoke();
    }

    private IEnumerator MoveSingleProjectile(Vector3 startPos, Vector3 targetPos, AttackAction action, System.Action onHit)
    {
        Quaternion initialRotation = action.rotateToFaceTarget
            ? Quaternion.LookRotation(targetPos - startPos)
            : action.projectilePrefab.transform.rotation;

        GameObject proj = Instantiate(action.projectilePrefab, startPos, initialRotation);

        // Grab the child graphics wrapper that we want to spin (assuming it has children)
        Transform graphicsTransform = proj.transform.childCount > 0 ? proj.transform.GetChild(0) : proj.transform;

        proj.transform.localScale *= action.projectileScaleMultiplier;

        float distance = Vector3.Distance(startPos, targetPos);
        float duration = distance / action.projectileSpeed;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (proj == null) break;

            // Move projectile root linearly
            proj.transform.position = Vector3.Lerp(startPos, targetPos, elapsed / duration);

            if (action.spinProjectile)
            {
                // Spin the graphics rapidly on its local Y axis (horizontally) instead of Z
                graphicsTransform.Rotate(0, 1500f * Time.deltaTime, 0, Space.Self);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Trigger damage and UI updates exactly when projectile lands
        onHit?.Invoke();

        if (action.returnsToCaster && proj != null)
        {
            elapsed = 0f;
            while (elapsed < duration)
            {
                if (proj == null) break;

                // Move projectile root linearly back to startPos
                proj.transform.position = Vector3.Lerp(targetPos, startPos, elapsed / duration);

                if (action.spinProjectile)
                {
                    // Spin the graphics rapidly on its local Y axis
                    graphicsTransform.Rotate(0, 1500f * Time.deltaTime, 0, Space.Self);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        if (proj != null)
        {
            Destroy(proj);
        }
    }

    private IEnumerator PlayGruntDelayed(AudioClip clip, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (TryGetComponent<AudioSource>(out var audioSource))
            audioSource.PlayOneShot(clip);
    }

    private IEnumerator PlayFootstepSounds()
    {
#if UNITY_EDITOR
        AudioClip stepSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Leohpaz/RPG_Essentials_Free/12_Player_Movement_SFX/08_Step_rock_02.wav");
        if (stepSound != null)
        {
            while (true)
            {
                AudioSource.PlayClipAtPoint(stepSound, transform.position);
                // The running animation has footsteps roughly every 0.3 seconds
                yield return new WaitForSeconds(0.3f);
            }
        }
#endif
        yield break;
    }

    private void Die()
    {
        _animator.SetBool("isDead", true);

        // Disable colliders so dead bodies don't float on their caps (check parent/children too)
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }

        if (TryGetComponent<UnityEngine.AI.NavMeshAgent>(out var agent))
        {
            agent.enabled = false;
        }

        if (TryGetComponent<CharacterController>(out var cc))
        {
            cc.enabled = false;
        }

        if (GetComponentInParent<CharacterController>() != null)
        {
            GetComponentInParent<CharacterController>().enabled = false;
        }

        StartCoroutine(SinkToGround());
    }

    /// <summary>Applies or refreshes a status effect (same type refreshes duration).</summary>
    public void ApplyStatusEffect(StatusEffect effect)
    {
        StatusEffect existing = activeStatusEffects.Find(e => e.type == effect.type);
        if (existing != null)
            existing.duration = Mathf.Max(existing.duration, effect.duration);
        else
            activeStatusEffects.Add(effect);
    }

    /// <summary>
    /// Processes all active status effects for the start of this unit's turn.
    /// Returns tick results that BaseBattleManager uses to apply burn damage, handle stuns, etc.
    /// Decrements durations and removes expired effects.
    /// </summary>
    public List<(string message, StatusEffectType type, int burnDamage)> TickStatusEffects()
    {
        var results = new List<(string, StatusEffectType, int)>();
        for (int i = activeStatusEffects.Count - 1; i >= 0; i--)
        {
            var e = activeStatusEffects[i];
            switch (e.type)
            {
                case StatusEffectType.Burn:
                    int dmg = Mathf.Max(1, Mathf.RoundToInt(maxHealth * e.value));
                    results.Add(($"{unitName} burns for {dmg} damage!", StatusEffectType.Burn, dmg));
                    break;
                case StatusEffectType.Stun:
                    results.Add(($"{unitName} is stunned and loses their turn!", StatusEffectType.Stun, 0));
                    break;
                case StatusEffectType.Weaken:
                    results.Add(($"{unitName} is weakened (attacks deal less damage).", StatusEffectType.Weaken, 0));
                    break;
                case StatusEffectType.Fortify:
                    results.Add(($"{unitName} stands fortified (reduced damage taken).", StatusEffectType.Fortify, 0));
                    break;
            }
            e.duration--;
            if (e.duration <= 0)
                activeStatusEffects.RemoveAt(i);
        }
        return results;
    }

    private IEnumerator SinkToGround()
    {
        yield return new WaitForSeconds(0.5f);

        float targetY = 0f;
        // Raycast from slightly above the current position down to find the ground
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 5f))
        {
            targetY = hit.point.y;
        }

        float startY = transform.position.y;

        // If already close enough to the ground, exit
        if (Mathf.Abs(startY - targetY) < 0.01f) yield break;

        float elapsed = 0f;
        float duration = 0.8f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Vector3 pos = transform.position;
            pos.y = Mathf.Lerp(startY, targetY, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            transform.position = pos;
            yield return null;
        }

        Vector3 finalPos = transform.position;
        finalPos.y = targetY;
        transform.position = finalPos;
    }
}

