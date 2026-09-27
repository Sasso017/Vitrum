using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Un quadro del mosaico con i suoi posti vuoti (SlotMosaico nei figli).
/// Il mosaico si ricompone tutto insieme: quando il giocatore ha raccolto tutti i tasselli,
/// avvicinandosi a uno qualsiasi dei quadri e premendo E vengono inseriti in entrambi i quadri
/// (se ne occupa MosaicoBasilica). Prima di allora il quadro indica quanti tasselli mancano.
/// Mettilo sull'oggetto principale del quadro (la base del dipinto).
/// </summary>
public class QuadroMosaico : MonoBehaviour
{
    [Tooltip("I posti vuoti del quadro. Se vuoto, vengono cercati tra i figli")]
    [SerializeField] private SlotMosaico[] slot;

    [Header("Interazione")]
    [SerializeField] private float distanzaInterazione = 2.5f;
    [Tooltip("Il giocatore deve guardare verso il quadro entro questo angolo (gradi)")]
    [SerializeField] private float angoloVisuale = 50f;
    [SerializeField] private string testoRicomponi = "Premi E per ricomporre il mosaico";
    [Tooltip("{0} = tasselli raccolti, {1} = tasselli totali")]
    [SerializeField] private string testoMancanti = "Hai trovato {0} tasselli su {1}: trovali tutti per ricomporre il mosaico";
#if !ENABLE_INPUT_SYSTEM
    [SerializeField] private KeyCode tastoInteragisci = KeyCode.E;
#endif

    /// <summary>I posti del quadro (usati da MosaicoBasilica).</summary>
    public SlotMosaico[] Slot => slot;

    /// <summary>True quando tutti i posti del quadro sono pieni.</summary>
    public bool Completo
    {
        get
        {
            foreach (SlotMosaico s in slot) if (!s.Pieno) return false;
            return slot.Length > 0;
        }
    }

    private Transform giocatore;
    private Camera cameraGiocatore;
    private MosaicoBasilica mosaico;

    private void Awake()
    {
        if (slot == null || slot.Length == 0)
            slot = GetComponentsInChildren<SlotMosaico>(true);
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) giocatore = playerObj.transform;
        cameraGiocatore = Camera.main;

        mosaico = FindObjectOfType<MosaicoBasilica>();
        if (mosaico == null)
            Debug.LogWarning("[QuadroMosaico] Nessun MosaicoBasilica nella scena: il mosaico non potrà essere ricomposto.", this);

        // Ripristina i tasselli già inseriti in precedenza
        GameManager gm = GameManager.instance;
        foreach (SlotMosaico s in slot)
            s.Ripristina(gm != null && gm.IsTasselloPosizionato(s.IdTassello));
    }

    private void Update()
    {
        bool puoInteragire = mosaico != null && !mosaico.Completato && !mosaico.InCorso &&
                             !PauseMenu.IsPaused && !SceneFader.InTransizione &&
                             !StazioneMinigioco.InUso && !IntroBasilica.InCorso && !DialogoNPC.InCorso;

        if (!puoInteragire || !GiocatoreVicino())
        {
            MessaggiSchermo.NascondiSuggerimento(this);
            return;
        }

        if (mosaico.TuttiITasselliRaccolti())
        {
            MessaggiSchermo.MostraSuggerimento(this, testoRicomponi);
            if (InteragisciPremuto())
            {
                MessaggiSchermo.NascondiSuggerimento(this);
                mosaico.Ricomponi();
            }
        }
        else
        {
            MessaggiSchermo.MostraSuggerimento(this,
                string.Format(testoMancanti, mosaico.TasselliRaccolti(), mosaico.TasselliTotali()));
        }
    }

    private bool GiocatoreVicino()
    {
        if (giocatore == null) return false;
        if (Vector3.Distance(giocatore.position, transform.position) > distanzaInterazione) return false;

        Transform occhi = cameraGiocatore != null ? cameraGiocatore.transform : giocatore;
        Vector3 direzione = (transform.position - occhi.position).normalized;
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
}