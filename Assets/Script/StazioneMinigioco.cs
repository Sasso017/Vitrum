using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Postazione di un minigioco giocabile direttamente nella Basilica, senza cambiare scena.
/// Avvicinandosi e premendo E: dissolvenza, la visuale passa alla camera del minigioco e il cursore si libera.
/// Esc per uscire. Alla vittoria lo script del minigioco chiama CompletaMinigioco():
/// la chiave viene assegnata e si torna in prima persona.
/// Mettilo sull'oggetto del minigioco (es. il leggio con il GameBoy).
/// </summary>
public class StazioneMinigioco : MonoBehaviour
{
    /// <summary>True mentre il giocatore sta usando una postazione (menu di pausa disattivato).</summary>
    public static bool InUso { get; private set; }

    [Header("Enigma")]
    [SerializeField] private Enigma enigma = Enigma.Gioco15;
    [Tooltip("Se attivo, il minigioco si apre solo dopo averne completato un altro")]
    [SerializeField] private bool richiedePrerequisito = true;
    [SerializeField] private Enigma enigmaRichiesto = Enigma.Mosaico;

    [Header("Interazione")]
    [Tooltip("Distanza massima dal giocatore per poter interagire")]
    [SerializeField] private float distanzaInterazione = 2.5f;
    [Tooltip("Il giocatore deve guardare verso la postazione entro questo angolo (gradi)")]
    [SerializeField] private float angoloVisuale = 50f;
    [Tooltip("Suggerimento mostrato in basso quando il giocatore è vicino (lascia vuoto per non mostrarlo)")]
    [SerializeField] private string testoSuggerimento = "Premi E per risolvere il gioco del 15";
#if !ENABLE_INPUT_SYSTEM
    [SerializeField] private KeyCode tastoInteragisci = KeyCode.E;
    [SerializeField] private KeyCode tastoEsci = KeyCode.Escape;
#endif

    [Header("Camere")]
    [Tooltip("La camera del giocatore (Main Camera di Carlo)")]
    [SerializeField] private Camera cameraGiocatore;
    [Tooltip("La camera fissa sopra il tavolo del minigioco (lasciala disattivata in scena)")]
    [SerializeField] private Camera cameraMinigioco;

    [Header("Giocatore")]
    [Tooltip("Script da disattivare durante il minigioco (es. FPSController, Accovacciamento)")]
    [SerializeField] private Behaviour[] controlliGiocatore;

    [Header("Minigioco")]
    [Tooltip("CanvasGroup sul Canvas del minigioco: i click funzionano solo mentre si gioca")]
    [SerializeField] private CanvasGroup interfacciaMinigioco;
    [Tooltip("Suggerimento mostrato in basso mentre si gioca")]
    [SerializeField] private string testoDuranteGioco = "Premi Esc per uscire";
    [Tooltip("Oggetti aggiuntivi visibili solo durante il gioco (opzionale)")]
    [SerializeField] private GameObject[] visibiliSoloDuranteGioco;

    [Header("Ricompensa (opzionale)")]
    [Tooltip("Se assegnata, alla vittoria la chiave emerge dal tavolo e va raccolta con E (la chiave viene assegnata alla raccolta)")]
    [SerializeField] private ChiaveRicompensa ricompensa;

    [Header("Messaggi (lascia vuoto un campo per non mostrare quel messaggio)")]
    [SerializeField] private string testoPrerequisito = "Prima devi completare il Mosaico";
    [SerializeField] private string testoGiaCompletato = "Hai già risolto questo enigma";
    [SerializeField] private string testoVittoria = "Gioco del 15 risolto!";
    [SerializeField] private float durataMessaggio = 3f;
    [Tooltip("Secondi dopo la vittoria prima di tornare in prima persona")]
    [SerializeField] private float ritardoUscitaDopoVittoria = 2f;
    [SerializeField] private AudioClip suonoChiave;

