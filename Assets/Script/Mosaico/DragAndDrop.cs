using UnityEngine;
using UnityEngine.EventSystems;

public class DragAndDrop : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int tesseraID;
    public bool IsLocked = false;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Transform parentOriginale;
    private Vector3 posizioneIniziale; // Salva la posizione di partenza
    private Canvas canvas;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();
    }

    private void Start()
    {
        // Salva il parent e la posizione iniziale esatta al caricamento della scena
        parentOriginale = transform.parent;
        posizioneIniziale = rectTransform.localPosition;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsLocked) return;

        // Porta la tessera in cima al Canvas per visualizzarla sopra tutto durante il drag
        transform.SetParent(canvas.transform);
        transform.SetAsLastSibling();

        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (IsLocked) return;

        // Movimento fluido del mouse
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        // Se è stata incastrata nello slot corretto (gestito da MosaicSlot)
        if (IsLocked)
        {
            transform.SetAsLastSibling();
            return;
        }

        // SE RILASCIATA A VUOTO: Torna nel genitore originale e alla SUA posizione iniziale esatta
        transform.SetParent(parentOriginale);
        rectTransform.localPosition = posizioneIniziale;
    }
}