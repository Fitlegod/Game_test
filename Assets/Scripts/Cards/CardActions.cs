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

    public float Apply(CombatManager combatManager, Combatant source, Combatant target, float? outgoingOverride = null)
    {
        if (kind == InstantActionKind.Rewind)
        {
            combatManager.Rewind(amount);
            return amount;
        }

        float outgoing = amount;
        for (int i = 0; i < hitCount; i++)
            outgoing = ApplySingleHit(combatManager, source, target, outgoingOverride);

        return outgoing;
    }

    // Применяет ровно одно попадание. Используется картами с RandomEnemy,
    // чтобы выбирать цель заново перед каждым ударом.
    // Apply() выше сохраняет прежнее поведение для действий врагов.
    public float ApplySingleHit(CombatManager combatManager, Combatant source, Combatant target, float? outgoingOverride = null)
    {
        if (kind == InstantActionKind.Rewind)
        {
            combatManager.Rewind(amount);
            return amount;
        }

        switch (kind)
        {
            case InstantActionKind.Damage:
                float damageOutgoing = outgoingOverride ?? source.CalculateOutgoingDamage(amount);
                int finalDamage = target.ApplyIncomingDamageModifiers(damageOutgoing);
                bool alive = target.CurrentHP > 0;
                target.TakeDamage(finalDamage);
                if (alive) combatManager.RaiseAttack(source, target, finalDamage);
                Debug.Log("Наносит " + finalDamage + " урона (база " + amount + ") цели " + target.name);
                return damageOutgoing;

            case InstantActionKind.Block:
                Combatant blockRecipient = target ?? source;
                float blockOutgoing = outgoingOverride ?? blockRecipient.CalculateIncomingBlock(amount);
                int finalBlock = (int)blockOutgoing;
                blockRecipient.GainBlock(finalBlock);
                Debug.Log("Даёт " + finalBlock + " блока цели " + blockRecipient.name + " (база " + amount + ")");
                return blockOutgoing;

            case InstantActionKind.Heal:
                Combatant healRecipient = target ?? source;
                healRecipient.Heal(amount);
                Debug.Log("Лечит " + amount + " HP цели " + healRecipient.name);
                return amount;

            default:
                return amount;
        }
    }

    public int ComputePreviewAmount(Combatant source, Combatant recipient)
    {
        switch (kind)
        {
            case InstantActionKind.Damage:
                float dmg = source.CalculateOutgoingDamage(amount);
                return recipient != null ? recipient.ApplyIncomingDamageModifiers(dmg) : Mathf.Max(0, Mathf.FloorToInt(dmg));
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
        if (tag == EffectTargetTag.RandomEnemy) return "RandomEnemy";
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
        if (statusType == StatusEffectType.Stun)
        {
            // Расходуется сразу, в словарь стаков не попадает: враг откладывает действие, игрок «проваливается» вперёд во времени
            int seconds = Mathf.RoundToInt(stacks);
            if (target is Enemy enemy)
                enemy.ApplyStun(seconds);
            else if (target is Player && seconds > 0)
                combatManager.AdvanceTime(seconds);
            Debug.Log("Накладывает Оглушение на " + stacks + " сек. цели " + target.name);
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
            case StatusEffectType.Invulnerable:
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
