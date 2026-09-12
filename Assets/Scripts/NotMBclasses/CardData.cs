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
}