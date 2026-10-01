using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class MapGeneratorTests
{
    const int SeedCount = 1000;
    static readonly int[] FixedRows = { 1, 5, 10, 12, 19, 20 };

    MapGenerationConfig config;
    EncounterData normalA, normalB, elite, boss;
    List<MapData> maps;

    static MapGenerationConfig MakeConfig(out EncounterData nA, out EncounterData nB, out EncounterData el, out EncounterData bo)
    {
        nA = ScriptableObject.CreateInstance<EncounterData>();
        nB = ScriptableObject.CreateInstance<EncounterData>();
        el = ScriptableObject.CreateInstance<EncounterData>();
        bo = ScriptableObject.CreateInstance<EncounterData>();
        var c = ScriptableObject.CreateInstance<MapGenerationConfig>();
        void Q(RoomType t, int n, int m) => c.quotas.Add(new RoomQuota { roomType = t, count = n, minRow = m });
        Q(RoomType.Enemy, 33, 2); Q(RoomType.Elite, 21, 5); Q(RoomType.Event, 16, 2);
        Q(RoomType.CardMerchant, 2, 2); Q(RoomType.EnchantMerchant, 2, 2); Q(RoomType.RiskyEvent, 10, 2);
        c.normalEncounters.Add(nA); c.normalEncounters.Add(nB);
        c.eliteEncounters.Add(el); c.bossEncounters.Add(bo);
        return c;
    }

    [OneTimeSetUp]
    public void Setup()
    {
        config = MakeConfig(out normalA, out normalB, out elite, out boss);
        maps = Enumerable.Range(0, SeedCount).Select(i => MapGenerator.Generate("seed" + i, config)).ToList();
    }

    static List<MapNode> Row(MapData m, int r) => m.nodes.Where(n => n.row == r).ToList();
    static List<MapNode> FreeNodes(MapData m) => m.nodes.Where(n => !FixedRows.Contains(n.row)).ToList();
    void ForAll(Action<MapData> check) { foreach (var m in maps) check(m); }

    [Test]
    public void RowSizes()
    {
        ForAll(m =>
        {
            Assert.AreEqual(1, Row(m, 1).Count, m.seed);
            Assert.AreEqual(RoomType.Start, Row(m, 1)[0].type, m.seed);
            Assert.AreEqual(1, Row(m, 20).Count, m.seed);
            Assert.AreEqual(RoomType.Boss, Row(m, 20)[0].type, m.seed);
            for (int r = 2; r <= 19; r++)
                Assert.That(Row(m, r).Count, Is.InRange(5, 7), m.seed + " row " + r);
        });
    }

    [Test]
    public void FreeRowsHaveExactly84Nodes()
    {
        ForAll(m => Assert.AreEqual(84, FreeNodes(m).Count, m.seed));
    }

    [Test]
    public void NoForbiddenEmptyPairs()
    {
        ForAll(m =>
        {
            for (int r = 2; r <= 19; r++)
            {
                var cols = Row(m, r).Select(n => n.col).ToList();
                Assert.IsFalse(!cols.Contains(1) && !cols.Contains(2), m.seed + " row " + r + " {1,2} empty");
                Assert.IsFalse(!cols.Contains(6) && !cols.Contains(7), m.seed + " row " + r + " {6,7} empty");
            }
        });
    }

    [Test]
    public void StartAndBossConnections()
    {
        ForAll(m =>
        {
            int start = Row(m, 1)[0].id, bossId = Row(m, 20)[0].id;
            foreach (var n in Row(m, 2)) Assert.IsTrue(m.edges.Any(e => e.fromId == start && e.toId == n.id), m.seed);
            foreach (var n in Row(m, 19)) Assert.IsTrue(m.edges.Any(e => e.fromId == n.id && e.toId == bossId), m.seed);
        });
    }

    [Test]
    public void MiddleEdgesGoToNextRowWithinOneColumn()
    {
        ForAll(m =>
        {
            foreach (var e in m.edges)
            {
                var a = m.Node(e.fromId); var b = m.Node(e.toId);
                if (a.row < 2 || b.row > 19) continue;
                Assert.AreEqual(a.row + 1, b.row, m.seed);
                Assert.LessOrEqual(Math.Abs(a.col - b.col), 1, m.seed);
            }
        });
    }

    [Test]
    public void DegreesInRange()
    {
        ForAll(m =>
        {
            foreach (var n in m.nodes.Where(n => n.row >= 2 && n.row <= 19))
                Assert.That(m.edges.Count(e => e.fromId == n.id), Is.InRange(1, 3), m.seed + " out " + n.id);
            foreach (var n in m.nodes.Where(n => n.row >= 2))
                Assert.GreaterOrEqual(m.edges.Count(e => e.toId == n.id), 1, m.seed + " in " + n.id);
            Assert.AreEqual(m.edges.Count, m.edges.Select(e => (e.fromId, e.toId)).Distinct().Count(), m.seed + " duplicates");
        });
    }

    [Test]
    public void EveryNodeReachableFromStartAndReachesBoss()
    {
        ForAll(m =>
        {
            var fwd = Reach(m, Row(m, 1)[0].id, e => e.fromId, e => e.toId);
            var back = Reach(m, Row(m, 20)[0].id, e => e.toId, e => e.fromId);
            Assert.AreEqual(m.nodes.Count, fwd.Count, m.seed + " from start");
            Assert.AreEqual(m.nodes.Count, back.Count, m.seed + " to boss");
        });
    }

    static HashSet<int> Reach(MapData m, int from, Func<MapEdge, int> src, Func<MapEdge, int> dst)
    {
        var seen = new HashSet<int> { from };
        var q = new Queue<int>(); q.Enqueue(from);
        while (q.Count > 0)
        {
            int cur = q.Dequeue();
            foreach (var e in m.edges)
                if (src(e) == cur && seen.Add(dst(e))) q.Enqueue(dst(e));
        }
        return seen;
    }

    [Test]
    public void CampfireAndChestRows()
    {
        ForAll(m =>
        {
            foreach (int r in new[] { 5, 12, 19 })
                Assert.IsTrue(Row(m, r).All(n => n.type == RoomType.Campfire), m.seed + " row " + r);
            Assert.IsTrue(Row(m, 10).All(n => n.type == RoomType.Chest), m.seed);
        });
    }

    [Test]
    public void FreeRoomTypeCountsMatchQuotas()
    {
        ForAll(m =>
        {
            var free = FreeNodes(m);
            foreach (var q in config.quotas)
                Assert.AreEqual(q.count, free.Count(n => n.type == q.roomType), m.seed + " " + q.roomType);
        });
    }

    [Test]
    public void NoTypeAboveItsMinRow()
    {
        ForAll(m =>
        {
            foreach (var q in config.quotas)
                Assert.IsFalse(m.nodes.Any(n => n.type == q.roomType && n.row < q.minRow), m.seed + " " + q.roomType);
            Assert.IsFalse(m.nodes.Any(n => n.type == RoomType.Elite && n.row <= 4), m.seed);
        });
    }

    [Test]
    public void EncountersMatchNodeType()
    {
        ForAll(m =>
        {
            foreach (var n in m.nodes)
            {
                switch (n.type)
                {
                    case RoomType.Start: case RoomType.Enemy:
                        Assert.That(n.encounter == normalA || n.encounter == normalB, m.seed + " " + n.type); break;
                    case RoomType.Elite: Assert.AreSame(elite, n.encounter, m.seed); break;
                    case RoomType.Boss: Assert.AreSame(boss, n.encounter, m.seed); break;
                    default: Assert.IsNull(n.encounter, m.seed + " " + n.type); break;
                }
            }
        });
    }

    static string Signature(MapData m) =>
        string.Join(";", m.nodes.Select(n => n.row + "," + n.col + "," + n.type + "," + n.encounter?.GetInstanceID())) + "|" +
        string.Join(";", m.edges.Select(e => e.fromId + ">" + e.toId));

    [Test]
    public void SameSeedIsDeterministicDifferentSeedsDiffer()
    {
        Assert.AreEqual(Signature(MapGenerator.Generate("abc", config)), Signature(MapGenerator.Generate("abc", config)));
        int same = 0;
        for (int i = 0; i + 1 < maps.Count; i++)
            if (Signature(maps[i]) == Signature(maps[i + 1])) same++;
        Assert.AreEqual(0, same, "одинаковых карт у соседних сидов");
    }

    [Test]
    public void QuotaSumNot84Throws()
    {
        var c = MakeConfig(out _, out _, out _, out _);
        c.quotas[0].count = 34;
        var ex = Assert.Throws<InvalidOperationException>(() => MapGenerator.Generate("x", c));
        StringAssert.Contains("85", ex.Message);
    }

    [Test]
    public void InfeasibleMinRowThrowsWithDetails()
    {
        var c = MakeConfig(out _, out _, out _, out _);
        c.quotas.Clear();
        c.quotas.Add(new RoomQuota { roomType = RoomType.Elite, count = 80, minRow = 5 });
        c.quotas.Add(new RoomQuota { roomType = RoomType.Enemy, count = 4, minRow = 2 });
        var ex = Assert.Throws<InvalidOperationException>(() => MapGenerator.Generate("x", c));
        StringAssert.Contains("Elite", ex.Message);
        StringAssert.Contains("сид x", ex.Message);
    }

    [Test]
    public void NavigationStartsAtStartThenOnlySuccessors()
    {
        var m = maps[0];
        var run = new MapRunState();
        var first = run.GetSelectableNodes(m);
        Assert.AreEqual(1, first.Count);
        Assert.AreEqual(RoomType.Start, first[0].type);

        Assert.IsFalse(run.TrySelect(m, Row(m, 2)[0].id)); // не Start
        Assert.IsNull(run.CurrentNodeId);
        Assert.AreEqual(0, run.Visited.Count);

        Assert.IsTrue(run.TrySelect(m, first[0].id));
        var expected = m.edges.Where(e => e.fromId == first[0].id).Select(e => e.toId).OrderBy(x => x).ToList();
        CollectionAssert.AreEqual(expected, run.GetSelectableNodes(m).Select(n => n.id).OrderBy(x => x).ToList());

        var notSuccessor = Row(m, 4)[0].id;
        Assert.IsFalse(run.TrySelect(m, notSuccessor));
        Assert.AreEqual(first[0].id, run.CurrentNodeId);
        Assert.AreEqual(1, run.Visited.Count);
    }
}
