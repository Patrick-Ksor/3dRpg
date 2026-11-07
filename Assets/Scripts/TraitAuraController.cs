using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages persistent trait aura GameObjects on a unit.
/// Vampiric, Thorns, and Regenerating auras are always-on.
/// The Enraged aura is dynamic — visible only when the unit is below 50% HP.
/// Each trait supports up to 2 stacked visual instances (second is scaled 1.2× and rotated 180°).
/// Call RefreshDynamicAuras() after every HP change on the owning unit.
/// </summary>
public class TraitAuraController : MonoBehaviour
{
    private Unit _unit;
    private readonly List<GameObject> _vampiricGOs = new List<GameObject>();
    private readonly List<GameObject> _thornsGOs = new List<GameObject>();
    private readonly List<GameObject> _regenGOs = new List<GameObject>();
    private readonly List<GameObject> _enragedGOs = new List<GameObject>();

    /// <summary>
    /// Initialise with the owning unit and lists of pre-instantiated aura GameObjects
    /// (any list may be empty if no prefab was assigned).
    /// </summary>
    public void Init(Unit unit,
        List<GameObject> vampiricInstances,
        List<GameObject> thornsInstances,
        List<GameObject> regenInstances,
        List<GameObject> enragedInstances)
    {
        _unit = unit;

        AddAll(_vampiricGOs, vampiricInstances, startActive: true);
        AddAll(_thornsGOs, thornsInstances, startActive: true);
        AddAll(_regenGOs, regenInstances, startActive: true);
        AddAll(_enragedGOs, enragedInstances, startActive: true);

        RefreshEnraged();
    }

    private static void AddAll(List<GameObject> dest, List<GameObject> src, bool startActive)
    {
        if (src == null) return;
        foreach (var go in src)
        {
            if (go == null) continue;
            go.SetActive(startActive);
            dest.Add(go);
        }
    }

    /// <summary>Call after every HP change to update all dynamic auras.</summary>
    public void RefreshDynamicAuras() => RefreshEnraged();

    private void RefreshEnraged()
    {
        if (_unit == null) return;
        bool shouldShow = _unit.currentHealth < _unit.maxHealth * 0.5f;
        foreach (var go in _enragedGOs)
            if (go != null && go.activeSelf != shouldShow)
                go.SetActive(shouldShow);
    }
}
