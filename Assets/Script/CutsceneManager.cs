using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Rappresenta la sequenza di fotogrammi (estratti da una GIF) di UNA slide.
/// Invece di trascinare a mano ogni singolo sprite, si indica solo il percorso
/// della cartella dentro Resources: i fotogrammi vengono caricati e ordinati
/// automaticamente all'avvio.
/// </summary>
[Serializable]
public class TextGifSequence
{
    [Tooltip("Percorso della cartella dentro una cartella 'Resources', SENZA includere 'Resources/' e senza estensione. Esempio: se i PNG sono in Assets/Resources/Cutscene/Gif1/, scrivi 'Cutscene/Gif1'")]
    public string resourcesFolderPath;

    [Tooltip("Quanti fotogrammi al secondo vengono mostrati (velocità dell'animazione)")]
    public float framesPerSecond = 12f;

    // Popolato automaticamente all'avvio da CutsceneManager, non modificare a mano
    [NonSerialized] public Sprite[] frames;
}

/// <summary>
/// Gestisce una cutscene iniziale composta da una sequenza di immagini,
/// ognuna accompagnata da una GIF di testo diversa, i cui fotogrammi vengono
/// caricati automaticamente da cartelle Resources e riprodotti manualmente
/// (senza usare Animator/Animation di Unity).
///
/// Flusso per ogni slide:
/// 1. Fade-in del pannello (immagine visibile).
/// 2. Parte la sequenza di fotogrammi della GIF di testo corrispondente alla slide.
/// 3. Un click mentre la GIF sta ancora animandosi la completa istantaneamente
///    (salta all'ultimo fotogramma).
/// 4. Un click a GIF terminata fa partire il fade-out e passa alla slide successiva.
/// 5. Dopo l'ultima slide, viene caricata (se impostata) la scena successiva.
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

    [Tooltip("Tempo minimo (in secondi) dopo il fade-in prima che il click possa avere effetto, per evitare click accidentali troppo rapidi")]
    [SerializeField] private float minTimeBeforeInput = 0.3f;

    [Tooltip("Quanti secondi aspettare, dopo che l'immagine è comparsa, prima di far comparire la GIF di testo")]
    [SerializeField] private float gifAppearDelay = 1f;

    [Header("Cosa fare alla fine della cutscene")]
    [Tooltip("Nome della scena da caricare quando la cutscene finisce (lascia vuoto per non caricare nulla)")]
    [SerializeField] private string nextSceneName = "";

    // Stato interno
    private int currentIndex = 0;
    private bool isTransitioning = false;  // true durante fade-in/fade-out (click ignorati)
    private bool isGifPlaying = false;     // true mentre la GIF di testo sta ancora animandosi
    private bool gifFullyShown = false;    // true quando la GIF ha finito e si può avanzare
    private Coroutine gifCoroutine;

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

        LoadAllGifFrames();
    }

    /// <summary>
    /// Carica automaticamente, per ogni slide, tutti gli sprite presenti nella
    /// cartella Resources indicata, ordinandoli per nome (quindi i file vanno
    /// numerati con zeri iniziali, es. frame_001, frame_002, ... frame_095).
    /// </summary>
    private void LoadAllGifFrames()
    {
        if (textGifSequences == null)
            return;

        foreach (TextGifSequence sequence in textGifSequences)
        {
            if (string.IsNullOrEmpty(sequence.resourcesFolderPath))
            {
                Debug.LogWarning("[CutsceneManager] Una sequenza non ha un resourcesFolderPath impostato, verrà saltata.");
                sequence.frames = new Sprite[0];
                continue;
            }

            Sprite[] loaded = Resources.LoadAll<Sprite>(sequence.resourcesFolderPath);

            if (loaded == null || loaded.Length == 0)
            {
                Debug.LogError($"[CutsceneManager] Nessuno sprite trovato in Resources/{sequence.resourcesFolderPath}. Controlla il percorso e che i file siano impostati come Sprite (2D and UI).");
                sequence.frames = new Sprite[0];
                continue;
            }

            // Ordina i fotogrammi per nome, cosi' l'ordine numerico e' rispettato
            // (funziona correttamente solo se i nomi hanno zeri iniziali, es. 001, 002... 095)
            sequence.frames = loaded.OrderBy(s => s.name).ToArray();

            Debug.Log($"[CutsceneManager] Caricati {sequence.frames.Length} fotogrammi da Resources/{sequence.resourcesFolderPath}");
        }
    }

    private void Start()
    {
        panelCanvasGroup.alpha = 0f;
        StartCoroutine(PlaySequence());
    }

    private void Update()
    {
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

    /// <summary>
    /// Mostra la prima slide (fade-in + avvio della GIF di testo).
    /// </summary>
    private IEnumerator PlaySequence()
    {
        isTransitioning = true;

        ShowSlideContent(currentIndex);
        yield return StartCoroutine(FadeCanvasGroup(0f, 1f, fadeDuration));

        yield return new WaitForSeconds(gifAppearDelay);
        textGifCanvasGroup.alpha = 1f;

        yield return new WaitForSeconds(minTimeBeforeInput);
        isTransitioning = false;

        StartGifSequence(currentIndex);
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
            ShowSlideContent(currentIndex);
            yield return StartCoroutine(FadeCanvasGroup(0f, 1f, fadeDuration));

            yield return new WaitForSeconds(gifAppearDelay);
            textGifCanvasGroup.alpha = 1f;

            yield return new WaitForSeconds(minTimeBeforeInput);
            isTransitioning = false;

            StartGifSequence(currentIndex);
        }
        else
        {
            EndCutscene();
        }
    }

    /// <summary>
    /// Imposta la sprite corretta per l'indice specificato, e pre-carica il primo
    /// fotogramma della GIF di testo corrispondente (ma la tiene invisibile,
    /// alpha 0). Questo evita che rimanga visibile per un istante l'ultimo
    /// fotogramma della GIF della slide precedente, e permette di far comparire
    /// la nuova GIF con un piccolo ritardo controllato (vedi gifAppearDelay).
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
    /// </summary>
    private IEnumerator FadeCanvasGroup(float from, float to, float duration)
    {
        float elapsed = 0f;
        panelCanvasGroup.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            panelCanvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        panelCanvasGroup.alpha = to;
    }

    /// <summary>
    /// Chiamata quando tutte le slide sono state mostrate.
    /// Carica la scena successiva, se specificata.
    /// </summary>
    private void EndCutscene()
    {
        Debug.Log("[CutsceneManager] Cutscene terminata.");

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }
}