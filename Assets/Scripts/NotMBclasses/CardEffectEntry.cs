using UnityEngine;

public enum EffectKind
{
    Damage,
    Block,
    ApplyStatus
}

[System.Serializable]
public class CardEffectEntry
{
    public EffectKind kind;
    public EffectTarget target = EffectTarget.Enemy;

    [Header("Damage / Block")]
    public int amount;

    [Header("Apply Status")]
    public StatusEffectType statusType;
    public int stacks;

    public void Apply(CombatManager combatManager)
    {
        Combatant targetCombatant = combatManager.GetCombatant(target);

        switch (kind)
        {
            case EffectKind.Damage:
                int finalDamage = combatManager.player.CalculateOutgoingDamage(amount);
                targetCombatant.TakeDamage(finalDamage);
                Debug.Log("Наносит " + finalDamage + " урона (база " + amount + ") цели " + target);
                break;

            case EffectKind.Block:
                if (targetCombatant is Player p)
                {
                    p.GainBlock(amount);
                    Debug.Log("Даёт " + amount + " блока цели " + target);
                }
                break;

            case EffectKind.ApplyStatus:
                targetCombatant.AddEffectStacks(statusType, stacks);
                Debug.Log("Применяет " + stacks + " стаков " + statusType + " цели " + target);
                break;
        }
    }
}