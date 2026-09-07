public interface IScheduledEvent
{
    float NextTime { get; }
    int Priority { get; }
    void Trigger();
}