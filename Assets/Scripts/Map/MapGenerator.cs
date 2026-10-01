using System;
using System.Collections.Generic;
using System.Text;

public static class MapGenerator
{
    const int Rows = 20, Cols = 7;
    static readonly int[] CampfireRows = { 5, 12, 19 };
    const int ChestRow = 10;

    // FNV-1a, 32 бита. string.GetHashCode() рандомизирован между запусками — не годится.
    public static int Fnv1a(string s)
    {
        uint h = 2166136261;
        foreach (byte b in Encoding.UTF8.GetBytes(s)) { h ^= b; h *= 16777619; }
        return unchecked((int)h);
    }

    static void Shuffle<T>(List<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public static MapData Generate(string seed, MapGenerationConfig config)
    {
        string error = config.Validate();
        if (error != null) throw new InvalidOperationException(error + " (сид " + seed + ")");

        var sizesRng = new Random(Fnv1a(seed + ":sizes"));
        var colsRng = new Random(Fnv1a(seed + ":columns"));
        var edgesRng = new Random(Fnv1a(seed + ":edges"));
        var typesRng = new Random(Fnv1a(seed + ":types"));
        var encRng = new Random(Fnv1a(seed + ":encounters"));

        // Шаги 1–3: размеры строк.
        var size = new int[Rows + 1];
        var fixedRows = new List<int>(CampfireRows) { ChestRow };
        fixedRows.Sort();
        foreach (int r in fixedRows) size[r] = sizesRng.Next(5, 8);

        var freeRows = new List<int>();
        for (int r = 2; r < Rows; r++) if (!fixedRows.Contains(r)) freeRows.Add(r);
        int k = sizesRng.Next(config.minFiveRows, config.maxFiveRows + 1);
        Shuffle(freeRows, sizesRng);
        for (int i = 0; i < freeRows.Count; i++)
            size[freeRows[i]] = i < k ? 5 : i < 2 * k ? 7 : 6;
        freeRows.Sort();

        // Шаг 4: занятые столбцы.
        var map = new MapData { seed = seed };
        var rowNodes = new List<MapNode>[Rows + 1];
        for (int r = 1; r <= Rows; r++)
        {
            rowNodes[r] = new List<MapNode>();
            var cols = (r == 1 || r == Rows) ? new List<int> { 4 } : PickColumns(size[r], colsRng);
            foreach (int c in cols)
            {
                var node = new MapNode { id = map.nodes.Count, row = r, col = c };
                map.nodes.Add(node);
                rowNodes[r].Add(node);
            }
        }

        BuildEdges(map, rowNodes, edgesRng, config.extraEdgeChance);
        AssignTypes(rowNodes, freeRows, typesRng, config, seed);
        AssignEncounters(map, encRng, config);
        return map;
    }

    // Перебор масок пустых столбцов (бит c-1 = столбец c пуст) без {1,2} и {6,7} целиком; выбор случайной.
    static List<int> PickColumns(int n, Random rng)
    {
        var options = new List<int>();
        for (int mask = 0; mask < 1 << Cols; mask++)
        {
            if (PopCount(mask) != Cols - n) continue;
            if ((mask & 0b0000011) == 0b0000011) continue;
            if ((mask & 0b1100000) == 0b1100000) continue;
            options.Add(mask);
        }
        int chosen = options[rng.Next(options.Count)];
        var cols = new List<int>();
        for (int c = 1; c <= Cols; c++) if ((chosen & (1 << (c - 1))) == 0) cols.Add(c);
        return cols;
    }

    static int PopCount(int v) { int n = 0; for (; v != 0; v &= v - 1) n++; return n; }

    static void BuildEdges(MapData map, List<MapNode>[] rowNodes, Random rng, float extraChance)
    {
        var seen = new HashSet<long>();
        void Add(MapNode u, MapNode v)
        {
            if (seen.Add((long)u.id * 100000 + v.id))
                map.edges.Add(new MapEdge { fromId = u.id, toId = v.id });
        }

        foreach (var v in rowNodes[2]) Add(rowNodes[1][0], v);
        foreach (var u in rowNodes[Rows - 1]) Add(u, rowNodes[Rows][0]);

        for (int r = 2; r <= Rows - 2; r++)
        {
            var cand = new List<(MapNode u, MapNode v)>();
            foreach (var u in rowNodes[r])
                foreach (var v in rowNodes[r + 1])
                    if (Math.Abs(u.col - v.col) <= 1) cand.Add((u, v));

            foreach (var u in rowNodes[r])
            {
                var mine = cand.FindAll(p => p.u == u);
                var pick = mine[rng.Next(mine.Count)];
                Add(pick.u, pick.v);
            }
            foreach (var v in rowNodes[r + 1])
            {
                if (map.edges.Exists(e => e.toId == v.id)) continue;
                var mine = cand.FindAll(p => p.v == v);
                var pick = mine[rng.Next(mine.Count)];
                Add(pick.u, pick.v);
            }
            foreach (var p in cand)
                if (!seen.Contains((long)p.u.id * 100000 + p.v.id) && rng.NextDouble() < extraChance)
                    Add(p.u, p.v);
        }
    }

    static void AssignTypes(List<MapNode>[] rowNodes, List<int> freeRows, Random rng,
                            MapGenerationConfig config, string seed)
    {
        rowNodes[1][0].type = RoomType.Start;
        rowNodes[Rows][0].type = RoomType.Boss;
        foreach (int r in CampfireRows) foreach (var n in rowNodes[r]) n.type = RoomType.Campfire;
        foreach (var n in rowNodes[ChestRow]) n.type = RoomType.Chest;

        var pool = new List<(RoomType type, int minRow)>();
        foreach (var q in config.quotas)
            for (int i = 0; i < q.count; i++) pool.Add((q.roomType, q.minRow));
        Shuffle(pool, rng);

        // Выполнимость: для любой строки t элементов с minRow >= t не больше, чем свободных комнат в строках >= t.
        // Проверяем все t, а не только свободные: minRow на несвободной строке (Elite/5) иначе не учитывается.
        for (int t = 1; t <= Rows; t++)
        {
            int need = 0, have = 0;
            var byType = new Dictionary<RoomType, int>();
            foreach (var p in pool)
                if (p.minRow >= t) { need++; byType[p.type] = byType.TryGetValue(p.type, out int c) ? c + 1 : 1; }
            foreach (int r in freeRows) if (r >= t) have += rowNodes[r].Count;
            if (need > have)
            {
                var sb = new StringBuilder();
                foreach (var kv in byType) sb.Append(kv.Key + " x" + kv.Value + " ");
                throw new InvalidOperationException("Карта невыполнима (сид " + seed + "): с minRow >= " + t +
                    " нужно " + need + " комнат (" + sb.ToString().Trim() + "), а свободных в строках >= " + t + " только " + have);
            }
        }

        foreach (int r in freeRows)
            foreach (var n in rowNodes[r])
            {
                int idx = pool.FindIndex(p => p.minRow <= r);
                n.type = pool[idx].type;
                pool.RemoveAt(idx);
            }
    }

    static void AssignEncounters(MapData map, Random rng, MapGenerationConfig config)
    {
        foreach (var n in map.nodes)
        {
            List<EncounterData> list = n.type switch
            {
                RoomType.Start or RoomType.Enemy => config.normalEncounters,
                RoomType.Elite => config.eliteEncounters,
                RoomType.Boss => config.bossEncounters,
                _ => null
            };
            if (list != null) n.encounter = list[rng.Next(list.Count)];
        }
    }
}
