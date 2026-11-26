using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class HandManagerTests
{
    GameObject deckObject;
    GameObject handObject;
    GameObject dummyCardObject;

    DeckManager deckManager;
    HandManager handManager;
    CardData cardData;

    [SetUp]
    public void SetUp()
    {
        deckObject = new GameObject("DeckManager_Test");
        deckManager = deckObject.AddComponent<DeckManager>();

        handObject = new GameObject("HandManager_Test");
        handManager = handObject.AddComponent<HandManager>();
        handManager.deckManager = deckManager;

        dummyCardObject = new GameObject("DummyCard_Test");
        var dummyCard = dummyCardObject.AddComponent<Card>();

        var field = typeof(HandManager).GetField(
            "cardsInSlots",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        var slots = (Card[])field.GetValue(handManager);

        for (int i = 0; i < HandManager.MaxHandSize; i++)
            slots[i] = dummyCard;

        cardData = ScriptableObject.CreateInstance<CardData>();
        cardData.nameKey = "card.jab.name";
    }

    [TearDown]
    public void TearDown()
    {
        if (deckObject != null)
            Object.DestroyImmediate(deckObject);

        if (handObject != null)
            Object.DestroyImmediate(handObject);

        if (dummyCardObject != null)
            Object.DestroyImmediate(dummyCardObject);

        if (cardData != null)
            Object.DestroyImmediate(cardData);
    }

    [Test]
    public void TryDrawToHand_FullHandDiscardsNormalCard()
    {
        var instance = new CardInstance(cardData);
        deckManager.characterDeck.Add(instance);
        deckManager.StartCombat();

        bool result = handManager.TryDrawToHand();

        Assert.IsFalse(result);
        Assert.AreEqual(1, deckManager.DiscardPile.Count);
        Assert.AreSame(instance, deckManager.DiscardPile[0]);
        Assert.AreEqual(0, deckManager.ExhaustPile.Count);
    }

    [Test]
    public void TryDrawToHand_FullHandExhaustsExhaustCard()
    {
        cardData.properties = CardPropertyFlags.Exhaust;

        var instance = new CardInstance(cardData);
        deckManager.characterDeck.Add(instance);
        deckManager.StartCombat();

        bool result = handManager.TryDrawToHand();

        Assert.IsFalse(result);
        Assert.AreEqual(0, deckManager.DiscardPile.Count);
        Assert.AreEqual(1, deckManager.ExhaustPile.Count);
        Assert.AreSame(instance, deckManager.ExhaustPile[0]);
    }
}
