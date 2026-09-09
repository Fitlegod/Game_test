using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public abstract class Combatant : MonoBehaviour, IPointerClickHandler
{
    public int maxHP = 30;
    public int CurrentHP { get; protected set; }
    public CombatManager combatManager;

    private Dictionary<StatusEffectType, int> effectStacks = new Dictionary<StatusEffectType, int>();
    private HashSet<StatusEffectType> typesWithScheduledEvent = new HashSet<StatusEffectType>();

    protected virtual void Awake()
    {
        CurrentHP = maxHP;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TargetSelectionManager.Instance.SelectTarget(this);

    }

    public int GetStacks(StatusEffectType type)
    {
        return effectStacks.TryGetValue(type, out int value) ? value : 0;
    }

    public void AddEffectStacks(StatusEffectType type, int amount)
    {
        effectStacks[type] = GetStacks(type) + amount;

        if (!typesWithScheduledEvent.Contains(type) &&
            StatusEffectRules.TryGetPeriodicBehavior(type, out float interval, out var action))
        {
            typesWithScheduledEvent.Add(type);
            var periodicEvent = new PeriodicEffectEvent(combatManager.CurrentTime, interval, () => action(this));
            combatManager.RegisterScheduledEvent(periodicEvent);
        }
    }

    public void RemoveStacks(StatusEffectType type, int amount)
    {
        effectStacks[type] = Mathf.Max(0, GetStacks(type) - amount);
    }

    public int CalculateOutgoingDamage(int baseDamage)
    {
        int result = baseDamage + GetStacks(StatusEffectType.Strength);
        if (GetStacks(StatusEffectType.Weak) > 0)
            result = Mathf.FloorToInt(result * 0.5f);
        return result;
    }

    public void Heal(int amount)
    {
        CurrentHP = Mathf.Min(maxHP, CurrentHP + amount);
    }

    public virtual void TakeDamage(int amount)
    {
        CurrentHP -= amount;
        if (CurrentHP < 0) CurrentHP = 0;
    }
}