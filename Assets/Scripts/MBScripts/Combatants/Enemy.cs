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

    private float staggerAppliedAt = float.NegativeInfinity;
    private float staggerAmount;

    public void ApplyStagger(float seconds)
    {
        nextActionTime += seconds;
        staggerAppliedAt = combatManager.CurrentTime;
        staggerAmount = seconds;
    }

    public float StaggerDisplay => Mathf.Max(0f, staggerAmount - (combatManager.CurrentTime - staggerAppliedAt));

    public void Trigger()
    {
        if (CurrentHP <= 0) return;

        ExecuteStep(pattern.steps[currentStepIndex]);
        currentStepIndex = (currentStepIndex + 1) % pattern.steps.Count;
        nextActionTime += pattern.steps[currentStepIndex].delaySeconds;
    }

    private void ExecuteStep(EnemyActionStep step)
    {
        List<Combatant> targets = ResolveTargets(step.target, step.targetEnemyIndex);

        if (targets.Count == 0)
        {
            if (step.fallback != null)
                ExecuteStep(step.fallback);
            return;
        }

        foreach (var target in targets)
        {
            foreach (var action in step.instantActions)
                action.Apply(combatManager, this, target);
            foreach (var effect in step.appliedEffects)
                effect.Apply(combatManager, this, target);
        }
    }

    private List<Combatant> ResolveTargets(EnemyActionTarget target, int specificIndex)
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
