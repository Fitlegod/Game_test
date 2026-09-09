using UnityEngine;

public enum InstantActionKind
{
    Damage,
    Block
}

[System.Serializable]
public class InstantActionEntry
{
    public InstantActionKind kind;
    public int amount;

    public void Apply(CombatManager combatManager, Combatant target)
    {
        switch (kind)
        {
            case InstantActionKind.Damage:
                int finalDamage = combatManager.player.CalculateOutgoingDamage(amount);
                target.TakeDamage(finalDamage);
                Debug.Log("Наносит " + finalDamage + " урона (база " + amount + ") цели " + target.name);
                break;
            case InstantActionKind.Block:
                combatManager.player.GainBlock(amount);
                Debug.Log("Даёт " + amount + " блока игроку");
                break;
        }
    }
}

[System.Serializable]
public class AppliedEffectEntry
{
    public StatusEffectType statusType;
    public int stacks;

    public void Apply(CombatManager combatManager, Combatant target)
    {
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