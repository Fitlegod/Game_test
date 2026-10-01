using System.Collections.Generic;

public class MapNode
{
    public int id;
    public int row; // 1..20
    public int col; // 1..7 (у Start и Boss 4, только для отрисовки)
    public RoomType type;
    public EncounterData encounter; // только у боевых узлов

    public bool IsCombat => type == RoomType.Start || type == RoomType.Enemy || type == RoomType.Elite || type == RoomType.Boss;
}

public class MapEdge
{
    public int fromId;
    public int toId;
}

public class MapData
{
    public string seed;
    public List<MapNode> nodes = new List<MapNode>();
    public List<MapEdge> edges = new List<MapEdge>();

    // id узла == индекс в nodes (генератор нумерует подряд).
    public MapNode Node(int id) => nodes[id];
}
