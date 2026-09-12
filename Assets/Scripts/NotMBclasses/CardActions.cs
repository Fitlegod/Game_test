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
}

[System.Flags]
public enum CardPropertyFlags
{
    None = 0,
    Exhaust = 1 << 0,
    FrontOfDraw = 1 << 1
}
