using System.Collections.Generic;
using UnityEngine;

// Экран: активен ровно один. Оверлей: открывается поверх экрана, не выключая его; закрывается верхний.
// Новый экран = компонент ScreenRoot с уникальным id на корневом объекте экрана, менеджер править не нужно.
public class ScreenManager : MonoBehaviour
{
    public const string Map = "Map";
    public const string Combat = "Combat";

    public static ScreenManager Instance { get; private set; }

    private readonly Dictionary<string, GameObject> screens = new Dictionary<string, GameObject>();
    private readonly List<GameObject> overlays = new List<GameObject>(); // последний = верхний

    public string CurrentScreenId { get; private set; }
    public bool HasOverlay => overlays.Count > 0;
    public GameObject TopOverlay => overlays.Count > 0 ? overlays[overlays.Count - 1] : null;

    void Awake()
    {
        Instance = this;
        foreach (var root in FindObjectsByType<ScreenRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Register(root.screenId, root.gameObject);
            root.gameObject.SetActive(false); // до первого ShowScreen не активен ни один
        }
    }

    public void Register(string id, GameObject root)
    {
        if (screens.ContainsKey(id))
        {
            Debug.LogError("ScreenManager: экран " + id + " уже зарегистрирован", root);
            return;
        }
        screens[id] = root;
    }

    public void ShowScreen(string id)
    {
        if (!screens.TryGetValue(id, out var target))
        {
            Debug.LogError("ScreenManager: неизвестный экран " + id);
            return;
        }
        CloseAllOverlays();
        foreach (var kv in screens)
            if (kv.Value != target) kv.Value.SetActive(false);
        target.SetActive(true);
        CurrentScreenId = id;
    }

    public bool IsOverlayOpen(GameObject root) => overlays.Contains(root);

    public void OpenOverlay(GameObject root)
    {
        if (overlays.Contains(root)) return;
        overlays.Add(root);
        root.SetActive(true);
    }

    public void CloseOverlay(GameObject root)
    {
        if (!overlays.Remove(root)) return;
        root.SetActive(false);
    }

    public void CloseTopOverlay()
    {
        if (overlays.Count > 0) CloseOverlay(overlays[overlays.Count - 1]);
    }

    public void ToggleOverlay(GameObject root)
    {
        if (overlays.Contains(root)) CloseOverlay(root);
        else OpenOverlay(root);
    }

    private void CloseAllOverlays()
    {
        while (overlays.Count > 0) CloseTopOverlay();
    }
}
