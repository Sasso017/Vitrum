using UnityEngine;
using UnityEngine.EventSystems;

public class DragAndDrop : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int tesseraID;
    public bool IsLocked = false;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Transform parentOriginale;
    private Canvas canvas;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsLocked) return;

        parentOriginale = transform.parent;

        // 1. Sposta la tessera direttamente come figlia del Canvas e in fondo alla Hierarchy
        // Questo la porta in PRIMISSIMO piano visivo sopra a qualsiasi Slot o Panello
        transform.SetParent(canvas.transform);
        transform.SetAsLastSibling();

        canvasGroup.blocksRaycasts = false;
        // Zero patina: non tocchiamo l'alpha!
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (IsLocked) return;

        // Movimento fluido che segue il mouse
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        // Se la tessera è stata posizionata nello slot corretto (gestito da MosaicSlot)
        if (IsLocked)
        {
            // Rimane dentro lo slot e va davanti allo sfondo dello slot stesso
            transform.SetAsLastSibling();
            return;
        }

        // Se il rilascio è errato, torna nel suo contenitore originale
        transform.SetParent(parentOriginale);
        rectTransform.localPosition = Vector3.zero;
    }
}