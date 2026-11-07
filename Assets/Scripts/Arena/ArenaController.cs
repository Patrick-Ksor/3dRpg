using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime handle for the built arena. Owns the lighting mood and recolours torches, fog and
/// ambient light as the run gets harder (tier 1 → warm, tier 2 → ember, tier 3 → violet,
/// mini-boss → fiery, boss → blood red).
/// </summary>
public class ArenaController : MonoBehaviour
{
    public static ArenaController Instance { get; private set; }

    private ArenaThemeSO _theme;
    private Light _sun;
    private Camera _cam;
    private List<Light> _torches;
    private Coroutine _fade;

    // Torch / fog / sun-intensity targets for a given mood.
    private struct Mood
    {
        public Color torch;
        public Color fog;
        public float sunScale;
        public float fogScale;
    }

    public void Init(ArenaThemeSO theme, Light sun, Camera cam, List<Light> torches)
    {
        Instance = this;
        _theme = theme;
        _sun = sun;
        _cam = cam;
        _torches = torches;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = theme.ambientColor;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = theme.fogColor;
        RenderSettings.fogDensity = theme.fogDensity;

        if (_sun != null)
        {
            _sun.color = theme.directionalColor;
            _sun.intensity = theme.directionalIntensity;
            _sun.shadows = LightShadows.Soft;
        }
        if (_cam != null)
        {
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = theme.backgroundColor;
        }

        Apply(1, false, false, instant: true);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Sets the arena mood for the current encounter. Safe to call every encounter.</summary>
    public void Apply(int encounter, bool isBoss, bool isMiniBoss, bool instant = false)
    {
        if (_theme == null) return;

        Mood m = GetMood(encounter, isBoss, isMiniBoss);
        if (_fade != null) StopCoroutine(_fade);
        if (instant) SetMood(m, 1f, GetCurrent());
        else _fade = StartCoroutine(FadeTo(m, 1f));
    }

    private Mood GetMood(int encounter, bool isBoss, bool isMiniBoss)
    {
        int tier = RunManager.GetEncounterTier(encounter);
        var m = new Mood { sunScale = 1f, fogScale = 1f, fog = _theme.fogColor };

        if (isBoss)
        {
            m.torch = new Color(1f, 0.12f, 0.08f);
            m.fog = new Color(0.14f, 0.02f, 0.03f);
            m.sunScale = 0.6f;
            m.fogScale = 1.5f;
        }
        else if (isMiniBoss)
        {
            m.torch = new Color(1f, 0.35f, 0.12f);
            m.fog = new Color(0.10f, 0.04f, 0.03f);
            m.sunScale = 0.8f;
            m.fogScale = 1.25f;
        }
        else if (tier == 1)
        {
            m.torch = new Color(1f, 0.6f, 0.25f);
        }
        else if (tier == 2)
        {
            m.torch = new Color(1f, 0.45f, 0.2f);
            m.fog = new Color(0.06f, 0.06f, 0.1f);
            m.sunScale = 0.9f;
        }
        else
        {
            m.torch = new Color(0.7f, 0.4f, 1f);
            m.fog = new Color(0.07f, 0.04f, 0.12f);
            m.sunScale = 0.8f;
            m.fogScale = 1.2f;
        }
        return m;
    }

    private Mood GetCurrent()
    {
        return new Mood
        {
            torch = _torches != null && _torches.Count > 0 && _torches[0] != null ? _torches[0].color : Color.white,
            fog = RenderSettings.fogColor,
            sunScale = _sun != null && _theme.directionalIntensity > 0f ? _sun.intensity / _theme.directionalIntensity : 1f,
            fogScale = _theme.fogDensity > 0f ? RenderSettings.fogDensity / _theme.fogDensity : 1f,
        };
    }

    private IEnumerator FadeTo(Mood target, float duration)
    {
        Mood from = GetCurrent();
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            SetMood(target, Mathf.Clamp01(t / duration), from);
            yield return null;
        }
        SetMood(target, 1f, from);
        _fade = null;
    }

    private void SetMood(Mood target, float k, Mood from)
    {
        if (_torches != null)
            foreach (var l in _torches)
                if (l != null) l.color = Color.Lerp(from.torch, target.torch, k);

        RenderSettings.fogColor = Color.Lerp(from.fog, target.fog, k);
        RenderSettings.fogDensity = _theme.fogDensity * Mathf.Lerp(from.fogScale, target.fogScale, k);
        if (_sun != null)
            _sun.intensity = _theme.directionalIntensity * Mathf.Lerp(from.sunScale, target.sunScale, k);
    }
}

/// <summary>Gently flickers a point light's intensity and range, like a torch flame.</summary>
[RequireComponent(typeof(Light))]
public class TorchFlicker : MonoBehaviour
{
    [SerializeField] private float _amount = 0.18f;
    [SerializeField] private float _speed = 2.2f;

    private Light _light;
    private float _baseIntensity;
    private float _baseRange;
    private float _seed;

    private void Awake()
    {
        _light = GetComponent<Light>();
        _baseIntensity = _light.intensity;
        _baseRange = _light.range;
        _seed = Random.value * 100f;
    }

    private void Update()
    {
        float n = Mathf.PerlinNoise(_seed, Time.time * _speed) * 2f - 1f;
        _light.intensity = _baseIntensity * (1f + n * _amount);
        _light.range = _baseRange * (1f + n * _amount * 0.3f);
    }
}
