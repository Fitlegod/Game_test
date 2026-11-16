using NUnit.Framework;
using UnityEngine;

public class CardInstanceTests
{
    CardData baseData;
    CardData upgradedData;

    [SetUp]
    public void SetUp()
    {
        baseData = ScriptableObject.CreateInstance<CardData>();
        upgradedData = ScriptableObject.CreateInstance<CardData>();

        baseData.properties = CardPropertyFlags.Exhaust;
        baseData.upgradedVersion = upgradedData;
    }

    [TearDown]
    public void TearDown()
    {
        if (baseData != null)
            Object.DestroyImmediate(baseData);

        if (upgradedData != null)
            Object.DestroyImmediate(upgradedData);
    }

    [Test]
    public void CreateCopy_PreservesDataAndInstanceProperties()
    {
        var original = new CardInstance(baseData);
        original.instanceProperties = CardPropertyFlags.FrontOfDraw;

        var copy = original.CreateCopy();

        Assert.AreSame(baseData, copy.data);
        Assert.AreEqual(original.instanceProperties, copy.instanceProperties);
        Assert.AreEqual(original.EffectiveProperties, copy.EffectiveProperties);
    }

    [Test]
    public void CreateCopy_ChangesDoNotAffectOriginal()
    {
        var original = new CardInstance(baseData);
        var copy = original.CreateCopy();

        copy.instanceProperties = CardPropertyFlags.FrontOfDraw;

        Assert.AreEqual(CardPropertyFlags.None, original.instanceProperties);
        Assert.AreEqual(CardPropertyFlags.FrontOfDraw, copy.instanceProperties);
    }

    [Test]
    public void TryUpgrade_ChangesOnlyThisInstance()
    {
        var original = new CardInstance(baseData);
        var anotherCopy = new CardInstance(baseData);

        Assert.IsTrue(original.CanUpgrade);
        Assert.IsTrue(original.TryUpgrade());

        Assert.AreSame(upgradedData, original.data);
        Assert.AreSame(baseData, anotherCopy.data);
        Assert.AreSame(baseData, baseData.upgradedVersion == upgradedData
            ? anotherCopy.data : null);
    }

    [Test]
    public void TryUpgrade_ReturnsFalseWhenNoUpgradeExists()
    {
        upgradedData.upgradedVersion = null;

        var instance = new CardInstance(upgradedData);

        Assert.IsFalse(instance.CanUpgrade);
        Assert.IsFalse(instance.TryUpgrade());
        Assert.AreSame(upgradedData, instance.data);
    }

    [Test]
    public void EffectiveProperties_CombinesBaseAndInstanceProperties()
    {
        var instance = new CardInstance(baseData);
        instance.instanceProperties = CardPropertyFlags.FrontOfDraw;

        Assert.AreEqual(
            CardPropertyFlags.Exhaust | CardPropertyFlags.FrontOfDraw,
            instance.EffectiveProperties
        );
    }
}
