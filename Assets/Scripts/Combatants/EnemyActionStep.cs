using System.Collections.Generic;

public class PlannedStep
{
    public readonly string stepNameKey;
    public readonly EnemyActionTarget target;
    public readonly List<Combatant> targets;
    public readonly List<InstantActionEntry> instantActions;
    public readonly List<AppliedEffectEntry> appliedEffects;

    public PlannedStep(string stepNameKey, EnemyActionTarget target, List<Combatant> targets, List<InstantActionEntry> instantActions, List<AppliedEffectEntry> appliedEffects)
    {
        this.stepNameKey = stepNameKey;
        this.target = target;
        this.targets = targets;
        this.instantActions = instantActions;
        this.appliedEffects = appliedEffects;
    }
}

// Запасной шаг: срабатывает в момент основного, поэтому без delaySeconds и без собственного запасного
[System.Serializable]
public class EnemyFallbackStep
{
    public string stepNameKey; // ключ в ru.txt: enemy.<паттерн>.<шаг>
    public EnemyActionTarget target;
    public int targetEnemyIndex;
    public List<InstantActionEntry> instantActions = new List<InstantActionEntry>();
    public List<AppliedEffectEntry> appliedEffects = new List<AppliedEffectEntry>();
}

[System.Serializable]
public class EnemyActionStep
{
    public string stepNameKey; // ключ в ru.txt: enemy.<паттерн>.<шаг>
    public float delaySeconds;
    public EnemyActionTarget target;
    public int targetEnemyIndex; // используется только при target == SpecificEnemyIndex
    public List<InstantActionEntry> instantActions = new List<InstantActionEntry>();
    public List<AppliedEffectEntry> appliedEffects = new List<AppliedEffectEntry>();
    public bool hasFallback; // false = при отсутствии цели шаг просто пропускается
    public EnemyFallbackStep fallback; // используется только при hasFallback
}
