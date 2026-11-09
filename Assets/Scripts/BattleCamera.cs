using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Battle camera presets. Press C in battle to cycle them; the choice is remembered between sessions.
/// All presets look at the fight from the open (-X / -Z) side of the arena.
/// </summary>
public class BattleCamera : MonoBehaviour
{
    private struct Preset
    {
        public string name;
        public Vector3 position;
        public Vector3 euler;
        public float fov;
        public Preset(string name, Vector3 position, Vector3 euler, float fov)
        {
            this.name = name; this.position = position; this.euler = euler; this.fov = fov;
        }
    }

    // Heroes stand around (-2.5, 0, -1.5), enemies spread around (2.5, 0, -3). Each preset aims at the midpoint.
    private static readonly Preset[] Presets =
    {
        new Preset("Classic (low, over the shoulder)", new Vector3(-4f, 1.5f, -4.5f),   new Vector3(14f, 47f, 0f), 60f),
        new Preset("High three-quarter",               new Vector3(-6.5f, 4.5f, -8.5f), new Vector3(23f, 47f, 0f), 55f),
        new Preset("Side-on (hero left, foes right)",  new Vector3(-0.6f, 3.2f, -11f),  new Vector3(17f, 12f, 0f), 52f),
        new Preset("Wide and high",                    new Vector3(-7.6f, 6.2f, -9.8f), new Vector3(27f, 47f, 0f), 52f),
    };

    private const string PrefKey = "BattleCameraPreset";

    private Camera _camera;
    private int _index;
    private Coroutine _move;

    public static int PresetCount => Presets.Length;

    private void Start()
    {
        _camera = GetComponent<Camera>();
        _index = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, Presets.Length - 1);
        Apply(_index, instant: true);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            SetPreset((_index + 1) % Presets.Length);
    }

    /// <summary>Switches preset (smoothly unless <paramref name="instant"/>) and remembers it.</summary>
    public void SetPreset(int index, bool instant = false)
    {
        _index = Mathf.Clamp(index, 0, Presets.Length - 1);
        PlayerPrefs.SetInt(PrefKey, _index);
        Apply(_index, instant);
        Debug.Log($"[BattleCamera] {_index + 1}/{Presets.Length}: {Presets[_index].name}");
    }

    private void Apply(int index, bool instant)
    {
        if (_move != null) StopCoroutine(_move);
        Preset p = Presets[index];
        if (instant || _camera == null)
        {
            transform.SetPositionAndRotation(p.position, Quaternion.Euler(p.euler));
            if (_camera != null) _camera.fieldOfView = p.fov;
        }
        else
        {
            _move = StartCoroutine(MoveTo(p));
        }
    }

    private IEnumerator MoveTo(Preset p)
    {
        Vector3 fromPos = transform.position;
        Quaternion fromRot = transform.rotation;
        float fromFov = _camera.fieldOfView;
        Quaternion toRot = Quaternion.Euler(p.euler);

        const float duration = 0.6f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;   // keep moving through hit-stop
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            transform.position = Vector3.Lerp(fromPos, p.position, k);
            transform.rotation = Quaternion.Slerp(fromRot, toRot, k);
            _camera.fieldOfView = Mathf.Lerp(fromFov, p.fov, k);
            yield return null;
        }
        transform.SetPositionAndRotation(p.position, toRot);
        _camera.fieldOfView = p.fov;
        _move = null;
    }
}
