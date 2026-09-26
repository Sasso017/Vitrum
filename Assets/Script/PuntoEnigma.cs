using UnityEngine;

/// <summary>
/// Oggetto nella Basilica che apre la scena di un enigma quando il giocatore ci clicca sopra da vicino.
/// Sostituisce MosaicoPlay e Gioco15Play: si configura tutto dall'Inspector.
/// Il passaggio alla scena del minigioco avviene con dissolvenza (SceneFader).
/// Richiede un Collider sull'oggetto (necessario per OnMouseDown).
/// </summary>
[RequireComponent(typeof(Collider))]
public class PuntoEnigma : MonoBehaviour
{
    [Header("Enigma")]
    [Tooltip("Quale enigma apre questo oggetto")]
    [SerializeField] private Enigma enigma = Enigma.Mosaico;
    [Tooltip("Nome esatto della scena del minigioco (deve essere nelle Build Settings)")]
    [SerializeField] private string nomeScena = "ScenaMosaico";
    [Tooltip("Distanza massima dal giocatore per poter interagire")]
    [SerializeField] private float distanzaInterazione = 4f;

    [Header("Prerequisito")]
    [Tooltip("Se attivo, l'enigma si apre solo dopo averne completato un altro")]
    [SerializeField] private bool richiedePrerequisito = false;
    [SerializeField] private Enigma enigmaRichiesto = Enigma.Mosaico;

    [Header("Tasselli del mosaico")]
    [Tooltip("Se attivo, l'enigma si apre solo dopo aver raccolto tutti i tasselli nascosti nella Basilica")]
    [SerializeField] private bool richiedeTuttiITasselli = false;
    [Tooltip("{0} = tasselli mancanti, {1} = raccolti, {2} = totali")]
    [SerializeField] private string testoTasselliMancanti = "Ti mancano ancora {0} tasselli del mosaico";

    [Header("Messaggi a schermo (opzionali)")]
    [Tooltip("Mostrato se il prerequisito non è soddisfatto (es. 'Prima devi completare il Mosaico')")]
    [SerializeField] private GameObject testoPrerequisito;
    [Tooltip("Mostrato se l'enigma è già stato completato (es. 'Hai già ottenuto questa chiave')")]
    [SerializeField] private GameObject testoGiaCompletato;
    [SerializeField] private float durataMessaggio = 3f;

    private Transform giocatore;
    private GameObject testoAttivo;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            giocatore = playerObj.transform;
        else
            Debug.LogWarning("[PuntoEnigma] Nessun oggetto con tag 'Player' trovato.", this);

        // I messaggi partono spenti
        if (testoPrerequisito != null) testoPrerequisito.SetActive(false);
        if (testoGiaCompletato != null) testoGiaCompletato.SetActive(false);
    }

    private void OnMouseDown()
    {
        // Nessuna interazione in pausa o durante un cambio scena
        if (PauseMenu.IsPaused || SceneFader.InTransizione) return;

        // Il giocatore deve essere abbastanza vicino
        if (giocatore == null) return;
        if (Vector3.Distance(transform.position, giocatore.position) > distanzaInterazione) return;

        GameManager gm = GameManager.instance;

        // Enigma già completato: chiave già ottenuta
        if (gm != null && gm.IsCompletato(enigma))
        {
            MostraMessaggio(testoGiaCompletato);
            return;
        }

        // Prerequisito non ancora soddisfatto
        if (richiedePrerequisito && gm != null && !gm.IsCompletato(enigmaRichiesto))
        {
            MostraMessaggio(testoPrerequisito);
            return;
        }

        // Tasselli del mosaico non ancora tutti raccolti
        if (richiedeTuttiITasselli && gm != null && !gm.HaTuttiITasselli)
        {
            int mancanti = gm.TasselliTotali - gm.NumeroTasselliRaccolti;
            MessaggiSchermo.MostraMessaggio(
                string.Format(testoTasselliMancanti, mancanti, gm.NumeroTasselliRaccolti, gm.TasselliTotali),
                durataMessaggio);
            return;
        }

        ApriMinigioco();
    }

    private void ApriMinigioco()
    {
        if (!Application.CanStreamedLevelBeLoaded(nomeScena))
        {
            Debug.LogError($"[PuntoEnigma] La scena '{nomeScena}' non esiste o non è nelle Build Settings.", this);
            return;
        }

        // Salva dove si trova il giocatore, per riportarlo qui al ritorno
        if (GameManager.instance != null)
            GameManager.instance.SalvaPosizioneGiocatore(giocatore);

        // Nel minigioco serve il cursore libero
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Dissolvenza al nero, caricamento del minigioco, dissolvenza in entrata
        SceneFader.Instance.CaricaScena(nomeScena);
    }

    // ---------- Messaggi temporanei ----------

    private void MostraMessaggio(GameObject testo)
    {
        if (testo == null) return;

        // Spegne un eventuale messaggio precedente ancora visibile
        if (testoAttivo != null && testoAttivo != testo)
            testoAttivo.SetActive(false);

        testo.SetActive(true);
        testoAttivo = testo;

        CancelInvoke(nameof(NascondiMessaggio)); // Se si clicca più volte, il timer riparte
        Invoke(nameof(NascondiMessaggio), durataMessaggio);
    }

    private void NascondiMessaggio()
    {
        if (testoAttivo != null)
        {
            testoAttivo.SetActive(false);
            testoAttivo = null;
        }
    }
}