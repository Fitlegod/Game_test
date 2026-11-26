using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class HandManagerIntegrationTests
{
    GameObject deckObject;
    GameObject handObject;
    GameObject cardPrefabObject;
    GameObject combatObject;
    GameObject[] slotObjects;

    DeckManager deckManager;
    HandManager handManager;
    CombatManager combatManager;

    List<CardData> testCardData = new List<CardData>();

    [SetUp]
    public void SetUp()
    {
        deckObject = new GameObject("DeckManager_Test");
        deckManager = deckObject.AddComponent<DeckManager>();

        combatObject = new GameObject("CombatManager_Test");
        combatManager = combatObject.AddComponent<CombatManager>();

        handObject = new GameObject("HandManager_Test");
        handManager = handObject.AddComponent<HandManager>();
        handManager.deckManager = deckManager;
        handManager.combatManager = combatManager;

        cardPrefabObject = new GameObject("CardPrefab_Test");
        cardPrefabObject.AddComponent<Card>();
        handManager.cardPrefab = cardPrefabObject;

        slotObjects = new GameObject[HandManager.MaxHandSize];
        handManager.slots = new RectTransform[HandManager.MaxHandSize];

        for (int i = 0; i < HandManager.MaxHandSize; i++)
        {
            slotObjects[i] = new GameObject("Slot_" + i);
            handManager.slots[i] = slotObjects[i].AddComponent<RectTransform>();
        }

        for (int i = 0; i < 6; i++)
        {
            var data = ScriptableObject.CreateInstance<CardData>();
            data.nameKey = "card.test_" + i + ".name";
            testCardData.Add(data);

            deckManager.characterDeck.Add(new CardInstance(data));
        }
    }


    [TearDown]
    public void TearDown()
    {
        if (handManager != null)
        {
            for (int i = 0; i < HandManager.MaxHandSize; i++)
            {
                Card card = handManager.GetCardInSlot(i);
                if (card != null)
                    Object.DestroyImmediate(card.gameObject);
            }
        }

        if (deckObject != null)
            Object.DestroyImmediate(deckObject);

        if (handObject != null)
            Object.DestroyImmediate(handObject);

        if (combatObject != null)
            Object.DestroyImmediate(combatObject);

        if (cardPrefabObject != null)
            Object.DestroyImmediate(cardPrefabObject);

        if (slotObjects != null)
        {
            foreach (var slot in slotObjects)
                if (slot != null)
                    Object.DestroyImmediate(slot);
        }

        foreach (var data in testCardData)
            if (data != null)
                Object.DestroyImmediate(data);
    }

    [Test]
    public void BeginNewHand_DrawsFourAndPeriodicEventDrawsAdditionalCards()
    {
        int characterDeckCount = deckManager.characterDeck.Count;

        handManager.BeginNewHand();

        int cardsInHand = 0;
        for (int i = 0; i < HandManager.MaxHandSize; i++)
            if (handManager.GetCardInSlot(i) != null)
                cardsInHand++;

        Assert.AreEqual(HandManager.InitialHandSize, cardsInHand);
        Assert.AreEqual(characterDeckCount - HandManager.InitialHandSize,
            deckManager.DrawPile.Count);
        Assert.AreEqual(characterDeckCount, deckManager.CharacterDeck.Count);

        combatManager.AdvanceTime(HandManager.DrawIntervalSeconds);
        combatManager.ResolveUpTo();

        Assert.IsNotNull(handManager.GetCardInSlot(4));
        Assert.AreEqual(5, CountCardsInHand());

        combatManager.AdvanceTime(HandManager.DrawIntervalSeconds);
        combatManager.ResolveUpTo();

        Assert.IsNotNull(handManager.GetCardInSlot(5));
        Assert.AreEqual(6, CountCardsInHand());

        Assert.AreEqual(0, deckManager.DrawPile.Count);
        Assert.AreEqual(0, deckManager.DiscardPile.Count);
        Assert.AreEqual(0, deckManager.ExhaustPile.Count);
        Assert.AreEqual(characterDeckCount, deckManager.CharacterDeck.Count);
    }

    private int CountCardsInHand()
    {
        int count = 0;
        for (int i = 0; i < HandManager.MaxHandSize; i++)
            if (handManager.GetCardInSlot(i) != null)
                count++;
        return count;
    }
}
