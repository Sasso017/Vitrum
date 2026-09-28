using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Rappresenta la sequenza di fotogrammi (estratti da una GIF) di UNA slide.
/// Invece di trascinare a mano ogni singolo sprite, si indica solo il percorso
/// della cartella dentro Resources: i fotogrammi vengono caricati e ordinati
/// automaticamente.
/// </summary>
[Serializable]
public class TextGifSequence
{
    [Tooltip("Percorso della cartella dentro una cartella 'Resources', SENZA includere 'Resources/' e senza estensione. Esempio: se i PNG sono in Assets/Resources/Cutscene/Gif1/, scrivi 'Cutscene/Gif1'")]
    public string resourcesFolderPath;

    [Tooltip("Quanti fotogrammi al secondo vengono mostrati (velocità dell'animazione)")]
    public float framesPerSecond = 12f;

    // Fotogrammi collegati in anticipo nell'Editor (menu ⋮ del componente → "Precarica fotogrammi"):
    // vengono caricati insieme alla scena, senza bloccare il gioco all'avvio della cutscene
    [HideInInspector] public Sprite[] fotogrammiPrecaricati;

    // Fotogrammi usati durante il gioco, non modificare a mano
    [NonSerialized] public Sprite[] frames;
}

/// <summary>
/// Gestisce una cutscene iniziale composta da una sequenza di immagini,
/// ognuna accompagnata da una GIF di testo diversa, i cui fotogrammi vengono
/// caricati da cartelle Resources e riprodotti manualmente
/// (senza usare Animator/Animation di Unity).
///
/// Flusso per ogni slide:
/// 1. Fade-in del pannello (immagine visibile).
/// 2. Dopo un breve ritardo (saltabile con un click) parte la GIF di testo.
/// 3. Un click mentre la GIF sta ancora animandosi la completa istantaneamente.
/// 4. Un click a GIF terminata fa partire il fade-out e passa alla slide successiva.
/// 5. Dopo l'ultima slide, viene caricata (se impostata) la scena successiva con dissolvenza.
///
/// Per evitare blocchi all'avvio, all'inizio vengono caricati solo i fotogrammi della
/// prima GIF: le altre si caricano in sottofondo, una per frame, durante la cutscene.
/// </summary>
public class CutsceneManager : MonoBehaviour
{
    [Header("Riferimenti UI")]
    [Tooltip("Il CanvasGroup del pannello che contiene sia l'immagine che la GIF di testo, usato per il fade")]
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [Tooltip("L'Image UI che mostrerà le immagini della cutscene")]
    [SerializeField] private Image cutsceneImage;

    [Tooltip("L'Image UI che mostrerà i fotogrammi della GIF di testo (senza Animator)")]
    [SerializeField] private Image textGifImage;

    [Tooltip("Il CanvasGroup attaccato allo stesso oggetto di textGifImage, usato per farla comparire con un ritardo")]
    [SerializeField] private CanvasGroup textGifCanvasGroup;

    [Header("Contenuti della cutscene")]
    [Tooltip("Le sprite da mostrare in sequenza, nell'ordine desiderato")]
    [SerializeField] private Sprite[] cutsceneSprites;

    [Tooltip("La sequenza di fotogrammi della GIF di testo per ogni slide (stessa lunghezza di cutsceneSprites). Basta indicare la cartella Resources, i fotogrammi si caricano da soli")]
    [SerializeField] private TextGifSequence[] textGifSequences;

    [Header("Impostazioni Fade")]
    [Tooltip("Durata in secondi del fade-in e del fade-out")]
    [SerializeField] private float fadeDuration = 1f;

    [Tooltip("Tempo minimo (in secondi) dopo la comparsa del testo prima che il click possa avere effetto, per evitare click accidentali troppo rapidi")]
    [SerializeField] private float minTimeBeforeInput = 0.3f;

    [Tooltip("Quanti secondi aspettare, dopo che l'immagine è comparsa, prima di far comparire la GIF di testo (un click salta l'attesa)")]
    [SerializeField] private float gifAppearDelay = 0.5f;

