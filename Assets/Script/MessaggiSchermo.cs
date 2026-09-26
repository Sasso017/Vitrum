using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Messaggi a schermo creati automaticamente, senza dover preparare testi nel Canvas.
///   - Suggerimento: in basso al centro, resta visibile finché serve (es. "Premi E per raccogliere").
///   - Messaggio: al centro, sparisce da solo dopo qualche secondo (es. "Hai ottenuto la chiave!").
/// Uso da qualsiasi script:
///   MessaggiSchermo.MostraSuggerimento(this, "Premi E per raccogliere");
///   MessaggiSchermo.NascondiSuggerimento(this);
///   MessaggiSchermo.MostraMessaggio("Hai ottenuto la chiave!", 3f);
/// Non serve metterlo in scena. Per personalizzarne l'aspetto puoi comunque aggiungerlo
/// a un GameObject e regolare i campi dall'Inspector.
/// </summary>
public class MessaggiSchermo : MonoBehaviour
{
    private static MessaggiSchermo instance;

    [Header("Aspetto")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private float dimensioneSuggerimento = 34f;
    [SerializeField] private float dimensioneMessaggio = 52f;
    [SerializeField] private Color coloreTesto = new Color(1f, 0.95f, 0.85f);
    [SerializeField] private Color coloreSfondo = new Color(0f, 0f, 0f, 0.55f);
    [Tooltip("Durata della dissolvenza di comparsa e scomparsa dei messaggi")]
    [SerializeField] private float durataDissolvenza = 0.3f;

    private TextMeshProUGUI testoSuggerimento;
    private CanvasGroup gruppoSuggerimento;
    private TextMeshProUGUI testoMessaggio;
    private CanvasGroup gruppoMessaggio;

    private Object proprietarioSuggerimento; // Chi sta mostrando il suggerimento in questo momento
    private Coroutine routineMessaggio;

    private static MessaggiSchermo Instance
    {
        get
        {
            if (instance == null)
                new GameObject("MessaggiSchermo").AddComponent<MessaggiSchermo>();
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        CostruisciInterfaccia();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ---------- API ----------

    /// <summary>Mostra il suggerimento in basso. "proprietario" è lo script che lo richiede (di solito this).</summary>
    public static void MostraSuggerimento(Object proprietario, string testo)
    {
        if (string.IsNullOrEmpty(testo)) return;
        MessaggiSchermo m = Instance;
        m.proprietarioSuggerimento = proprietario;
        m.testoSuggerimento.text = testo;
        m.gruppoSuggerimento.alpha = 1f;
    }

    /// <summary>Nasconde il suggerimento, ma solo se è stato mostrato da questo proprietario.</summary>
    public static void NascondiSuggerimento(Object proprietario)
    {
        if (instance == null || instance.proprietarioSuggerimento != proprietario) return;
        instance.proprietarioSuggerimento = null;
        instance.gruppoSuggerimento.alpha = 0f;
    }

    /// <summary>Mostra un messaggio al centro dello schermo per il numero di secondi indicato.</summary>
    public static void MostraMessaggio(string testo, float durata = 3f)
    {
        if (string.IsNullOrEmpty(testo)) return;
        MessaggiSchermo m = Instance;
        if (m.routineMessaggio != null) m.StopCoroutine(m.routineMessaggio);
        m.routineMessaggio = m.StartCoroutine(m.SequenzaMessaggio(testo, durata));
    }

    /// <summary>Nasconde subito il messaggio centrale.</summary>
    public static void NascondiMessaggio()
    {
        if (instance == null) return;
        if (instance.routineMessaggio != null) instance.StopCoroutine(instance.routineMessaggio);
        instance.gruppoMessaggio.alpha = 0f;
    }

    // ---------- Interni ----------

    private IEnumerator SequenzaMessaggio(string testo, float durata)
    {
        testoMessaggio.text = testo;

        yield return Dissolvi(gruppoMessaggio, gruppoMessaggio.alpha, 1f);
        yield return new WaitForSecondsRealtime(durata);
        yield return Dissolvi(gruppoMessaggio, 1f, 0f);

        routineMessaggio = null;
    }

    private IEnumerator Dissolvi(CanvasGroup g, float da, float a)
    {
        float t = 0f;
        while (t < durataDissolvenza)
        {
            t += Time.unscaledDeltaTime;
            g.alpha = Mathf.Lerp(da, a, t / durataDissolvenza);
            yield return null;
        }
        g.alpha = a;
    }

    private void CostruisciInterfaccia()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500; // Sopra al gioco, sotto la dissolvenza tra le scene

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Suggerimento in basso al centro
        testoSuggerimento = CreaRiquadro("Suggerimento", new Vector2(0.5f, 0f), new Vector2(0f, 120f),
                                          dimensioneSuggerimento, out gruppoSuggerimento);
        // Messaggio al centro, un po' sopra
        testoMessaggio = CreaRiquadro("Messaggio", new Vector2(0.5f, 0.5f), new Vector2(0f, 150f),
                                       dimensioneMessaggio, out gruppoMessaggio);
    }

    private TextMeshProUGUI CreaRiquadro(string nome, Vector2 ancora, Vector2 posizione, float dimensione, out CanvasGroup gruppo)
    {
        // Sfondo scuro che si adatta alla lunghezza del testo
        GameObject sfondo = new GameObject(nome, typeof(RectTransform), typeof(Image),
                                           typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter), typeof(CanvasGroup));
        sfondo.transform.SetParent(transform, false);

        RectTransform rt = sfondo.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = ancora;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = posizione;

        sfondo.GetComponent<Image>().color = coloreSfondo;
        sfondo.GetComponent<Image>().raycastTarget = false;

        HorizontalLayoutGroup layout = sfondo.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(32, 32, 14, 14);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = sfondo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        gruppo = sfondo.GetComponent<CanvasGroup>();
        gruppo.alpha = 0f;
        gruppo.blocksRaycasts = false;
        gruppo.interactable = false;

        // Testo
        GameObject oggettoTesto = new GameObject("Testo", typeof(RectTransform), typeof(TextMeshProUGUI));
        oggettoTesto.transform.SetParent(sfondo.transform, false);

        TextMeshProUGUI testo = oggettoTesto.GetComponent<TextMeshProUGUI>();
        if (font != null) testo.font = font;
        testo.fontSize = dimensione;
        testo.color = coloreTesto;
        testo.alignment = TextAlignmentOptions.Center;
        testo.enableWordWrapping = false;
        testo.raycastTarget = false;

        return testo;
    }
}
