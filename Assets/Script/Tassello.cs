using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
        }
    }

    private void Update()
    {
        if (raccolto) return;

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