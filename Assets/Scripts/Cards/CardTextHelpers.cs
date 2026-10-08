public static class CardTextHelpers
{
    // Суффикс с ведущим пробелом или пустая строка; вставляется в фразу как {1}
    public static string HitCountSuffix(int hitCount)
    {
        if (hitCount <= 1) return "";
        if (hitCount == 2) return " " + Loc.Get("card.hits.twice");
        if (hitCount <= 4) return " " + Loc.Format("card.hits.few", hitCount);
        return " " + Loc.Format("card.hits.many", hitCount);
    }

    public static string GetGenitiveName(StatusEffectType type) => Loc.Get("effect.genitive." + type);
}
