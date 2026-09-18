using System.Collections.Generic;
using UnityEngine;

public class EncounterManager : MonoBehaviour
{
    public List<EncounterData> encounters;
    public GameObject playerPrefab;
    public RectTransform playerSpawnPoint;
    public RectTransform enemiesArea;
    public CombatManager combatManager;
    public DeckManager deckManager;
    public HandManager handManager;

    private int currentEncounterIndex = -1;
    private Player currentPlayer;
    private List<Enemy> currentEnemies = new List<Enemy>();

    void Start()
    {
        combatManager.OnVictory += HandleVictory;
        StartNextEncounter();
    }

    private void HandleVictory()
    {
        PlayerRunState.PersistedHP = currentPlayer.CurrentHP;
        CleanupCombatants();
        StartNextEncounter();
    }

    private void CleanupCombatants()
    {
        if (currentPlayer != null) Destroy(currentPlayer.gameObject);
        foreach (var e in currentEnemies)
            if (e != null) Destroy(e.gameObject);
        currentEnemies.Clear();
    }

    private void StartNextEncounter()
    {
        currentEncounterIndex = (currentEncounterIndex + 1) % encounters.Count;
        EncounterData encounter = encounters[currentEncounterIndex];

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
            enemy.player = currentPlayer;
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
