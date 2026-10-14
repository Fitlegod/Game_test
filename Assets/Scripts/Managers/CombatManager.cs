using System.Collections.Generic;
using UnityEngine;
using TMPro;

// Запись журнала: как отменить одно изменение боя. Хранит реальную разницу, а не входное число.
public class UndoEntry
{
    public int FireOrder;        // номер срабатывания события (CombatManager.fireCounter)
    public int Time;             // момент события, внутри которого сделано изменение
    public Combatant Target;     // чьё состояние меняется (null — состояние не боевого участника)
    public IScheduledEvent Source; // событие, внутри которого сделано изменение
    public System.Action Undo;
}

public class CombatManager : MonoBehaviour
{
    public TMP_Text budgetText;
    public TMP_Text combatResultText;
    public Player player;

    private List<Enemy> enemies = new List<Enemy>();
    public IReadOnlyList<Enemy> Enemies => enemies;
    public int CurrentTime { get; private set; }
    public int ReplayUntil { get; private set; } = -1; // действия врагов в моменты <= этого значения не бьют по игроку; -1 — отката не было

    private List<IScheduledEvent> scheduledEvents = new List<IScheduledEvent>();
    public IReadOnlyList<IScheduledEvent> ScheduledEvents => scheduledEvents;
    private bool combatOver;

    private List<UndoEntry> journal = new List<UndoEntry>();
    public IReadOnlyList<UndoEntry> Journal => journal;
    public IScheduledEvent CurrentEvent { get; private set; }
    public int CurrentEventTime { get; private set; }
    public int CurrentFireOrder { get; private set; }
    // Номера прежних срабатываний события, которое откат вернул в ожидание: сверху — самое раннее (откат идёт от новых записей к старым)
    private Dictionary<IScheduledEvent, Stack<int>> replayOrders = new Dictionary<IScheduledEvent, Stack<int>>();
    private int fireCounter; // сквозной номер срабатывания; при откате не уменьшается
    private bool isRewinding;

    public event System.Action OnVictory;
    public event System.Action OnDefeat;

    void Start()
    {
        UpdateBudgetText();
        if (combatResultText != null)
            combatResultText.text = "";
    }

    public void SetPlayer(Player newPlayer)
    {
        player = newPlayer;
        player.OnDeath += HandlePlayerDeath;
    }

    public void ResetForNewCombat()
    {
        scheduledEvents.Clear();
        CurrentTime = 0;
        ReplayUntil = -1;
        journal.Clear();
        fireCounter = 0;
        replayOrders.Clear();
        CurrentEvent = null;
        isRewinding = false;
        combatOver = false;
        enemies.Clear();
        UpdateBudgetText();
        if (combatResultText != null) combatResultText.text = "";
        if (TargetSelectionManager.Instance != null) TargetSelectionManager.Instance.UnlockInput();
    }

    public void RegisterEnemies(List<Enemy> spawnedEnemies)
    {
        enemies = spawnedEnemies;
        foreach (var enemy in enemies)
            enemy.OnDeath += HandleEnemyDeath;
    }

    // «Мировое» — враги и их тики; всё остальное (таймер добора, тики на игроке, карты) — сторона игрока
    private static bool IsWorld(IScheduledEvent ev) => ev is Enemy || (ev is PeriodicEffectEvent p && p.Owner is Enemy);
    private static bool IsPlayerPeriodic(IScheduledEvent ev) => ev is PeriodicEffectEvent p && !(p.Owner is Enemy);
    private static Combatant OwnerOf(IScheduledEvent ev) => ev as Enemy ?? (ev as PeriodicEffectEvent)?.Owner;

    public void RegisterScheduledEvent(IScheduledEvent scheduledEvent)
    {
        scheduledEvents.Add(scheduledEvent);
        if (IsWorld(scheduledEvent))
            Record(OwnerOf(scheduledEvent), () => scheduledEvents.Remove(scheduledEvent));
    }

    public void UnregisterScheduledEvent(IScheduledEvent scheduledEvent)
    {
        if (!scheduledEvents.Remove(scheduledEvent)) return;
        if (IsWorld(scheduledEvent))
            Record(OwnerOf(scheduledEvent), () => scheduledEvents.Add(scheduledEvent));
    }

