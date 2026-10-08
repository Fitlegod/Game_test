using System.Collections.Generic;

public class PlannedStep
{
    public readonly string stepName;
    public readonly EnemyActionTarget target;
    public readonly List<Combatant> targets;
    public readonly List<InstantActionEntry> instantActions;
    public readonly List<AppliedEffectEntry> appliedEffects;

    public PlannedStep(string stepName, EnemyActionTarget target, List<Combatant> targets, List<InstantActionEntry> instantActions, List<AppliedEffectEntry> appliedEffects)
    {
        this.stepName = stepName;
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
    public string stepName;
    public EnemyActionTarget target;
    public int targetEnemyIndex;
    public List<InstantActionEntry> instantActions = new List<InstantActionEntry>();
    public List<AppliedEffectEntry> appliedEffects = new List<AppliedEffectEntry>();
}

[System.Serializable]
public class EnemyActionStep
{
    public string stepName;
    public float delaySeconds;
    public EnemyActionTarget target;
    public int targetEnemyIndex; // используется только при target == SpecificEnemyIndex
    public List<InstantActionEntry> instantActions = new List<InstantActionEntry>();
    public List<AppliedEffectEntry> appliedEffects = new List<AppliedEffectEntry>();
    public bool hasFallback; // false = при отсутствии цели шаг просто пропускается
    public EnemyFallbackStep fallback; // используется только при hasFallback

    public static string DescribeTarget(EnemyActionTarget target)
    {
        switch (target)
        {
            case EnemyActionTarget.Self: return "";
            case EnemyActionTarget.Player: return " по игроку";
            case EnemyActionTarget.AllOtherEnemies: return " союзникам";
            case EnemyActionTarget.LowestHpOtherEnemy: return " самому слабому союзнику";
            case EnemyActionTarget.SpecificEnemyIndex: return " союзнику";
            default: return "";
        }
    }
}
