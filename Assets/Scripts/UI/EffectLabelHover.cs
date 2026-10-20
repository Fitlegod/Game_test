using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

// На объекте effectsLabel (добавляется кодом в CombatantStatusDisplay.Start): ищет <link> под курсором (позиция из PointerEventData, не из Mouse.current)
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

    public void OnPointerExit(PointerEventData e) => HideTooltip();

    void OnDisable() => HideTooltip(); // экран боя выключили под курсором — плашка не должна остаться

    private static void HideTooltip()
    {
        if (EffectTooltip.Instance != null) EffectTooltip.Instance.Hide();
    }
}
