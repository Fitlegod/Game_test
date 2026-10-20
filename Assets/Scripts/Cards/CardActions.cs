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

    // Возвращает «исходящее» число: урон после модификаторов источника (до модификаторов цели), блок после Крепости/Хрупкости получателя, лечение.
    // outgoingOverride — число из первого срабатывания карты (повтор после отката): формулы источника заново не считаются.
    public float Apply(CombatManager combatManager, Combatant source, Combatant target, float? outgoingOverride = null)
    {
        if (kind == InstantActionKind.Rewind)
        {
            combatManager.Rewind(amount);
            return amount;
        }

        float outgoing = amount;

        for (int i = 0; i < hitCount; i++)
        {
            switch (kind)
            {
                case InstantActionKind.Damage:
                    outgoing = outgoingOverride ?? source.CalculateOutgoingDamage(amount);
                    int finalDamage = target.ApplyIncomingDamageModifiers(outgoing);
                    bool alive = target.CurrentHP > 0; // удар по уже мёртвой цели (второй hit после убийства) — не удар
                    target.TakeDamage(finalDamage);
                    if (alive) combatManager.RaiseAttack(source, target, finalDamage);
                    Debug.Log("Наносит " + finalDamage + " урона (база " + amount + ") цели " + target.name);
                    break;

                case InstantActionKind.Block:
                    Combatant blockRecipient = target ?? source;
                    int finalBlock = (int)(outgoing = outgoingOverride ?? blockRecipient.CalculateIncomingBlock(amount));
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
