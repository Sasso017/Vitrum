using UnityEngine;
using UnityEngine.SceneManagement;

public class MosaicoPlay : MonoBehaviour
{
    public string nomeScenaMosaico = "ScenaMosaico"; // Nome esatto della scena del minigioco
    public float distanzaInterazione = 4f;           // Distanza massima per interagire

    private Transform giocatore;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            giocatore = playerObj.transform;
        }
    }

    private void OnMouseDown()
    {
        // Se il mosaico è già stato completato, ignora l'interazione
        if (GameManager.instance != null && GameManager.instance.isMosaicoCompletato)
            return;

        // Verifica che il giocatore sia abbastanza vicino al cubo
        if (giocatore != null && Vector3.Distance(transform.position, giocatore.position) <= distanzaInterazione)
        {
            ApriMinigioco();
        }
    }

    void ApriMinigioco()
    {
        // Salva la posizione e la rotazione attuali nel GameManager
        if (giocatore != null && GameManager.instance != null)
        {
            GameManager.instance.ultimaPosizioneGiocatore = giocatore.position;
            GameManager.instance.ultimaRotazioneGiocatore = giocatore.rotation;
        }

        // Sblocca il mouse per la scena del minigioco
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Carica la scena del mosaico
        SceneManager.LoadScene(nomeScenaMosaico);
    }
}