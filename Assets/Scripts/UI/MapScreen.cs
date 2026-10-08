using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum MapScreenMode { Select, View, Finished }

public class MapScreen : MonoBehaviour
{
    const float ColSpacing = 130f, RowSpacing = 100f, Pad = 80f;

    public GameObject panelRoot;
    public ScrollRect scrollRect;
    public RectTransform content;
    public GameObject nodePrefab; // корень: Image + Button + Outline; дети: Icon (Image), Label (TMP_Text)
    public TMP_Text seedText;
    public TMP_Text statusText;
    public Button closeButton;
    public Sprite skullSprite;

    public Action<int> onNodeClicked;

    static readonly Color EnemyColor = new Color(0.75f, 0.3f, 0.3f);
    static readonly Color EliteColor = new Color(0.95f, 0.6f, 0.1f);
    static readonly Color BossColor = new Color(0.6f, 0.2f, 0.85f);
    static readonly Color NonCombatColor = new Color(0.5f, 0.5f, 0.5f);
    static readonly Color EdgeColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);

    void Awake()
    {
        closeButton.onClick.AddListener(Close);
    }

    public void Close() => ScreenManager.Instance.CloseOverlay(gameObject);

    public void Show(MapData map, MapRunState run, MapScreenMode mode)
    {
        panelRoot.SetActive(true);
        closeButton.gameObject.SetActive(mode == MapScreenMode.View);
        seedText.text = Loc.Format("map.seed", map.seed);
        statusText.text = mode switch
        {
            MapScreenMode.Select => Loc.Get("map.status.select"),
            MapScreenMode.View => Loc.Get("map.status.view"),
            _ => Loc.Get("map.status.finished")
        };

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);
            child.SetParent(null); // чтобы childCount сразу был точным, а не после конца кадра
            Destroy(child.gameObject);
        }

        float height = Pad * 2 + RowSpacing * 19;
        content.sizeDelta = new Vector2(0, height);

        // Рёбра первыми — значит, рисуются под узлами.
        foreach (var e in map.edges) DrawEdge(map.Node(e.fromId), map.Node(e.toId));

        var selectable = run.GetSelectableNodes(map);
        foreach (var node in map.nodes)
            CreateNode(node, run, mode == MapScreenMode.Select && selectable.Contains(node));

        FocusOnCurrent(map, run, height);
    }

    static Vector2 NodePosition(MapNode n) => new Vector2((n.col - 4) * ColSpacing, Pad + (n.row - 1) * RowSpacing);

    void DrawEdge(MapNode from, MapNode to)
    {
        Vector2 a = NodePosition(from), b = NodePosition(to);
        var go = new GameObject("Edge", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(content, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
        rt.sizeDelta = new Vector2((b - a).magnitude, 3f);
        rt.anchoredPosition = (a + b) / 2;
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
        var img = go.GetComponent<Image>();
        img.color = EdgeColor;
        img.raycastTarget = false;
    }

    void CreateNode(MapNode node, MapRunState run, bool clickable)
    {
        var go = Instantiate(nodePrefab, content);
        go.name = "Node_" + node.id;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);
        rt.anchoredPosition = NodePosition(node);

        var bg = go.GetComponent<Image>();
        var icon = go.transform.Find("Icon").GetComponent<Image>();
        var label = go.transform.Find("Label").GetComponent<TMP_Text>();
        var outline = go.GetComponent<Outline>();

        bg.color = node.type switch
        {
            RoomType.Boss => BossColor,
            RoomType.Elite => EliteColor,
            RoomType.Start or RoomType.Enemy => EnemyColor,
            _ => NonCombatColor
        };
        icon.gameObject.SetActive(node.IsCombat);
        icon.sprite = skullSprite;
        label.gameObject.SetActive(!node.IsCombat);
        label.text = node.IsCombat ? "" : Loc.Get("map.room." + node.type);

        bool current = run.CurrentNodeId == node.id;
        bool visited = run.Visited.Contains(node.id);
        if (current) { outline.effectColor = Color.yellow; outline.effectDistance = new Vector2(6, 6); }
        else if (visited) { outline.effectColor = Color.green; outline.effectDistance = new Vector2(3, 3); }
        else if (clickable) { outline.effectColor = Color.white; outline.effectDistance = new Vector2(4, 4); }
        else outline.enabled = false;

        if (clickable) rt.localScale = Vector3.one * 1.15f;
        else if (!current && !visited) bg.color *= new Color(0.6f, 0.6f, 0.6f, 1f); // недоступные приглушены

        var button = go.GetComponent<Button>();
        button.interactable = clickable;
        int id = node.id;
        button.onClick.AddListener(() => onNodeClicked?.Invoke(id));
    }

    void FocusOnCurrent(MapData map, MapRunState run, float contentHeight)
    {
        Canvas.ForceUpdateCanvases();
        float view = scrollRect.viewport != null ? scrollRect.viewport.rect.height : ((RectTransform)scrollRect.transform).rect.height;
        float y = run.CurrentNodeId == null ? 0 : NodePosition(map.Node(run.CurrentNodeId.Value)).y + RowSpacing;
        float range = contentHeight - view;
        scrollRect.verticalNormalizedPosition = range <= 0 ? 0 : Mathf.Clamp01((y - view / 2) / range);
    }
}
