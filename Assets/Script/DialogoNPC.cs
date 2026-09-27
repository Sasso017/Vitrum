using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Dialogo con un NPC. Avvicinandosi compare "Premi E per parlare"; premendo E si apre il riquadro
/// di dialogo, con il testo che compare lettera per lettera.
/// Click sinistro, E o Spazio: se il testo sta ancora comparendo lo completa, altrimenti passa alla battuta successiva.
/// Le battute si scrivono nell'Inspector. Dopo la prima conversazione si possono usare battute diverse (più brevi).
/// Il riquadro viene creato via codice: non serve preparare nulla nel Canvas.
/// </summary>
public class DialogoNPC : MonoBehaviour
{
    /// <summary>True mentre un dialogo è aperto (il menu di pausa è disattivato).</summary>
    public static bool InCorso { get; private set; }

    [Header("Chi parla")]
    [SerializeField] private string nomeNPC = "???";

    [Header("Battute")]
    [Tooltip("Le battute della prima conversazione, nell'ordine")]
    [TextArea(2, 5)]
    [SerializeField]
    private string[] battute =
    {
        "Un altro viandante intrappolato tra queste mura...",
        "Le porte si apriranno solo per chi possiede entrambe le chiavi."
    };
    [Tooltip("Battute usate quando gli si riparla dopo la prima volta (se vuoto, si ripetono quelle iniziali)")]
    [TextArea(2, 5)]
    [SerializeField]
    private string[] battuteSuccessive =
    {
        "Cerca bene, viandante. La basilica nasconde più di quanto mostri."
    };

    [Header("Battute legate ai minigiochi")]
    [Tooltip("Reazione al mosaico completato: detta una sola volta, la prima volta che gli si parla dopo averlo completato")]
    [TextArea(2, 5)]
    [SerializeField]
    private string[] battuteMosaico =
    {
        "Hai ricomposto il mosaico... Pochi hanno avuto la pazienza di cercare ogni tassello.",
        "Quella chiave apparteneva a qualcuno che non è mai più uscito da qui."
    };
    [Tooltip("Reazione al gioco del 15 completato: detta una sola volta, la prima volta che gli si parla dopo averlo completato")]
    [TextArea(2, 5)]
    [SerializeField]
    private string[] battuteGioco15 =
    {
        "Hai risolto l'enigma del leggio. Mente acuta, la tua.",
        "Ma l'ingegno da solo non basta per lasciare questo luogo."
    };
    [Tooltip("Ripetute ogni volta che gli si parla quando entrambi i minigiochi sono completati e le reazioni sono già state dette")]
    [TextArea(2, 5)]
    [SerializeField]
    private string[] battuteTuttoCompletato =
    {
        "Cosa aspetti? La porta ti attende. Vattene, finché puoi."
    };

    [Header("Richiamo (la prima volta che vede il giocatore)")]
    [Tooltip("Se attivo, la prima volta che vede il giocatore l'NPC lo chiama")]
    [SerializeField] private bool usaRichiamo = true;
    [Tooltip("Frase del richiamo (lascia vuoto per non mostrarla)")]
    [TextArea(1, 3)]
    [SerializeField] private string testoRichiamo = "Hey, vieni qui!";
    [Tooltip("Distanza entro cui l'NPC può notare il giocatore")]
    [SerializeField] private float distanzaRichiamo = 10f;
    [Tooltip("Per quanti secondi resta visibile il richiamo")]
    [SerializeField] private float durataRichiamo = 3f;
    [Tooltip("Altezza degli occhi dell'NPC rispetto alla sua base (serve a controllare che veda davvero il giocatore)")]
    [SerializeField] private float altezzaOcchi = 1.6f;
    [Tooltip("Suono del richiamo (opzionale, es. una voce)")]
    [SerializeField] private AudioClip suonoRichiamo;
    [Tooltip("Se attivo, dopo ogni minigioco completato l'NPC richiama il giocatore quando lo vede")]
    [SerializeField] private bool richiamaDopoOgniMinigioco = true;
    [TextArea(1, 3)]
    [SerializeField] private string testoRichiamoMinigioco = "Hai fatto qualcosa di interessante... vieni a parlarmi!";

