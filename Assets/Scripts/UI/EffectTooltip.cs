using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Одна общая плашка с описанием эффекта: показывает EffectLabelHover, пока курсор над ссылкой эффекта
public class EffectTooltip : MonoBehaviour
{
    public static EffectTooltip Instance { get; private set; }

    public GameObject panel; // сама плашка (pivot 0,0); корень остаётся активным, чтобы отработал Awake
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
        panel.SetActive(true);
        var rt = (RectTransform)panel.transform;
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt); // размер по новому тексту нужен до расчёта позиции
        Vector2 size = Vector2.Scale(rt.rect.size, rt.lossyScale); // пиксели экрана с учётом масштаба Canvas

        // По умолчанию — справа сверху от курсора; не помещается справа — слева, не помещается сверху — под курсором
        Vector2 pos = screenPos + offset;
        if (pos.x + size.x > Screen.width) pos.x = screenPos.x - offset.x - size.x;
        if (pos.y + size.y > Screen.height) pos.y = screenPos.y - offset.y - size.y;
        pos.x = Mathf.Clamp(pos.x, 0, Mathf.Max(0, Screen.width - size.x)); // плашка шире/выше свободного места — прижимаем к краю
        pos.y = Mathf.Clamp(pos.y, 0, Mathf.Max(0, Screen.height - size.y));
        panel.transform.position = pos;
    }

    public void Hide() => panel.SetActive(false);
}
