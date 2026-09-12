using System.Collections.Generic;
using UnityEngine;

public class EncounterManager : MonoBehaviour
{
    public List<GameObject> enemyPrefabs; // один элемент списка — один враг в этом бою
    public RectTransform enemiesArea;
    public CombatManager combatManager;
    public Player player;

    void Start()
    {
        List<Enemy> spawned = new List<Enemy>();
        float spacing = enemiesArea.rect.width / (enemyPrefabs.Count + 1);

        for (int i = 0; i < enemyPrefabs.Count; i++)
        {
            GameObject obj = Instantiate(enemyPrefabs[i], enemiesArea);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(-enemiesArea.rect.width / 2 + spacing * (i + 1), 0);

            Enemy enemy = obj.GetComponent<Enemy>();
            enemy.combatManager = combatManager;
            enemy.player = player;
            combatManager.RegisterScheduledEvent(enemy);

            var timerDisplay = obj.GetComponentInChildren<EnemyAttackTimerDisplay>();
            if (timerDisplay != null)
                timerDisplay.combatManager = combatManager;

            spawned.Add(enemy);
        }

        combatManager.RegisterEnemies(spawned);
    }
}