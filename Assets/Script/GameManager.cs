using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    // Stato dei minigiochi (salvati tra un cambio scena e l'altro)
    public bool isMosaicoCompletato = false;
    public bool isGioco15Completato = false; // Nuovo flag per il Gioco del 15

    // Posizione e rotazione del giocatore salvate prima di entrare nei minigiochi
    public Vector3 ultimaPosizioneGiocatore;
    public Quaternion ultimaRotazioneGiocatore;

    private void Awake()
    {
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