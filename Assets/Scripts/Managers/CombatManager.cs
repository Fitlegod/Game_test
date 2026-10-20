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

    [Header("Затухание блока")]
    public int blockDecayBase = 2;
    public int blockDecayStepSeconds = 10;
    public int blockDecayPerStep = 1;

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
    // Момент, в который «происходит» эффект: внутри события — момент события, вне — текущее время (не конец каста)
    public int EffectTime => CurrentEvent != null ? CurrentEventTime : CurrentTime;
    // Номера прежних срабатываний события, которое откат вернул в ожидание: сверху — самое раннее (откат идёт от новых записей к старым)
    private Dictionary<IScheduledEvent, Stack<int>> replayOrders = new Dictionary<IScheduledEvent, Stack<int>>();
    private Dictionary<IScheduledEvent, int> creationOrders = new Dictionary<IScheduledEvent, int>(); // номер создания события; повторная регистрация его не меняет
    private int creationCounter;
    private HashSet<Combatant> decayRegistered = new HashSet<Combatant>(); // у кого уже есть тик затухания блока; сбрасывается в ResetForNewCombat
    private int fireCounter; // сквозной номер срабатывания; при откате не уменьшается
    private bool isRewinding;

    public event System.Action OnVictory;
    public event System.Action OnDefeat;

    // События для триггеров (docs/combat-core.md). Первые три — вне таймлайна: журнал их не пишет, после отката они не повторяются.
    public event System.Action OnCombatStart;
    public event System.Action<CardInstance, Combatant> OnCardPlayed;
    public event System.Action<int> OnCastSeconds;
    public event System.Action<CardInstance, Enemy, bool> OnParry; // один раз на срабатывание карты, до эффектов Парирования
    public event System.Action<Combatant, Combatant, int, bool> OnAttack; // внутри события: подписчик пишет своё состояние через Record(null, undo)

    public void RaiseCombatStart() => OnCombatStart?.Invoke();
    public void RaiseCardPlayed(CardInstance card, Combatant target) => OnCardPlayed?.Invoke(card, target);
    public void RaiseCastSeconds(int seconds) => OnCastSeconds?.Invoke(seconds);
    public void RaiseParry(CardInstance card, Enemy enemy) =>
        OnParry?.Invoke(card, enemy, CurrentEvent != null && CurrentEventTime <= ReplayUntil);
    public void RaiseAttack(Combatant source, Combatant target, int damage) =>
        OnAttack?.Invoke(source, target, damage, CurrentEvent != null && CurrentEventTime <= ReplayUntil);

    // «Подождать»: секунда без розыгрыша карты; секундами каста не считается
    public void Wait()
    {
        if (combatOver) return;
        AdvanceTime(1);
        ResolveUpTo();
    }

    void Start()
    {
        UpdateBudgetText();
        if (combatResultText != null)
            combatResultText.text = "";
    }

    // Вызывать после ResetForNewCombat: сброс очищает расписание и список бойцов с тиком затухания
    public void SetPlayer(Player newPlayer)
    {
        if (player != null) player.OnDeath -= HandlePlayerDeath; // повторный SetPlayer не должен подписывать обработчик второй раз
        player = newPlayer;
        player.OnDeath += HandlePlayerDeath;
        RegisterBlockDecay(player);
    }

    // Затухание блока в момент t: база + прибавка за каждые полные step секунд (step <= 0 — без прибавки)
    public int BlockDecayAt(int t) => blockDecayBase + (blockDecayStepSeconds > 0 ? blockDecayPerStep * (t / blockDecayStepSeconds) : 0);

    // У каждого бойца свой тик раз в секунду; не в activeScheduledEffects (это не статус-эффект)
    private void RegisterBlockDecay(Combatant owner)
    {
        if (!decayRegistered.Add(owner)) return; // повторный SetPlayer / RegisterEnemies без сброса не создаёт второй тик
        RegisterScheduledEvent(new PeriodicEffectEvent(CurrentTime, 1, () => owner.DecayBlock(BlockDecayAt(CurrentEventTime)), owner, null, true));
    }

    public void ResetForNewCombat()
    {
        scheduledEvents.Clear();
        CurrentTime = 0;
        ReplayUntil = -1;
        journal.Clear();
        fireCounter = 0;
        replayOrders.Clear();
        creationOrders.Clear();
        decayRegistered.Clear();
        creationCounter = 0;
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
        foreach (var enemy in enemies)
            enemy.OnDeath -= HandleEnemyDeath;
        enemies = spawnedEnemies;
        foreach (var enemy in enemies)
        {
            enemy.OnDeath -= HandleEnemyDeath; // повторный RegisterEnemies не подписывает второй раз
            enemy.OnDeath += HandleEnemyDeath;
            RegisterBlockDecay(enemy);
        }
    }

    // «Мировое» — враги и их тики; всё остальное (таймер добора, тики на игроке, карты) — сторона игрока
    private static bool IsWorld(IScheduledEvent ev) => ev is Enemy || (ev is PeriodicEffectEvent p && p.Owner is Enemy);
    private static bool IsPlayerPeriodic(IScheduledEvent ev) => ev is PeriodicEffectEvent p && !(p.Owner is Enemy);
    private static Combatant OwnerOf(IScheduledEvent ev) => ev as Enemy ?? (ev as PeriodicEffectEvent)?.Owner;

    public void RegisterScheduledEvent(IScheduledEvent scheduledEvent)
    {
        scheduledEvents.Add(scheduledEvent);
        if (!creationOrders.ContainsKey(scheduledEvent)) creationOrders[scheduledEvent] = ++creationCounter;
        if (IsWorld(scheduledEvent))
            Record(OwnerOf(scheduledEvent), () => scheduledEvents.Remove(scheduledEvent));
    }

    public void UnregisterScheduledEvent(IScheduledEvent scheduledEvent)
    {
        int index = scheduledEvents.IndexOf(scheduledEvent);
        if (index < 0) return;
        scheduledEvents.RemoveAt(index);
        if (IsWorld(scheduledEvent)) // обратно — на прежнее место
            Record(OwnerOf(scheduledEvent), () => scheduledEvents.Insert(Mathf.Min(index, scheduledEvents.Count), scheduledEvent));
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
            if (combatOver) break; // бой закончен: события, запланированные позже, не срабатывают (дизайн-документ §4.2)
            IScheduledEvent next = null;
            OrderKey nextKey = default;
            foreach (var ev in scheduledEvents)
            {
                if (!ev.IsPending || ev.NextTime > CurrentTime) continue;
                var key = EffectiveKey(ev);
                if (next == null || key.CompareTo(nextKey) < 0)
                {
                    next = ev;
                    nextKey = key;
                }
            }
            if (next == null) break;
            if (next.NextTime > ReplayUntil) replayOrders.Clear(); // вышли за интервал повтора: номера несработавших повторов больше не действуют
            CurrentEvent = next;
            CurrentEventTime = next.NextTime;
            CurrentFireOrder = ++fireCounter;
            if (replayOrders.TryGetValue(next, out var pending) && pending.Count > 0) pending.Pop(); // это срабатывание — повтор самого раннего прежнего
            try { next.Trigger(); }
            finally { CurrentEvent = null; }
        }
    }

    // Порядок событий задаёт один ключ — кортеж, сравниваемый по полям по очереди (дизайн-документ §4.1):
    // момент; окружение (группа 2) в конце момента; повторяемые — раньше новых; номер прежнего срабатывания; группа (EventGroup);
    // носитель (индекс врага слева направо; только в группе врагов); номер создания; признак «Регенерация».
    private readonly struct OrderKey : System.IComparable<OrderKey>
    {
        public readonly (int, int, int, int, int, int, int, int) Fields;
        public OrderKey((int, int, int, int, int, int, int, int) fields) { Fields = fields; }
        public int CompareTo(OrderKey other) => Fields.CompareTo(other.Fields);
    }

    private OrderKey BaseKey(IScheduledEvent ev)
    {
        int replay = ReplayOrderOf(ev);
        int owner = ev.Priority == EventGroup.Enemies ? enemies.IndexOf(OwnerOf(ev) as Enemy) : 0;
        int created = creationOrders.TryGetValue(ev, out var c) ? c : 0;
        return new OrderKey((ev.NextTime, ev.Priority == EventGroup.Environment ? 1 : 0, replay > 0 ? 0 : 1, replay, ev.Priority, owner, created, 1));
    }

    // Тик «лечит по времени» (Регенерация) берёт минимум из своего ключа и ключей ожидающих тиков «бьёт по времени» того же бойца в тот же момент
    // и ставится прямо перед ними (последнее поле 0). Остальные события — базовый ключ.
    private OrderKey EffectiveKey(IScheduledEvent ev)
    {
        var key = BaseKey(ev);
        if (!(ev is PeriodicEffectEvent p) || p.Owner == null || !(p.EffectType is StatusEffectType type) || !StatusEffectRules.IsHealOverTime(type))
            return key;
        foreach (var other in scheduledEvents)
        {
            if (other == ev || !other.IsPending || other.NextTime != ev.NextTime) continue;
            if (other is PeriodicEffectEvent q && q.Owner == p.Owner && q.EffectType is StatusEffectType otherType && StatusEffectRules.IsDamageOverTime(otherType))
            {
                var otherKey = BaseKey(other);
                if (otherKey.CompareTo(key) < 0) key = otherKey;
            }
        }
        var f = key.Fields;
        f.Item8 = 0;
        return new OrderKey(f);
    }

    // 0 — обычное событие; >0 — номер прежнего срабатывания, которое оно сейчас повторяет
    private int ReplayOrderOf(IScheduledEvent ev) =>
        ev.NextTime <= ReplayUntil && replayOrders.TryGetValue(ev, out var s) && s.Count > 0 ? s.Peek() : 0;

    public void PushReplayOrder(IScheduledEvent ev, int order)
    {
        if (!replayOrders.TryGetValue(ev, out var s)) replayOrders[ev] = s = new Stack<int>();
        s.Push(order);
    }

    // Откат: вызывается из эффекта карты (внутри события). Игрок не меняется, враги и мир возвращаются обратными процедурами.
    public void Rewind(int n)
    {
        if (CurrentEvent == null || combatOver) return; // после конца боя откат невозможен (дизайн-документ §8.2)
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
        if (combatOver) return; // бой уже закончен (например, победой) — второго исхода не будет
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