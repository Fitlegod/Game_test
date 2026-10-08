using System.Collections.Generic;
using UnityEngine;

public class Enemy : Combatant, IScheduledEvent
{
    public Player player;
    public EnemyPatternData pattern;

    private int currentStepIndex;
    private float nextActionTime;

    public float NextTime => nextActionTime;
    public int Priority => 1;
    public int CurrentStepIndex => currentStepIndex;

    private float staggerAppliedAt = float.NegativeInfinity;
    private float staggerAmount;

    public void ApplyStagger(float seconds)
    {
        nextActionTime += seconds;
        staggerAppliedAt = combatManager.CurrentTime;
        staggerAmount = seconds;
    }

    public float StaggerDisplay => Mathf.Max(0f, staggerAmount - (combatManager.CurrentTime - staggerAppliedAt));

    public void ScheduleFirstAction()
    {
        nextActionTime = combatManager.CurrentTime + pattern.steps[0].delaySeconds;
    }

    public void Trigger()
    {
        if (CurrentHP <= 0) return;

        var step = pattern.steps[currentStepIndex];
        if (!TryExecute(step.target, step.targetEnemyIndex, step.instantActions, step.appliedEffects) && step.hasFallback)
            TryExecute(step.fallback.target, step.fallback.targetEnemyIndex, step.fallback.instantActions, step.fallback.appliedEffects);
        currentStepIndex = (currentStepIndex + 1) % pattern.steps.Count;
        nextActionTime += pattern.steps[currentStepIndex].delaySeconds;
    }

    private bool TryExecute(EnemyActionTarget targetKind, int targetIndex, List<InstantActionEntry> instantActions, List<AppliedEffectEntry> appliedEffects)
    {
        List<Combatant> targets = ResolveTargets(targetKind, targetIndex);
        if (targets.Count == 0) return false;

        foreach (var target in targets)
        {
            foreach (var action in instantActions)
                action.Apply(combatManager, this, target);
            foreach (var effect in appliedEffects)
                effect.Apply(combatManager, this, target);
        }
        return true;
    }

    public List<Combatant> ResolveTargets(EnemyActionTarget target, int specificIndex)
    {
        switch (target)
        {
            case EnemyActionTarget.Self:
                return new List<Combatant> { this };

            case EnemyActionTarget.Player:
                return new List<Combatant> { player };

            case EnemyActionTarget.AllOtherEnemies:
                {
                    var result = new List<Combatant>();
                    foreach (var enemy in combatManager.Enemies)
                        if (enemy != this && enemy.CurrentHP > 0)
                            result.Add(enemy);
                    return result;
                }

            case EnemyActionTarget.LowestHpOtherEnemy:
                {
                    Enemy lowest = null;
                    float lowestRatio = float.PositiveInfinity;
                    foreach (var enemy in combatManager.Enemies)
                    {
                        if (enemy == this || enemy.CurrentHP <= 0) continue;
                        float ratio = (float)enemy.CurrentHP / enemy.maxHP;
                        if (ratio < lowestRatio)
                        {
                            lowestRatio = ratio;
                            lowest = enemy;
                        }
                    }
                    return lowest != null ? new List<Combatant> { lowest } : new List<Combatant>();
                }

            case EnemyActionTarget.SpecificEnemyIndex:
                {
                    var enemies = combatManager.Enemies;
                    if (specificIndex >= 0 && specificIndex < enemies.Count && enemies[specificIndex].CurrentHP > 0)
                        return new List<Combatant> { enemies[specificIndex] };
                    return new List<Combatant>();
                }

            default:
                return new List<Combatant>();
        }
    }
}
