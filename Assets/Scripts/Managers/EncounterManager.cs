using System.Collections.Generic;
using UnityEngine;

public class EncounterManager : MonoBehaviour
{
    public GameObject playerPrefab;
    public RectTransform playerSpawnPoint;
    public RectTransform enemiesArea;
    public CombatManager combatManager;
    public DeckManager deckManager;
    public HandManager handManager;
    public MapManager mapManager;

    private Player currentPlayer;
    private List<Enemy> currentEnemies = new List<Enemy>();

    void Start()
    {
        combatManager.OnVictory += HandleVictory;
        combatManager.OnDefeat += HandleDefeat;
    }

    private void HandleVictory()
    {
        PlayerRunState.PersistedHP = currentPlayer.CurrentHP;
        CleanupCombatants();
        mapManager.OnCombatEnded(true);
    }

    private void HandleDefeat()
    {
        PlayerRunState.PersistedHP = null; // следующий бой — с полным HP
        CleanupCombatants();
        mapManager.OnCombatEnded(false);
    }

    private void CleanupCombatants()
    {
        if (currentPlayer != null) Destroy(currentPlayer.gameObject);
        foreach (var e in currentEnemies)
            if (e != null) Destroy(e.gameObject);
        currentEnemies.Clear();
    }

    public void StartEncounter(EncounterData encounter)
    {
        combatManager.ResetForNewCombat();

        SpawnPlayer();
        SpawnEnemies(encounter);

        handManager.BeginNewHand();
    }

    private void SpawnPlayer()
    {
        GameObject obj = Instantiate(playerPrefab, playerSpawnPoint);
        Player player = obj.GetComponent<Player>();
        player.combatManager = combatManager;
        player.Init();
        if (PlayerRunState.PersistedHP.HasValue)
            player.SetCurrentHP(PlayerRunState.PersistedHP.Value);
        combatManager.SetPlayer(player);
        currentPlayer = player;
    }

    private void SpawnEnemies(EncounterData encounter)
    {
        List<Enemy> spawned = new List<Enemy>();
        float spacing = enemiesArea.rect.width / (encounter.enemyPrefabs.Count + 1);

        for (int i = 0; i < encounter.enemyPrefabs.Count; i++)
        {
            GameObject obj = Instantiate(encounter.enemyPrefabs[i], enemiesArea);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(-enemiesArea.rect.width / 2 + spacing * (i + 1), 0);

            Enemy enemy = obj.GetComponent<Enemy>();
            enemy.combatManager = combatManager;
            enemy.Init();
            enemy.player = currentPlayer;
            enemy.ScheduleFirstAction();
            combatManager.RegisterScheduledEvent(enemy);
            enemy.OnDeath += () => combatManager.UnregisterScheduledEvent(enemy);

            var statusDisplay = obj.GetComponentInChildren<CombatantStatusDisplay>();
            if (statusDisplay != null)
                statusDisplay.combatManager = combatManager;

            spawned.Add(enemy);
        }

        currentEnemies = spawned;
        combatManager.RegisterEnemies(spawned);
    }
}
