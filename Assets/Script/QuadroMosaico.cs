using System;
using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Un quadro del mosaico con i suoi posti vuoti (SlotMosaico nei figli).
/// Avvicinandosi e premendo E, tutti i tasselli raccolti che appartengono a questo quadro
/// vengono inseriti automaticamente al loro posto. I tasselli di altri quadri restano al giocatore.
/// Mettilo sull'oggetto principale del quadro.
/// </summary>
public class QuadroMosaico : MonoBehaviour
{
    [Tooltip("I posti vuoti del quadro. Se vuoto, vengono cercati tra i figli")]
    [SerializeField] private SlotMosaico[] slot;

    [Header("Interazione")]
    [SerializeField] private float distanzaInterazione = 2.5f;
    [Tooltip("Il giocatore deve guardare verso il quadro entro questo angolo (gradi)")]
    [SerializeField] private float angoloVisuale = 50f;
    [Tooltip("{0} = numero di tasselli che verranno inseriti")]
    [SerializeField] private string testoPosiziona = "Premi E per posizionare i tasselli ({0})";
    [Tooltip("{0} = tasselli ancora mancanti in questo quadro")]
    [SerializeField] private string testoMancanti = "Mancano {0} tasselli per completare questo quadro";
    [SerializeField] private string testoCompletato = "Quadro completato!";
#if !ENABLE_INPUT_SYSTEM
    [SerializeField] private KeyCode tastoInteragisci = KeyCode.E;
#endif

    [Header("Effetti")]
    [Tooltip("Lo stesso prefab dell'effetto 'puf' dei lucchetti (opzionale)")]
    [SerializeField] private ParticleSystem prefabPuf;
    [SerializeField] private AudioClip suonoInserimento;
    [SerializeField] private AudioClip suonoCompletato;
    [Tooltip("Tempo tra l'inserimento di un tassello e il successivo")]
    [SerializeField] private float intervalloInserimento = 0.35f;

    /// <summary>Lanciato quando l'ultimo tassello del quadro viene inserito.</summary>
    public event Action OnCompletato;

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
    private bool inserendo = false;

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

        // Ripristina i tasselli già inseriti in precedenza
        GameManager gm = GameManager.instance;
        foreach (SlotMosaico s in slot)
            s.Ripristina(gm != null && gm.IsTasselloPosizionato(s.IdTassello));
    }

    private void Update()
    {
        if (inserendo || Completo)
        {
            MessaggiSchermo.NascondiSuggerimento(this);
            return;
        }

        bool puoInteragire = !PauseMenu.IsPaused && !SceneFader.InTransizione &&
                             !StazioneMinigioco.InUso && !IntroBasilica.InCorso;

        if (!puoInteragire || !GiocatoreVicino())
        {
            MessaggiSchermo.NascondiSuggerimento(this);
            return;
        }

        int inseribili = ContaInseribili();
        if (inseribili > 0)
        {
            MessaggiSchermo.MostraSuggerimento(this, string.Format(testoPosiziona, inseribili));
            if (InteragisciPremuto()) StartCoroutine(InserisciTasselli());
        }
        else
        {
            MessaggiSchermo.MostraSuggerimento(this, string.Format(testoMancanti, ContaMancanti()));
        }
    }

    /// <summary>Tasselli raccolti dal giocatore che vanno in questo quadro e non sono ancora inseriti.</summary>
    private int ContaInseribili()
    {
        GameManager gm = GameManager.instance;
        if (gm == null) return 0;

        int n = 0;
        foreach (SlotMosaico s in slot)
            if (!s.Pieno && gm.IsTasselloRaccolto(s.IdTassello)) n++;
        return n;
    }

    private int ContaMancanti()
    {
        int n = 0;
        foreach (SlotMosaico s in slot) if (!s.Pieno) n++;
        return n;
    }

    private IEnumerator InserisciTasselli()
    {
        inserendo = true;
        MessaggiSchermo.NascondiSuggerimento(this);
        GameManager gm = GameManager.instance;

        foreach (SlotMosaico s in slot)
        {
            if (s.Pieno || !gm.IsTasselloRaccolto(s.IdTassello)) continue;

            gm.PosizionaTassello(s.IdTassello);
            yield return s.Posiziona(prefabPuf, suonoInserimento);
            yield return new WaitForSeconds(intervalloInserimento);
        }

        if (Completo)
        {
            if (suonoCompletato != null && SFXPlayer.Instance != null) SFXPlayer.Instance.Play(suonoCompletato);
            MessaggiSchermo.MostraMessaggio(testoCompletato, 2.5f);
            OnCompletato?.Invoke();
        }

        inserendo = false;
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
