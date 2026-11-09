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
    private int _idleHash;   // resolved idle state (the prefab's configured name may not exist in its controller)

    private Vector3 _homePosition;
    private Quaternion _homeRotation;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        // Movement is script-driven (lunges, return slides). Root motion on top of that makes units drift and then snap home.
        _animator.applyRootMotion = false;
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
        StartCoroutine(ResolveIdleState());
    }

    /// <summary>
    /// The controller's default state is the idle pose. Prefabs often name it "Barb_Idle" rather than the
    /// default "Idle", and CrossFade to a missing state is silently ignored, so find the real one.
    /// </summary>
    private IEnumerator ResolveIdleState()
    {
        int configured = Animator.StringToHash(idleAnimatorState);
        if (_animator.HasState(0, configured))
        {
            _idleHash = configured;
            yield break;
        }
        yield return null;   // the first frame has run, so the animator sits in its default state
        _idleHash = _animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
    }

    private void CrossFadeToIdle(float duration)
    {
        if (_idleHash != 0) _animator.CrossFadeInFixedTime(_idleHash, duration);
    }

    /// <summary>Smoothly turns to face a point on the ground plane.</summary>
    private IEnumerator TurnToward(Vector3 worldPoint, float duration)
    {
        Vector3 flat = new Vector3(worldPoint.x, transform.position.y, worldPoint.z) - transform.position;
        if (flat.sqrMagnitude < 0.0001f) yield break;
        yield return TurnTo(Quaternion.LookRotation(flat), duration);
    }

    private IEnumerator TurnTo(Quaternion target, float duration)
    {
        Quaternion start = transform.rotation;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(start, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        transform.rotation = target;
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

    private float _hitUntil;
    private Coroutine _hitRoutine;
    private Coroutine _recoilRoutine;

    public void TriggerHitAnimation()
    {
        // If currently in a hit state, rewind it so rapid multi-hits keep flinching
        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName("Hit") || stateInfo.IsName("GetHit") || stateInfo.IsName("Damage")
            || stateInfo.IsName("Barb_Hit") || stateInfo.IsName("Knight_Hit") || stateInfo.IsName("Skeleton_Hit"))
        {
            _animator.Play(stateInfo.fullPathHash, -1, 0f);
        }

        // One routine holds isHit until the LAST hit's window ends (overlapping routines used to clear it early).
        _hitUntil = Time.time + 0.2f;
        if (_hitRoutine == null) _hitRoutine = StartCoroutine(HandleHitAnimation());
    }

    private IEnumerator HandleHitAnimation()
    {
        _animator.SetBool("isHit", true);
        while (Time.time < _hitUntil) yield return null;
        _animator.SetBool("isHit", false);
        _hitRoutine = null;
    }

    /// <summary>A quick shove backwards from the hit and an eased return. Skipped while the unit is away from home (lunging).</summary>
    public void PlayRecoil(float distance)
    {
        if (isDead) return;
        if ((transform.position - _homePosition).sqrMagnitude > 0.04f) return;
        if (_recoilRoutine != null) StopCoroutine(_recoilRoutine);
        _recoilRoutine = StartCoroutine(RecoilRoutine(distance));
    }

    /// <summary>The killing blow shoves the body back and lets it stay there (no return).</summary>
    public void PlayDeathKnockback(float distance)
    {
        if (_recoilRoutine != null) StopCoroutine(_recoilRoutine);
        _recoilRoutine = StartCoroutine(DeathKnockbackRoutine(distance));
    }

    private IEnumerator DeathKnockbackRoutine(float distance)
    {
        Vector3 back = -(_homeRotation * Vector3.forward);
        back.y = 0f;
        back.Normalize();
        Vector3 start = transform.position;
        const float duration = 0.4f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / duration), 3f);   // fast out, long settle
            transform.position = start + back * (distance * k);
            yield return null;
        }
        _recoilRoutine = null;
    }

    private IEnumerator RecoilRoutine(float distance)
    {
        Vector3 back = -(_homeRotation * Vector3.forward);   // units face their opponent, so "back" is away from the fight
        back.y = 0f;
        back.Normalize();

        const float outTime = 0.06f, backTime = 0.24f;
        float t = 0f;
        while (t < outTime)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / outTime), 2f);   // fast ease-out
            transform.position = _homePosition + back * (distance * k);
            yield return null;
        }

        t = 0f;
        while (t < backTime)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(t / backTime));
            transform.position = _homePosition + back * (distance * k);
            yield return null;
        }

        transform.position = _homePosition;
        _recoilRoutine = null;
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

        // Face the target (a quick turn instead of a snap)
        yield return TurnToward(lungeAnchor, 0.12f);

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
        int attackHash = Animator.StringToHash(action.animationName);
        if (_animator.HasState(0, attackHash))
            _animator.CrossFadeInFixedTime(attackHash, 0.1f);
        else
            Debug.LogWarning($"[Unit] {unitName}: animator has no state '{action.animationName}' for {action.actionName}.");

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
            yield return TurnToward(_homePosition, 0.12f);

            Vector3 runFrom = transform.position;   // not endPos: the attack may have moved us
            elapsed = 0f;
            while (elapsed < moveDuration)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / moveDuration);
                transform.position = Vector3.Lerp(runFrom, _homePosition, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.position = _homePosition;
            _animator.SetBool("isRunning", false);
            yield return TurnTo(_homeRotation, 0.15f);   // settle back to facing the opponent
        }
        else
        {
            // Blend to idle and simultaneously slide position/rotation smoothly back to home
            CrossFadeToIdle(0.3f);

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

        CrossFadeToIdle(0.2f);
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

            // Ease-in so the projectile launches instead of appearing at full speed (same total flight time)
            float k = Mathf.Clamp01(elapsed / duration);
            proj.transform.position = Vector3.Lerp(startPos, targetPos, Mathf.Lerp(k, k * k, 0.55f));

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

                // Ease the return trip so it does not whip around at the turn
                proj.transform.position = Vector3.Lerp(targetPos, startPos, Mathf.SmoothStep(0f, 1f, elapsed / duration));

                if (action.spinProjectile)
                {
                    // Spin the graphics rapidly on its local Y axis
                    graphicsTransform.Rotate(0, 1500f * Time.deltaTime, 0, Space.Self);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        if (proj != null) RetireProjectile(proj);
    }

    /// <summary>
    /// Hides the projectile body but lets its particles and trails fade out, instead of vanishing mid-trail.
    /// </summary>
    private static void RetireProjectile(GameObject proj)
    {
        foreach (var r in proj.GetComponentsInChildren<Renderer>())
            if (!(r is ParticleSystemRenderer) && !(r is TrailRenderer)) r.enabled = false;
        foreach (var ps in proj.GetComponentsInChildren<ParticleSystem>())
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(proj, 1.2f);
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

