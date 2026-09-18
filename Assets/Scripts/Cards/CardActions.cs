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

    public string Describe(int displayValue)
    {
        string hitSuffix = CardTextHelpers.HitCountSuffix(hitCount);

        switch (kind)
        {
            case InstantActionKind.Damage:
                if (targetTag == EffectTargetTag.Enemy) return "Наносит " + displayValue + " урона" + hitSuffix;
                if (targetTag == EffectTargetTag.AllEnemies) return "Наносит " + displayValue + " урона" + hitSuffix + " всем врагам";
                return "Наносит " + displayValue + " урона" + hitSuffix + " себе";

            case InstantActionKind.Block:
                if (targetTag == EffectTargetTag.Enemy) return "Даёт " + displayValue + " блока" + hitSuffix + " врагу";
                if (targetTag == EffectTargetTag.AllEnemies) return "Даёт " + displayValue + " блока" + hitSuffix + " всем врагам";
                return "Даёт " + displayValue + " блока" + hitSuffix;

            case InstantActionKind.Heal:
                if (targetTag == EffectTargetTag.Enemy) return "Восстанавливает " + displayValue + " HP врагу";
                if (targetTag == EffectTargetTag.AllEnemies) return "Восстанавливает " + displayValue + " HP всем врагам";
                return "Восстанавливает " + displayValue + " HP";

            default:
                return "";
        }
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

    public string Describe()
    {
        string name = CardTextHelpers.GetGenitiveName(statusType);
        string n = stacks.ToString("0.##");

        if (IsPositive())
        {
            if (targetTag == EffectTargetTag.Enemy) return "Даёт " + n + " " + name + " врагу";
            if (targetTag == EffectTargetTag.AllEnemies) return "Даёт " + n + " " + name + " всем врагам";
            return "Даёт " + n + " " + name;
        }
        else
        {
            if (targetTag == EffectTargetTag.Enemy) return "Накладывает " + n + " " + name;
            if (targetTag == EffectTargetTag.AllEnemies) return "Накладывает " + n + " " + name + " всем врагам";
            return "Даёт " + n + " " + name;
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
