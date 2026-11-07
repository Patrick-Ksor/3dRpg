using UnityEngine;

/// <summary>
/// Assign your custom 32×32 sprites for each status effect icon here.
/// Create the asset via:  Assets ▶ Create ▶ RPG ▶ Status Icon Config
/// Then place the asset at:  Assets/Resources/StatusIconConfig.asset
/// Any slot left empty falls back to the coloured square with a unicode glyph.
/// </summary>
[CreateAssetMenu(fileName = "StatusIconConfig", menuName = "RPG/Status Icon Config")]
public class StatusIconConfig : ScriptableObject
{
    [Header("Status Effect Icons  (recommended: 32 × 32 px)")]
    public Sprite burnIcon;
    public Sprite stunIcon;
    public Sprite weakenIcon;
    public Sprite fortifyIcon;

    public Sprite GetIcon(StatusEffectType type) => type switch
    {
        StatusEffectType.Burn => burnIcon,
        StatusEffectType.Stun => stunIcon,
        StatusEffectType.Weaken => weakenIcon,
        StatusEffectType.Fortify => fortifyIcon,
        _ => null,
    };
}
