using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCard", menuName = "Cards/Card Data")]
public class CardData : ScriptableObject
{
    public string cardName;
    public float timeCostSeconds;
    public float effectDelaySeconds;
    public CardType cardType;

    [Header("Разовые действия")]
    public List<InstantActionEntry> instantActions = new List<InstantActionEntry>();

    [Header("Добор карт")]
    public int drawCardsOnPlay;

    [Header("Накладываемые эффекты")]
    public List<AppliedEffectEntry> appliedEffects = new List<AppliedEffectEntry>();

    [Header("Свойства карты")]
    public CardPropertyFlags properties;

    void OnValidate()
    {
        if (timeCostSeconds != Mathf.Round(timeCostSeconds) || effectDelaySeconds != Mathf.Round(effectDelaySeconds))
            Debug.LogWarning(name + ": время должно быть целым (timeCostSeconds=" + timeCostSeconds + ", effectDelaySeconds=" + effectDelaySeconds + ")", this);
        if (timeCostSeconds < 1)
            Debug.LogWarning(name + ": timeCostSeconds должен быть >= 1 (сейчас " + timeCostSeconds + ")", this);
        if (effectDelaySeconds < 0)
            Debug.LogWarning(name + ": effectDelaySeconds должен быть >= 0 (сейчас " + effectDelaySeconds + ")", this);
    }
}