    private Transform giocatore;
    private bool inGioco = false;
    private bool completando = false;
    private bool risolto = false; // Minigioco vinto: la postazione non risponde più

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) giocatore = playerObj.transform;
        else Debug.LogWarning("[StazioneMinigioco] Nessun oggetto con tag 'Player' trovato.", this);

        if (cameraMinigioco != null) cameraMinigioco.enabled = false;
        ImpostaInterfaccia(false);
        MostraTutti(visibiliSoloDuranteGioco, false);
    }

    private void Update()
    {
        if (inGioco)
        {
            if (!completando && !SceneFader.InTransizione && EsciPremuto())
                Esci();
            return;
        }

        // Minigioco già vinto: niente più interazione (il tasto E serve per raccogliere la chiave)
        if (risolto)
        {
            MessaggiSchermo.NascondiSuggerimento(this);
            return;
        }

        // Nessuna interazione in pausa, in cutscene, durante una transizione o se un'altra postazione è in uso
        if (PauseMenu.IsPaused || IntroBasilica.InCorso || SceneFader.InTransizione || InUso)
        {
            MessaggiSchermo.NascondiSuggerimento(this);
            return;
        }

        bool vicino = GiocatoreVicino();
        if (vicino) MessaggiSchermo.MostraSuggerimento(this, testoSuggerimento);
        else MessaggiSchermo.NascondiSuggerimento(this);

        if (vicino && InteragisciPremuto())
            ProvaAEntrare();
    }

    // ---------- Entrata e uscita ----------

    private void ProvaAEntrare()
    {
        GameManager gm = GameManager.instance;

        if (gm != null && gm.IsCompletato(enigma))
        {
            MessaggiSchermo.MostraMessaggio(testoGiaCompletato, durataMessaggio);
            return;
        }

        if (richiedePrerequisito && gm != null && !gm.IsCompletato(enigmaRichiesto))
        {
            MessaggiSchermo.MostraMessaggio(testoPrerequisito, durataMessaggio);
            return;
        }

        Entra();
    }

    private void Entra()
    {
        if (cameraMinigioco == null)
        {
            Debug.LogError("[StazioneMinigioco] Nessuna camera del minigioco assegnata.", this);
            return;
        }

        InUso = true;
        inGioco = true;
        MessaggiSchermo.NascondiSuggerimento(this);
        SetControlli(false); // Subito: Carlo non si muove durante la dissolvenza

        SceneFader.Instance.Dissolvenza(() =>
        {
            if (cameraGiocatore != null) cameraGiocatore.enabled = false;
            cameraMinigioco.enabled = true;

            ImpostaInterfaccia(true);
            MostraTutti(visibiliSoloDuranteGioco, true);
            MessaggiSchermo.MostraSuggerimento(this, testoDuranteGioco);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        });
    }

    private void Esci()
    {
        SceneFader.Instance.Dissolvenza(() =>
        {
            cameraMinigioco.enabled = false;
            if (cameraGiocatore != null) cameraGiocatore.enabled = true;

            ImpostaInterfaccia(false);
            MostraTutti(visibiliSoloDuranteGioco, false);
            MessaggiSchermo.NascondiSuggerimento(this);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            SetControlli(true);
            inGioco = false;
            completando = false;
            InUso = false;

            // Tornati in prima persona dopo la vittoria: la chiave emerge dal tavolo
            if (risolto && ricompensa != null)
                ricompensa.Compari();
        });
    }

    /// <summary>
    /// Da chiamare dallo script del minigioco quando il giocatore vince.
    /// Dopo una breve pausa riporta il giocatore in prima persona. La chiave viene assegnata subito
    /// oppure, se c'è una ChiaveRicompensa, quando il giocatore la raccoglie.
    /// </summary>
    public void CompletaMinigioco()
    {
        if (!inGioco || completando) return;
        completando = true;
        risolto = true;

        // Senza ricompensa da raccogliere, la chiave viene assegnata subito
        if (ricompensa == null && GameManager.instance != null)
            GameManager.instance.CompletaEnigma(enigma);

        if (interfacciaMinigioco != null) interfacciaMinigioco.blocksRaycasts = false; // Niente più mosse
        MessaggiSchermo.NascondiSuggerimento(this); // Niente più "Esc per uscire": si esce da soli
        MessaggiSchermo.MostraMessaggio(testoVittoria, ritardoUscitaDopoVittoria);
        if (suonoChiave != null && SFXPlayer.Instance != null) SFXPlayer.Instance.Play(suonoChiave);

        StartCoroutine(UscitaDopoVittoria());
    }

    private IEnumerator UscitaDopoVittoria()
    {
        yield return new WaitForSeconds(ritardoUscitaDopoVittoria);

        // Attende eventuali transizioni ancora in corso
        while (SceneFader.InTransizione) yield return null;
        Esci();
    }

    // ---------- Utilità ----------

    private bool GiocatoreVicino()
    {
        if (giocatore == null) return false;
        if (Vector3.Distance(giocatore.position, transform.position) > distanzaInterazione) return false;

        // Il giocatore deve guardare più o meno verso la postazione
        Transform occhi = cameraGiocatore != null ? cameraGiocatore.transform : giocatore;
        Vector3 direzione = (transform.position - occhi.position).normalized;
        return Vector3.Angle(occhi.forward, direzione) <= angoloVisuale;
    }

    private void ImpostaInterfaccia(bool attiva)
    {
        if (interfacciaMinigioco == null) return;

        // Blocca solo i click, così le tessere restano visibili e con il loro colore normale
        interfacciaMinigioco.blocksRaycasts = attiva;

        // Un Canvas in World Space riceve i click solo tramite la camera che lo inquadra
        Canvas canvas = interfacciaMinigioco.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
            canvas.worldCamera = cameraMinigioco;
    }

    private void SetControlli(bool attivi)
    {
        if (controlliGiocatore == null) return;
        foreach (Behaviour b in controlliGiocatore)
        {
            if (b != null) b.enabled = attivi;
        }
    }

    private static void Mostra(GameObject obj, bool visibile)
    {
        if (obj != null && obj.activeSelf != visibile) obj.SetActive(visibile);
    }

    private static void MostraTutti(GameObject[] oggetti, bool visibili)
    {
        if (oggetti == null) return;
        foreach (GameObject o in oggetti) Mostra(o, visibili);
    }

    private bool InteragisciPremuto()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(tastoInteragisci);
#endif
    }

    private bool EsciPremuto()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(tastoEsci);
#endif
    }

    private void OnDisable()
    {
        MessaggiSchermo.NascondiSuggerimento(this);
    }

    private void OnDestroy()
    {
        if (inGioco) InUso = false;
    }
}