using UnityEngine;

/// <summary>
/// Renders a hero prefab (idle animation, gentle sway) into a RenderTexture for the character-select screen.
/// The rig sits far below the origin so it never shows up in the scene itself. Destroyed with the menu scene.
/// </summary>
public class HeroPreview : MonoBehaviour
{
    private static readonly Vector3 RigOrigin = new Vector3(0f, -500f, 0f);

    private RenderTexture _texture;
    private Camera _camera;
    private GameObject _model;
    private float _swayStart;

    public RenderTexture Texture => _texture;

    public static HeroPreview Create()
    {
        var go = new GameObject("HeroPreview");
        go.transform.position = RigOrigin;
        var preview = go.AddComponent<HeroPreview>();
        preview.Build();
        return preview;
    }

    private void Build()
    {
        _texture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32) { name = "HeroPreviewRT", antiAliasing = 4 };

        var camGo = new GameObject("PreviewCamera");
        camGo.transform.SetParent(transform, false);
        _camera = camGo.AddComponent<Camera>();
        _camera.targetTexture = _texture;
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        _camera.fieldOfView = 26f;
        _camera.nearClipPlane = 0.1f;
        _camera.farClipPlane = 30f;
        _camera.allowHDR = false;
        _camera.enabled = true;

        var lightGo = new GameObject("PreviewLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        light.color = new Color(1f, 0.93f, 0.82f);
    }

    /// <summary>Shows the prefab, replacing the previous one. Returns false if it cannot be previewed.</summary>
    public bool Show(GameObject prefab)
    {
        if (_model != null) Destroy(_model);
        _model = null;
        if (prefab == null) return false;

        _model = Instantiate(prefab, transform);
        _model.transform.localPosition = Vector3.zero;
        _model.transform.localRotation = Quaternion.identity;

        // The prefab is a battle unit; we only want its model and idle pose.
        var unit = _model.GetComponent<Unit>();
        if (unit != null) unit.enabled = false;
        foreach (var audio in _model.GetComponentsInChildren<AudioSource>()) audio.enabled = false;
        foreach (var ps in _model.GetComponentsInChildren<ParticleSystem>()) ps.gameObject.SetActive(false);

        Bounds b = default;
        bool has = false;
        foreach (var r in _model.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        if (!has) { Destroy(_model); _model = null; return false; }

        // Frame the whole body from the front.
        float height = b.size.y * 1.12f;   // body height with a little headroom (width varies with the pose)
        float distance = (height * 0.5f) / Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        Vector3 focus = b.center;
        _camera.transform.position = focus + new Vector3(0f, height * 0.04f, distance);
        _camera.transform.LookAt(focus);

        _swayStart = Time.time;
        return true;
    }

    private void Update()
    {
        if (_model == null) return;
        // Turn a few degrees each way, biased toward the camera, so the face stays visible.
        float sway = 14f + Mathf.Sin((Time.time - _swayStart) * 0.9f) * 16f;
        _model.transform.localRotation = Quaternion.Euler(0f, sway, 0f);
    }

    private void OnDestroy()
    {
        if (_texture != null)
        {
            _texture.Release();
            Destroy(_texture);
        }
    }
}
