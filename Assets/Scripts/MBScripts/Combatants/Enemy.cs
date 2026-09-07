using UnityEngine;

public class Enemy : Combatant, IScheduledEvent
{
    public float scheduledHitTime = 3f;
    public float attackIntervalSeconds = 3f;
    public int attackDamage = 18;
    public Player player;

    public float NextTime => scheduledHitTime;
    public int Priority => 1;

    protected override void Awake()
    {
        base.Awake();
        combatManager.RegisterScheduledEvent(this);
    }

    public void Trigger()
    {
        int damage = CalculateOutgoingDamage(attackDamage);
        player.TakeDamage(damage);
        Debug.Log("Враг бьёт на " + damage + " урона (база " + attackDamage + ") на отметке " + scheduledHitTime + "! HP игрока: " + player.CurrentHP);
        scheduledHitTime += attackIntervalSeconds;
    }
}