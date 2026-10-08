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
        if (pattern == null || pattern.steps.Count == 0)
        {
            Debug.LogError("Враг " + name + ": пустой паттерн, враг не будет действовать", this);
            nextActionTime = float.PositiveInfinity;
            return;
        }
        nextActionTime = combatManager.CurrentTime + pattern.steps[0].delaySeconds;
    }

    public void Trigger()
    {
        if (CurrentHP <= 0) return;

        var plan = GetPlannedStep();
        foreach (var target in plan.targets)
        {
            foreach (var action in plan.instantActions)
                action.Apply(combatManager, this, target);
            foreach (var effect in plan.appliedEffects)
                effect.Apply(combatManager, this, target);
        }
        currentStepIndex = (currentStepIndex + 1) % pattern.steps.Count;
        nextActionTime += pattern.steps[currentStepIndex].delaySeconds;
    }

    // Что враг сделает на текущем шаге: основной шаг или, если у него нет цели, запасной.
    // Единственное место, где это решается, — и Trigger, и телеграф берут план отсюда.
    public PlannedStep GetPlannedStep()
    {
        var step = pattern.steps[currentStepIndex];
        var targets = ResolveTargets(step.target, step.targetEnemyIndex);
        if (targets.Count == 0 && step.hasFallback)
        {
            var fb = step.fallback;
            return new PlannedStep(fb.stepNameKey, fb.target, ResolveTargets(fb.target, fb.targetEnemyIndex), fb.instantActions, fb.appliedEffects);
        }
        return new PlannedStep(step.stepNameKey, step.target, targets, step.instantActions, step.appliedEffects);
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
