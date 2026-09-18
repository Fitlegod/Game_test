using UnityEngine;

public enum InstantActionKind
{
    Damage,
    Block,
    Heal
}

[System.Serializable]
public class InstantActionEntry
{
    public InstantActionKind kind;
    public int amount;
    public int hitCount = 1;
    public EffectTargetTag targetTag;

    public void Apply(CombatManager combatManager, Combatant source, Combatant target)
    {
        for (int i = 0; i < hitCount; i++)
        {
            switch (kind)
            {
                case InstantActionKind.Damage:
                    int finalDamage = source.CalculateOutgoingDamage(amount);
                    finalDamage = target.ApplyIncomingDamageModifiers(finalDamage);
                    target.TakeDamage(finalDamage);
                    Debug.Log("Наносит " + finalDamage + " урона (база " + amount + ") цели " + target.name);
                    break;

                case InstantActionKind.Block:
                    Combatant blockRecipient = target ?? source;
                    int finalBlock = blockRecipient.CalculateIncomingBlock(amount);
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

    public static string HitCountSuffix(int hitCount)
    {
        if (hitCount <= 1) return "";
        if (hitCount == 2) return " дважды";
        if (hitCount <= 4) return " " + hitCount + " раза";
        return " " + hitCount + " раз";
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
                enemy.ApplyStagger(stacks);
            Debug.Log("Накладывает Пошатывание на " + stacks + " сек. цели " + target.name);
            return;
        }

        target.AddEffectStacks(statusType, stacks);
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

[System.Flags]
public enum CardPropertyFlags
{
    None = 0,
    Exhaust = 1 << 0,
    FrontOfDraw = 1 << 1
}
