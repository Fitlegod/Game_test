using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;

public class LocTests
{
    const string StringsPath = "Assets/Resources/Strings/ru.txt";

    static Dictionary<string, string> table;
    static List<string> parseErrors;

    [OneTimeSetUp]
    public void LoadTable()
    {
        table = new Dictionary<string, string>();
        parseErrors = Loc.Parse(File.ReadAllText(StringsPath), table);
    }

    private static void AssertKeysExist(IEnumerable<string> keys, string what)
    {
        var missing = keys.Distinct().Where(k => !table.ContainsKey(k)).OrderBy(k => k).ToList();
        Assert.IsEmpty(missing, what + ": нет в ru.txt:\n" + string.Join("\n", missing));
    }

    private static IEnumerable<string> Files(string pattern)
        => Directory.GetFiles("Assets", pattern, SearchOption.AllDirectories);

    [Test]
    public void RuTxt_NoDuplicateKeysAndNoMalformedLines()
    {
        Assert.IsEmpty(parseErrors, string.Join("\n", parseErrors));
        Assert.Greater(table.Count, 0);
    }

    [Test]
    public void KeysPassedToLocAsLiterals_ExistInTable()
    {
        var regex = new Regex(@"Loc\.(?:Get|Format|Has)\(\s*""([^""]+)""\s*[,)]");
        var keys = new List<string>();
        foreach (string file in Files("*.cs"))
        {
            if (file.Replace('\\', '/').Contains("/Tests/")) continue;
            foreach (Match m in regex.Matches(File.ReadAllText(file)))
                keys.Add(m.Groups[1].Value);
        }
        Assert.Greater(keys.Count, 0, "в коде не найдено ни одного литерального ключа — регулярка устарела?");
        AssertKeysExist(keys, "литеральные ключи в коде");
    }

    [Test]
    public void LocalizedTextKeysInScenesAndPrefabs_ExistInTable()
    {
        var regex = new Regex(@"^\s*locKey:\s*(\S.*?)\s*$", RegexOptions.Multiline);
        var keys = new List<string>();
        foreach (string file in Files("*.unity").Concat(Files("*.prefab")))
            foreach (Match m in regex.Matches(File.ReadAllText(file)))
                keys.Add(m.Groups[1].Value);
        Assert.Greater(keys.Count, 0);
        AssertKeysExist(keys, "locKey в сцене и префабах");
    }

    [Test]
    public void DynamicKeys_StatusEffects()
    {
        var keys = new List<string>();
        foreach (StatusEffectType t in Enum.GetValues(typeof(StatusEffectType)))
        {
            keys.Add("effect.genitive." + t); // CardTextHelpers.GetGenitiveName
            keys.Add("effect.name." + t);     // CombatantStatusDisplay.Join
        }
        AssertKeysExist(keys, "эффекты");
    }

    [Test]
    public void DynamicKeys_CardActionsAndEffects()
    {
        var keys = new List<string>();
        foreach (InstantActionKind kind in Enum.GetValues(typeof(InstantActionKind)))
            foreach (EffectTargetTag tag in Enum.GetValues(typeof(EffectTargetTag)))
                keys.Add("card.action." + kind + "." + InstantActionEntry.TagGroup(tag)); // InstantActionEntry.Describe
        foreach (EffectTargetTag tag in Enum.GetValues(typeof(EffectTargetTag)))
        {
            keys.Add("card.effect.positive." + InstantActionEntry.TagGroup(tag)); // AppliedEffectEntry.Describe
            keys.Add("card.effect.negative." + InstantActionEntry.TagGroup(tag));
        }
        AssertKeysExist(keys, "действия и эффекты карт");
    }

    [Test]
    public void DynamicKeys_EnemyTelegraph()
    {
        var keys = new List<string>();
        foreach (EnemyActionTarget target in Enum.GetValues(typeof(EnemyActionTarget)))
        {
            foreach (InstantActionKind kind in Enum.GetValues(typeof(InstantActionKind)))
                keys.Add("combat.action." + kind + "." + target); // CombatantStatusDisplay.DescribeInstantAction
            keys.Add("combat.effect.Give." + target);              // CombatantStatusDisplay.DescribeAppliedEffect
            keys.Add("combat.effect.Apply." + target);
        }
        AssertKeysExist(keys, "телеграф врага");
    }

    [Test]
    public void DynamicKeys_RoomTypes()
    {
        var keys = new List<string>();
        foreach (RoomType type in Enum.GetValues(typeof(RoomType)))
            if (!new MapNode { type = type }.IsCombat)
                keys.Add("map.room." + type); // MapScreen.CreateNode
        AssertKeysExist(keys, "типы комнат");
    }

    [Test]
    public void DynamicKeys_DeckPileLabels()
    {
        var keys = new List<string>();
        foreach (PileKind pile in Enum.GetValues(typeof(PileKind)))
            keys.Add("ui.deck.count." + pile); // DeckPileCountDisplay.Update
        AssertKeysExist(keys, "подписи стопок");
    }

    [Test]
    public void AllCardDataAssets_HaveNameKeyInTable()
    {
        var keys = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:CardData"))
        {
            var card = AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
            Assert.IsFalse(string.IsNullOrEmpty(card.nameKey), card.name + ": пустой nameKey");
            keys.Add(card.nameKey);
        }
        Assert.Greater(keys.Count, 0);
        AssertKeysExist(keys, "названия карт");
    }

    [Test]
    public void AllEnemyPatternSteps_HaveNameKeyInTable()
    {
        var keys = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:EnemyPatternData"))
        {
            var pattern = AssetDatabase.LoadAssetAtPath<EnemyPatternData>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var step in pattern.steps)
            {
                Assert.IsFalse(string.IsNullOrEmpty(step.stepNameKey), pattern.name + ": пустой stepNameKey");
                keys.Add(step.stepNameKey);
                if (step.hasFallback)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(step.fallback.stepNameKey), pattern.name + ": пустой stepNameKey запасного шага");
                    keys.Add(step.fallback.stepNameKey);
                }
            }
        }
        Assert.Greater(keys.Count, 0);
        AssertKeysExist(keys, "шаги врагов");
    }
}
