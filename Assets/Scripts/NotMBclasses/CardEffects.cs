using UnityEngine;

[System.Serializable]
public abstract class CardEffect
{
    public abstract void Apply(CombatManager combatManager, Combatant target);
}

[System.Serializable]
public class DamageEffect : CardEffect
{
    public int amount;

    public override void Apply(CombatManager combatManager, Combatant target)
    {
        int finalDamage = combatManager.player.CalculateOutgoingDamage(amount);
        target.TakeDamage(finalDamage);
        Debug.Log("Наносит " + finalDamage + " урона (база " + amount + ") цели " + target.name);
    }
}

[System.Serializable]
public class BlockEffect : CardEffect
{
    public int amount;

    public override void Apply(CombatManager combatManager, Combatant target)
    {
        combatManager.player.GainBlock(amount);
        Debug.Log("Даёт " + amount + " блока игроку");
    }
}

[System.Serializable]
public class ApplyStatusEffect : CardEffect
{
    public StatusEffectType statusType;
    public int stacks;

    public override void Apply(CombatManager combatManager, Combatant target)
    {
        target.AddEffectStacks(statusType, stacks);
        Debug.Log("Применяет " + stacks + " стаков " + statusType + " цели " + target.name);
    }
}