    // Пишет обратную процедуру только внутри события и не во время самого отката
    public void Record(Combatant target, System.Action undo)
    {
        if (CurrentEvent == null || isRewinding) return;
        journal.Add(new UndoEntry { FireOrder = CurrentFireOrder, Time = CurrentEventTime, Target = target, Source = CurrentEvent, Undo = undo });
    }

    public void AdvanceTime(int amount)
    {
        CurrentTime += amount;
        UpdateBudgetText();
    }

    public void ResolveUpTo()
    {
        while (true)
        {
            IScheduledEvent next = null;
            foreach (var ev in scheduledEvents)
            {
                if (!ev.IsPending || ev.NextTime > CurrentTime) continue;
                if (next == null || GoesBefore(ev, next))
                    next = ev;
            }
            if (next == null) break;
            CurrentEvent = next;
            CurrentEventTime = next.NextTime;
            CurrentFireOrder = ++fireCounter;
            if (replayOrders.TryGetValue(next, out var pending) && pending.Count > 0) pending.Pop(); // это срабатывание — повтор самого раннего прежнего
            try { next.Trigger(); }
            finally { CurrentEvent = null; }
        }
    }

    // Порядок при равном NextTime: повторяемые события — в порядке прежнего срабатывания и раньше новых; иначе Priority
    private bool GoesBefore(IScheduledEvent a, IScheduledEvent b)
    {
        if (a.NextTime != b.NextTime) return a.NextTime < b.NextTime;
        int ra = ReplayOrderOf(a), rb = ReplayOrderOf(b);
        if (ra > 0 && rb > 0) return ra < rb;
        if ((ra > 0) != (rb > 0)) return ra > 0;
        return a.Priority < b.Priority;
    }

    // 0 — обычное событие; >0 — номер прежнего срабатывания, которое оно сейчас повторяет
    private int ReplayOrderOf(IScheduledEvent ev) => replayOrders.TryGetValue(ev, out var s) && s.Count > 0 ? s.Peek() : 0;

    public void PushReplayOrder(IScheduledEvent ev, int order)
    {
        if (!replayOrders.TryGetValue(ev, out var s)) replayOrders[ev] = s = new Stack<int>();
        s.Push(order);
    }

    // Откат: вызывается из эффекта карты (внутри события). Игрок не меняется, враги и мир возвращаются обратными процедурами.
    public void Rewind(int n)
    {
        if (CurrentEvent == null) return;
        if (!(CurrentEvent is OneShotEvent))
        {
            Debug.LogError("Откат можно вызвать только из события карты игрока, а не из " + (CurrentEvent is Enemy enemy ? "действия врага " + enemy.name : CurrentEvent.GetType().Name));
            return;
        }
        int t = CurrentEventTime;
        int r = Mathf.Max(0, t - n);
        int shift = t - r;
        if (shift == 0) return;

        var self = CurrentEvent;
        isRewinding = true;
        for (int i = journal.Count - 1; i >= 0; i--)
        {
            var e = journal[i];
            if (e.Time < r || e.Target is Player || e.Source == self) continue;
            if (e.Source != null && IsPlayerPeriodic(e.Source)) continue;
            e.Undo();
        }
        journal.RemoveAll(e => e.Time >= r);

        foreach (var ev in scheduledEvents)
            if (ev is PeriodicEffectEvent p && IsPlayerPeriodic(p))
                p.Shift(-shift);

        CurrentTime -= shift;
        ReplayUntil = Mathf.Max(ReplayUntil, t);
        isRewinding = false;
        UpdateBudgetText();
    }

    private void HandlePlayerDeath()
    {
        EndCombat("combat.defeat");
        OnDefeat?.Invoke();
    }

    private void HandleEnemyDeath()
    {
        if (combatOver) return; // бой уже закончен (например, поражением) — второго исхода не будет
        if (enemies.TrueForAll(e => e.CurrentHP <= 0))
        {
            EndCombat("combat.victory");
            OnVictory?.Invoke();
        }
    }

    private void EndCombat(string messageKey)
    {
        if (combatOver) return;
        combatOver = true;
        if (combatResultText != null)
            combatResultText.text = Loc.Get(messageKey);
        if (TargetSelectionManager.Instance != null) TargetSelectionManager.Instance.LockInput();
    }

    private void UpdateBudgetText()
    {
        if (budgetText != null)
            budgetText.text = Loc.Format("ui.combat.time", CurrentTime.ToString());
    }
}