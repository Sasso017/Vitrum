using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Overlay in un angolo dello schermo con lo stato di avanzamento:
/// chiavi ottenute e tasselli del mosaico trovati. Si aggiorna da solo e fa un piccolo
/// "lampo" dorato quando un valore cambia. Si nasconde durante la cutscene e i minigiochi.
/// Mettilo su un GameObject vuoto nella ScenaBasilica: l'interfaccia viene creata via codice.
/// </summary>
public class HUDProgressi : MonoBehaviour
{
    public enum Angolo { AltoSinistra, AltoDestra }

    [Header("Posizione")]
    [SerializeField] private Angolo angolo = Angolo.AltoSinistra;
    [SerializeField] private Vector2 margine = new Vector2(30f, 30f);

    [Header("Testi")]
    [SerializeField] private string etichettaChiavi = "Chiavi";
    [SerializeField] private string etichettaTasselli = "Tasselli";
    [Tooltip("Mostra la riga dei tasselli solo finché il mosaico non è completato")]
    [SerializeField] private bool nascondiTasselliDopoMosaico = true;

    [Header("Icone (opzionali)")]
    [Tooltip("Se assegnate, compaiono al posto delle etichette di testo")]
    [SerializeField] private Sprite iconaChiave;
    [SerializeField] private Sprite iconaTassello;

