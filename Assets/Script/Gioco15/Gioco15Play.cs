using UnityEngine;
using UnityEngine.SceneManagement;

public class Gioco15Play : MonoBehaviour
{
    public string nomeScenaGioco15 = "ScenaGioco15";
    public float distanzaInterazione = 4f;

    [Header("UI Testo")]
    public GameObject testoDaMostrare; // Trascina qui l'oggetto del testo dal Canvas

    private Transform giocatore;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            giocatore = playerObj.transform;
        }

        // Assicura che il testo sia SPENTO all'avvio del gioco
        if (testoDaMostrare != null)
        {
            testoDaMostrare.SetActive(false);
        }
    }

    private void OnMouseDown()
    {
        // Se il gioco del 15 è già stato fatto, non fare nulla
        if (GameManager.instance != null && GameManager.instance.isGioco15Completato)
            return;

        // Controllo distanza dal giocatore
        if (giocatore != null && Vector3.Distance(transform.position, giocatore.position) <= distanzaInterazione)
        {
            // Se il mosaico NON è ancora stato completato
            if (GameManager.instance != null && !GameManager.instance.isMosaicoCompletato)
            {
                MostraTestoTemporaneo();
                return;
            }

            // Se il mosaico è completato, apre il gioco del 15
            ApriMinigioco();
        }
    }

    void MostraTestoTemporaneo()
    {
        if (testoDaMostrare != null)
        {
            testoDaMostrare.SetActive(true); // ACCENDE il testo

            CancelInvoke("SpegniTesto");     // Resetta il timer se clicchi più volte
            Invoke("SpegniTesto", 3f);       // SPEGNE il testo dopo 3 secondi
        }
    }

    void SpegniTesto()
    {
        if (testoDaMostrare != null)
        {
            testoDaMostrare.SetActive(false); // SPEGNE il testo
        }
    }

    void ApriMinigioco()
    {
        if (giocatore != null && GameManager.instance != null)
        {
            GameManager.instance.ultimaPosizioneGiocatore = giocatore.position;
            GameManager.instance.ultimaRotazioneGiocatore = giocatore.rotation;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene(nomeScenaGioco15);
    }
}