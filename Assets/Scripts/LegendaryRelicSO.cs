using System;
using System.Collections.Generic;
using UnityEngine;

public enum RelicSocket { RightHand, LeftHand, Head, Back }

[Serializable]
public class RelicVisualAttachment
{
    [Tooltip("The prefab to instantiate at the socket (e.g. a weapon or armour mesh).")]
    public GameObject prefab;
    public RelicSocket socket;
}

/// <summary>
/// ScriptableObject that defines a legendary relic reward.
/// Create via Assets > Create > RPG > Legendary Relic.
/// </summary>
[CreateAssetMenu(fileName = "NewLegendaryRelic", menuName = "RPG/Legendary Relic")]
public class LegendaryRelicSO : ScriptableObject
{
    [Tooltip("Must match unit.unitName exactly, or use 'Any' for universal relics.")]
    public string targetCharacterName = "Any";

    public string relicName;

    [TextArea(2, 4)]
    public string description;

    [TextArea(1, 3)]
    public string flavorText;

    public Sprite icon;

    [Header("Stat Boosts")]
    [Tooltip("All RunUpgrade entries are applied to the unit when the relic is equipped.")]
    public List<RunUpgrade> statBoosts = new List<RunUpgrade>();

    [Header("Granted Attack")]
    [Tooltip("This AttackAction is cloned and added to the unit's available attacks.")]
    public AttackAction grantedAttack;

    [Header("Visual Attachments")]
    [Tooltip("Prefabs instantiated onto the character's RelicAttachmentPoints sockets.")]
    public List<RelicVisualAttachment> visuals = new List<RelicVisualAttachment>();
}
