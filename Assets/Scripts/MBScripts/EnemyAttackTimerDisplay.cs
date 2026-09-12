using UnityEngine;
using TMPro;

public class EnemyAttackTimerDisplay : MonoBehaviour
{
    public Enemy enemy;
    public CombatManager combatManager;
    public TMP_Text label;

    void Update()
    {
        float remaining = enemy.scheduledHitTime - combatManager.CurrentTime;
        label.text = "Атака через: " + Mathf.Max(0, remaining).ToString("0.0") + " с";
    }
}