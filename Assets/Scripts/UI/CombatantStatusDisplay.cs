using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CombatantStatusDisplay : MonoBehaviour
{
    public Combatant target;
    public Slider hpSlider;
    public TMP_Text blockLabel;
    public TMP_Text effectsLabel;
    public Enemy enemyForTelegraph; // заполняется только у EnemyPrefab, пусто у игрока
    public TMP_Text telegraphLabel;
    public CombatManager combatManager; // нужен только для телеграфа (CurrentTime)

    void Start()
    {
        if (hpSlider != null) hpSlider.maxValue = target.maxHP;
    }

    void Update()
    {
        if (hpSlider != null) hpSlider.value = target.CurrentHP;
        if (blockLabel != null) blockLabel.text = target.CurrentBlock.ToString();
        if (effectsLabel != null) effectsLabel.text = BuildEffectsText();
        if (enemyForTelegraph != null && telegraphLabel != null) telegraphLabel.text = BuildTelegraphText();
    }

    private string BuildEffectsText()
    {
        string text = "";
        AppendIfPresent(ref text, "Сила", StatusEffectType.Strength);
        AppendIfPresent(ref text, "Слабость", StatusEffectType.Weak);
        AppendIfPresent(ref text, "Лечение", StatusEffectType.Regen);
        AppendIfPresent(ref text, "Крепкость", StatusEffectType.Toughness);
        AppendIfPresent(ref text, "Хрупкость", StatusEffectType.Frailty);
        AppendIfPresent(ref text, "Уязвимость", StatusEffectType.Vulnerable);

        if (target is Enemy enemy && enemy.StaggerDisplay > 0f)
        {
            if (text.Length > 0) text += " | ";
            text += "Пошатывание x" + enemy.StaggerDisplay.ToString("0.##");
        }

        return text;
    }

    private void AppendIfPresent(ref string text, string name, StatusEffectType type)
    {
        float stacks = target.GetStacks(type);
        if (stacks <= 0) return;
        if (text.Length > 0) text += " | ";
        text += name + " x" + stacks.ToString("0.##");
    }

    private string BuildTelegraphText()
    {
        float remaining = enemyForTelegraph.NextTime - combatManager.CurrentTime;
        string text = "Атака через: " + Mathf.Max(0, remaining).ToString("0.0") + " с";

        if (enemyForTelegraph.pattern != null && enemyForTelegraph.pattern.steps.Count > 0)
        {
            EnemyActionStep step = enemyForTelegraph.pattern.steps[enemyForTelegraph.CurrentStepIndex];
            List<Combatant> targets = enemyForTelegraph.ResolveTargets(step.target, step.targetEnemyIndex);

            text += "\n" + step.stepName;
            foreach (var action in step.instantActions)
                text += "\n" + DescribeInstantAction(action, targets, step.target);
            foreach (var effect in step.appliedEffects)
                text += "\n" + DescribeAppliedEffect(effect, targets, step.target);
        }

        return text;
    }

    private string DescribeInstantAction(InstantActionEntry action, List<Combatant> targets, EnemyActionTarget targetKind)
    {
        if (targets.Count == 0) return "(нет цели)";

        Combatant recipient = targetKind == EnemyActionTarget.AllOtherEnemies ? null : targets[0];
        int value = action.ComputePreviewAmount(enemyForTelegraph, recipient);
        string hitSuffix = CardTextHelpers.HitCountSuffix(action.hitCount);
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

        string name = CardTextHelpers.GetGenitiveName(effect.statusType);
        string n = effect.stacks.ToString("0.##");
        string verb = effect.IsPositive() ? "Даёт" : "Накладывает";
        return verb + " " + n + " " + name + EnemyActionStep.DescribeTarget(targetKind);
    }
}
