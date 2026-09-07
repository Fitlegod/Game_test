using System.Collections.Generic;
using UnityEngine;

public class Card : MonoBehaviour
{
    public string cardName;
    public float timeCostSeconds;
    public float effectDelaySeconds;
    public CardType cardType;
    public CombatManager combatManager;
    public List<CardEffectEntry> effects = new List<CardEffectEntry>();

    void Start()
    {
        Debug.Log(cardName + " (" + timeCostSeconds + " сек, эффект через " + effectDelaySeconds + " сек, " + cardType + ")");
    }

    public void OnClicked()
    {
        float effectTime = combatManager.CurrentTime + effectDelaySeconds;
        combatManager.RegisterScheduledEvent(new OneShotEvent(effectTime, priority: 0, ApplyAllEffects));

        combatManager.AdvanceTime(timeCostSeconds);
        combatManager.ResolveUpTo(combatManager.CurrentTime);
    }

    private void ApplyAllEffects()
    {
        foreach (var effect in effects)
            effect.Apply(combatManager);
    }
}