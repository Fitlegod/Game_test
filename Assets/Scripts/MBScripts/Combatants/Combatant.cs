using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public abstract class Combatant : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public int maxHP = 30;
    public int CurrentHP { get; protected set; }
    public int CurrentBlock { get; private set; }
    public CombatManager combatManager;

    private Dictionary<StatusEffectType, float> effectStacks = new Dictionary<StatusEffectType, float>();
    private Dictionary<StatusEffectType, PeriodicEffectEvent> activeScheduledEffects = new Dictionary<StatusEffectType, PeriodicEffectEvent>();

    protected virtual void Awake()
    {
        CurrentHP = maxHP;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TargetSelectionManager.Instance.SelectTarget(this);
    }

    public void OnPointerEnter(PointerEventData eventData) => TargetSelectionManager.Instance.SetHoveredTarget(this);
    public void OnPointerExit(PointerEventData eventData) => TargetSelectionManager.Instance.ClearHoveredTarget(this);

    public float GetStacks(StatusEffectType type)
    {
        return effectStacks.TryGetValue(type, out float value) ? value : 0f;
    }

    public void AddEffectStacks(StatusEffectType type, float amount)
    {
        if (CurrentHP <= 0) return;

        effectStacks[type] = GetStacks(type) + amount;

        if (!activeScheduledEffects.ContainsKey(type) &&
            StatusEffectRules.TryGetPeriodicBehavior(type, out float interval, out var action))
        {
            var periodicEvent = new PeriodicEffectEvent(combatManager.CurrentTime, interval, () => action(this));
            activeScheduledEffects[type] = periodicEvent;
            combatManager.RegisterScheduledEvent(periodicEvent);
        }
    }

    public void ClearAllScheduledEvents()
    {
        foreach (var kvp in activeScheduledEffects)
            combatManager.UnregisterScheduledEvent(kvp.Value);
        activeScheduledEffects.Clear();
    }

    public void RemoveStacks(StatusEffectType type, float amount)
    {
        effectStacks[type] = Mathf.Max(0f, GetStacks(type) - amount);
    }

    public int CalculateOutgoingDamage(int baseDamage)
    {
        int result = baseDamage + Mathf.RoundToInt(GetStacks(StatusEffectType.Strength));
        if (GetStacks(StatusEffectType.Weak) > 0)
            result = Mathf.FloorToInt(result * 0.5f);
        return result;
    }

    public int ApplyIncomingDamageModifiers(int damage)
    {
        if (GetStacks(StatusEffectType.Vulnerable) > 0)
            damage = Mathf.FloorToInt(damage * 1.5f);
        return damage;
    }

    public int CalculateIncomingBlock(int baseAmount)
    {
        int result = baseAmount + Mathf.RoundToInt(GetStacks(StatusEffectType.Toughness));
        if (GetStacks(StatusEffectType.Frailty) > 0)
            result = Mathf.FloorToInt(result * 0.5f);
        return Mathf.Max(0, result);
    }

    public void Heal(int amount)
    {
        if (CurrentHP <= 0) return;
        CurrentHP = Mathf.Min(maxHP, CurrentHP + amount);
    }

    public void GainBlock(int amount)
    {
        CurrentBlock += amount;
    }

    public event System.Action OnDeath;

    public virtual void TakeDamage(int amount)
    {
        if (CurrentHP <= 0) return;

        int absorbed = Mathf.Min(CurrentBlock, amount);
        CurrentBlock -= absorbed;
        amount -= absorbed;

        CurrentHP -= amount;
        if (CurrentHP < 0) CurrentHP = 0;

        if (CurrentHP == 0)
        {
            ClearAllScheduledEvents();
            OnDeath?.Invoke();
        }
    }
}