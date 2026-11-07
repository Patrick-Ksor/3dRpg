public enum StatusEffectType { None, Burn, Stun, Weaken, Fortify }

/// <summary>A single status effect instance on a unit.</summary>
public class StatusEffect
{
    public StatusEffectType type;
    public int duration;
    public float value;
    public string displayName;

    public static StatusEffect Burn(int turns = 2) =>
        new StatusEffect { type = StatusEffectType.Burn, duration = turns, value = 0.08f, displayName = "Burned" };

    public static StatusEffect Stun(int turns = 1) =>
        new StatusEffect { type = StatusEffectType.Stun, duration = turns, value = 0f, displayName = "Stunned" };

    public static StatusEffect Weaken(int turns = 2) =>
        new StatusEffect { type = StatusEffectType.Weaken, duration = turns, value = 0.25f, displayName = "Weakened" };

    public static StatusEffect Fortify(int turns = 2) =>
        new StatusEffect { type = StatusEffectType.Fortify, duration = turns, value = 0.25f, displayName = "Fortified" };
}
