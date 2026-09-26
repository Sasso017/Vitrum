using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Porta principale della Basilica con i lucchetti.
/// Avvicinandosi senza tutte le chiavi mostra un messaggio; con tutte le chiavi
/// invita a premere E per sbloccarla e lancia l'evento "Quando Sbloccata".
/// Mettilo sull'oggetto Porta (quello che contiene cardini, ante e lucchetti).
/// </summary>
public class PortaBasilica : MonoBehaviour
{
    [Header("Interazione")]
    [Tooltip("Distanza massima dalla porta per mostrare i messaggi")]
    [SerializeField] private float distanzaInterazione = 3f;
    [Tooltip("Il giocatore deve guardare verso la porta entro questo angolo (gradi)")]
    [SerializeField] private float angoloVisuale = 50f;
    [Tooltip("Opzionale: il punto da usare come centro della porta (es. uno dei lucchetti). Se vuoto viene calcolato dal modello")]
    [SerializeField] private Transform puntoRiferimento;
#if !ENABLE_INPUT_SYSTEM
    [SerializeField] private KeyCode tastoInteragisci = KeyCode.E;
#endif

    [Header("Testi")]
    [Tooltip("Mostrato vicino alla porta finché mancano delle chiavi. {0} = chiavi ottenute, {1} = chiavi totali")]
    [SerializeField] private string testoChiaviMancanti = "Hai bisogno di entrambe le chiavi per sbloccare la porta";
    [Tooltip("Mostrato quando il giocatore ha tutte le chiavi")]
    [SerializeField] private string testoSblocca = "Premi E per sbloccare la porta";

    [Header("Sblocco")]
    [Tooltip("Cosa succede premendo E con tutte le chiavi (verrà collegato all'animazione di apertura)")]
    [SerializeField] private UnityEvent quandoSbloccata;

    private Transform giocatore;
    private Camera cameraGiocatore;
    private bool sbloccata = false;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) giocatore = playerObj.transform;
        cameraGiocatore = Camera.main;
    }

    private void Update()
    {
        bool puoInteragire = !sbloccata && !PauseMenu.IsPaused && !SceneFader.InTransizione &&
                             !StazioneMinigioco.InUso && !IntroBasilica.InCorso;

        if (!puoInteragire || !GiocatoreVicino())
        {
            MessaggiSchermo.NascondiSuggerimento(this);
            return;
        }

        GameManager gm = GameManager.instance;
        bool haTutteLeChiavi = gm != null && gm.HaTutteLeChiavi;

        if (!haTutteLeChiavi)
        {
            int ottenute = gm != null ? gm.ChiaviRaccolte : 0;
            MessaggiSchermo.MostraSuggerimento(this, string.Format(testoChiaviMancanti, ottenute, GameManager.ChiaviTotali));
            return;
        }

        MessaggiSchermo.MostraSuggerimento(this, testoSblocca);

        if (InteragisciPremuto())
        {
            sbloccata = true;
            MessaggiSchermo.NascondiSuggerimento(this);
            quandoSbloccata?.Invoke();
        }
    }

    /// <summary>Centro della porta: il punto di riferimento, oppure il centro dei modelli figli.</summary>
    private Vector3 CentroPorta()
    {
        if (puntoRiferimento != null) return puntoRiferimento.position;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return transform.position;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b.center;
    }

    private bool GiocatoreVicino()
    {
        if (giocatore == null) return false;

        Vector3 centro = CentroPorta();
        if (Vector3.Distance(giocatore.position, centro) > distanzaInterazione) return false;

        Transform occhi = cameraGiocatore != null ? cameraGiocatore.transform : giocatore;
        Vector3 direzione = (centro - occhi.position).normalized;
        return Vector3.Angle(occhi.forward, direzione) <= angoloVisuale;
    }

    private bool InteragisciPremuto()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(tastoInteragisci);
#endif
    }

    private void OnDisable()
    {
        MessaggiSchermo.NascondiSuggerimento(this);
    }

    private void OnDrawGizmosSelected()
    {
        // Nell'Editor mostra il raggio entro cui compaiono i messaggi
        Gizmos.color = new Color(1f, 0.8f, 0.2f);
        Gizmos.DrawWireSphere(CentroPorta(), distanzaInterazione);
    }
}