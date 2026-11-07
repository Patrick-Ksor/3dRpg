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

    private static readonly Color ColorNormal = Color.white;
    private static readonly Color ColorCrit = new Color(1.00f, 0.85f, 0.00f);  // gold
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
        lbl.fontSize = type == PopType.Crit ? 22f : 17f;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.color = GetColor(type);
        lbl.fontStyle = type == PopType.Crit ? FontStyles.Bold : FontStyles.Normal;

        float duration = 0.85f;
        float floatDistance = 42f;
        float elapsed = 0f;
        Vector2 startPos = rt.anchoredPosition;
        Color startColor = lbl.color;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            rt.anchoredPosition = startPos + Vector2.up * (floatDistance * t);
            float alpha = t < 0.5f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.5f) / 0.5f);
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
