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
    private Dictionary<StatusEffectType, PeriodicEffectEvent> undoneTicks = new Dictionary<StatusEffectType, PeriodicEffectEvent>(); // тики, отменённые откатом: при повторе возвращаются тем же объектом

    // Явная инициализация при спавне: Awake не вызывается под неактивным родителем, поэтому HP нельзя ставить там.
    public void Init()
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

    public void AddEffectStacks(StatusEffectType type, float amount, Combatant applier = null)
    {
        if (CurrentHP <= 0) return;

        float before = GetStacks(type);
        effectStacks[type] = before + amount;
        float added = effectStacks[type] - before;
        combatManager.Record(this, () => effectStacks[type] = GetStacks(type) - added);

        if (!activeScheduledEffects.ContainsKey(type) &&
            StatusEffectRules.TryGetPeriodicBehavior(type, out int interval, out var action))
        {
            PeriodicEffectEvent periodicEvent;
            // Тик, отменённый откатом, при повторе возвращается тем же объектом: сохраняет номер создания и номера повтора
            if (undoneTicks.TryGetValue(type, out periodicEvent) && periodicEvent.Applier == applier && combatManager.EffectTime <= combatManager.ReplayUntil)
                periodicEvent.Restart(combatManager.EffectTime);
            else
                periodicEvent = new PeriodicEffectEvent(combatManager.EffectTime, interval, () => action(this), this, applier, false, type);
            undoneTicks.Remove(type);
            activeScheduledEffects[type] = periodicEvent;
            combatManager.Record(this, () => { activeScheduledEffects.Remove(type); undoneTicks[type] = periodicEvent; });
            combatManager.RegisterScheduledEvent(periodicEvent);
        }
    }

    public void ClearAllScheduledEvents()
    {
        foreach (var kvp in activeScheduledEffects)
        {
            var type = kvp.Key;
            var periodicEvent = kvp.Value;
            combatManager.Record(this, () => activeScheduledEffects[type] = periodicEvent);
            combatManager.UnregisterScheduledEvent(periodicEvent);
        }
        activeScheduledEffects.Clear();
    }

    public void RemoveStacks(StatusEffectType type, float amount)
    {
        float before = GetStacks(type);
        effectStacks[type] = Mathf.Max(0f, before - amount);
        float removed = before - effectStacks[type];
        combatManager.Record(this, () => effectStacks[type] = GetStacks(type) + removed);

        // Стаки кончились — тик эффекта снимается; повторное наложение создаст новый
        if (effectStacks[type] <= 0f && activeScheduledEffects.TryGetValue(type, out var tick))
        {
            activeScheduledEffects.Remove(type);
            combatManager.Record(this, () => activeScheduledEffects[type] = tick);
            combatManager.UnregisterScheduledEvent(tick);
        }
    }

    // Урон = (База + Сила) × Слабость × Уязвимость цели, округление вниз один раз в конце (ApplyIncomingDamageModifiers)
    public float CalculateOutgoingDamage(int baseDamage)
    {
        float result = baseDamage + Mathf.RoundToInt(GetStacks(StatusEffectType.Strength));
        if (GetStacks(StatusEffectType.Weak) > 0)
            result *= 0.5f;
        return result;
    }

    public int ApplyIncomingDamageModifiers(float damage)
    {
        if (GetStacks(StatusEffectType.Vulnerable) > 0)
            damage *= 1.5f;
        return Mathf.Max(0, Mathf.FloorToInt(damage));
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
        int before = CurrentHP;
        CurrentHP = Mathf.Min(maxHP, CurrentHP + amount);
        int healed = CurrentHP - before;
        combatManager.Record(this, () => CurrentHP -= healed);
        if (healed > 0 && GetStacks(StatusEffectType.Bleeding) > 0)
            RemoveStacks(StatusEffectType.Bleeding, healed); // лечение снимает столько Кровотечения, сколько реально вылечено
    }

    // Потеря HP в обход блока (Отравление). Неуязвимость защищает и от неё.
    public void LoseHP(int amount)
    {
        if (CurrentHP <= 0 || amount <= 0 || GetStacks(StatusEffectType.Invulnerable) > 0) return;
        ReduceHP(amount, 0);
    }

    public void GainBlock(int amount)
    {
        if (CurrentHP <= 0) return;
        CurrentBlock += amount;
        combatManager.Record(this, () => CurrentBlock -= amount);
    }

    // Затухание блока: снимает не больше, чем есть; в журнал — реальная разница
    public void DecayBlock(int amount)
    {
        if (CurrentHP <= 0 || amount <= 0 || CurrentBlock <= 0) return;
        int removed = Mathf.Min(CurrentBlock, amount);
        CurrentBlock -= removed;
        combatManager.Record(this, () => CurrentBlock += removed);
    }

    public void SetCurrentHP(int hp)
    {
        CurrentHP = Mathf.Clamp(hp, 0, maxHP);
    }

    public event System.Action OnDeath;

    public virtual void TakeDamage(int amount)
    {
        if (CurrentHP <= 0 || amount <= 0 || GetStacks(StatusEffectType.Invulnerable) > 0) return; // Неуязвимость: ни урона, ни траты блока

        int absorbed = Mathf.Min(CurrentBlock, amount);
        CurrentBlock -= absorbed;
        ReduceHP(amount - absorbed, absorbed);
    }

    // Общий хвост TakeDamage и LoseHP: снять HP, записать в журнал реальную разницу (и поглощённый блок), при 0 HP — смерть
    private void ReduceHP(int amount, int absorbedBlock)
    {
        int hpBefore = CurrentHP;
        CurrentHP = Mathf.Max(0, CurrentHP - amount);
        int hpLost = hpBefore - CurrentHP;
        combatManager.Record(this, () => { CurrentBlock += absorbedBlock; CurrentHP += hpLost; });

        if (CurrentHP == 0)
        {
            ClearAllScheduledEvents();
            OnDeath?.Invoke();
        }
    }
}
