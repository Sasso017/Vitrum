using UnityEngine;
using UnityEngine.SceneManagement;

public class MosaicManager : MonoBehaviour
{
    public static MosaicManager instance;

    public MosaicSlot[] slots;
    public string ScenaBasilica = "ScenaBasilica"; // Nome esatto della tua scena principale

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        // Garantisce che all'inizio del minigioco il mouse sia SEMPRE sbloccato e visibile
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CheckVictory()
    {
        foreach (MosaicSlot slot in slots)
        {
            if (!slot.isOccupied)
            {
                return; // Esce se c'è almeno uno slot ancora vuoto
            }
        }

        Debug.Log("MOSAICO COMPLETATO!");

        // 1. Salva lo stato di completamento nel GameManager persistente
        if (GameManager.instance != null)
        {
            GameManager.instance.isMosaicoCompletato = true;
        }

        // 2. Torna alla scena principale dopo un piccolo ritardo
        Invoke("TornaAllaBasilica", 1.5f);
    }

    void TornaAllaBasilica()
    {
        SceneManager.LoadScene(ScenaBasilica);
    }
}