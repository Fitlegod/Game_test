using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class EnemyAttackTimerDisplay : MonoBehaviour
{
    public Enemy enemy;
    public CombatManager combatManager;
    public TMP_Text label;

    void Update()
    {
        float remaining = enemy.NextTime - combatManager.CurrentTime;
        string text = "Атака через: " + Mathf.Max(0, remaining).ToString("0.0") + " с";

        if (enemy.pattern != null && enemy.pattern.steps.Count > 0)
        {
            EnemyActionStep step = enemy.pattern.steps[enemy.CurrentStepIndex];
            List<Combatant> targets = enemy.ResolveTargets(step.target, step.targetEnemyIndex);

            text += "\n" + step.stepName;
            foreach (var action in step.instantActions)
                text += "\n" + DescribeInstantAction(action, targets, step.target);
            foreach (var effect in step.appliedEffects)
                text += "\n" + DescribeAppliedEffect(effect, targets, step.target);
        }

        label.text = text;
    }

    private string DescribeInstantAction(InstantActionEntry action, List<Combatant> targets, EnemyActionTarget targetKind)
    {
        if (targets.Count == 0) return "(нет цели)";

        Combatant recipient = targetKind == EnemyActionTarget.AllOtherEnemies ? null : targets[0];
        int value = action.ComputePreviewAmount(enemy, recipient);
        string hitSuffix = InstantActionEntry.HitCountSuffix(action.hitCount);
        string targetSuffix = EnemyActionStep.DescribeTarget(targetKind);

        switch (action.kind)
        {
            case InstantActionKind.Damage: return "Наносит " + value + " урона" + hitSuffix + targetSuffix;
            case InstantActionKind.Block: return "Даёт " + value + " блока" + hitSuffix + targetSuffix;
            case InstantActionKind.Heal: return "Восстанавливает " + value + " HP" + targetSuffix;
            default: return "";
        }
    }

    private string DescribeAppliedEffect(AppliedEffectEntry effect, List<Combatant> targets, EnemyActionTarget targetKind)
    {
        if (targets.Count == 0) return "(нет цели)";

        string name = AppliedEffectEntry.GetGenitiveName(effect.statusType);
        string n = effect.stacks.ToString("0.##");
        string verb = effect.IsPositive() ? "Даёт" : "Накладывает";
        return verb + " " + n + " " + name + EnemyActionStep.DescribeTarget(targetKind);
    }
}
