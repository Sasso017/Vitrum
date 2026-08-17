using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    // Stato del mosaico (salvato tra un cambio scena e l'altro)
    public bool isMosaicoCompletato = false;

    // Posizione e rotazione del giocatore salvate prima di entrare nel minigioco
    public Vector3 ultimaPosizioneGiocatore;
    public Quaternion ultimaRotazioneGiocatore;

    private void Awake()
    {
        // Garantisce che il GameManager sia unico e non venga distrutto al cambio scena
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}