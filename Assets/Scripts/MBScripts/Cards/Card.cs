using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class Card : MonoBehaviour, IPointerClickHandler
{
    public CardInstance instance;
    public CombatManager combatManager;
    public TMP_Text nameLabel;

    public CardData data => instance.data;

    void Awake()
    {
        if (instance != null && instance.data != null && nameLabel != null)
            nameLabel.text = data.cardName;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TargetSelectionManager.Instance.SelectCard(this);
    }

    public bool RequiresTarget =>
        data.instantActions.Exists(a => a.kind == InstantActionKind.Damage) || data.appliedEffects.Count > 0;

    public bool IsValidTarget(Combatant target)
    {
        bool hasDamage = data.instantActions.Exists(a => a.kind == InstantActionKind.Damage);
        if (hasDamage && target is Player)
            return false;
        return true;
    }

    public void Play(Combatant target)
    {
        float effectTime = combatManager.CurrentTime + data.effectDelaySeconds;
        combatManager.RegisterScheduledEvent(new OneShotEvent(effectTime, priority: 0, () => ApplyAllEffects(target)));

        combatManager.AdvanceTime(data.timeCostSeconds);
        combatManager.ResolveUpTo(combatManager.CurrentTime);

        if (HandManager.Instance != null)
            HandManager.Instance.OnCardPlayed(this);
    }

    private void ApplyAllEffects(Combatant target)
    {
        foreach (var action in data.instantActions)
            action.Apply(combatManager, target);
        foreach (var effect in data.appliedEffects)
            effect.Apply(combatManager, target);
    }
}