    [Header("Suggerimento per continuare")]
    [Tooltip("Mostra in basso a destra un riquadro che invita a cliccare, quando si può passare alla slide successiva")]
    [SerializeField] private bool mostraSuggerimento = true;
    [SerializeField] private string testoSuggerimento = "Premi Click Sinistro per continuare";
    [Tooltip("Lo stesso font usato nei riquadri di testo della cutscene (se vuoto, il font di default)")]
    [SerializeField] private TMP_FontAsset fontSuggerimento;
    [SerializeField] private float dimensioneTestoSuggerimento = 24f;
    [Tooltip("Distanza dall'angolo in basso a destra")]
    [SerializeField] private Vector2 margineSuggerimento = new Vector2(40f, 40f);
    [SerializeField] private Color coloreFondo = new Color(0f, 0f, 0f, 0.9f);
    [SerializeField] private Color coloreTestoSuggerimento = Color.white;

    [Header("Precaricamento (Editor)")]
    [Tooltip("Durante 'Precarica fotogrammi' toglie i fotogrammi vuoti all'inizio di ogni GIF e accorcia le lunghe pause iniziali")]
    [SerializeField] private bool rimuoviAttesaIniziale = true;
    [Tooltip("Pausa massima (in secondi) tenuta all'inizio di una GIF quando i primi fotogrammi sono tutti uguali")]
    [SerializeField] private float pausaInizialeMassima = 0.5f;

    [Header("Cosa fare alla fine della cutscene")]
    [Tooltip("Nome della scena da caricare quando la cutscene finisce (lascia vuoto per non caricare nulla)")]
    [SerializeField] private string nextSceneName = "";

    // Massimo deltaTime usato nelle dissolvenze: evita che un frame lento (es. dopo un caricamento)
    // faccia saltare la dissolvenza tutta in una volta
    private const float MaxDeltaFade = 1f / 30f;

    // Stato interno
    private int currentIndex = 0;
    private bool isTransitioning = false;  // true durante fade-in/fade-out (click ignorati)
    private bool isGifPlaying = false;     // true mentre la GIF di testo sta ancora animandosi
    private bool gifFullyShown = false;    // true quando la GIF ha finito e si può avanzare
    private Coroutine gifCoroutine;
    private CanvasGroup gruppoSuggerimento;

    private void Awake()
    {
        if (panelCanvasGroup == null)
            Debug.LogError("[CutsceneManager] panelCanvasGroup non assegnato nell'Inspector!");

        if (cutsceneImage == null)
            Debug.LogError("[CutsceneManager] cutsceneImage non assegnata nell'Inspector!");

        if (textGifImage == null)
            Debug.LogError("[CutsceneManager] textGifImage non assegnata nell'Inspector!");

        if (textGifCanvasGroup == null)
            Debug.LogError("[CutsceneManager] textGifCanvasGroup non assegnato nell'Inspector!");

        if (cutsceneSprites == null || cutsceneSprites.Length == 0)
            Debug.LogError("[CutsceneManager] Nessuna sprite assegnata nell'array cutsceneSprites!");

        if (textGifSequences == null || textGifSequences.Length != cutsceneSprites.Length)
            Debug.LogError("[CutsceneManager] L'array textGifSequences deve avere la stessa lunghezza di cutsceneSprites!");

        // All'avvio serve solo la prima GIF: le altre si caricano durante la cutscene
        EnsureSequenceLoaded(0);
    }

    private void Start()
    {
        panelCanvasGroup.alpha = 0f;
        textGifCanvasGroup.alpha = 0f;

        if (mostraSuggerimento) CreaSuggerimento();

        StartCoroutine(LoadRemainingSequences());
        StartCoroutine(PlaySequence());
    }

    private void Update()
    {
        AggiornaSuggerimento();

        if (isTransitioning || !Input.GetMouseButtonDown(0))
            return;

        if (isGifPlaying)
        {
            // Click mentre la GIF sta ancora animandosi: salta subito all'ultimo fotogramma
            CompleteGifInstantly();
        }
        else if (gifFullyShown)
        {
            // Click a GIF terminata: avanza alla slide successiva
            gifFullyShown = false;
            StartCoroutine(AdvanceToNextSlide());
        }
    }

    // ---------- Suggerimento "Premi Click Sinistro per continuare" ----------

    private void AggiornaSuggerimento()
    {
        if (gruppoSuggerimento == null) return;

        // Visibile solo quando il testo della slide è completo e si può andare avanti
        bool visibile = gifFullyShown && !isTransitioning;

        if (visibile)
        {
            // Compare con una breve dissolvenza e resta fisso
            gruppoSuggerimento.alpha = Mathf.MoveTowards(gruppoSuggerimento.alpha, 1f, Time.unscaledDeltaTime * 3f);
        }
        else
        {
            gruppoSuggerimento.alpha = Mathf.MoveTowards(gruppoSuggerimento.alpha, 0f, Time.unscaledDeltaTime * 6f);
        }
    }

