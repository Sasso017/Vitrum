using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Tassello del mosaico nascosto nella Basilica. Ondeggia leggermente per farsi notare;
/// avvicinandosi e premendo E viene raccolto e vola verso il giocatore.
/// Una volta raccolto non ricompare più (nemmeno tornando nella Basilica dal mosaico).
/// Mettilo su ogni tassello: l'identificativo di default è il nome dell'oggetto,
/// quindi ogni tassello deve avere un nome diverso (es. Tassello1, Tassello2...).
/// </summary>
public class Tassello : MonoBehaviour
{
    // Registro dei tasselli presenti nella scena (usato per contarli in automatico)
    private static readonly HashSet<string> registro = new HashSet<string>();

    /// <summary>Numero di tasselli presenti nella scena attuale (raccolti o no).</summary>
    public static int NumeroInScena => registro.Count;

    [Tooltip("Identificativo unico del tassello. Se vuoto, viene usato il nome dell'oggetto")]
    [SerializeField] private string idTassello = "";

    [Header("Aspetto")]
    [Tooltip("Il modello da far ondeggiare e ruotare (se vuoto, l'oggetto stesso)")]
    [SerializeField] private Transform modello;
    [SerializeField] private float ampiezzaOndeggio = 0.02f;
    [SerializeField] private float velocitaOndeggio = 1.5f;
    [Tooltip("Rotazione lenta sul posto (gradi al secondo, 0 = ferma)")]
    [SerializeField] private float velocitaRotazione = 20f;

    public enum ModalitaSuono { Continuo, Singolo }

    [Header("Suono di richiamo (opzionale)")]
    [Tooltip("Suono che aiuta a trovare il tassello: si sente solo da vicino e arriva dalla sua direzione")]
    [SerializeField] private AudioClip suonoVicinanza;
    [Tooltip("Continuo: suono in loop che cresce avvicinandosi. Singolo: un 'ding' quando si entra nel raggio")]
    [SerializeField] private ModalitaSuono modalitaSuono = ModalitaSuono.Continuo;
    [Tooltip("Distanza entro cui si sente il suono (metri)")]
    [SerializeField] private float distanzaUdibile = 6f;
    [SerializeField, Range(0f, 1f)] private float volumeSuono = 0.6f;
    [Tooltip("Gruppo SFX dell'AudioMixer, così il suono segue gli slider del volume")]
    [SerializeField] private AudioMixerGroup gruppoSFX;
    [Tooltip("Se attivo, il richiamo inizia solo dopo la prima conversazione con l'NPC")]
    [SerializeField] private bool richiamoDopoDialogoNPC = true;
    [Tooltip("Secondi in cui il suono cresce quando il richiamo si attiva per la prima volta")]
    [SerializeField] private float durataComparsaSuono = 2f;

    [Header("Raccolta")]
    [SerializeField] private float distanzaRaccolta = 2f;
    [Tooltip("Il giocatore deve guardare verso il tassello entro questo angolo (gradi)")]
    [SerializeField] private float angoloVisuale = 45f;
    [SerializeField] private string testoSuggerimento = "Premi E per raccogliere il tassello";
    [Tooltip("{0} = tasselli raccolti, {1} = tasselli totali. Lascia vuoto per non mostrarlo")]
    [SerializeField] private string testoRaccolta = "Tassello del mosaico trovato! ({0}/{1})";
    [SerializeField] private AudioClip suonoRaccolta;
    [Tooltip("Camera del giocatore: il tassello vola verso di lei (se vuota, viene usata la Main Camera)")]
    [SerializeField] private Camera cameraGiocatore;
#if !ENABLE_INPUT_SYSTEM
    [SerializeField] private KeyCode tastoRaccogli = KeyCode.E;
#endif

    /// <summary>Identificativo del tassello (usato dai quadri per riconoscerlo).</summary>
    public string Id => string.IsNullOrEmpty(idTassello) ? gameObject.name : idTassello;

