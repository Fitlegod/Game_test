using UnityEngine;

public class PeriodicEffectEvent : IScheduledEvent
{
    private readonly System.Action onTrigger;
    private readonly float interval;
    public float NextTime { get; private set; }
    public int Priority => 1;

    public PeriodicEffectEvent(float startTime, float interval, System.Action onTrigger)
    {
        this.interval = interval;
        this.onTrigger = onTrigger;
        NextTime = startTime + interval;
    }

    public void Trigger()
    {
        onTrigger();
        NextTime += interval;
    }
}

public class OneShotEvent : IScheduledEvent
{
    public float NextTime { get; private set; }
    public int Priority { get; }
    private readonly System.Action onTrigger;

    public OneShotEvent(float time, int priority, System.Action onTrigger)
    {
        NextTime = time;
        Priority = priority;
        this.onTrigger = onTrigger;
    }

    public void Trigger()
    {
        onTrigger?.Invoke();
        NextTime = float.PositiveInfinity;
    }
}

public static class StatusEffectRules
{
    public static bool TryGetPeriodicBehavior(StatusEffectType type, out float interval, out System.Action<Combatant> action)
    {
        switch (type)
        {
            case StatusEffectType.Weak:
                interval = 3f;
                action = owner => owner.RemoveStacks(StatusEffectType.Weak, 1);
                return true;

            case StatusEffectType.Frailty:
                interval = 3f;
                action = owner => owner.RemoveStacks(StatusEffectType.Frailty, 1);
                return true;

            case StatusEffectType.Vulnerable:
                interval = 3f;
                action = owner => owner.RemoveStacks(StatusEffectType.Vulnerable, 1);
                return true;

            case StatusEffectType.Regen:
                interval = 1f;
                action = owner => owner.Heal(Mathf.RoundToInt(owner.GetStacks(StatusEffectType.Regen)));
                return true;

            default:
                interval = 0f;
                action = null;
                return false;
        }
    }
}