    [Header("Aspetto")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private float dimensioneTesto = 32f;
    [SerializeField] private Color coloreTesto = new Color(1f, 0.95f, 0.85f);
    [SerializeField] private Color coloreLampo = new Color(1f, 0.8f, 0.2f);
    [SerializeField] private Color coloreCompletato = new Color(0.6f, 1f, 0.6f);
    [SerializeField] private Color coloreSfondo = new Color(0f, 0f, 0f, 0.5f);

    private CanvasGroup gruppo;
    private RigaHUD rigaChiavi;
    private RigaHUD rigaTasselli;
    private GameManager gmIscritto;

    private class RigaHUD
    {
        public GameObject oggetto;
        public TextMeshProUGUI testo;
        public string etichetta;
        public int ultimoValore = -1;
        public Coroutine lampo;
    }

    private void Start()
    {
        CostruisciInterfaccia();
        Iscriviti();
        Aggiorna(false);
    }

    private void OnDestroy()
    {
        if (gmIscritto != null)
        {
            gmIscritto.OnEnigmaCompletato -= QuandoEnigmaCompletato;
            gmIscritto.OnTasselloRaccolto -= QuandoTasselloRaccolto;
        }
    }

    private void Update()
    {
        // Nascosto durante la cutscene iniziale e mentre si gioca a un minigioco
        bool visibile = !IntroBasilica.InCorso && !StazioneMinigioco.InUso;
        float obiettivo = visibile ? 1f : 0f;
        gruppo.alpha = Mathf.MoveTowards(gruppo.alpha, obiettivo, Time.unscaledDeltaTime * 4f);

        if (gmIscritto == null) Iscriviti(); // Nel caso il GameManager arrivi dopo
    }

    // ---------- Aggiornamento ----------

    private void Iscriviti()
    {
        GameManager gm = GameManager.instance;
        if (gm == null || gm == gmIscritto) return;

        gmIscritto = gm;
        gm.OnEnigmaCompletato += QuandoEnigmaCompletato;
        gm.OnTasselloRaccolto += QuandoTasselloRaccolto;
        Aggiorna(false);
    }

    private void QuandoEnigmaCompletato(Enigma e) => Aggiorna(true);
    private void QuandoTasselloRaccolto(string id) => Aggiorna(true);

    private void Aggiorna(bool conLampo)
    {
        GameManager gm = GameManager.instance;
        int chiavi = gm != null ? gm.ChiaviRaccolte : 0;
        int tasselli = gm != null ? gm.NumeroTasselliRaccolti : 0;
        int tasselliTot = gm != null ? gm.TasselliTotali : Tassello.NumeroInScena;

        ImpostaRiga(rigaChiavi, chiavi, GameManager.ChiaviTotali, conLampo);
        ImpostaRiga(rigaTasselli, tasselli, tasselliTot, conLampo);

        bool mosaicoFatto = gm != null && gm.IsCompletato(Enigma.Mosaico);
        rigaTasselli.oggetto.SetActive(tasselliTot > 0 && !(nascondiTasselliDopoMosaico && mosaicoFatto));
    }

    private void ImpostaRiga(RigaHUD riga, int valore, int totale, bool conLampo)
    {
        string prefisso = string.IsNullOrEmpty(riga.etichetta) ? "" : riga.etichetta + "  ";
        riga.testo.text = $"{prefisso}{valore}/{totale}";

        Color base_ = (totale > 0 && valore >= totale) ? coloreCompletato : coloreTesto;

        if (conLampo && riga.ultimoValore >= 0 && valore != riga.ultimoValore)
        {
            if (riga.lampo != null) StopCoroutine(riga.lampo);
            riga.lampo = StartCoroutine(Lampo(riga, base_));
        }
        else if (riga.lampo == null)
        {
            riga.testo.color = base_;
        }

        riga.ultimoValore = valore;
    }

    private IEnumerator Lampo(RigaHUD riga, Color coloreFinale)
    {
        Transform t = riga.oggetto.transform;
        float durata = 0.6f, trascorso = 0f;

        while (trascorso < durata)
        {
            trascorso += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(trascorso / durata);

            // Si ingrandisce di colpo e torna alla dimensione normale, sfumando dal dorato
            float scala = 1f + 0.35f * Mathf.Sin(k * Mathf.PI) * (1f - k * 0.5f);
            t.localScale = Vector3.one * scala;
            riga.testo.color = Color.Lerp(coloreLampo, coloreFinale, k);
            yield return null;
        }

        t.localScale = Vector3.one;
        riga.testo.color = coloreFinale;
        riga.lampo = null;
    }

    // ---------- Costruzione dell'interfaccia ----------

    private void CostruisciInterfaccia()
    {
        GameObject canvasObj = new GameObject("HUDProgressi_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasObj.transform.SetParent(transform, false);

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400; // Sopra al gioco, sotto i messaggi e le dissolvenze

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gruppo = canvasObj.GetComponent<CanvasGroup>();
        gruppo.blocksRaycasts = false;
        gruppo.interactable = false;
        gruppo.alpha = 0f;

        // Pannello con sfondo che si adatta al contenuto
        GameObject pannello = new GameObject("Pannello", typeof(RectTransform), typeof(Image),
                                             typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        pannello.transform.SetParent(canvasObj.transform, false);

        bool destra = angolo == Angolo.AltoDestra;
        RectTransform rt = pannello.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(destra ? 1f : 0f, 1f);
        rt.anchoredPosition = new Vector2(destra ? -margine.x : margine.x, -margine.y);

        Image sfondo = pannello.GetComponent<Image>();
        sfondo.color = coloreSfondo;
        sfondo.raycastTarget = false;

        VerticalLayoutGroup layout = pannello.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(22, 22, 14, 14);
        layout.spacing = 6f;
        layout.childAlignment = destra ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = pannello.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        rigaChiavi = CreaRiga(pannello.transform, "Chiavi", iconaChiave, etichettaChiavi);
        rigaTasselli = CreaRiga(pannello.transform, "Tasselli", iconaTassello, etichettaTasselli);
    }

    private RigaHUD CreaRiga(Transform genitore, string nome, Sprite icona, string etichetta)
    {
        GameObject riga = new GameObject(nome, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        riga.transform.SetParent(genitore, false);

        HorizontalLayoutGroup h = riga.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 12f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;

        if (icona != null)
        {
            GameObject img = new GameObject("Icona", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            img.transform.SetParent(riga.transform, false);
            Image i = img.GetComponent<Image>();
            i.sprite = icona;
            i.preserveAspect = true;
            i.raycastTarget = false;
            LayoutElement le = img.GetComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = dimensioneTesto * 1.2f;
        }

        GameObject oggettoTesto = new GameObject("Testo", typeof(RectTransform), typeof(TextMeshProUGUI));
        oggettoTesto.transform.SetParent(riga.transform, false);
        TextMeshProUGUI testo = oggettoTesto.GetComponent<TextMeshProUGUI>();
        if (font != null) testo.font = font;
        testo.fontSize = dimensioneTesto;
        testo.color = coloreTesto;
        testo.enableWordWrapping = false;
        testo.raycastTarget = false;

        return new RigaHUD
        {
            oggetto = riga,
            testo = testo,
            etichetta = icona != null ? "" : etichetta // Con l'icona non serve l'etichetta
        };
    }
}