    /// <summary>Crea il riquadro in basso a destra: fondo nero e testo bianco.</summary>
    private void CreaSuggerimento()
    {
        GameObject canvasObj = new GameObject("Suggerimento_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasObj.transform.SetParent(transform, false);

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50; // Sopra la cutscene, sotto la dissolvenza tra le scene

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gruppoSuggerimento = canvasObj.GetComponent<CanvasGroup>();
        gruppoSuggerimento.alpha = 0f;
        gruppoSuggerimento.blocksRaycasts = false;
        gruppoSuggerimento.interactable = false;

        // Fondo nero che si adatta alla lunghezza del testo
        GameObject fondo = new GameObject("Fondo", typeof(RectTransform), typeof(Image),
                                          typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        fondo.transform.SetParent(canvasObj.transform, false);
        RectTransform rf = fondo.GetComponent<RectTransform>();
        rf.anchorMin = rf.anchorMax = rf.pivot = new Vector2(1f, 0f);
        rf.anchoredPosition = new Vector2(-margineSuggerimento.x, margineSuggerimento.y);
        Image imgFondo = fondo.GetComponent<Image>();
        imgFondo.color = coloreFondo;
        imgFondo.raycastTarget = false;

        HorizontalLayoutGroup lf = fondo.GetComponent<HorizontalLayoutGroup>();
        lf.padding = new RectOffset(22, 22, 12, 12);
        lf.childAlignment = TextAnchor.MiddleCenter;
        lf.childControlWidth = lf.childControlHeight = true;
        lf.childForceExpandWidth = lf.childForceExpandHeight = false;
        ContentSizeFitter ff = fondo.GetComponent<ContentSizeFitter>();
        ff.horizontalFit = ff.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Testo
        GameObject oggettoTesto = new GameObject("Testo", typeof(RectTransform), typeof(TextMeshProUGUI));
        oggettoTesto.transform.SetParent(fondo.transform, false);
        TextMeshProUGUI testo = oggettoTesto.GetComponent<TextMeshProUGUI>();
        if (fontSuggerimento != null) testo.font = fontSuggerimento;
        testo.text = testoSuggerimento;
        testo.fontSize = dimensioneTestoSuggerimento;
        testo.color = coloreTestoSuggerimento;
        testo.fontStyle = FontStyles.Normal;

        // Il colore finale è moltiplicato per il colore "Face" del materiale del font:
        // se il materiale condiviso è stato colorato (es. per le scritte del menu), il testo non sarebbe bianco.
        // Usiamo una copia del materiale con il Face bianco e senza contorno.
        Material mat = testo.fontMaterial; // Crea una copia solo per questo testo
        mat.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
        if (mat.HasProperty(ShaderUtilities.ID_OutlineWidth)) mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
        testo.fontMaterial = mat;
        testo.alignment = TextAlignmentOptions.Center;
        testo.enableWordWrapping = false;
        testo.raycastTarget = false;
    }

    // ---------- Caricamento dei fotogrammi ----------

    /// <summary>
    /// Carica in sottofondo le GIF delle slide successive, una per frame,
    /// così il gioco non si blocca.
    /// </summary>
    private IEnumerator LoadRemainingSequences()
    {
        if (textGifSequences == null) yield break;

        for (int i = 1; i < textGifSequences.Length; i++)
        {
            yield return null; // Un frame di respiro tra un caricamento e l'altro
            EnsureSequenceLoaded(i);
        }
    }

    /// <summary>
    /// Carica i fotogrammi della sequenza indicata, se non sono già stati caricati.
    /// I fotogrammi vengono ordinati per nome (i file vanno numerati con zeri iniziali,
    /// es. frame_001, frame_002, ... frame_095).
    /// </summary>
    private void EnsureSequenceLoaded(int index)
    {
        if (textGifSequences == null || index < 0 || index >= textGifSequences.Length)
            return;

        TextGifSequence sequence = textGifSequences[index];
        if (sequence == null || sequence.frames != null)
            return; // Già caricata (o sequenza mancante)

        // Fotogrammi già collegati nell'Editor: nessun caricamento durante il gioco
        if (sequence.fotogrammiPrecaricati != null && sequence.fotogrammiPrecaricati.Length > 0)
        {
            sequence.frames = sequence.fotogrammiPrecaricati;
            return;
        }

        // Altrimenti caricamento da Resources (più lento: blocca il gioco per un momento)
        Debug.LogWarning($"[CutsceneManager] La sequenza {index} non è precaricata: usa il menu del componente → " +
                         "'Precarica fotogrammi' per evitare il blocco all'avvio della cutscene.", this);

        if (string.IsNullOrEmpty(sequence.resourcesFolderPath))
        {
            Debug.LogWarning($"[CutsceneManager] La sequenza {index} non ha un resourcesFolderPath impostato, verrà saltata.");
            sequence.frames = new Sprite[0];
            return;
        }

        Sprite[] loaded = Resources.LoadAll<Sprite>(sequence.resourcesFolderPath);

        if (loaded == null || loaded.Length == 0)
        {
            Debug.LogError($"[CutsceneManager] Nessuno sprite trovato in Resources/{sequence.resourcesFolderPath}. Controlla il percorso e che i file siano impostati come Sprite (2D and UI).");
            sequence.frames = new Sprite[0];
            return;
        }

        sequence.frames = loaded.OrderBy(s => s.name).ToArray();
        Debug.Log($"[CutsceneManager] Caricati {sequence.frames.Length} fotogrammi da Resources/{sequence.resourcesFolderPath}");
    }

    // ---------- Sequenza delle slide ----------

    /// <summary>
    /// Mostra la prima slide (fade-in + avvio della GIF di testo).
    /// </summary>
    private IEnumerator PlaySequence()
    {
        isTransitioning = true;

        // Un frame di attesa: lascia terminare il caricamento della scena prima della dissolvenza
        yield return null;

        yield return ShowSlide(currentIndex);
    }

    /// <summary>
    /// Fade-out della slide corrente, passaggio alla successiva (fade-in + nuova GIF),
    /// oppure fine cutscene se non ci sono più slide.
    /// </summary>
    private IEnumerator AdvanceToNextSlide()
    {
        isTransitioning = true;

        yield return StartCoroutine(FadeCanvasGroup(1f, 0f, fadeDuration));

        currentIndex++;

        if (currentIndex < cutsceneSprites.Length)
        {
            yield return ShowSlide(currentIndex);
        }
        else
        {
            EndCutscene();
        }
    }

    /// <summary>
    /// Fade-in della slide, breve attesa (saltabile con un click), poi avvio della GIF di testo.
    /// </summary>
    private IEnumerator ShowSlide(int index)
    {
        EnsureSequenceLoaded(index); // Nel caso il caricamento in sottofondo non sia ancora arrivato qui
        ShowSlideContent(index);

        yield return StartCoroutine(FadeCanvasGroup(0f, 1f, fadeDuration));

        yield return WaitOrClick(gifAppearDelay);
        textGifCanvasGroup.alpha = 1f;
        StartGifSequence(index);

        // Breve protezione contro i click accidentali, mentre la GIF è già partita
        yield return new WaitForSeconds(minTimeBeforeInput);
        isTransitioning = false;
    }

    /// <summary>
    /// Aspetta il tempo indicato, ma si interrompe subito se il giocatore clicca.
    /// </summary>
    private IEnumerator WaitOrClick(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (Input.GetMouseButtonDown(0)) yield break;
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// Imposta la sprite corretta per l'indice specificato, e pre-carica il primo
    /// fotogramma della GIF di testo corrispondente (ma la tiene invisibile, alpha 0).
    /// Questo evita che rimanga visibile per un istante l'ultimo fotogramma della
    /// GIF della slide precedente.
    /// </summary>
    private void ShowSlideContent(int index)
    {
        cutsceneImage.sprite = cutsceneSprites[index];

        TextGifSequence sequence = textGifSequences[index];
        if (sequence != null && sequence.frames != null && sequence.frames.Length > 0)
        {
            textGifImage.sprite = sequence.frames[0];
        }
        else
        {
            textGifImage.sprite = null;
        }

        // Nasconde la GIF di testo finché non decidiamo di rivelarla
        textGifCanvasGroup.alpha = 0f;
    }

    /// <summary>
    /// Avvia manualmente la riproduzione della sequenza di fotogrammi
    /// della GIF di testo corrispondente alla slide indicata.
    /// </summary>
    private void StartGifSequence(int index)
    {
        TextGifSequence sequence = textGifSequences[index];

        if (sequence == null || sequence.frames == null || sequence.frames.Length == 0)
        {
            Debug.LogWarning($"[CutsceneManager] Nessun fotogramma disponibile per l'indice {index}, salto direttamente allo stato 'completato'.");
            textGifImage.sprite = null;
            isGifPlaying = false;
            gifFullyShown = true;
            return;
        }

        if (gifCoroutine != null)
        {
            StopCoroutine(gifCoroutine);
        }

        gifCoroutine = StartCoroutine(PlayFrames(sequence));
    }

    /// <summary>
    /// Mostra un fotogramma alla volta, alla velocità impostata, fino all'ultimo.
    /// Si ferma sull'ultimo fotogramma (nessun loop).
    /// </summary>
    private IEnumerator PlayFrames(TextGifSequence sequence)
    {
        isGifPlaying = true;
        gifFullyShown = false;

        float delayPerFrame = 1f / Mathf.Max(sequence.framesPerSecond, 0.01f);

        for (int i = 0; i < sequence.frames.Length; i++)
        {
            textGifImage.sprite = sequence.frames[i];
            yield return new WaitForSeconds(delayPerFrame);
        }

        // Resta sull'ultimo fotogramma, animazione conclusa
        isGifPlaying = false;
        gifFullyShown = true;
    }

    /// <summary>
    /// Salta immediatamente all'ultimo fotogramma della sequenza corrente,
    /// usata quando il giocatore clicca mentre l'animazione è ancora in corso.
    /// </summary>
    private void CompleteGifInstantly()
    {
        if (gifCoroutine != null)
        {
            StopCoroutine(gifCoroutine);
        }

        TextGifSequence sequence = textGifSequences[currentIndex];
        if (sequence != null && sequence.frames != null && sequence.frames.Length > 0)
        {
            textGifImage.sprite = sequence.frames[sequence.frames.Length - 1];
        }

        isGifPlaying = false;
        gifFullyShown = true;
    }

    /// <summary>
    /// Anima il valore alpha del CanvasGroup da "from" a "to" nel tempo "duration".
    /// Il deltaTime è limitato, così un frame lento non fa saltare la dissolvenza.
    /// </summary>
    private IEnumerator FadeCanvasGroup(float from, float to, float duration)
    {
        float elapsed = 0f;
        panelCanvasGroup.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Mathf.Min(Time.deltaTime, MaxDeltaFade);
            float t = Mathf.Clamp01(elapsed / duration);
            panelCanvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        panelCanvasGroup.alpha = to;
    }

    /// <summary>
    /// Chiamata quando tutte le slide sono state mostrate.
    /// Carica la scena successiva con dissolvenza, se specificata.
    /// </summary>
    private void EndCutscene()
    {
        gifFullyShown = false;
        Debug.Log("[CutsceneManager] Cutscene terminata.");

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneFader.Instance.CaricaScena(nextSceneName);
        }
    }

#if UNITY_EDITOR
    // ---------- Strumenti dell'Editor ----------

    /// <summary>
    /// Collega in anticipo alla scena tutti i fotogrammi delle GIF, leggendoli dalle cartelle Resources.
    /// Va rifatto solo se si aggiungono o modificano i fotogrammi.
    /// </summary>
    [ContextMenu("Precarica fotogrammi")]
    private void PrecaricaFotogrammi()
    {
        if (textGifSequences == null) return;

        UnityEditor.Undo.RecordObject(this, "Precarica fotogrammi");
        var riepilogo = new System.Text.StringBuilder();

        for (int i = 0; i < textGifSequences.Length; i++)
        {
            TextGifSequence seq = textGifSequences[i];
            if (seq == null) continue;

            if (string.IsNullOrEmpty(seq.resourcesFolderPath))
            {
                riepilogo.AppendLine($"Sequenza {i}: nessuna cartella indicata");
                continue;
            }

            Sprite[] caricati = Resources.LoadAll<Sprite>(seq.resourcesFolderPath).OrderBy(sp => sp.name).ToArray();
            int vuoti = 0, doppi = 0;

            if (rimuoviAttesaIniziale && caricati.Length > 1)
                caricati = TogliAttesaIniziale(caricati, seq.framesPerSecond, out vuoti, out doppi);

            seq.fotogrammiPrecaricati = caricati;

            float durata = caricati.Length / Mathf.Max(seq.framesPerSecond, 0.01f);
            riepilogo.Append($"Sequenza {i}: {caricati.Length} fotogrammi, {durata:0.0}s ({seq.resourcesFolderPath})");
            if (vuoti > 0 || doppi > 0)
                riepilogo.Append($"  → rimossi all'inizio: {vuoti} vuoti, {doppi} ripetuti");
            riepilogo.AppendLine();
        }

        riepilogo.AppendLine();
        riepilogo.AppendLine($"Attesa prima del testo (Gif Appear Delay): {gifAppearDelay:0.0}s");
        riepilogo.AppendLine($"Durata delle dissolvenze (Fade Duration): {fadeDuration:0.0}s");

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        UnityEditor.EditorUtility.DisplayDialog("Precarica fotogrammi",
            riepilogo + "\nRicordati di salvare la scena (Ctrl+S).", "OK");
    }

    /// <summary>
    /// Toglie dall'inizio della sequenza i fotogrammi completamente trasparenti e,
    /// se i primi fotogrammi sono tutti uguali, ne tiene solo quanti bastano per "pausaInizialeMassima" secondi.
    /// </summary>
    private Sprite[] TogliAttesaIniziale(Sprite[] fotogrammi, float fps, out int vuoti, out int doppi)
    {
        vuoti = 0;
        doppi = 0;
        var firme = new string[fotogrammi.Length];

        // 1. Fotogrammi vuoti (trasparenti) all'inizio
        int inizio = 0;
        while (inizio < fotogrammi.Length - 1)
        {
            if (!AnalizzaFotogramma(fotogrammi[inizio], out firme[inizio])) break; // Non vuoto: stop
            inizio++;
        }
        vuoti = inizio;

        // 2. Fotogrammi identici al primo visibile, oltre la pausa massima
        AnalizzaFotogramma(fotogrammi[inizio], out firme[inizio]);
        int uguali = 1;
        while (inizio + uguali < fotogrammi.Length)
        {
            AnalizzaFotogramma(fotogrammi[inizio + uguali], out firme[inizio + uguali]);
            if (firme[inizio + uguali] != firme[inizio]) break;
            uguali++;
        }

        int daTenere = Mathf.Max(1, Mathf.CeilToInt(pausaInizialeMassima * Mathf.Max(fps, 1f)));
        doppi = Mathf.Max(0, uguali - daTenere);

        return fotogrammi.Skip(inizio + doppi).ToArray();
    }

    /// <summary>
    /// Legge l'immagine di un fotogramma dal file (senza modificare le impostazioni di importazione).
    /// Restituisce true se è completamente trasparente; "firma" identifica il contenuto per confrontare fotogrammi uguali.
    /// </summary>
    private static bool AnalizzaFotogramma(Sprite sprite, out string firma)
    {
        firma = sprite != null ? sprite.name : "";
        if (sprite == null || sprite.texture == null) return false;

        string percorso = UnityEditor.AssetDatabase.GetAssetPath(sprite.texture);
        if (string.IsNullOrEmpty(percorso) || !System.IO.File.Exists(percorso)) return false;

        var tex = new Texture2D(2, 2);
        try
        {
            if (!tex.LoadImage(System.IO.File.ReadAllBytes(percorso))) return false;

            Color32[] pixel = tex.GetPixels32();
            byte alfaMassimo = 0;
            unchecked
            {
                // Firma veloce del contenuto: somma pesata di un pixel ogni 7
                long h = 17;
                for (int i = 0; i < pixel.Length; i += 7)
                {
                    Color32 p = pixel[i];
                    if (p.a > alfaMassimo) alfaMassimo = p.a;
                    h = h * 31 + p.r; h = h * 31 + p.g; h = h * 31 + p.b; h = h * 31 + p.a;
                }
                for (int i = 0; i < pixel.Length; i++)
                    if (pixel[i].a > alfaMassimo) alfaMassimo = pixel[i].a;
                firma = h.ToString();
            }
            return alfaMassimo < 8; // Praticamente trasparente
        }
        finally
        {
            DestroyImmediate(tex);
        }
    }

    [ContextMenu("Svuota fotogrammi precaricati")]
    private void SvuotaFotogrammi()
    {
        if (textGifSequences == null) return;
        UnityEditor.Undo.RecordObject(this, "Svuota fotogrammi");
        foreach (TextGifSequence seq in textGifSequences)
            if (seq != null) seq.fotogrammiPrecaricati = null;
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif
}