    [Header("Interazione")]
    [SerializeField] private float distanzaInterazione = 2.5f;
    [Tooltip("Il giocatore deve guardare verso l'NPC entro questo angolo (gradi)")]
    [SerializeField] private float angoloVisuale = 50f;
    [SerializeField] private string testoSuggerimento = "Premi E per parlare";
    [Tooltip("Script da disattivare durante il dialogo (es. FPSController di Carlo, Accovacciamento)")]
    [SerializeField] private Behaviour[] controlliGiocatore;

    [Header("Testo")]
    [Tooltip("Lettere al secondo dell'effetto macchina da scrivere (0 = testo subito completo)")]
    [SerializeField] private float lettereAlSecondo = 40f;
    [Tooltip("Suono breve riprodotto mentre il testo compare (opzionale, es. un leggero 'blip')")]
    [SerializeField] private AudioClip suonoVoce;
    [Tooltip("Ogni quante lettere riprodurre il suono della voce")]
    [SerializeField] private int suonoOgniLettere = 3;

    [Header("Aspetto del riquadro")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private float dimensioneTesto = 34f;
    [SerializeField] private Color coloreNome = new Color(1f, 0.8f, 0.35f);
    [SerializeField] private Color coloreTesto = new Color(1f, 0.95f, 0.88f);
    [SerializeField] private Color coloreSfondo = new Color(0.03f, 0.02f, 0.02f, 0.85f);

    private Transform giocatore;
    private Camera cameraGiocatore;
    private bool giaParlato = false;
    private bool giaRichiamato = false;
    // Minigiochi di cui l'NPC ha già detto la sua reazione, e per cui ha già richiamato il giocatore
    private readonly HashSet<Enigma> commentati = new HashSet<Enigma>();
    private readonly HashSet<Enigma> richiamati = new HashSet<Enigma>();
    private Coroutine routineRichiamo;

    // Interfaccia
    private CanvasGroup gruppo;
    private TextMeshProUGUI testoNome;
    private TextMeshProUGUI testoBattuta;
    private TextMeshProUGUI indicatoreAvanti;

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) giocatore = playerObj.transform;
        cameraGiocatore = Camera.main;

