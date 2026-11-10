using System.Collections;
using UnityEngine;

/// <summary>
/// Impact "feel": a small recoil on the target, a camera shake that scales with the hit, and a very short
/// hit-stop on crits and kills. Call <see cref="Play"/> once per damaging hit.
/// </summary>
public static class HitFeedback
{
    public static void Play(Unit target, int damage, bool crit)
    {
        if (target == null) return;

        bool kill = target.isDead;
        float weight = Mathf.Clamp01(damage / Mathf.Max(1f, target.maxHealth) * 2f);   // 0..1, a half-health hit is a full-weight hit

        if (kill) target.PlayDeathKnockback(0.35f + (crit ? 0.2f : 0f));
        else target.PlayRecoil(0.10f + 0.22f * weight + (crit ? 0.10f : 0f));

        float trauma = 0.10f + 0.30f * weight + (crit ? 0.25f : 0f) + (kill ? 0.20f : 0f);
        CameraShake.Add(trauma);

        if (crit || kill) HitStop.Do(kill ? 0.09f : 0.06f);
    }
}

/// <summary>Trauma-based camera shake: offset and a hint of roll driven by smooth noise, decaying quickly.</summary>
public class CameraShake : MonoBehaviour
{
    private const float MaxOffset = 0.11f;
    private const float MaxRoll = 1.2f;
    private const float Decay = 1.9f;

    private float _trauma;
    private Vector3 _lastOffset;
    private float _lastRoll;

    public static void Add(float amount)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        var shake = cam.GetComponent<CameraShake>();
        if (shake == null) shake = cam.gameObject.AddComponent<CameraShake>();
        shake._trauma = Mathf.Min(1f, shake._trauma + amount);
    }

    private void LateUpdate()
    {
        // Remove last frame's offset first, so anything else that moves the camera is left alone.
        transform.localPosition -= _lastOffset;
        transform.Rotate(0f, 0f, -_lastRoll, Space.Self);
        _lastOffset = Vector3.zero;
        _lastRoll = 0f;

        if (_trauma <= 0.001f) { _trauma = 0f; return; }

        float shake = _trauma * _trauma;
        float t = Time.unscaledTime * 24f;
        _lastOffset = new Vector3(Noise(t, 0f), Noise(t, 11f), Noise(t, 23f) * 0.5f) * (MaxOffset * shake);
        _lastRoll = Noise(t, 37f) * MaxRoll * shake;
        transform.localPosition += _lastOffset;
        transform.Rotate(0f, 0f, _lastRoll, Space.Self);

        _trauma = Mathf.Max(0f, _trauma - Decay * Time.unscaledDeltaTime);
    }

    private static float Noise(float t, float seed) => Mathf.PerlinNoise(t, seed) * 2f - 1f;
}

/// <summary>Freezes game time for a few frames on a heavy hit. Restores to 1 even if hits overlap.</summary>
public class HitStop : MonoBehaviour
{
    private static HitStop s_Instance;
    /// <summary>Set by the pause screen so a finishing hit-stop does not un-pause the game.</summary>
    public static bool Paused;
    private int _active;

    public static void Do(float seconds)
    {
        if (s_Instance == null)
        {
            var go = new GameObject("HitStop");
            DontDestroyOnLoad(go);
            s_Instance = go.AddComponent<HitStop>();
        }
        s_Instance.StartCoroutine(s_Instance.Run(seconds));
    }

    private IEnumerator Run(float seconds)
    {
        _active++;
        Time.timeScale = 0.04f;
        yield return new WaitForSecondsRealtime(seconds);
        _active--;
        if (_active <= 0)
        {
            _active = 0;
            Time.timeScale = Paused ? 0f : 1f;
        }
    }

    private void OnDisable()
    {
        if (_active > 0) { _active = 0; Time.timeScale = 1f; }
    }
}
