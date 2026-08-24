using UnityEngine;
using UnityEngine.EventSystems;

public class MosaicSlot : MonoBehaviour, IDropHandler
{
    public int slotID;
    public bool isOccupied = false;

    public void OnDrop(PointerEventData eventData)
    {
        DragAndDrop tessera = eventData.pointerDrag.GetComponent<DragAndDrop>();

        if (tessera != null && !isOccupied)
        {
            if (tessera.tesseraID == slotID)
            {
                // Incolla la tessera come figlia dello slot e la centra
                tessera.transform.SetParent(transform);
                tessera.transform.localPosition = Vector3.zero;

                tessera.IsLocked = true;
                isOccupied = true;

                // Controlla la vittoria dopo un piccolissimo ritardo (0.05 secondi)
                // in modo da far terminare prima l'evento OnEndDrag della tessera!
                Invoke("NotificaVittoria", 0.05f);
            }
        }
    }

    void NotificaVittoria()
    {
        MosaicManager.instance.CheckVictory();
    }
}