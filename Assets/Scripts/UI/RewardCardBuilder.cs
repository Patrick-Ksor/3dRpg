using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Builds the card elements used by the reward, merchant, event and room-choice screens,
/// so the layout and rarity styling live in one place (styles: Resources/UI/GameUi.uss).
/// </summary>
public static class RewardCardBuilder
{
    /// <summary>
    /// A reward card, or a merchant card when <paramref name="price"/> &gt; 0 (shows the price, how much gold is
    /// missing, and asks for a second click to confirm the purchase).
    /// </summary>
    public static VisualElement CreateRewardCard(RewardChoice choice, Action<RewardChoice> onSelect,
                                                 int price = 0, int currentGold = -1)
    {
        bool isShop = price > 0;
        bool affordable = !isShop || currentGold < 0 || currentGold >= price;

        var card = new VisualElement();
        card.AddToClassList("reward-card");

        if (choice.legendaryRelic != null) card.AddToClassList("reward-card--relic");
        else if (IsLegendaryTrait(choice)) card.AddToClassList("reward-card--legendary");
        if (choice.curseEffect != null) card.AddToClassList("reward-card--curse");

        card.Add(CreateTypeHeader(GetChoiceTypeLabel(choice)));

        var body = new VisualElement();
        body.AddToClassList("reward-card-body");

        Sprite icon = GetRewardIcon(choice);
        if (icon != null)
        {
            var img = new Image { sprite = icon };
            img.AddToClassList("reward-card-icon");
            body.Add(img);
        }

        body.Add(CreateLabel(choice.name, "reward-card-name"));
        body.Add(CreateLabel(choice.description, "reward-card-desc"));

        if (choice.legendaryRelic != null && !string.IsNullOrEmpty(choice.targetUnitName))
            body.Add(CreateLabel($"For: {choice.targetUnitName}", "reward-card-for"));

        card.Add(body);

        if (isShop)
        {
            var priceRow = new VisualElement();
            priceRow.AddToClassList("reward-card-price");
            var coin = new VisualElement();
            coin.AddToClassList("reward-card-price-icon");
            priceRow.Add(coin);
            priceRow.Add(CreateLabel($"{price}", affordable ? "reward-card-price-text" : "reward-card-price-text reward-card-price-text--short"));
            card.Add(priceRow);

            if (!affordable)
                card.Add(CreateLabel($"Need {price - currentGold} more gold", "reward-card-need"));
        }

        string text = isShop ? (affordable ? "BUY" : "TOO EXPENSIVE") : "SELECT";
        card.Add(CreateSelectButton(text, affordable, () => onSelect?.Invoke(choice), confirm: isShop));

        if (!affordable) card.style.opacity = 0.55f;
        return card;
    }

    /// <summary>A "choose your path" room card.</summary>
    public static VisualElement CreateRoomCard(RoomChoice choice, Action<RoomChoice> onSelect)
    {
        var card = new VisualElement();
        card.AddToClassList("reward-card");

        card.Add(CreateTypeHeader(choice.encounterType.ToString().ToUpperInvariant()));

        var body = new VisualElement();
        body.AddToClassList("reward-card-body");
        body.Add(CreateLabel(choice.title, "reward-card-title"));
        body.Add(CreateLabel(choice.description, "reward-card-desc"));
        card.Add(body);

        card.Add(CreateSelectButton("ENTER", true, () => onSelect?.Invoke(choice)));
        return card;
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private static VisualElement CreateTypeHeader(string text)
    {
        var header = new VisualElement();
        header.AddToClassList("reward-card-type");
        header.Add(CreateLabel(text, "reward-card-type-label"));
        return header;
    }

    private static Label CreateLabel(string text, string cssClass)
    {
        var label = new Label(text);
        label.AddToClassList(cssClass);
        return label;
    }

    /// <param name="confirm">When true, the first click arms the button ("CONFIRM?") and a second click within 2.5s fires it.</param>
    private static Button CreateSelectButton(string text, bool enabled, Action onClick, bool confirm = false)
    {
        Button btn = null;
        bool armed = false;
        btn = new Button(() =>
        {
            if (!confirm || armed) { onClick?.Invoke(); return; }

            armed = true;
            btn.text = "CONFIRM?";
            btn.AddToClassList("reward-button--confirm");
            btn.schedule.Execute(() =>
            {
                armed = false;
                btn.text = text;
                btn.RemoveFromClassList("reward-button--confirm");
            }).StartingIn(2500);
        }) { text = text };
        btn.AddToClassList("reward-button");
        btn.SetEnabled(enabled);
        return btn;
    }

    private static bool IsTrait(UpgradeType type) =>
        type == UpgradeType.Vampiric || type == UpgradeType.Thorns ||
        type == UpgradeType.Regenerating || type == UpgradeType.Enraged;

    private static bool IsLegendaryTrait(RewardChoice choice) =>
        choice.statUpgrade != null && IsTrait(choice.statUpgrade.upgradeType);

    public static string GetChoiceTypeLabel(RewardChoice choice)
    {
        if (choice.curseEffect != null) return "CURSE";
        if (choice.legendaryRelic != null) return "RELIC";
        if (choice.statUpgrade != null)
            return IsTrait(choice.statUpgrade.upgradeType) ? "TRAIT" : "UPGRADE";
        if (choice.attackUpgrade != null)
            return choice.attackUpgrade.upgradeType == AttackUpgradeType.UnlockNewAttack
                ? "NEW SKILL" : "ATTACK UP";
        return "REWARD";
    }

    private static Sprite LoadRewardIcon(string resourceName) =>
        Resources.Load<Sprite>($"Rewards/{resourceName}");

    public static Sprite GetRewardIcon(RewardChoice choice)
    {
        // Relic icon from the SO asset itself
        if (choice.legendaryRelic != null && choice.legendaryRelic.icon != null)
            return choice.legendaryRelic.icon;

        // Attack-specific icon (attack name, spaces stripped)
        if (choice.attackUpgrade != null && !string.IsNullOrEmpty(choice.attackUpgrade.targetAttackName))
        {
            Sprite s = LoadRewardIcon(choice.attackUpgrade.targetAttackName.Replace(" ", ""));
            if (s != null) return s;
        }

        // Type fallback
        if (choice.statUpgrade != null)
        {
            string key = choice.statUpgrade.upgradeType switch
            {
                UpgradeType.MaxHealth => "MaxHealth",
                UpgradeType.Attack => "Attack",
                UpgradeType.Defense => "Defense",
                UpgradeType.Speed => "Speed",
                UpgradeType.MaxMana => "MaxMana",
                UpgradeType.Luck => "Luck",
                UpgradeType.Vampiric => "Vampiric",
                UpgradeType.Thorns => "Thorns",
                UpgradeType.Regenerating => "Regenerating",
                UpgradeType.Enraged => "Enraged",
                _ => null,
            };
            if (key != null) return LoadRewardIcon(key);
        }

        if (choice.attackUpgrade != null)
        {
            string key = choice.attackUpgrade.upgradeType switch
            {
                AttackUpgradeType.DamageBoost => "Damage",
                AttackUpgradeType.ManaCostReduce => "ManaCost",
                AttackUpgradeType.AoEUnlock => "AoE",
                AttackUpgradeType.ProjectileCountBoost => "Projectile",
                AttackUpgradeType.SpreadAngleBoost => "Spread",
                AttackUpgradeType.UnlockNewAttack => "NewAttack",
                AttackUpgradeType.HitCountBoost => "MultiHit",
                _ => null,
            };
            if (key != null) return LoadRewardIcon(key);
        }

        return null;
    }
}
