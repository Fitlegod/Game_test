using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCard", menuName = "Cards/Card Data")]
public class CardData : ScriptableObject
{
    [Header("Основное")]
    public string nameKey;
    public CardType cardType;
    public CardRarity rarity;
    public Sprite illustration;

    [Header("Время")]
    public int timeCostSeconds;
    public int effectDelaySeconds;

    [Header("Улучшение")]
    public CardData upgradedVersion;

    [Header("Разовые действия")]
    public List<InstantActionEntry> instantActions = new List<InstantActionEntry>();

    [Header("Добор карт")]
    public int drawCardsOnPlay;

    [Header("Накладываемые эффекты")]
    public List<AppliedEffectEntry> appliedEffects = new List<AppliedEffectEntry>();

    [Header("Парирование")]
    public List<InstantActionEntry> parryActions = new List<InstantActionEntry>();
    public List<AppliedEffectEntry> parryEffects = new List<AppliedEffectEntry>();

    [Header("Свойства карты")]
    public CardPropertyFlags properties;

    public string DisplayName => Loc.Get(nameKey);

    void OnValidate()
    {
        if (!string.IsNullOrEmpty(nameKey) && !Loc.Has(nameKey))
            Debug.LogWarning(name + ": нет строки для ключа названия [" + nameKey + "]", this);

        if (timeCostSeconds < 1)
            Debug.LogWarning(name + ": timeCostSeconds должен быть >= 1 (сейчас " + timeCostSeconds + ")", this);

        if (effectDelaySeconds < 0)
            Debug.LogWarning(name + ": effectDelaySeconds должен быть >= 0 (сейчас " + effectDelaySeconds + ")", this);

        if (upgradedVersion == this)
            Debug.LogWarning(name + ": карта не может быть улучшенной версией самой себя", this);

        if (upgradedVersion != null && upgradedVersion.upgradedVersion != null)
            Debug.LogWarning(name + ": улучшенная версия не должна иметь собственную улучшенную версию", this);
    }
}
