using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Fades to black, loads a scene, then fades back in. Survives scene loads; created on demand
/// (or on the first scene load, so a scene entered directly in the Editor also fades in).
/// Use <see cref="Load"/> instead of SceneManager.LoadScene for player-facing transitions.
/// </summary>
public class SceneFader : MonoBehaviour
{
    private const float FadeOutTime = 0.25f;
    private const float FadeInTime = 0.4f;

    private static SceneFader s_Instance;

    private CanvasGroup _group;
    private bool _busy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        Instance.StartFade(1f, 0f, FadeInTime);   // start opaque, reveal the first scene
    }

    private static SceneFader Instance
    {
        get
        {
            if (s_Instance != null) return s_Instance;

            var go = new GameObject("SceneFader");
            DontDestroyOnLoad(go);
            s_Instance = go.AddComponent<SceneFader>();
            s_Instance.Build();
            return s_Instance;
        }
    }

    private void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;   // above every other canvas and the UI Toolkit panels

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.interactable = false;
        _group.blocksRaycasts = false;
        _group.alpha = 0f;

        var imgGO = new GameObject("Black");
        imgGO.transform.SetParent(transform, false);
        var img = imgGO.AddComponent<Image>();
        img.color = new Color(0.03f, 0.025f, 0.025f, 1f);
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>Fade out, load the scene, fade in. Ignores calls while a transition is running.</summary>
    public static void Load(string sceneName)
    {
        SceneFader f = Instance;
        if (f._busy) return;
        f.StartCoroutine(f.LoadRoutine(sceneName));
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        _busy = true;
        _group.blocksRaycasts = true;   // no clicks during the transition
        yield return Fade(_group.alpha, 1f, FadeOutTime);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (op != null && !op.isDone) yield return null;
        yield return null;   // let the new scene build its UI before revealing it

        yield return Fade(1f, 0f, FadeInTime);
        _group.blocksRaycasts = false;
        _busy = false;
    }

    private void StartFade(float from, float to, float time)
    {
        StartCoroutine(Fade(from, to, time));
    }

    private IEnumerator Fade(float from, float to, float time)
    {
        float t = 0f;
        _group.alpha = from;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / time));
            yield return null;
        }
        _group.alpha = to;
    }
}
