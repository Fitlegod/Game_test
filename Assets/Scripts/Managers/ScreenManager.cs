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

    private readonly List<GameObject> registeredOverlays = new List<GameObject>();

    public string CurrentScreenId { get; private set; }
    public bool HasOverlay { get { PruneHidden(); return overlays.Count > 0; } }
    public GameObject TopOverlay { get { PruneHidden(); return overlays.Count > 0 ? overlays[overlays.Count - 1] : null; } }

    void Awake()
    {
        Instance = this;
        foreach (var root in FindObjectsByType<ScreenRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Register(root.screenId, root.gameObject);
            root.gameObject.SetActive(false); // до первого ShowScreen не активен ни один
        }
        foreach (var overlay in FindObjectsByType<OverlayRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (overlay.GetComponent<ScreenRoot>() != null)
                Debug.LogError("ScreenManager: " + overlay.name + " одновременно ScreenRoot и OverlayRoot — это противоречивые роли", overlay);
            registeredOverlays.Add(overlay.gameObject);
            overlay.gameObject.SetActive(false); // оверлей открывается только явным OpenOverlay
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
        foreach (var overlay in registeredOverlays) overlay.SetActive(false);
        foreach (var kv in screens)
            if (kv.Value != target) kv.Value.SetActive(false);
        target.SetActive(true);
        CurrentScreenId = id;
    }

    public bool IsOverlayOpen(GameObject root) { PruneHidden(); return overlays.Contains(root); }

    public void OpenOverlay(GameObject root)
    {
        PruneHidden();
        if (overlays.Contains(root)) return;
        overlays.Add(root);
        root.transform.SetAsLastSibling(); // порядок отрисовки = порядок открытия
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
        if (IsOverlayOpen(root)) CloseOverlay(root);
        else OpenOverlay(root);
    }

    // «Открыт» не должен расходиться с реальной видимостью: если объект кто-то выключил в обход менеджера, забываем его.
    private void PruneHidden()
    {
        overlays.RemoveAll(o => o == null || !o.activeSelf);
    }

    private void CloseAllOverlays()
    {
        while (overlays.Count > 0) CloseTopOverlay();
    }
}
