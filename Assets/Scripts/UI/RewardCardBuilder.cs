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
                                                 int price = 0, int currentGold = -1,
                                                 Action<VisualElement, RewardChoice> onPick = null)
    {
        bool isShop = price > 0;
        bool affordable = !isShop || currentGold < 0 || currentGold >= price;

        var card = new VisualElement();
        card.AddToClassList("reward-card");

        if (choice.legendaryRelic != null) card.AddToClassList("reward-card--relic");
        else if (IsLegendaryTrait(choice)) card.AddToClassList("reward-card--legendary");
        else if (choice.attackUpgrade != null && choice.attackUpgrade.upgradeType == AttackUpgradeType.UnlockNewAttack)
            card.AddToClassList("reward-card--skill");
        if (choice.curseEffect != null) card.AddToClassList("reward-card--curse");

        string tag = choice.curseEffect != null ? "CURSED"
                   : choice.legendaryRelic != null ? "LEGENDARY"
                   : IsLegendaryTrait(choice) ? "EPIC"
                   : choice.attackUpgrade != null && choice.attackUpgrade.upgradeType == AttackUpgradeType.UnlockNewAttack ? "RARE"
                   : "COMMON";
        card.Add(CreateTypeHeader(GetChoiceTypeLabel(choice), tag));

        var body = new VisualElement();
        body.AddToClassList("reward-card-body");

        // Framed icon: a custom sprite when one exists, otherwise a generic pictogram picked from USS.
        var frame = new VisualElement();
        frame.AddToClassList("reward-card-icon");
        Sprite icon = GetRewardIcon(choice);
        if (icon != null)
        {
            var img = new Image { sprite = icon };
            img.AddToClassList("reward-card-icon-img");
            frame.Add(img);
        }
        else
        {
            var glyph = new VisualElement();
            glyph.AddToClassList("reward-card-icon-img");
            glyph.AddToClassList(GetFallbackIconClass(choice));
            frame.Add(glyph);
        }
        body.Add(frame);

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

        if (onPick != null)
        {
            // Pick-then-confirm mode: the whole card is clickable, the screen owns the confirm button.
            card.AddToClassList("reward-card--pick");
            card.RegisterCallback<ClickEvent>(_ => onPick(card, choice));
        }
        else
        {
            string text = isShop ? (affordable ? "BUY" : "TOO EXPENSIVE") : "SELECT";
            card.Add(CreateSelectButton(text, affordable, () => onSelect?.Invoke(choice), confirm: isShop));
        }

        if (!affordable) card.style.opacity = 0.55f;
        return card;
    }

    /// <summary>A "choose your path" card: click to select, then confirm with the Enter button on the screen.</summary>
    public static VisualElement CreateRoomCard(RoomChoice choice, Action<VisualElement, RoomChoice> onPick)
    {
        var look = GetRoomLook(choice.encounterType);

        var card = new VisualElement();
        card.AddToClassList("reward-card");
        card.AddToClassList("reward-card--pick");
        card.AddToClassList("room-card");

        var header = new VisualElement();
        header.AddToClassList("reward-card-type");
        header.style.borderBottomColor = look.color;
        var kind = CreateLabel(choice.encounterType.ToString().ToUpperInvariant(), "reward-card-type-label");
        kind.style.color = look.color;
        header.Add(kind);
        card.Add(header);

        var body = new VisualElement();
        body.AddToClassList("reward-card-body");

        var frame = new VisualElement();
        frame.AddToClassList("reward-card-icon");
        frame.style.borderTopColor = frame.style.borderBottomColor = frame.style.borderLeftColor = frame.style.borderRightColor = look.color;
        var glyph = new VisualElement();
        glyph.AddToClassList("reward-card-icon-img");
        glyph.AddToClassList(look.icon);
        frame.Add(glyph);
        body.Add(frame);

        body.Add(CreateLabel(choice.title, "reward-card-title"));
        body.Add(CreateLabel(choice.description, "reward-card-desc"));
        card.Add(body);

        var stats = new VisualElement();
        stats.AddToClassList("room-card-stats");
        stats.Add(CreateStatRow("Reward", look.reward, new Color(0.93f, 0.89f, 0.81f)));
        stats.Add(CreateStatRow("Danger", look.danger, look.dangerColor));
        card.Add(stats);

        card.RegisterCallback<ClickEvent>(_ => onPick(card, choice));
        return card;
    }

    private static VisualElement CreateStatRow(string label, string value, Color valueColor)
    {
        var row = new VisualElement();
        row.AddToClassList("room-card-stat");
        row.Add(CreateLabel(label, "room-card-stat-label"));
        var v = CreateLabel(value, "room-card-stat-value");
        v.style.color = valueColor;
        row.Add(v);
        return row;
    }

    private static (Color color, string icon, string reward, string danger, Color dangerColor) GetRoomLook(EncounterType type)
    {
        switch (type)
        {
            case EncounterType.Merchant:
                return (new Color(0.91f, 0.82f, 0.35f), "icon-coin", "Shop", "None", new Color(0.56f, 0.83f, 0.59f));
            case EncounterType.Shrine:
                return (new Color(0.56f, 0.83f, 0.59f), "icon-star", "Blessing", "None", new Color(0.56f, 0.83f, 0.59f));
            case EncounterType.Mystery:
                return (new Color(0.76f, 0.60f, 0.94f), "icon-book", "Unknown", "Varies", new Color(0.70f, 0.64f, 0.56f));
            default:
                return (new Color(1.00f, 0.62f, 0.56f), "icon-sword", "Spoils + Gold", "Normal", new Color(0.91f, 0.82f, 0.35f));
        }
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private static VisualElement CreateTypeHeader(string text, string tag = "")
    {
        var header = new VisualElement();
        header.AddToClassList("reward-card-type");
        header.Add(CreateLabel(text, "reward-card-type-label"));
        if (!string.IsNullOrEmpty(tag)) header.Add(CreateLabel(tag, "reward-card-tag"));
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
        if (choice.battleItem != null) return "SUPPLIES";
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

    /// <summary>USS class (from GameUi.uss) of a generic icon for rewards that have no sprite in Resources/Rewards.</summary>
    public static string GetFallbackIconClass(RewardChoice choice)
    {
        if (choice.curseEffect != null) return "icon-skull";
        if (choice.battleItem != null) return choice.battleItem.itemType == ItemType.ManaPotion ? "icon-potion-mana" : "icon-potion-health";
        if (choice.legendaryRelic != null) return "icon-gem";
        if (choice.statUpgrade != null)
        {
            return choice.statUpgrade.upgradeType switch
            {
                UpgradeType.MaxHealth => "icon-heart",
                UpgradeType.Attack => "icon-sword",
                UpgradeType.Defense => "icon-shield",
                UpgradeType.Speed => "icon-energy",
                UpgradeType.MaxMana => "icon-star",
                UpgradeType.Luck => "icon-clover",
                UpgradeType.Vampiric => "icon-heart-dark",
                UpgradeType.Thorns => "icon-shield",
                UpgradeType.Regenerating => "icon-potion",
                UpgradeType.Enraged => "icon-bomb",
                _ => "icon-gem",
            };
        }
        if (choice.attackUpgrade != null)
        {
            return choice.attackUpgrade.upgradeType switch
            {
                AttackUpgradeType.UnlockNewAttack => "icon-book",
                AttackUpgradeType.DamageBoost => "icon-sword",
                AttackUpgradeType.ManaCostReduce => "icon-star",
                AttackUpgradeType.AoEUnlock => "icon-bomb",
                AttackUpgradeType.HitCountBoost => "icon-hammer",
                _ => "icon-energy",
            };
        }
        return "icon-gem";
    }

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
