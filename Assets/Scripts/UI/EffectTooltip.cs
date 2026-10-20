using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

// Одна общая плашка с описанием эффекта: показывает ближайший EffectLabelHover, пока курсор над ссылкой эффекта
public class EffectTooltip : MonoBehaviour
{
    public static EffectTooltip Instance { get; private set; }

    public GameObject panel; // сама плашка; корень остаётся активным, чтобы отработал Awake
    public TMP_Text label;
    public Vector2 offset = new Vector2(16, 16);

    void Awake()
    {
        Instance = this;
        Hide();
    }

    public void Show(string type, Vector2 screenPos)
    {
        label.text = "<b>" + Loc.Get("effect.name." + type) + "</b>\n" + Loc.Get("effect.desc." + type);
        panel.transform.position = screenPos + offset;
        panel.SetActive(true);
    }

    public void Hide() => panel.SetActive(false);
}

// На объекте effectsLabel: ищет <link> под курсором (позиция из PointerEventData, не из Mouse.current)
public class EffectLabelHover : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler
{
    private TMP_Text text;

    public void OnPointerMove(PointerEventData e)
    {
        if (text == null) text = GetComponent<TMP_Text>();
        int i = TMP_TextUtilities.FindIntersectingLink(text, e.position, e.enterEventCamera);
        if (i < 0 || EffectTooltip.Instance == null) { OnPointerExit(e); return; }
        EffectTooltip.Instance.Show(text.textInfo.linkInfo[i].GetLinkID(), e.position);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (EffectTooltip.Instance != null) EffectTooltip.Instance.Hide();
    }
}
