using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mostra per qualche secondo un riquadro con i comandi di gioco, subito dopo la fine
/// della cutscene iniziale della porta (anche se saltata). Compare solo all'inizio della partita.
/// Mettilo su un GameObject vuoto nella ScenaBasilica: l'interfaccia viene creata via codice.
/// </summary>
public class GuidaComandi : MonoBehaviour
{
    [Serializable]
    public class Comando
    {
        [Tooltip("Il tasto, es. 'WASD', 'E', 'Shift'")]
        public string tasto;
        [Tooltip("Cosa fa, es. 'Muoviti'")]
        public string descrizione;
    }

    [Header("Comandi")]
    [SerializeField] private string titolo = "Comandi";
    [SerializeField] private Comando[] comandi =
    {
        new Comando { tasto = "WASD",  descrizione = "Muoviti" },
        new Comando { tasto = "Mouse", descrizione = "Guardati intorno" },
        new Comando { tasto = "Shift", descrizione = "Corri" },
        new Comando { tasto = "Ctrl",  descrizione = "Accovacciati" },
        new Comando { tasto = "E",     descrizione = "Interagisci" },
        new Comando { tasto = "Esc",   descrizione = "Pausa" }
    };

    [Header("Tempi (secondi)")]
    [Tooltip("Attesa tra la fine della cutscene e la comparsa della guida")]
    [SerializeField] private float ritardo = 0.3f;
    [Tooltip("Per quanto resta visibile la guida")]
    [SerializeField] private float durataVisibile = 2f;
    [SerializeField] private float durataDissolvenza = 0.4f;

    [Header("Posizione")]
    [Tooltip("Distanza dall'angolo in alto a sinistra")]
    [SerializeField] private Vector2 margine = new Vector2(30f, 30f);

    [Header("Aspetto")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private float dimensioneTesto = 28f;
    [SerializeField] private Color coloreTesto = new Color(1f, 0.95f, 0.88f);
    [SerializeField] private Color coloreTitolo = new Color(1f, 0.8f, 0.35f);
    [SerializeField] private Color coloreSfondo = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] private Color coloreTasto = new Color(1f, 1f, 1f, 0.15f);

    /// <summary>True mentre la guida è a schermo (utile per spostare altri elementi dell'interfaccia).</summary>
    public static bool Visibile { get; private set; }

    private CanvasGroup gruppo;

    private void Start()
    {
        // Solo all'inizio della partita: se la cutscene è già stata vista, la guida non compare
        GameManager gm = GameManager.instance;
        if (gm != null && gm.introBasilicaVista) return;

        CostruisciInterfaccia();
        StartCoroutine(Sequenza());
    }

    private IEnumerator Sequenza()
    {
        // Aspetta che la cutscene inizi (al massimo un paio di secondi, nel caso non ci sia)...
        float attesa = 0f;
        while (!IntroBasilica.InCorso && attesa < 2f)
        {
            attesa += Time.unscaledDeltaTime;
            yield return null;
        }
        // ...e poi che finisca
        while (IntroBasilica.InCorso) yield return null;

        yield return new WaitForSeconds(ritardo);

        Visibile = true;
        yield return Dissolvi(0f, 1f);
        yield return new WaitForSeconds(durataVisibile);
        yield return Dissolvi(1f, 0f);
        Visibile = false;

        Destroy(gruppo.gameObject); // Non serve più
    }

    private IEnumerator Dissolvi(float da, float a)
    {
        float t = 0f;
        while (t < durataDissolvenza)
        {
            t += Time.unscaledDeltaTime;
            gruppo.alpha = Mathf.Lerp(da, a, t / durataDissolvenza);
            yield return null;
        }
        gruppo.alpha = a;
    }

    // ---------- Interfaccia ----------

    private void CostruisciInterfaccia()
    {
        GameObject canvasObj = new GameObject("GuidaComandi_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasObj.transform.SetParent(transform, false);

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 420; // Sopra l'overlay dei progressi

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gruppo = canvasObj.GetComponent<CanvasGroup>();
        gruppo.alpha = 0f;
        gruppo.blocksRaycasts = false;
        gruppo.interactable = false;

        // Pannello in alto a sinistra che si adatta al contenuto
        GameObject pannello = new GameObject("Pannello", typeof(RectTransform), typeof(Image),
                                             typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        pannello.transform.SetParent(canvasObj.transform, false);

        RectTransform rt = pannello.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(margine.x, -margine.y);

        Image sfondo = pannello.GetComponent<Image>();
        sfondo.color = coloreSfondo;
        sfondo.raycastTarget = false;

        VerticalLayoutGroup layout = pannello.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(22, 26, 16, 18);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = pannello.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (!string.IsNullOrEmpty(titolo))
        {
            TextMeshProUGUI t = CreaTesto(pannello.transform, titolo, dimensioneTesto * 1.05f, coloreTitolo);
            t.fontStyle = FontStyles.Bold;
        }

        if (comandi == null) return;
        foreach (Comando c in comandi)
        {
            if (c == null || string.IsNullOrEmpty(c.tasto)) continue;
            CreaRiga(pannello.transform, c);
        }
    }

    private void CreaRiga(Transform genitore, Comando c)
    {
        GameObject riga = new GameObject("Riga_" + c.tasto, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        riga.transform.SetParent(genitore, false);
        HorizontalLayoutGroup h = riga.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 14f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;

        // Il tasto, disegnato come un tastino (larghezza minima uguale per tutti, così le descrizioni sono allineate)
        GameObject tasto = new GameObject("Tasto", typeof(RectTransform), typeof(Image),
                                          typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        tasto.transform.SetParent(riga.transform, false);
        tasto.GetComponent<Image>().color = coloreTasto;
        tasto.GetComponent<Image>().raycastTarget = false;
        HorizontalLayoutGroup ht = tasto.GetComponent<HorizontalLayoutGroup>();
        ht.padding = new RectOffset(12, 12, 4, 4);
        ht.childAlignment = TextAnchor.MiddleCenter;
        ht.childControlWidth = ht.childControlHeight = true;
        ht.childForceExpandWidth = true;
        tasto.GetComponent<LayoutElement>().minWidth = dimensioneTesto * 3.4f;

        TextMeshProUGUI testoTasto = CreaTesto(tasto.transform, c.tasto, dimensioneTesto * 0.9f, coloreTesto);
        testoTasto.fontStyle = FontStyles.Bold;
        testoTasto.alignment = TextAlignmentOptions.Center;

        CreaTesto(riga.transform, c.descrizione, dimensioneTesto, coloreTesto);
    }

    private TextMeshProUGUI CreaTesto(Transform genitore, string testo, float dimensione, Color colore)
    {
        GameObject obj = new GameObject("Testo", typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(genitore, false);
        TextMeshProUGUI t = obj.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = testo;
        t.fontSize = dimensione;
        t.color = colore;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        return t;
    }

    private void OnDestroy()
    {
        Visibile = false;
    }
}
