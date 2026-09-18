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
        {
            Combatant recipient = action.targetTag switch
            {
                EffectTargetTag.Player => combatManager.player,
                EffectTargetTag.Enemy => previewTarget,
                _ => null
            };
            int value = action.ComputePreviewAmount(combatManager.player, recipient);
            lines.Add(action.Describe(value));
        }
        foreach (var effect in data.appliedEffects)
            lines.Add(effect.Describe());
        if (data.drawCardsOnPlay > 0)
            lines.Add("Добор: " + data.drawCardsOnPlay);

        effectLabel.text = string.Join("\n", lines);
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
