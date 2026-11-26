using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class DeckManagerTests
{
    GameObject managerObject;
    DeckManager deckManager;
    List<CardData> cardDataList;

    [SetUp]
    public void SetUp()
    {
        managerObject = new GameObject("DeckManager_Test");
        deckManager = managerObject.AddComponent<DeckManager>();
        cardDataList = new List<CardData>();
    }

    [TearDown]
    public void TearDown()
    {
        if (managerObject != null)
            Object.DestroyImmediate(managerObject);

        foreach (var data in cardDataList)
        {
            if (data != null)
                Object.DestroyImmediate(data);
        }
    }

    CardInstance CreateCard(
        CardPropertyFlags properties = CardPropertyFlags.None)
    {
        var data = ScriptableObject.CreateInstance<CardData>();
        data.nameKey = "card.jab.name";
        data.properties = properties;
        cardDataList.Add(data);

        return new CardInstance(data);
    }

    [Test]
    public void StartCombat_CopiesCharacterDeckWithoutChangingIt()
    {
        var first = CreateCard();
        var second = CreateCard();

        deckManager.characterDeck.Add(first);
        deckManager.characterDeck.Add(second);

        deckManager.StartCombat();

        Assert.AreEqual(2, deckManager.DrawPile.Count);
        Assert.AreEqual(2, deckManager.CharacterDeck.Count);

        CollectionAssert.Contains(deckManager.DrawPile, first);
        CollectionAssert.Contains(deckManager.DrawPile, second);

        Assert.AreSame(first, deckManager.CharacterDeck[0]);
        Assert.AreSame(second, deckManager.CharacterDeck[1]);
    }

    [Test]
    public void StartCombat_ClearsDiscardAndExhaustPiles()
    {
        var normal = CreateCard();
        var exhaust = CreateCard(CardPropertyFlags.Exhaust);

        deckManager.Discard(normal);
        deckManager.Discard(exhaust);

        Assert.AreEqual(1, deckManager.DiscardPile.Count);
        Assert.AreEqual(1, deckManager.ExhaustPile.Count);

        deckManager.StartCombat();

        Assert.AreEqual(0, deckManager.DiscardPile.Count);
        Assert.AreEqual(0, deckManager.ExhaustPile.Count);
    }

    [Test]
    public void DrawCard_DrawsAllCardsWithoutLosingThem()
    {
        var first = CreateCard();
        var second = CreateCard();
        var third = CreateCard();

        deckManager.characterDeck.Add(first);
        deckManager.characterDeck.Add(second);
        deckManager.characterDeck.Add(third);

        deckManager.StartCombat();

        var drawn = new List<CardInstance>();

        for (int i = 0; i < 3; i++)
            drawn.Add(deckManager.DrawCard());

        Assert.AreEqual(3, drawn.Count);
        Assert.IsNotNull(drawn[0]);
        Assert.IsNotNull(drawn[1]);
        Assert.IsNotNull(drawn[2]);

        CollectionAssert.AreEquivalent(
            new[] { first, second, third },
            drawn
        );

        Assert.AreEqual(0, deckManager.DrawPile.Count);
    }

    [Test]
    public void DrawCard_ReturnsNullWhenDrawAndDiscardAreEmpty()
    {
        deckManager.StartCombat();

        var drawn = deckManager.DrawCard();

        Assert.IsNull(drawn);
        Assert.AreEqual(0, deckManager.DrawPile.Count);
        Assert.AreEqual(0, deckManager.DiscardPile.Count);
    }

    [Test]
    public void DrawCard_ReshufflesDiscardWhenDrawPileIsEmpty()
    {
        var first = CreateCard();
        var second = CreateCard();

        deckManager.Discard(first);
        deckManager.Discard(second);

        var drawn = deckManager.DrawCard();

        Assert.IsNotNull(drawn);
        CollectionAssert.Contains(new[] { first, second }, drawn);

        Assert.AreEqual(1, deckManager.DrawPile.Count);
        Assert.AreEqual(0, deckManager.DiscardPile.Count);

        var secondDrawn = deckManager.DrawCard();

        Assert.IsNotNull(secondDrawn);
        Assert.AreNotSame(drawn, secondDrawn);
        Assert.AreEqual(0, deckManager.DrawPile.Count);
    }

    [Test]
    public void Discard_RoutesCardsAccordingToExhaustProperty()
    {
        var normal = CreateCard();
        var exhaust = CreateCard(CardPropertyFlags.Exhaust);

        deckManager.Discard(normal);
        deckManager.Discard(exhaust);

        Assert.AreEqual(1, deckManager.DiscardPile.Count);
        Assert.AreSame(normal, deckManager.DiscardPile[0]);

        Assert.AreEqual(1, deckManager.ExhaustPile.Count);
        Assert.AreSame(exhaust, deckManager.ExhaustPile[0]);
    }

    [Test]
    public void StartCombat_FrontOfDrawCardIsDrawnFirst()
    {
        var normal = CreateCard();
        var priority = CreateCard(CardPropertyFlags.FrontOfDraw);
        var anotherNormal = CreateCard();

        deckManager.characterDeck.Add(normal);
        deckManager.characterDeck.Add(priority);
        deckManager.characterDeck.Add(anotherNormal);

        deckManager.StartCombat();

        var firstDrawn = deckManager.DrawCard();

        Assert.AreSame(priority, firstDrawn);
    }
}
