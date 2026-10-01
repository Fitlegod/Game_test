using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RoomQuota
{
    public RoomType roomType;
    public int count;
    public int minRow;
}

[CreateAssetMenu(fileName = "MapGenerationConfig", menuName = "Map/Map Generation Config")]
public class MapGenerationConfig : ScriptableObject
{
    public const int FreeRoomCount = 84;

    public List<RoomQuota> quotas = new List<RoomQuota>();
    public float extraEdgeChance = 0.3f;
    public int minFiveRows = 4, maxFiveRows = 5;
    public List<EncounterData> normalEncounters = new List<EncounterData>();
    public List<EncounterData> eliteEncounters = new List<EncounterData>();
    public List<EncounterData> bossEncounters = new List<EncounterData>();

    public int QuotaSum
    {
        get { int s = 0; foreach (var q in quotas) s += q.count; return s; }
    }

    // Общая проверка для OnValidate (лог) и MapGenerator (исключение).
    public string Validate()
    {
        if (QuotaSum != FreeRoomCount)
            return "MapGenerationConfig: сумма квот = " + QuotaSum + ", должна быть " + FreeRoomCount;
        if (normalEncounters.Count == 0 || eliteEncounters.Count == 0 || bossEncounters.Count == 0)
            return "MapGenerationConfig: списки normalEncounters/eliteEncounters/bossEncounters не должны быть пустыми";
        return null;
    }

    void OnValidate()
    {
        string error = Validate();
        if (error != null) Debug.LogError(error, this);
    }
}
