using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCardLibrary", menuName = "Cards/Card Library")]
public class CardLibrary : ScriptableObject
{
    [Header("Доступные карты")]
    public List<CardData> cards = new List<CardData>();

    public IReadOnlyList<CardData> Cards => cards;
}