        CostruisciInterfaccia();
    }

    private void Update()
    {
        if (InCorso) return; // Durante il dialogo se ne occupa la coroutine

        bool puoInteragire = !PauseMenu.IsPaused && !SceneFader.InTransizione &&
                             !StazioneMinigioco.InUso && !IntroBasilica.InCorso;

        // La prima volta che vede il giocatore, lo chiama
        if (puoInteragire && routineRichiamo == null)
        {
            if (usaRichiamo && !giaRichiamato && !giaParlato && VedeIlGiocatore())
            {
                // Prima volta che vede il giocatore
                giaRichiamato = true;
                routineRichiamo = StartCoroutine(Richiamo(testoRichiamo));
            }
            else if (richiamaDopoOgniMinigioco && giaParlato)
            {
                // Minigioco appena completato e reazione non ancora detta: l'NPC chiama il giocatore
                bool novita = false;
                foreach (Enigma e in new[] { Enigma.Mosaico, Enigma.Gioco15 })
                {
                    if (Completato(e) && !commentati.Contains(e) && !richiamati.Contains(e) && HaTesto(BattuteDi(e)))
                        novita = true;
                }

                if (novita && VedeIlGiocatore())
                {
                    foreach (Enigma e in new[] { Enigma.Mosaico, Enigma.Gioco15 })
                        if (Completato(e)) richiamati.Add(e);
                    routineRichiamo = StartCoroutine(Richiamo(testoRichiamoMinigioco));
                }
            }
        }

        if (!puoInteragire || !GiocatoreVicino())
        {
            MessaggiSchermo.NascondiSuggerimento(this);
            return;
        }

        MessaggiSchermo.MostraSuggerimento(this, testoSuggerimento);

        if (TastoEPremuto())
        {
            // Se il richiamo è ancora a schermo, lascia subito il posto al dialogo
            if (routineRichiamo != null)
            {
                StopCoroutine(routineRichiamo);
                routineRichiamo = null;
            }
            StartCoroutine(Dialogo());
        }
    }

    // ---------- Scelta delle battute ----------

    /// <summary>
    /// Sceglie cosa dice l'NPC in base ai minigiochi completati:
    /// presentazione → reazione a ciascun minigioco (una volta sola) → frase finale ripetuta.
    /// Se ci sono più reazioni nuove, vengono dette tutte nella stessa conversazione.
    /// </summary>
    private string[] ScegliBattute()
    {
        // Prima conversazione: sempre la presentazione
        if (!giaParlato) return battute;

        // Reazioni ai minigiochi completati e non ancora commentati (nell'ordine del gioco)
        var reazioni = new List<string>();
        foreach (Enigma e in new[] { Enigma.Mosaico, Enigma.Gioco15 })
        {
            if (!Completato(e) || commentati.Contains(e)) continue;
            string[] righe = BattuteDi(e);
            if (HaTesto(righe)) reazioni.AddRange(righe);
            commentati.Add(e);
        }
        if (reazioni.Count > 0) return reazioni.ToArray();

        // Tutto completato: frase finale, ripetuta
        if (Completato(Enigma.Mosaico) && Completato(Enigma.Gioco15) && HaTesto(battuteTuttoCompletato))
            return battuteTuttoCompletato;

        // Altrimenti le battute generiche per quando gli si riparla
        return HaTesto(battuteSuccessive) ? battuteSuccessive : battute;
    }

    private string[] BattuteDi(Enigma e)
    {
        switch (e)
        {
            case Enigma.Mosaico: return battuteMosaico;
            case Enigma.Gioco15: return battuteGioco15;
            default: return null;
        }
    }

    private static bool Completato(Enigma e)
    {
        return GameManager.instance != null && GameManager.instance.IsCompletato(e);
    }

    private static bool HaTesto(string[] righe)
    {
        if (righe == null) return false;
        foreach (string r in righe)
            if (!string.IsNullOrWhiteSpace(r)) return true;
        return false;
    }

    // ---------- Richiamo ----------

    /// <summary>
    /// L'NPC chiama il giocatore nel riquadro di dialogo, senza bloccarlo:
    /// il giocatore può continuare a muoversi e il riquadro sparisce da solo.
    /// </summary>
    private IEnumerator Richiamo(string testo)
    {
        if (suonoRichiamo != null && SFXPlayer.Instance != null) SFXPlayer.Instance.Play(suonoRichiamo);
        if (string.IsNullOrEmpty(testo)) { routineRichiamo = null; yield break; }

        testoNome.text = nomeNPC;
        testoNome.gameObject.SetActive(!string.IsNullOrEmpty(nomeNPC));
        indicatoreAvanti.enabled = false;
        testoBattuta.text = testo;
        testoBattuta.maxVisibleCharacters = int.MaxValue;

        yield return Dissolvi(gruppo.alpha, 1f, 0.2f);
        yield return new WaitForSeconds(durataRichiamo);
        yield return Dissolvi(1f, 0f, 0.4f);

        routineRichiamo = null;
    }

    /// <summary>True se il giocatore è abbastanza vicino e non ci sono ostacoli tra lui e l'NPC.</summary>
    private bool VedeIlGiocatore()
    {
        if (giocatore == null) return false;

        Vector3 occhi = transform.position + Vector3.up * altezzaOcchi;
        Vector3 bersaglio = cameraGiocatore != null ? cameraGiocatore.transform.position : giocatore.position + Vector3.up;
        Vector3 direzione = bersaglio - occhi;
        float distanza = direzione.magnitude;
        if (distanza > distanzaRichiamo) return false;

        // Controlla che in mezzo non ci siano muri o colonne (ignorando l'NPC stesso e il giocatore)
        RaycastHit[] colpi = Physics.RaycastAll(occhi, direzione / distanza, distanza, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit c in colpi)
        {
            Transform t = c.transform;
            if (t == transform || t.IsChildOf(transform)) continue;       // L'NPC stesso
            if (t == giocatore || t.IsChildOf(giocatore)) continue;       // Il giocatore
            return false;                                                 // Un ostacolo
        }
        return true;
    }

    // ---------- Dialogo ----------

    private IEnumerator Dialogo()
    {
        string[] righe = ScegliBattute();

        if (righe == null || righe.Length == 0) yield break;

        InCorso = true;
        giaRichiamato = true; // Se gli si parla prima che chiami, il richiamo non serve più
        MessaggiSchermo.NascondiSuggerimento(this);
        SetControlli(false);

        testoNome.text = nomeNPC;
        testoNome.gameObject.SetActive(!string.IsNullOrEmpty(nomeNPC));
        testoBattuta.text = "";
        yield return Dissolvi(gruppo.alpha, 1f, 0.2f);

        // Salta il frame in cui è stato premuto E per aprire il dialogo
        yield return null;

        foreach (string riga in righe)
        {
            if (string.IsNullOrWhiteSpace(riga)) continue;
            yield return MostraBattuta(riga);

            // Battuta completa: aspetta il comando per andare avanti
            indicatoreAvanti.enabled = true;
            while (!AvantiPremuto())
            {
                // L'indicatore lampeggia dolcemente
                indicatoreAvanti.alpha = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
                yield return null;
            }
            indicatoreAvanti.enabled = false;
            yield return null; // Evita che lo stesso click venga letto due volte
        }

        yield return Dissolvi(1f, 0f, 0.2f);

        giaParlato = true;

        // Dopo la prima conversazione il giocatore sa cosa cercare: compare l'overlay dei progressi
        if (GameManager.instance != null)
            GameManager.instance.obiettivoRivelato = true;

        SetControlli(true);
        InCorso = false;
    }

    /// <summary>Scrive la battuta lettera per lettera; un comando la completa subito.</summary>
    private IEnumerator MostraBattuta(string riga)
    {
        indicatoreAvanti.enabled = false;
        testoBattuta.text = riga;
        testoBattuta.ForceMeshUpdate();
        int totale = testoBattuta.textInfo.characterCount;

        if (lettereAlSecondo <= 0f)
        {
            testoBattuta.maxVisibleCharacters = totale;
            yield break;
        }

        testoBattuta.maxVisibleCharacters = 0;
        float visibili = 0f;
        int ultimoSuono = 0;

        while (testoBattuta.maxVisibleCharacters < totale)
        {
            if (AvantiPremuto())
            {
                testoBattuta.maxVisibleCharacters = totale; // Completa subito la battuta
                yield return null; // Il click che ha completato non deve anche far avanzare
                yield break;
            }

            visibili += lettereAlSecondo * Time.unscaledDeltaTime;
            testoBattuta.maxVisibleCharacters = Mathf.Min(totale, Mathf.FloorToInt(visibili));

            // Suono della voce ogni tot lettere
            if (suonoVoce != null && SFXPlayer.Instance != null &&
                testoBattuta.maxVisibleCharacters - ultimoSuono >= Mathf.Max(1, suonoOgniLettere))
            {
                ultimoSuono = testoBattuta.maxVisibleCharacters;
                SFXPlayer.Instance.Play(suonoVoce, 0.5f);
            }

            yield return null;
        }
    }

    private IEnumerator Dissolvi(float da, float a, float durata)
    {
        float t = 0f;
        while (t < durata)
        {
            t += Time.unscaledDeltaTime;
            gruppo.alpha = Mathf.Lerp(da, a, t / durata);
            yield return null;
        }
        gruppo.alpha = a;
    }

    // ---------- Interfaccia ----------

    private void CostruisciInterfaccia()
    {
        GameObject canvasObj = new GameObject("DialogoNPC_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasObj.transform.SetParent(transform, false);

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 450; // Sopra all'overlay dei progressi, sotto le dissolvenze

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gruppo = canvasObj.GetComponent<CanvasGroup>();
        gruppo.alpha = 0f;
        gruppo.blocksRaycasts = false;
        gruppo.interactable = false;

        // Riquadro in basso, largo quanto buona parte dello schermo
        GameObject riquadro = new GameObject("Riquadro", typeof(RectTransform), typeof(Image));
        riquadro.transform.SetParent(canvasObj.transform, false);
        RectTransform rt = riquadro.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.15f, 0f);
        rt.anchorMax = new Vector2(0.85f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 60f);
        rt.sizeDelta = new Vector2(0f, 230f);
        Image sfondo = riquadro.GetComponent<Image>();
        sfondo.color = coloreSfondo;
        sfondo.raycastTarget = false;

        // Nome dell'NPC in alto a sinistra
        testoNome = CreaTesto(riquadro.transform, "Nome", dimensioneTesto * 0.9f, coloreNome,
                              new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -18f), new Vector2(800f, 50f),
                              TextAlignmentOptions.TopLeft);
        testoNome.fontStyle = FontStyles.Bold;

        // Testo della battuta
        testoBattuta = CreaTesto(riquadro.transform, "Battuta", dimensioneTesto, coloreTesto,
                                 new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero,
                                 TextAlignmentOptions.TopLeft);
        RectTransform rtb = testoBattuta.rectTransform;
        rtb.offsetMin = new Vector2(40f, 50f);
        rtb.offsetMax = new Vector2(-40f, -70f);
        testoBattuta.enableWordWrapping = true;

        // Indicatore "continua" in basso a destra
        indicatoreAvanti = CreaTesto(riquadro.transform, "Avanti", dimensioneTesto * 0.7f, coloreNome,
                                     new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 16f), new Vector2(300f, 40f),
                                     TextAlignmentOptions.BottomRight);
        indicatoreAvanti.rectTransform.pivot = new Vector2(1f, 0f);
        indicatoreAvanti.text = "Clic per continuare >>";
        indicatoreAvanti.enabled = false;
    }

    private TextMeshProUGUI CreaTesto(Transform genitore, string nome, float dimensione, Color colore,
                                      Vector2 ancoraMin, Vector2 ancoraMax, Vector2 posizione, Vector2 dimensioni,
                                      TextAlignmentOptions allineamento)
    {
        GameObject obj = new GameObject(nome, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(genitore, false);

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = ancoraMin;
        rt.anchorMax = ancoraMax;
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = posizione;
        rt.sizeDelta = dimensioni;

        TextMeshProUGUI t = obj.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = dimensione;
        t.color = colore;
        t.alignment = allineamento;
        t.raycastTarget = false;
        return t;
    }

    // ---------- Utilità ----------

    private bool GiocatoreVicino()
    {
        if (giocatore == null) return false;
        if (Vector3.Distance(giocatore.position, transform.position) > distanzaInterazione) return false;

        Transform occhi = cameraGiocatore != null ? cameraGiocatore.transform : giocatore;
        Vector3 bersaglio = transform.position + Vector3.up * 1f; // Più o meno all'altezza del busto
        Vector3 direzione = (bersaglio - occhi.position).normalized;
        return Vector3.Angle(occhi.forward, direzione) <= angoloVisuale;
    }

    private void SetControlli(bool attivi)
    {
        if (controlliGiocatore == null) return;
        foreach (Behaviour b in controlliGiocatore)
            if (b != null) b.enabled = attivi;
    }

    private bool TastoEPremuto()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    /// <summary>Click sinistro, E o Spazio.</summary>
    private bool AvantiPremuto()
    {
#if ENABLE_INPUT_SYSTEM
        bool mouse = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool tastiera = Keyboard.current != null &&
                        (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame);
        return mouse || tastiera;
#else
        return Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space);
#endif
    }

    private void OnDisable()
    {
        MessaggiSchermo.NascondiSuggerimento(this);
    }

    private void OnDrawGizmosSelected()
    {
        // Nell'Editor: in azzurro la distanza del richiamo, in giallo quella per parlare
        Gizmos.color = new Color(0.4f, 0.8f, 1f);
        Gizmos.DrawWireSphere(transform.position, distanzaRichiamo);
        Gizmos.color = new Color(1f, 0.85f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, distanzaInterazione);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * altezzaOcchi);
    }

    private void OnDestroy()
    {
        if (InCorso)
        {
            InCorso = false;
            SetControlli(true);
        }
    }
}