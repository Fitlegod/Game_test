public interface IScheduledEvent
{
    int NextTime { get; }
    int Priority { get; }
    bool IsPending { get; } // false = событие сейчас ничего не ждёт; из списка его не убирают
    void Trigger();
}
