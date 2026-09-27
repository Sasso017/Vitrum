using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Sequenza finale della Basilica: i lucchetti spariscono, le ante si aprono, lo schermo sfuma
/// e compare il messaggio di vittoria. Poi si torna al menu principale.
/// Mettilo su un GameObject vuoto nella ScenaBasilica e collega AvviaFinale()
/// all'evento "Quando Sbloccata" della PortaBasilica.
/// </summary>
public class FinaleBasilica : MonoBehaviour
{
    /// <summary>True durante la sequenza finale (menu di pausa disattivato).</summary>
    public static bool InCorso { get; private set; }

    [Header("Giocatore")]
    [Tooltip("Script da disattivare durante il finale (es. FPSController di Carlo, Accovacciamento)")]
    [SerializeField] private Behaviour[] controlliGiocatore;

    [Header("Lucchetti")]
    [Tooltip("I lucchetti sulle ante, nell'ordine in cui devono sparire")]
    [SerializeField] private Transform[] lucchetti;
    [Tooltip("Lo stesso prefab dell'effetto 'puf' usato nella cutscene iniziale")]
    [SerializeField] private ParticleSystem prefabPuf;
    [SerializeField] private AudioClip suonoSblocco;
    [Tooltip("Pausa prima che sparisca il primo lucchetto")]
    [SerializeField] private float attesaIniziale = 0.5f;
    [SerializeField] private float intervalloTraLucchetti = 0.6f;
    [SerializeField] private float durataScomparsa = 0.25f;

    [Header("Porta")]
    [Tooltip("I cardini delle ante (gli stessi usati in IntroBasilica)")]
    [SerializeField] private Transform antaSinistra;
    [SerializeField] private Transform antaDestra;
    [Tooltip("Rotazione locale delle ante aperte. Usa i valori opposti se devono aprirsi verso l'esterno")]
    [SerializeField] private Vector3 rotazioneApertaSinistra = new Vector3(0f, -90f, 0f);
    [SerializeField] private Vector3 rotazioneApertaDestra = new Vector3(0f, 90f, 0f);
    [SerializeField] private float attesaPrimaApertura = 0.6f;
    [SerializeField] private float durataApertura = 3f;
    [SerializeField] private AudioClip suonoApertura;

    [Header("Luce dall'esterno (opzionale)")]
    [Tooltip("Una luce oltre la porta (es. Spot o Point Light bianca/calda) che si accende mentre le ante si aprono")]
    [SerializeField] private Light luceEsterna;
    [SerializeField] private float intensitaLuceEsterna = 3f;

    [Header("Schermata finale")]
    [SerializeField] private string titolo = "Hai vinto!";
    [SerializeField] private string sottotitolo = "Sei tornato a casa...";
    [SerializeField] private string testoContinua = "Premi un tasto per continuare";
    [Tooltip("Colore verso cui sfuma lo schermo")]
    [SerializeField] private Color coloreSchermo = new Color(1f, 0.97f, 0.9f);
    [SerializeField] private Color coloreTitolo = new Color(0.25f, 0.15f, 0.08f);
    [SerializeField] private Color coloreSottotitolo = new Color(0.35f, 0.25f, 0.15f);
    [SerializeField] private TMP_FontAsset font;
    [Tooltip("Quando inizia la dissolvenza, rispetto all'inizio dell'apertura delle ante (secondi)")]
    [SerializeField] private float inizioDissolvenza = 1.8f;
    [SerializeField] private float durataDissolvenza = 2.5f;
    [Tooltip("Secondi prima che compaia 'Premi un tasto per continuare'")]
    [SerializeField] private float attesaPrimaDiContinuare = 3f;
    [Tooltip("Musica della schermata finale (opzionale, usa il MusicPlayer)")]
    [SerializeField] private AudioClip musicaVittoria;

    [Header("Dopo la vittoria")]
    [SerializeField] private string scenaMenuPrincipale = "StartMenu";

    private bool avviato = false;

    // Interfaccia della schermata finale
    private CanvasGroup gruppoSchermo;
    private CanvasGroup gruppoTitolo;
    private CanvasGroup gruppoSottotitolo;
    private CanvasGroup gruppoContinua;

    /// <summary>Avvia il finale. Collegalo all'evento "Quando Sbloccata" della PortaBasilica.</summary>
    public void AvviaFinale()
    {
        if (avviato) return;
        avviato = true;
        StartCoroutine(Sequenza());
    }

    private void Start()
    {
        if (luceEsterna != null)
        {
            luceEsterna.intensity = 0f;
            luceEsterna.enabled = false;
        }
    }

    private IEnumerator Sequenza()
    {
        InCorso = true;
        SetControlli(false);
        CostruisciSchermata();

        // 1. I lucchetti spariscono uno alla volta
        yield return new WaitForSeconds(attesaIniziale);
        if (lucchetti != null)
        {
            foreach (Transform l in lucchetti)
            {
                if (l == null || !l.gameObject.activeInHierarchy) continue;
                StartCoroutine(ScomparsaLucchetto(l));
                yield return new WaitForSeconds(intervalloTraLucchetti);
            }
        }

        // 2. Le ante si aprono, e intanto parte la dissolvenza
        yield return new WaitForSeconds(attesaPrimaApertura);
        if (suonoApertura != null && SFXPlayer.Instance != null) SFXPlayer.Instance.Play(suonoApertura);
        if (luceEsterna != null) luceEsterna.enabled = true;

        StartCoroutine(AperturaAnte());
        yield return new WaitForSeconds(inizioDissolvenza);

        // 3. Lo schermo sfuma e compare il messaggio di vittoria
        if (musicaVittoria != null && MusicPlayer.Instance != null) MusicPlayer.Instance.PlayMusic(musicaVittoria);
        yield return Dissolvi(gruppoSchermo, 0f, 1f, durataDissolvenza);
        yield return Dissolvi(gruppoTitolo, 0f, 1f, 1.2f);
        yield return new WaitForSeconds(0.5f);
        yield return Dissolvi(gruppoSottotitolo, 0f, 1f, 1.5f);

        // 4. Attende un tasto e torna al menu principale
        yield return new WaitForSeconds(attesaPrimaDiContinuare);
        if (!string.IsNullOrEmpty(testoContinua))
            yield return Dissolvi(gruppoContinua, 0f, 1f, 0.8f);

        while (!TastoPremuto())
        {
            if (!string.IsNullOrEmpty(testoContinua))
                gruppoContinua.alpha = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 2.5f);
            yield return null;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        InCorso = false;
        SceneFader.Instance.CaricaScena(scenaMenuPrincipale);
    }

