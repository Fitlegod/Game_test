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

    [Header("Damage / Block")]
    public int amount;

    [Header("Apply Status")]
    public StatusEffectType statusType;
    public int stacks;

    public void Apply(CombatManager combatManager, Combatant target)
    {
        switch (kind)
        {
            case EffectKind.Damage:
                int finalDamage = combatManager.player.CalculateOutgoingDamage(amount);
                target.TakeDamage(finalDamage);
                Debug.Log("Наносит " + finalDamage + " урона (база " + amount + ") цели " + target.name);
                break;

            case EffectKind.Block:
                combatManager.player.GainBlock(amount);
                Debug.Log("Даёт " + amount + " блока игроку");
                break;

            case EffectKind.ApplyStatus:
                target.AddEffectStacks(statusType, stacks);
                Debug.Log("Применяет " + stacks + " стаков " + statusType + " цели " + target.name);
                break;
        }
    }
}