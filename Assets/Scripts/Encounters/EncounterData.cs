using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEncounter", menuName = "Encounters/Encounter Data")]
public class EncounterData : ScriptableObject
{
    public List<GameObject> enemyPrefabs = new List<GameObject>();
}
