using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class CombatManager : MonoBehaviour
{
    public TMP_Text budgetText;
    public TMP_Text combatResultText;
    public Player player;

    private List<Enemy> enemies = new List<Enemy>();
    public float CurrentTime { get; private set; }

    private List<IScheduledEvent> scheduledEvents = new List<IScheduledEvent>();
    private bool combatOver;

    void Start()
    {
        UpdateBudgetText();
        player.OnDeath += HandlePlayerDeath;
        if (combatResultText != null)
            combatResultText.text = "";
    }

    public void RegisterEnemies(List<Enemy> spawnedEnemies)
    {
        enemies = spawnedEnemies;
        foreach (var enemy in enemies)
            enemy.OnDeath += HandleEnemyDeath;
    }

    public void RegisterScheduledEvent(IScheduledEvent scheduledEvent)
    {
        scheduledEvents.Add(scheduledEvent);
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

    private void HandlePlayerDeath() => EndCombat("Поражение");

    private void HandleEnemyDeath()
    {
        if (enemies.TrueForAll(e => e.CurrentHP <= 0))
            EndCombat("Победа");
    }

    private void EndCombat(string message)
    {
        if (combatOver) return;
        combatOver = true;
        if (combatResultText != null)
            combatResultText.text = message;
        TargetSelectionManager.Instance.LockInput();
    }

    private void UpdateBudgetText()
    {
        budgetText.text = "Прошло времени: " + CurrentTime.ToString("0.0") + " с";
    }
}