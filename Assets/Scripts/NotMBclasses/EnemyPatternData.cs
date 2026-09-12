using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyPattern", menuName = "Enemies/Enemy Pattern Data")]
public class EnemyPatternData : ScriptableObject
{
    public List<EnemyActionStep> steps = new List<EnemyActionStep>();
}
