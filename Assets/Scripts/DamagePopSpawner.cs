using System.Collections;
using TMPro;
using UnityEngine;

public enum PopType { Normal, Crit, Heal, Thorns, Burn, Miss }

/// <summary>
/// Spawns floating damage/heal numbers above units using the shared FloatingHealthBar overlay canvas.
/// Call Show() from any context — it manages its own MonoBehaviour host.
/// </summary>
public class DamagePopSpawner : MonoBehaviour
{
    private static DamagePopSpawner s_Instance;

    private static readonly Color ColorNormal = new Color(0.93f, 0.89f, 0.81f); // parchment
    private static readonly Color ColorCrit = new Color(0.95f, 0.76f, 0.34f);  // gold (matches the UI accent)
    private static readonly Color ColorHeal = new Color(0.18f, 0.82f, 0.44f);  // green
    private static readonly Color ColorThorns = new Color(1.00f, 0.53f, 0.13f);  // orange
    private static readonly Color ColorBurn = new Color(1.00f, 0.35f, 0.10f);  // red-orange
    private static readonly Color ColorMiss = new Color(0.65f, 0.65f, 0.65f);  // grey

    /// <summary>Shows a damage or heal pop above the given world-space transform.</summary>
    public static void Show(Transform source, int amount, PopType type)
    {
        if (source == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 screenPos = cam.WorldToScreenPoint(source.position + Vector3.up * 2.2f);
        if (screenPos.z < 0f) return;
        screenPos.y = Mathf.Min(screenPos.y, Screen.height - 80f);   // keep the rise on screen

        if (type == PopType.Miss) AudioManager.Play(Sfx.Miss, 0.8f);
        else if (type == PopType.Heal) AudioManager.Play(Sfx.Heal, 0.7f);

        EnsureInstance();
        s_Instance.StartCoroutine(s_Instance.PopRoutine(screenPos, amount, type));
    }

    private static void EnsureInstance()
    {
        if (s_Instance != null) return;
        var go = new GameObject("DamagePopSpawner");
        DontDestroyOnLoad(go);
        s_Instance = go.AddComponent<DamagePopSpawner>();
    }

    private IEnumerator PopRoutine(Vector3 screenPos, int amount, PopType type)
    {
        Canvas canvas = FloatingHealthBar.GetSharedCanvas();
        if (canvas == null) yield break;

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, new Vector2(screenPos.x, screenPos.y), null, out Vector2 localPos);

        var go = new GameObject("DmgPop");
        go.transform.SetParent(canvas.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(80f, 30f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = localPos;

        var lbl = go.AddComponent<TextMeshProUGUI>();
        lbl.text = FormatText(amount, type);
        lbl.fontSize = type == PopType.Crit ? 36f : type == PopType.Miss ? 22f : 26f;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.color = GetColor(type);
        lbl.fontStyle = FontStyles.Bold;
        lbl.enableWordWrapping = false;
        lbl.raycastTarget = false;
        // Dark outline keeps the numbers readable over bright floors and walls.
        lbl.outlineWidth = 0.22f;
        lbl.outlineColor = new Color32(12, 9, 9, 255);
        rt.sizeDelta = new Vector2(160f, 50f);

        float duration = type == PopType.Crit ? 1.05f : 0.9f;
        float floatDistance = type == PopType.Crit ? 70f : 54f;
        float drift = Random.Range(-14f, 14f);
        float elapsed = 0f;
        Vector2 startPos = rt.anchoredPosition;
        Color startColor = lbl.color;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float rise = 1f - (1f - t) * (1f - t);                 // ease-out
            Vector2 pos = startPos + new Vector2(drift * rise, floatDistance * rise);
            if (type == PopType.Crit && t < 0.25f)                  // quick shake on a crit
                pos += new Vector2(Mathf.Sin(elapsed * 90f) * 4f * (1f - t / 0.25f), 0f);
            rt.anchoredPosition = pos;

            // Punch in: start big, settle to 1 within the first 0.15s.
            float punch = Mathf.Clamp01(elapsed / 0.15f);
            float scale = Mathf.Lerp(type == PopType.Crit ? 1.7f : 1.4f, 1f, punch * punch);
            rt.localScale = Vector3.one * scale;

            float alpha = t < 0.55f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.55f) / 0.45f);
            lbl.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(go);
    }

    private static string FormatText(int amount, PopType type)
    {
        if (type == PopType.Miss) return "MISS";
        if (type == PopType.Heal) return $"+{amount}";
        if (type == PopType.Crit) return amount + "!";
        return amount.ToString();
    }

    private static Color GetColor(PopType type) => type switch
    {
        PopType.Crit => ColorCrit,
        PopType.Heal => ColorHeal,
        PopType.Thorns => ColorThorns,
        PopType.Burn => ColorBurn,
        PopType.Miss => ColorMiss,
        _ => ColorNormal,
    };
}