    // ---------- Lucchetti e ante ----------

    private IEnumerator ScomparsaLucchetto(Transform lucchetto)
    {
        if (prefabPuf != null)
        {
            ParticleSystem puf = Instantiate(prefabPuf, lucchetto.position, Quaternion.identity);
            puf.Play();
            Destroy(puf.gameObject, puf.main.duration + puf.main.startLifetime.constantMax + 0.5f);
        }
        if (suonoSblocco != null && SFXPlayer.Instance != null) SFXPlayer.Instance.Play(suonoSblocco);

        // Si gonfia un attimo, poi si rimpicciolisce fino a sparire
        Vector3 scala = lucchetto.localScale;
        float t = 0f;
        while (t < durataScomparsa)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / durataScomparsa);
            float f = k < 0.3f ? 1f + 0.25f * (k / 0.3f) : 1.25f * (1f - (k - 0.3f) / 0.7f);
            lucchetto.localScale = scala * f;
            yield return null;
        }
        lucchetto.gameObject.SetActive(false);
        lucchetto.localScale = scala;
    }

    private IEnumerator AperturaAnte()
    {
        Quaternion inizioS = antaSinistra != null ? antaSinistra.localRotation : Quaternion.identity;
        Quaternion inizioD = antaDestra != null ? antaDestra.localRotation : Quaternion.identity;
        Quaternion fineS = Quaternion.Euler(rotazioneApertaSinistra);
        Quaternion fineD = Quaternion.Euler(rotazioneApertaDestra);

        float t = 0f;
        while (t < durataApertura)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / durataApertura); // Parte lenta, accelera, rallenta

            if (antaSinistra != null) antaSinistra.localRotation = Quaternion.Slerp(inizioS, fineS, k);
            if (antaDestra != null) antaDestra.localRotation = Quaternion.Slerp(inizioD, fineD, k);
            if (luceEsterna != null) luceEsterna.intensity = intensitaLuceEsterna * k;
            yield return null;
        }
    }

    // ---------- Schermata finale ----------

    private void CostruisciSchermata()
    {
        GameObject canvasObj = new GameObject("FinaleBasilica_Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObj.transform.SetParent(transform, false);

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900; // Sopra a tutto, tranne la dissolvenza tra le scene

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Sfondo a tutto schermo
        GameObject sfondo = new GameObject("Sfondo", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        sfondo.transform.SetParent(canvasObj.transform, false);
        RectTransform rs = sfondo.GetComponent<RectTransform>();
        rs.anchorMin = Vector2.zero; rs.anchorMax = Vector2.one;
        rs.offsetMin = rs.offsetMax = Vector2.zero;
        sfondo.GetComponent<Image>().color = coloreSchermo;
        gruppoSchermo = sfondo.GetComponent<CanvasGroup>();
        gruppoSchermo.alpha = 0f;
        gruppoSchermo.blocksRaycasts = false;

        gruppoTitolo = CreaTesto(sfondo.transform, titolo, 110f, coloreTitolo, new Vector2(0f, 90f), FontStyles.Bold);
        gruppoSottotitolo = CreaTesto(sfondo.transform, sottotitolo, 52f, coloreSottotitolo, new Vector2(0f, -30f), FontStyles.Italic);
        gruppoContinua = CreaTesto(sfondo.transform, testoContinua, 30f, coloreSottotitolo, new Vector2(0f, -380f), FontStyles.Normal);
    }

    private CanvasGroup CreaTesto(Transform genitore, string testo, float dimensione, Color colore, Vector2 posizione, FontStyles stile)
    {
        GameObject obj = new GameObject("Testo", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(CanvasGroup));
        obj.transform.SetParent(genitore, false);

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = posizione;
        rt.sizeDelta = new Vector2(1600f, 200f);

        TextMeshProUGUI t = obj.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = testo;
        t.fontSize = dimensione;
        t.color = colore;
        t.fontStyle = stile;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;

        CanvasGroup g = obj.GetComponent<CanvasGroup>();
        g.alpha = 0f;
        return g;
    }

    private IEnumerator Dissolvi(CanvasGroup g, float da, float a, float durata)
    {
        float t = 0f;
        while (t < durata)
        {
            t += Time.unscaledDeltaTime;
            g.alpha = Mathf.Lerp(da, a, t / durata);
            yield return null;
        }
        g.alpha = a;
    }

    // ---------- Utilità ----------

    private void SetControlli(bool attivi)
    {
        if (controlliGiocatore == null) return;
        foreach (Behaviour b in controlliGiocatore)
            if (b != null) b.enabled = attivi;
    }

    private bool TastoPremuto()
    {
#if ENABLE_INPUT_SYSTEM
        return (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
               (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
#else
        return Input.anyKeyDown;
#endif
    }

    private void OnDestroy()
    {
        InCorso = false;
    }
}
