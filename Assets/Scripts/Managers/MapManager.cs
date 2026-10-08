using UnityEngine;
using UnityEngine.UI;

public class MapManager : MonoBehaviour
{
    public MapGenerationConfig config;
    public string seed;
    public MapScreen mapScreen;
    public EncounterManager encounterManager;
    public Button viewMapButton; // HUD-кнопка «Карта» во время боя

    public MapData CurrentMap { get; private set; }
    public MapRunState RunState { get; private set; }

    private bool selecting;

    void Start()
    {
        if (string.IsNullOrEmpty(seed))
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            var rng = new System.Random();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 8; i++) sb.Append(chars[rng.Next(chars.Length)]);
            seed = sb.ToString();
            Debug.Log("MapManager: случайный сид " + seed);
        }

        PlayerRunState.PersistedHP = null; // новый забег
        CurrentMap = MapGenerator.Generate(seed, config);
        RunState = new MapRunState();
        mapScreen.onNodeClicked = OnNodeClicked;
        viewMapButton.onClick.AddListener(OpenView);
        OpenSelection();
    }

    public void OnNodeClicked(int nodeId)
    {
        if (!selecting) return;
        if (!RunState.TrySelect(CurrentMap, nodeId)) return;

        MapNode node = CurrentMap.Node(nodeId);
        if (node.IsCombat)
        {
            selecting = false;
            ScreenManager.Instance.ShowScreen(ScreenManager.Combat);
            encounterManager.StartEncounter(node.encounter);
        }
        else
        {
            OpenSelection(); // небоевой узел: игрок просто переместился
        }
    }

    public void OnCombatEnded(bool victory)
    {
        if (victory && CurrentMap.Node(RunState.CurrentNodeId.Value).type == RoomType.Boss)
        {
            RunState.IsFinished = true;
            selecting = false;
            ScreenManager.Instance.ShowScreen(ScreenManager.Map);
            mapScreen.Show(CurrentMap, RunState, MapScreenMode.Finished);
            return;
        }
        OpenSelection();
    }

    private void OpenSelection()
    {
        selecting = true;
        ScreenManager.Instance.ShowScreen(ScreenManager.Map);
        mapScreen.Show(CurrentMap, RunState, MapScreenMode.Select);
    }

    private void OpenView()
    {
        if (selecting || RunState.IsFinished) return;
        mapScreen.Show(CurrentMap, RunState, MapScreenMode.View);
        ScreenManager.Instance.OpenOverlay(mapScreen.gameObject);
    }
}
