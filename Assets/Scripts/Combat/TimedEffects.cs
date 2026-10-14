using UnityEngine;

public class PeriodicEffectEvent : IScheduledEvent
{
    private readonly System.Action onTrigger;
    private readonly int interval;
    public int NextTime { get; private set; }
    // Группа порядка: 0 — сторона игрока, 1 — враги (тик на враге, наложенный врагом или неизвестно кем)
    public int Priority => Owner is Enemy && !(Applier is Player) ? 1 : 0;
    public bool IsPending => true;
    public Combatant Owner { get; } // null — таймер добора
    public Combatant Applier { get; } // кто наложил эффект, создавший тик; может быть null

    public PeriodicEffectEvent(int startTime, int interval, System.Action onTrigger, Combatant owner = null, Combatant applier = null)
    {
        this.interval = interval;
        this.onTrigger = onTrigger;
        Owner = owner;
        Applier = applier;
        NextTime = startTime + interval;
    }

    public void Trigger()
    {
        if (Owner != null)
        {
            int order = Owner.combatManager.CurrentFireOrder;
            Owner.combatManager.Record(Owner, () => { NextTime -= interval; Owner.combatManager.PushReplayOrder(this, order); });
        }
        onTrigger();
        NextTime += interval;
    }

    public void Shift(int delta) => NextTime += delta;
}

public class OneShotEvent : IScheduledEvent
{
    public int NextTime { get; }
    public int Priority { get; }
    public bool IsPending => !fired;
    public bool IsReplay { get; private set; }
    private bool fired;
    private readonly CombatManager combatManager;
    private readonly System.Action<bool> onTrigger; // аргумент — IsReplay

    public OneShotEvent(CombatManager combatManager, int time, int priority, System.Action<bool> onTrigger)
    {
        this.combatManager = combatManager;
        NextTime = time;
        Priority = priority;
        this.onTrigger = onTrigger;
    }

    public void Trigger()
    {
        fired = true;
        int order = combatManager.CurrentFireOrder;
        combatManager.Record(null, () => { Rearm(true); combatManager.PushReplayOrder(this, order); });
        onTrigger?.Invoke(IsReplay);
    }

    public void Rearm(bool replay)
    {
        fired = false;
        IsReplay = replay;
    }
}

public static class StatusEffectRules
{
    public static bool TryGetPeriodicBehavior(StatusEffectType type, out int interval, out System.Action<Combatant> action)
    {
        switch (type)
        {
            case StatusEffectType.Weak:
                interval = 3;
                action = owner => owner.RemoveStacks(StatusEffectType.Weak, 1);
                return true;

            case StatusEffectType.Frailty:
                interval = 3;
                action = owner => owner.RemoveStacks(StatusEffectType.Frailty, 1);
                return true;

            case StatusEffectType.Vulnerable:
                interval = 3;
                action = owner => owner.RemoveStacks(StatusEffectType.Vulnerable, 1);
                return true;

            case StatusEffectType.Regen:
                interval = 1;
                action = owner => owner.Heal(Mathf.RoundToInt(owner.GetStacks(StatusEffectType.Regen)));
                return true;

            default:
                interval = 0;
                action = null;
                return false;
        }
    }
}