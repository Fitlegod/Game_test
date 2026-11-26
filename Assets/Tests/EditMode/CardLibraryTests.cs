using NUnit.Framework;
using UnityEngine;

public class CardLibraryTests
{
    CardLibrary library;
    CardData firstCard;
    CardData secondCard;

    [SetUp]
    public void SetUp()
    {
        library = ScriptableObject.CreateInstance<CardLibrary>();
        firstCard = ScriptableObject.CreateInstance<CardData>();
        secondCard = ScriptableObject.CreateInstance<CardData>();
    }

    [TearDown]
    public void TearDown()
    {
        if (library != null)
            Object.DestroyImmediate(library);

        if (firstCard != null)
            Object.DestroyImmediate(firstCard);

        if (secondCard != null)
            Object.DestroyImmediate(secondCard);
    }

    [Test]
    public void Cards_StoresAndExposesCardData()
    {
        library.cards.Add(firstCard);
        library.cards.Add(secondCard);

        Assert.AreEqual(2, library.Cards.Count);
        Assert.AreSame(firstCard, library.Cards[0]);
        Assert.AreSame(secondCard, library.Cards[1]);
    }

    [Test]
    public void Cards_IsEmptyWhenLibraryHasNoCards()
    {
        Assert.IsNotNull(library.Cards);
        Assert.AreEqual(0, library.Cards.Count);
    }
}