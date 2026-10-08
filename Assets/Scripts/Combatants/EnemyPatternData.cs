using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyPattern", menuName = "Enemies/Enemy Pattern Data")]
public class EnemyPatternData : ScriptableObject
{
    public List<EnemyActionStep> steps = new List<EnemyActionStep>();

    void OnValidate()
    {
        foreach (var step in steps)
        {
            if (!Loc.Has(step.stepNameKey))
                Debug.LogWarning(name + ": нет строки для ключа шага [" + step.stepNameKey + "]", this);
            if (step.hasFallback && !Loc.Has(step.fallback.stepNameKey))
                Debug.LogWarning(name + ": нет строки для ключа запасного шага [" + step.fallback.stepNameKey + "]", this);
        }
    }
}
