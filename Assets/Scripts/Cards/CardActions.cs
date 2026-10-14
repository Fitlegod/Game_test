using UnityEngine;

public enum InstantActionKind
{
    Damage,
    Block,
    Heal,
    Rewind // amount = на сколько секунд откатить бой; hitCount игнорируется
}

[System.Serializable]
public class InstantActionEntry
{
    public InstantActionKind kind;
    public int amount;
    public int hitCount = 1;
    public EffectTargetTag targetTag;

    // Возвращает «исходящее» число: урон после модификаторов источника (до модификаторов цели), блок после Крепкости/Хрупкости получателя, лечение.
    // outgoingOverride — число из первого срабатывания карты (повтор после отката): формулы источника заново не считаются.
    public int Apply(CombatManager combatManager, Combatant source, Combatant target, int? outgoingOverride = null)
    {
        if (kind == InstantActionKind.Rewind)
        {
            combatManager.Rewind(amount);
            return amount;
        }

        int outgoing = amount;

        for (int i = 0; i < hitCount; i++)
        {
            switch (kind)
            {
                case InstantActionKind.Damage:
                    outgoing = outgoingOverride ?? source.CalculateOutgoingDamage(amount);
                    int finalDamage = target.ApplyIncomingDamageModifiers(outgoing);
                    target.TakeDamage(finalDamage);
                    Debug.Log("Наносит " + finalDamage + " урона (база " + amount + ") цели " + target.name);
                    break;

                case InstantActionKind.Block:
                    Combatant blockRecipient = target ?? source;
                    int finalBlock = outgoing = outgoingOverride ?? blockRecipient.CalculateIncomingBlock(amount);
                    blockRecipient.GainBlock(finalBlock);
                    Debug.Log("Даёт " + finalBlock + " блока цели " + blockRecipient.name + " (база " + amount + ")");
                    break;

                case InstantActionKind.Heal:
                    Combatant healRecipient = target ?? source;
                    healRecipient.Heal(amount);
                    Debug.Log("Лечит " + amount + " HP цели " + healRecipient.name);
                    break;
            }
        }
        return outgoing;
    }

    public int ComputePreviewAmount(Combatant source, Combatant recipient)
    {
        switch (kind)
        {
            case InstantActionKind.Damage:
                int dmg = source.CalculateOutgoingDamage(amount);
                if (recipient != null) dmg = recipient.ApplyIncomingDamageModifiers(dmg);
                return dmg;
            case InstantActionKind.Block:
                return recipient != null ? recipient.CalculateIncomingBlock(amount) : amount;
            default:
                return amount;
        }
    }

    public string Describe(int displayValue)
    {
        return Loc.Format("card.action." + kind + "." + TagGroup(targetTag), displayValue, CardTextHelpers.HitCountSuffix(hitCount));
    }

    // Enemy / AllEnemies / Player (всё остальное — действие на себя)
    public static string TagGroup(EffectTargetTag tag)
    {
        if (tag == EffectTargetTag.Enemy) return "Enemy";
        if (tag == EffectTargetTag.AllEnemies) return "AllEnemies";
        return "Player";
    }
}

[System.Serializable]
public class AppliedEffectEntry
{
    public StatusEffectType statusType;
    public float stacks;
    public EffectTargetTag targetTag;

    public void Apply(CombatManager combatManager, Combatant source, Combatant target)
    {
        if (statusType == StatusEffectType.Stagger)
        {
            if (target is Enemy enemy)
                enemy.ApplyStagger(Mathf.RoundToInt(stacks));
            Debug.Log("Накладывает Пошатывание на " + stacks + " сек. цели " + target.name);
            return;
        }

        target.AddEffectStacks(statusType, stacks, source);
        Debug.Log("Применяет " + stacks + " стаков " + statusType + " цели " + target.name);
    }

    public bool IsPositive()
    {
        switch (statusType)
        {
            case StatusEffectType.Strength:
            case StatusEffectType.Regen:
                return true;
            case StatusEffectType.Toughness:
                return stacks >= 0;
            default:
                return false;
        }
    }

    public string Describe()
    {
        string name = CardTextHelpers.GetGenitiveName(statusType);
        string n = stacks.ToString("0.##");
        return Loc.Format("card.effect." + (IsPositive() ? "positive." : "negative.") + InstantActionEntry.TagGroup(targetTag), n, name);
    }
}

[System.Flags]
public enum CardPropertyFlags
{
    None = 0,
    Exhaust = 1 << 0,
    FrontOfDraw = 1 << 1
}
