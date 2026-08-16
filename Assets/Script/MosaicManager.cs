using UnityEngine;
using UnityEngine.SceneManagement;

public class MosaicManager : MonoBehaviour
{
    public static MosaicManager instance;

    public MosaicSlot[] slots; // Trascina qui tutti gli slot
    public string nomeScenaBasilica = "ScenaBasilica"; // Nome della scena principale

    private void Awake()
    {
        instance = this;
    }

    public void CheckVictory()
    {
        foreach (MosaicSlot slot in slots)
        {
            if (!slot.isOccupied)
            {
                return; // Se anche solo uno slot non è occupato, il puzzle non è finito
            }
        }

        Debug.Log("MOSAICO COMPLETATO!");

        // Esegui la vittoria (es. torna alla basilica dopo 2 secondi o sblocca un evento)
        Invoke("TornaAllaBasilica", 2f);
    }

    void TornaAllaBasilica()
    {
        SceneManager.LoadScene(nomeScenaBasilica);
    }
}