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

    public bool RequiresTarget =>
        effects.Exists(e => e.kind == EffectKind.Damage || e.kind == EffectKind.ApplyStatus);

    void Start()
    {
        Debug.Log(cardName + " (" + timeCostSeconds + " сек, эффект через " + effectDelaySeconds + " сек, " + cardType + ")");
    }

    public bool IsValidTarget(Combatant target)
    {
        bool hasDamage = effects.Exists(e => e.kind == EffectKind.Damage);
        if (hasDamage && target is Player)
            return false; // урон не может лететь в игрока
        return true;
    }

    public void Play(Combatant target)
    {
        float effectTime = combatManager.CurrentTime + effectDelaySeconds;
        combatManager.RegisterScheduledEvent(new OneShotEvent(effectTime, priority: 0, () => ApplyAllEffects(target)));

        combatManager.AdvanceTime(timeCostSeconds);
        combatManager.ResolveUpTo(combatManager.CurrentTime);
    }

    private void ApplyAllEffects(Combatant target)
    {
        foreach (var effect in effects)
            effect.Apply(combatManager, target);
    }
}