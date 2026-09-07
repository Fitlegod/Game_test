using UnityEngine;

public class Player : Combatant
{
    public int CurrentBlock { get; private set; }

    public void GainBlock(int amount)
    {
        CurrentBlock += amount;
    }

    public override void TakeDamage(int amount)
    {
        int absorbed = Mathf.Min(CurrentBlock, amount);
        CurrentBlock -= absorbed;
        base.TakeDamage(amount - absorbed);
    }
}