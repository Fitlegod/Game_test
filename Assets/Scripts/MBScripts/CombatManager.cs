using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class CombatManager : MonoBehaviour
{
    public TMP_Text budgetText;
    public Enemy enemy;
    public Player player;

    public float CurrentTime { get; private set; }

    private List<IScheduledEvent> scheduledEvents = new List<IScheduledEvent>();

    void Start()
    {
        UpdateBudgetText();
    }

    public void RegisterScheduledEvent(IScheduledEvent scheduledEvent)
    {
        scheduledEvents.Add(scheduledEvent);
    }

    public Combatant GetCombatant(EffectTarget target)
    {
        return target == EffectTarget.Self ? (Combatant)player : enemy;
    }

    public void AdvanceTime(float amount)
    {
        CurrentTime += amount;
        UpdateBudgetText();
    }

    public void ResolveUpTo(float targetTime)
    {
        while (true)
        {
            IScheduledEvent next = null;
            foreach (var ev in scheduledEvents)
            {
                if (ev.NextTime > targetTime) continue;
                if (next == null || ev.NextTime < next.NextTime ||
                    (ev.NextTime == next.NextTime && ev.Priority < next.Priority))
                    next = ev;
            }
            if (next == null) break;
            next.Trigger();
        }
    }

    private void UpdateBudgetText()
    {
        budgetText.text = "Прошло времени: " + CurrentTime.ToString("0.0") + " с";
    }
}