    private Transform giocatore;
    private Vector3 posizioneBaseModello;
    private Vector3 scalaModello;
    private float faseOndeggio;
    private bool raccolto = false;
    private AudioSource sorgente;
    private bool dentroRaggio = false; // Per la modalità Singolo
    private bool suonoAvviato = false; // Il loop è già partito almeno una volta
    private float volumeAttuale = 0f;  // Per far crescere il suono alla prima attivazione

    private void Awake()
    {
        if (!registro.Add(Id))
            Debug.LogWarning($"[Tassello] Esistono due tasselli con lo stesso identificativo '{Id}': rinominane uno.", this);
    }

    private void Start()
    {
        if (modello == null) modello = transform;
        posizioneBaseModello = modello.localPosition;
        scalaModello = modello.localScale;
        faseOndeggio = Random.Range(0f, Mathf.PI * 2f); // Ogni tassello ondeggia a modo suo

        if (cameraGiocatore == null) cameraGiocatore = Camera.main;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) giocatore = playerObj.transform;

        // Già raccolto in precedenza: non ricompare
        if (GameManager.instance != null && GameManager.instance.IsTasselloRaccolto(Id))
        {
            raccolto = true;
            gameObject.SetActive(false);
            return;
        }

        PreparaSuono();
    }

    // ---------- Suono di richiamo ----------

    /// <summary>Crea una sorgente audio 3D sul tassello: si sente solo da vicino e dalla sua direzione.</summary>
    private void PreparaSuono()
    {
        if (suonoVicinanza == null) return;

        sorgente = gameObject.AddComponent<AudioSource>();
        sorgente.clip = suonoVicinanza;
        sorgente.outputAudioMixerGroup = gruppoSFX;
        sorgente.playOnAwake = false;
        sorgente.spatialBlend = 1f;                     // Completamente 3D
        sorgente.rolloffMode = AudioRolloffMode.Linear; // Svanisce in modo regolare fino a zero
        sorgente.minDistance = Mathf.Min(1f, distanzaUdibile * 0.2f);
        sorgente.maxDistance = distanzaUdibile;
        sorgente.dopplerLevel = 0f;
        sorgente.volume = volumeSuono;

        if (modalitaSuono == ModalitaSuono.Continuo)
            sorgente.loop = true; // Parte in AggiornaSuono, quando il richiamo è attivo
    }

    /// <summary>True quando il giocatore ha già parlato con l'NPC (o se l'attesa è disattivata).</summary>
    private bool RichiamoAttivo()
    {
        if (!richiamoDopoDialogoNPC) return true;
        GameManager gm = GameManager.instance;
        return gm == null || gm.obiettivoRivelato; // Senza GameManager (test) il richiamo è sempre attivo
    }

    private void AggiornaSuono()
    {
        if (sorgente == null) return;

        // Prima del dialogo con l'NPC il richiamo non c'è ancora
        if (!RichiamoAttivo()) return;

        // In pausa, durante la cutscene, un minigioco o un dialogo il richiamo tace
        bool silenzio = PauseMenu.IsPaused || IntroBasilica.InCorso || StazioneMinigioco.InUso || DialogoNPC.InCorso;

        if (modalitaSuono == ModalitaSuono.Continuo)
        {
            if (silenzio)
            {
                if (sorgente.isPlaying) sorgente.Pause();
                return;
            }

            if (!suonoAvviato)
            {
                // Prima attivazione: parte da un punto a caso del loop (i tasselli non suonano in sincrono)
                suonoAvviato = true;
                volumeAttuale = 0f;
                sorgente.volume = 0f;
                sorgente.time = Random.Range(0f, suonoVicinanza.length);
                sorgente.Play();
            }
            else if (!sorgente.isPlaying)
            {
                sorgente.UnPause();
            }

            // Il suono cresce gradualmente fino al volume impostato
            if (volumeAttuale < volumeSuono)
            {
                volumeAttuale = Mathf.MoveTowards(volumeAttuale, volumeSuono,
                    volumeSuono * Time.deltaTime / Mathf.Max(durataComparsaSuono, 0.01f));
                sorgente.volume = volumeAttuale;
            }
            return;
        }

        // Modalità Singolo: un "ding" quando il giocatore entra nel raggio
        if (giocatore == null || silenzio) return;
        float distanza = Vector3.Distance(giocatore.position, modello.position);

        if (!dentroRaggio && distanza <= distanzaUdibile)
        {
            dentroRaggio = true;
            sorgente.PlayOneShot(suonoVicinanza, volumeSuono);
        }
        else if (dentroRaggio && distanza > distanzaUdibile * 1.2f)
        {
            dentroRaggio = false; // Uscito dal raggio: al prossimo ingresso suonerà di nuovo
        }
    }

    private IEnumerator SfumaSuono(float durata)
    {
        if (sorgente == null) yield break;
        float iniziale = sorgente.volume, t = 0f;
        while (t < durata)
        {
            t += Time.deltaTime;
            sorgente.volume = Mathf.Lerp(iniziale, 0f, t / durata);
            yield return null;
        }
        sorgente.Stop();
    }

    private void Update()
    {
        if (raccolto) return;

        AggiornaSuono();

        // Ondeggia e ruota lentamente per farsi notare
        faseOndeggio += Time.deltaTime * velocitaOndeggio;
        if (modello == transform)
            transform.position += Vector3.up * (Mathf.Cos(faseOndeggio) * velocitaOndeggio * ampiezzaOndeggio * Time.deltaTime);
        else
            modello.localPosition = posizioneBaseModello + Vector3.up * Mathf.Sin(faseOndeggio) * ampiezzaOndeggio;
        if (velocitaRotazione != 0f)
            modello.Rotate(Vector3.up, velocitaRotazione * Time.deltaTime, Space.World);

        bool puoInteragire = !PauseMenu.IsPaused && !SceneFader.InTransizione &&
                             !StazioneMinigioco.InUso && !IntroBasilica.InCorso;
        bool vicino = puoInteragire && GiocatoreVicino();

        if (vicino) MessaggiSchermo.MostraSuggerimento(this, testoSuggerimento);
        else MessaggiSchermo.NascondiSuggerimento(this);

        if (vicino && RaccogliPremuto())
            StartCoroutine(Raccogli());
    }

    private IEnumerator Raccogli()
    {
        raccolto = true;
        MessaggiSchermo.NascondiSuggerimento(this);
        StartCoroutine(SfumaSuono(0.4f)); // Il richiamo sfuma mentre il tassello vola via

        GameManager gm = GameManager.instance;
        if (gm != null) gm.RaccogliTassello(Id);

        if (suonoRaccolta != null && SFXPlayer.Instance != null) SFXPlayer.Instance.Play(suonoRaccolta);

        if (gm != null && !string.IsNullOrEmpty(testoRaccolta))
            MessaggiSchermo.MostraMessaggio(string.Format(testoRaccolta, gm.NumeroTasselliRaccolti, gm.TasselliTotali), 2.5f);

        // Vola verso la camera rimpicciolendosi
        Vector3 partenza = modello.position;
        float durata = 0.5f, t = 0f;
        while (t < durata)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / durata);
            Vector3 destinazione = cameraGiocatore != null
                ? cameraGiocatore.transform.position + cameraGiocatore.transform.forward * 0.3f
                : partenza + Vector3.up * 0.5f;

            modello.position = Vector3.Lerp(partenza, destinazione, k * k);
            modello.localScale = scalaModello * (1f - k);
            modello.Rotate(Vector3.up, 720f * Time.deltaTime, Space.World);
            yield return null;
        }

        gameObject.SetActive(false);
    }

    private bool GiocatoreVicino()
    {
        if (giocatore == null) return false;
        if (Vector3.Distance(giocatore.position, modello.position) > distanzaRaccolta) return false;

        Transform occhi = cameraGiocatore != null ? cameraGiocatore.transform : giocatore;
        Vector3 direzione = (modello.position - occhi.position).normalized;
        return Vector3.Angle(occhi.forward, direzione) <= angoloVisuale;
    }

    private bool RaccogliPremuto()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(tastoRaccogli);
#endif
    }

    private void OnDisable()
    {
        MessaggiSchermo.NascondiSuggerimento(this);
    }

    private void OnDestroy()
    {
        registro.Remove(Id);
    }
}