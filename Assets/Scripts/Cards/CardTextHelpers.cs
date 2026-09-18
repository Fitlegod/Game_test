public static class CardTextHelpers
{
    public static string HitCountSuffix(int hitCount)
    {
        if (hitCount <= 1) return "";
        if (hitCount == 2) return " дважды";
        if (hitCount <= 4) return " " + hitCount + " раза";
        return " " + hitCount + " раз";
    }

    public static string GetGenitiveName(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Strength: return "Силы";
            case StatusEffectType.Weak: return "Слабости";
            case StatusEffectType.Toughness: return "Крепкости";
            case StatusEffectType.Frailty: return "Хрупкости";
            case StatusEffectType.Vulnerable: return "Уязвимости";
            case StatusEffectType.Stagger: return "Пошатывания";
            case StatusEffectType.Regen: return "Лечения";
            default: return type.ToString();
        }
    }
}
