using UnityEngine;

// Группы порядка событий в одном моменте (IScheduledEvent.Priority)
public static class EventGroup
{
    public const int Player = 0;      // карты, таймер добора, тики на игроке и тики, наложенные игроком
    public const int Enemies = 1;     // действия врагов и тики, наложенные врагом
    public const int Environment = 2; // затухание блока — всегда в конце момента
}

public class PeriodicEffectEvent : IScheduledEvent
{
    private readonly System.Action onTrigger;
    private readonly int interval;
    public int NextTime { get; private set; }
    // Группа порядка (EventGroup): враги — тик на враге, наложенный врагом или неизвестно кем
    public int Priority => isEnvironment ? EventGroup.Environment : Owner is Enemy && !(Applier is Player) ? EventGroup.Enemies : EventGroup.Player;
    private readonly bool isEnvironment;
    public bool IsPending => true;
    public Combatant Owner { get; } // null — таймер добора
    public Combatant Applier { get; } // кто наложил эффект, создавший тик; может быть null
    public StatusEffectType? EffectType { get; } // какой статус-эффект тикает; null — таймер добора и затухание блока

    public PeriodicEffectEvent(int startTime, int interval, System.Action onTrigger, Combatant owner = null, Combatant applier = null, bool isEnvironment = false, StatusEffectType? effectType = null)
    {
        EffectType = effectType;
        this.isEnvironment = isEnvironment;
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
    public void Restart(int startTime) => NextTime = startTime + interval;
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
    // «Лечит по времени» — тик такого эффекта идёт перед тиками «бьёт по времени» того же бойца (CombatManager, эффективный ключ порядка)
    public static bool IsHealOverTime(StatusEffectType type) => type == StatusEffectType.Regen;
    public static bool IsDamageOverTime(StatusEffectType type) => type == StatusEffectType.Poison || type == StatusEffectType.Bleeding;

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

            case StatusEffectType.Regen: // лечит на текущие стаки, затем −1
                interval = 1;
                action = owner =>
                {
                    owner.Heal(Mathf.RoundToInt(owner.GetStacks(StatusEffectType.Regen)));
                    owner.RemoveStacks(StatusEffectType.Regen, 1);
                };
                return true;

            case StatusEffectType.Poison: // потеря HP мимо блока и Уязвимости, затем −1
                interval = 1;
                action = owner =>
                {
                    owner.LoseHP(Mathf.RoundToInt(owner.GetStacks(StatusEffectType.Poison)));
                    owner.RemoveStacks(StatusEffectType.Poison, 1);
                };
                return true;

            case StatusEffectType.Bleeding: // урон по стакам (блок поглощает, Уязвимость не действует); сам не затухает
                interval = 3;
                action = owner => owner.TakeDamage(Mathf.RoundToInt(owner.GetStacks(StatusEffectType.Bleeding)));
                return true;

            case StatusEffectType.Invulnerable: // стаки — секунды
                interval = 1;
                action = owner => owner.RemoveStacks(StatusEffectType.Invulnerable, 1);
                return true;

            default:
                interval = 0;
                action = null;
                return false;
        }
    }
}