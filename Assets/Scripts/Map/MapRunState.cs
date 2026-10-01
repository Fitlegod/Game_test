using System.Collections.Generic;

public class MapRunState
{
    public int? CurrentNodeId;
    public HashSet<int> Visited = new HashSet<int>();
    public bool IsFinished; // выставляет MapManager: победа над Boss

    public List<MapNode> GetSelectableNodes(MapData map)
    {
        var result = new List<MapNode>();
        if (IsFinished) return result;
        if (CurrentNodeId == null)
        {
            result.Add(map.nodes.Find(n => n.type == RoomType.Start));
            return result;
        }
        foreach (var e in map.edges)
            if (e.fromId == CurrentNodeId.Value) result.Add(map.Node(e.toId));
        return result;
    }

    public bool TrySelect(MapData map, int nodeId)
    {
        if (!GetSelectableNodes(map).Exists(n => n.id == nodeId)) return false;
        CurrentNodeId = nodeId;
        Visited.Add(nodeId);
        return true;
    }
}
