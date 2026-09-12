using System.Collections.Generic;

[System.Serializable]
public class EnemyActionStep
{
    public string stepName;
    public float delaySeconds;
    public EnemyActionTarget target;
    public int targetEnemyIndex; // используется только при target == SpecificEnemyIndex
    public List<InstantActionEntry> instantActions = new List<InstantActionEntry>();
    public List<AppliedEffectEntry> appliedEffects = new List<AppliedEffectEntry>();
    public EnemyActionStep fallback; // null = при отсутствии цели шаг просто пропускается
}
