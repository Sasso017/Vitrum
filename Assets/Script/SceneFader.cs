using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Transizione con dissolvenza tra le scene: lo schermo sfuma al nero, la scena viene caricata,
/// poi lo schermo torna visibile.
/// Non serve metterlo in scena: si crea da solo alla prima chiamata.
/// Uso da qualsiasi script: SceneFader.Instance.CaricaScena("ScenaBasilica");
/// (Se vuoi cambiarne durata o colore dall'Inspector, puoi comunque metterlo su un GameObject nella StartMenu.)
/// </summary>
public class SceneFader : MonoBehaviour
{
    private static SceneFader instance;

    /// <summary>Restituisce il fader, creandolo se non esiste ancora.</summary>
    public static SceneFader Instance
    {
        get
        {
            if (instance == null)
                new GameObject("SceneFader").AddComponent<SceneFader>();
            return instance;
        }
    }

    /// <summary>True mentre è in corso una transizione.</summary>
    public static bool InTransizione => instance != null && instance.inTransizione;

    [Tooltip("Durata della dissolvenza verso il nero (secondi)")]
    [SerializeField] private float durataFadeOut = 0.6f;
    [Tooltip("Durata della dissolvenza dal nero alla nuova scena (secondi)")]
    [SerializeField] private float durataFadeIn = 0.6f;
    [SerializeField] private Color colore = Color.black;

    [Header("Audio")]
    [Tooltip("Nei cambi scena anche l'audio sfuma insieme all'immagine (non modifica gli slider del volume)")]
    [SerializeField] private bool dissolvenzaAudio = true;
    [Tooltip("Anche nelle dissolvenze senza cambio scena (es. entrata in un minigioco) l'audio si abbassa")]
    [SerializeField] private bool audioAncheSenzaCambioScena = false;

    private CanvasGroup gruppo;
    private bool inTransizione = false;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        CostruisciSchermo();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>Carica una scena con dissolvenza in uscita e in entrata.</summary>
    public void CaricaScena(string nomeScena)
    {
        if (inTransizione) return; // Evita transizioni sovrapposte

        if (!Application.CanStreamedLevelBeLoaded(nomeScena))
        {
            Debug.LogError($"[SceneFader] La scena '{nomeScena}' non esiste o non è nelle Build Settings.", this);
            return;
        }

        StartCoroutine(Transizione(nomeScena));
    }

    /// <summary>
    /// Dissolvenza senza cambio scena: sfuma al nero, esegue l'azione (es. cambiare camera)
    /// mentre lo schermo è nero, poi torna visibile.
    /// </summary>
    public void Dissolvenza(Action azioneAlBuio)
    {
        if (inTransizione) return;
        StartCoroutine(TransizioneSenzaScena(azioneAlBuio));
    }

    private IEnumerator TransizioneSenzaScena(Action azioneAlBuio)
    {
        inTransizione = true;
        gruppo.blocksRaycasts = true;

        bool audio = dissolvenzaAudio && audioAncheSenzaCambioScena;
        yield return Dissolvi(0f, 1f, durataFadeOut, audio);

        azioneAlBuio?.Invoke();
        yield return null; // Un frame per applicare i cambiamenti prima di mostrarli

        yield return Dissolvi(1f, 0f, durataFadeIn, audio);

        gruppo.blocksRaycasts = false;
        inTransizione = false;
    }

    private IEnumerator Transizione(string nomeScena)
    {
        inTransizione = true;
        gruppo.blocksRaycasts = true; // Blocca i click durante la transizione

        // 1. Sfuma al nero (e l'audio al silenzio)
        yield return Dissolvi(0f, 1f, durataFadeOut, dissolvenzaAudio);

        // 2. Carica la nuova scena mentre lo schermo è nero
        Time.timeScale = 1f;
        AsyncOperation caricamento = SceneManager.LoadSceneAsync(nomeScena);
        while (!caricamento.isDone)
            yield return null;

        // Attende un frame: lascia eseguire gli Start della nuova scena
        // (es. il ripristino della posizione del giocatore) prima di mostrarla
        yield return null;

        // 3. Sfuma dal nero alla nuova scena (e l'audio torna al suo volume)
        yield return Dissolvi(1f, 0f, durataFadeIn, dissolvenzaAudio);

        gruppo.blocksRaycasts = false;
        inTransizione = false;
    }

    /// <param name="audio">Se true, il volume di ascolto segue l'immagine: 1 con schermo visibile, 0 con schermo nero</param>
    private IEnumerator Dissolvi(float da, float a, float durata, bool audio = false)
    {
        if (durata <= 0f)
        {
            ImpostaNero(a, audio);
            yield break;
        }

        float t = 0f;
        while (t < durata)
        {
            // Tempo reale: funziona anche se il gioco era in pausa (timeScale = 0)
            t += Time.unscaledDeltaTime;
            ImpostaNero(Mathf.Lerp(da, a, t / durata), audio);
            yield return null;
        }
        ImpostaNero(a, audio);
    }

    /// <summary>0 = scena visibile e audio pieno, 1 = schermo nero e silenzio.</summary>
    private void ImpostaNero(float valore, bool audio)
    {
        gruppo.alpha = valore;

        if (audio)
        {
            // Curva più morbida per l'orecchio: il volume scende più lentamente all'inizio
            float v = 1f - Mathf.Clamp01(valore);
            AudioListener.volume = v * v * (3f - 2f * v);
        }
    }

    private void OnDisable()
    {
        // Sicurezza: il volume di ascolto non deve mai restare abbassato
        AudioListener.volume = 1f;
    }

    /// <summary>Crea via codice il Canvas con l'immagine a tutto schermo.</summary>
    private void CostruisciSchermo()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // Sopra a qualsiasi altra interfaccia

        gameObject.AddComponent<GraphicRaycaster>();

        gruppo = gameObject.AddComponent<CanvasGroup>();
        gruppo.alpha = 0f;
        gruppo.blocksRaycasts = false;
        gruppo.interactable = false;

        GameObject schermo = new GameObject("Schermo", typeof(RectTransform), typeof(Image));
        schermo.transform.SetParent(transform, false);

        RectTransform rt = schermo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        schermo.GetComponent<Image>().color = colore;
    }
}