using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class Card : MonoBehaviour, IPointerClickHandler
{
    public CardInstance instance;
    public CombatManager combatManager;
    public TMP_Text nameLabel;
    public TMP_Text effectLabel;
    public TMP_Text propertiesLabel;

    public CardData data => instance.data;

    void Awake()
    {
        if (instance != null && instance.data != null && nameLabel != null)
            nameLabel.text = data.cardName;
        UpdatePropertiesText();
    }

    void Update()
    {
        UpdateEffectText();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TargetSelectionManager.Instance.SelectCard(this);
    }

    public bool RequiresTarget =>
        data.instantActions.Exists(a => a.targetTag == EffectTargetTag.Enemy) ||
        data.appliedEffects.Exists(e => e.targetTag == EffectTargetTag.Enemy);

    public bool IsValidTarget(Combatant target) => target is Enemy;

    public void Play(Combatant target)
    {
        float effectTime = combatManager.CurrentTime + data.effectDelaySeconds;
        combatManager.RegisterScheduledEvent(new OneShotEvent(effectTime, priority: 0, () => ApplyAllEffects(target)));

        combatManager.AdvanceTime(data.timeCostSeconds);
        combatManager.ResolveUpTo(combatManager.CurrentTime);

        if (HandManager.Instance != null)
            HandManager.Instance.OnCardPlayed(this);
    }

    private void ApplyAllEffects(Combatant chosenTarget)
    {
        foreach (var action in data.instantActions)
            foreach (var target in ResolveTargets(action.targetTag, chosenTarget))
                action.Apply(combatManager, combatManager.player, target);

        foreach (var effect in data.appliedEffects)
            foreach (var target in ResolveTargets(effect.targetTag, chosenTarget))
                effect.Apply(combatManager, combatManager.player, target);

        if (data.drawCardsOnPlay > 0 && HandManager.Instance != null)
            for (int i = 0; i < data.drawCardsOnPlay; i++)
                HandManager.Instance.TryDrawToHand();
    }

    private List<Combatant> ResolveTargets(EffectTargetTag tag, Combatant chosenTarget)
    {
        switch (tag)
        {
            case EffectTargetTag.Player:
                return new List<Combatant> { combatManager.player };
            case EffectTargetTag.Enemy:
                return chosenTarget != null ? new List<Combatant> { chosenTarget } : new List<Combatant>();
            case EffectTargetTag.AllEnemies:
                return combatManager.Enemies.Where(e => e.CurrentHP > 0).Cast<Combatant>().ToList();
            default:
                return new List<Combatant>();
        }
    }

    private void UpdateEffectText()
    {
        if (effectLabel == null || instance == null || instance.data == null) return;

        Combatant previewTarget = null;
        bool isPending = TargetSelectionManager.Instance != null && TargetSelectionManager.Instance.PendingCard == this;
        if (isPending)
            previewTarget = TargetSelectionManager.Instance.HoveredTarget;

        var lines = new List<string>();
        foreach (var action in data.instantActions)
            lines.Add(DescribeInstantAction(action, previewTarget));
        foreach (var effect in data.appliedEffects)
            lines.Add(DescribeAppliedEffect(effect));
        if (data.drawCardsOnPlay > 0)
            lines.Add("Добор: " + data.drawCardsOnPlay);

        effectLabel.text = string.Join("\n", lines);
    }

    private int ComputePreviewAmount(InstantActionEntry action, Combatant previewTarget)
    {
        Combatant recipient = action.targetTag switch
        {
            EffectTargetTag.Player => combatManager.player,
            EffectTargetTag.Enemy => previewTarget,
            _ => null
        };

        switch (action.kind)
        {
            case InstantActionKind.Damage:
                int dmg = combatManager.player.CalculateOutgoingDamage(action.amount);
                if (recipient != null) dmg = recipient.ApplyIncomingDamageModifiers(dmg);
                return dmg;
            case InstantActionKind.Block:
                return recipient != null ? recipient.CalculateIncomingBlock(action.amount) : action.amount;
            default:
                return action.amount;
        }
    }

    private string HitCountSuffix(int hitCount)
    {
        if (hitCount <= 1) return "";
        if (hitCount == 2) return " дважды";
        if (hitCount <= 4) return " " + hitCount + " раза";
        return " " + hitCount + " раз";
    }

    private string DescribeInstantAction(InstantActionEntry action, Combatant previewTarget)
    {
        int value = ComputePreviewAmount(action, previewTarget);
        string hitSuffix = HitCountSuffix(action.hitCount);

        switch (action.kind)
        {
            case InstantActionKind.Damage:
                if (action.targetTag == EffectTargetTag.Enemy) return "Наносит " + value + " урона" + hitSuffix;
                if (action.targetTag == EffectTargetTag.AllEnemies) return "Наносит " + value + " урона" + hitSuffix + " всем врагам";
                return "Наносит " + value + " урона" + hitSuffix + " себе";

            case InstantActionKind.Block:
                if (action.targetTag == EffectTargetTag.Enemy) return "Даёт " + value + " блока" + hitSuffix + " врагу";
                if (action.targetTag == EffectTargetTag.AllEnemies) return "Даёт " + value + " блока" + hitSuffix + " всем врагам";
                return "Даёт " + value + " блока" + hitSuffix;

            case InstantActionKind.Heal:
                if (action.targetTag == EffectTargetTag.Enemy) return "Восстанавливает " + value + " HP врагу";
                if (action.targetTag == EffectTargetTag.AllEnemies) return "Восстанавливает " + value + " HP всем врагам";
                return "Восстанавливает " + value + " HP";

            default:
                return "";
        }
    }

    private bool IsPositiveEffect(AppliedEffectEntry effect)
    {
        switch (effect.statusType)
        {
            case StatusEffectType.Strength:
            case StatusEffectType.Regen:
                return true;
            case StatusEffectType.Toughness:
                return effect.stacks >= 0;
            default:
                return false;
        }
    }

    private string DescribeAppliedEffect(AppliedEffectEntry effect)
    {
        string name = GetGenitiveName(effect.statusType);
        string n = effect.stacks.ToString("0.##");

        if (IsPositiveEffect(effect))
        {
            if (effect.targetTag == EffectTargetTag.Enemy) return "Даёт " + n + " " + name + " врагу";
            if (effect.targetTag == EffectTargetTag.AllEnemies) return "Даёт " + n + " " + name + " всем врагам";
            return "Даёт " + n + " " + name;
        }
        else
        {
            if (effect.targetTag == EffectTargetTag.Enemy) return "Накладывает " + n + " " + name;
            if (effect.targetTag == EffectTargetTag.AllEnemies) return "Накладывает " + n + " " + name + " всем врагам";
            return "Даёт " + n + " " + name;
        }
    }

    private static string GetGenitiveName(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Strength: return "Силы";
            case StatusEffectType.Weak: return "Слабости";
            case StatusEffectType.Toughness: return "Крепкости";
            case StatusEffectType.Frailty: return "Хрупкости";
            case StatusEffectType.Vulnerable: return "Уязвимости";
            case StatusEffectType.Stagger: return "Пошатывания";
            case StatusEffectType.Regen: return "Лечения";
            default: return type.ToString();
        }
    }

    private void UpdatePropertiesText()
    {
        if (propertiesLabel == null || instance == null || instance.data == null) return;
        var props = new List<string>();
        var effective = instance.EffectiveProperties;
        if ((effective & CardPropertyFlags.Exhaust) != 0) props.Add("Сжигаемое");
        if ((effective & CardPropertyFlags.FrontOfDraw) != 0) props.Add("Впереди");
        propertiesLabel.text = string.Join(", ", props);
    }
}
