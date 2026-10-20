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

    private CanvasGroup enemyGroup; // только у врага: корень скрывается при смерти, но остаётся активным (Update должен работать — после отката враг снова виден)

    void Update()
    {
        if (enemyForTelegraph != null) UpdateEnemyVisibility();
        if (hpSlider != null) hpSlider.value = target.CurrentHP;
        if (blockLabel != null) blockLabel.text = target.CurrentBlock.ToString();
        if (effectsLabel != null) effectsLabel.text = BuildEffectsText();
        if (enemyForTelegraph != null && telegraphLabel != null) telegraphLabel.text = BuildTelegraphText();
    }

    private void UpdateEnemyVisibility()
    {
        if (enemyGroup == null)
        {
            var root = enemyForTelegraph.gameObject;
            if (!root.TryGetComponent(out enemyGroup)) enemyGroup = root.AddComponent<CanvasGroup>();
        }
        bool alive = enemyForTelegraph.CurrentHP > 0;
        enemyGroup.alpha = alive ? 1 : 0;
        enemyGroup.blocksRaycasts = alive;
        enemyGroup.interactable = alive;
    }

    private string BuildEffectsText()
    {
        string text = "";
        AppendIfPresent(ref text, StatusEffectType.Strength);
        AppendIfPresent(ref text, StatusEffectType.Weak);
        AppendIfPresent(ref text, StatusEffectType.Regen);
        AppendIfPresent(ref text, StatusEffectType.Toughness);
        AppendIfPresent(ref text, StatusEffectType.Frailty);
        AppendIfPresent(ref text, StatusEffectType.Vulnerable);
        AppendIfPresent(ref text, StatusEffectType.Poison);
        AppendIfPresent(ref text, StatusEffectType.Bleeding);
        AppendIfPresent(ref text, StatusEffectType.Invulnerable);

        if (target is Enemy enemy && enemy.StunDisplay > 0)
            Join(ref text, StatusEffectType.Stun, enemy.StunDisplay);

        return text;
    }

    private void AppendIfPresent(ref string text, StatusEffectType type)
    {
        float stacks = target.GetStacks(type);
        if (stacks <= 0) return;
        Join(ref text, type, stacks);
    }

    private static void Join(ref string text, StatusEffectType type, float stacks)
    {
        string entry = Loc.Format("effect.stacks", Loc.Get("effect.name." + type), stacks.ToString("0.##"));
        text = text.Length > 0 ? Loc.Format("effect.join", text, entry) : entry;
    }

    private string BuildTelegraphText()
    {
        int remaining = enemyForTelegraph.NextTime - combatManager.CurrentTime;
        string text = Loc.Format("combat.telegraph.time", Mathf.Max(0, remaining).ToString());

        if (enemyForTelegraph.pattern != null && enemyForTelegraph.pattern.steps.Count > 0)
        {
            PlannedStep step = enemyForTelegraph.GetPlannedStep();

            text += "\n" + Loc.Get(step.stepNameKey);
            foreach (var action in step.instantActions)
                text += "\n" + DescribeInstantAction(action, step.targets, step.target);
            foreach (var effect in step.appliedEffects)
                text += "\n" + DescribeAppliedEffect(effect, step.targets, step.target);
        }

        return text;
    }

    private string DescribeInstantAction(InstantActionEntry action, List<Combatant> targets, EnemyActionTarget targetKind)
    {
        if (targets.Count == 0) return Loc.Get("combat.telegraph.notarget");

        Combatant recipient = targetKind == EnemyActionTarget.AllOtherEnemies ? null : targets[0];
        int value = action.ComputePreviewAmount(enemyForTelegraph, recipient);
        return Loc.Format("combat.action." + action.kind + "." + targetKind, value, CardTextHelpers.HitCountSuffix(action.hitCount));
    }

    private string DescribeAppliedEffect(AppliedEffectEntry effect, List<Combatant> targets, EnemyActionTarget targetKind)
    {
        if (targets.Count == 0) return Loc.Get("combat.telegraph.notarget");

        return Loc.Format("combat.effect." + (effect.IsPositive() ? "Give." : "Apply.") + targetKind,
            effect.stacks.ToString("0.##"), CardTextHelpers.GetGenitiveName(effect.statusType));
    }
}
