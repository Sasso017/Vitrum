using System.Collections;
using UnityEngine;

/// <summary>
/// Uno dei posti vuoti di un quadro del mosaico. Accetta un solo tassello, quello corretto.
/// Struttura:
///     Slot (questo script)
///     └── Pezzo (l'aspetto del tassello una volta inserito nel quadro: nascosto finché non viene posizionato)
/// Il pezzo è solo decorativo: se contiene lo script Tassello (ad esempio perché è stato duplicato
/// da un tassello raccoglibile), lo script viene tolto automaticamente, così il pezzo resta immobile.
/// </summary>
public class SlotMosaico : MonoBehaviour
{
    [Tooltip("Il tassello nascosto nella Basilica che va in questo posto")]
    [SerializeField] private Tassello tasselloCorretto;

    [Tooltip("L'aspetto del tassello inserito nel quadro. Se vuoto, viene usato il primo figlio")]
    [SerializeField] private GameObject pezzo;

    /// <summary>True quando il tassello è stato inserito.</summary>
    public bool Pieno { get; private set; }

    /// <summary>Identificativo del tassello che va in questo posto.</summary>
    public string IdTassello => tasselloCorretto != null ? tasselloCorretto.Id : "";

    private Vector3 scalaPezzo = Vector3.one;
    private Vector3 posizionePezzo;
    private Quaternion rotazionePezzo;

    private void Awake()
    {
        if (pezzo == null && transform.childCount > 0)
            pezzo = transform.GetChild(0).gameObject;

        if (tasselloCorretto == null)
            Debug.LogWarning($"[SlotMosaico] '{name}' non ha un tassello corretto assegnato.", this);

        if (pezzo == null) return;

        // Il pezzo nel quadro è solo decorativo: niente ondeggiamento, rotazione o raccolta
        RimuoviScriptTassello();

        // Posizione, rotazione e scala del pezzo così come sono state sistemate nella scena
        scalaPezzo = pezzo.transform.localScale;
        posizionePezzo = pezzo.transform.localPosition;
        rotazionePezzo = pezzo.transform.localRotation;
    }

    /// <summary>Toglie lo script Tassello dal pezzo (e dai suoi figli), se presente.</summary>
    private void RimuoviScriptTassello()
    {
        Tassello[] script = pezzo.GetComponentsInChildren<Tassello>(true);
        if (script.Length == 0) return;

        foreach (Tassello t in script)
        {
            // Se il campo "Tassello Corretto" punta per errore al pezzo stesso, il quadro non potrebbe mai completarsi
            if (t == tasselloCorretto)
                Debug.LogError($"[SlotMosaico] '{name}': in 'Tassello Corretto' è stato messo il pezzo del quadro invece del " +
                               "tassello da raccogliere nella basilica. Trascina il tassello originale.", this);

            t.enabled = false; // Si ferma subito, prima ancora di partire
            Destroy(t);
        }

        Debug.LogWarning($"[SlotMosaico] Dal pezzo '{pezzo.name}' dello slot '{name}' è stato tolto lo script Tassello: " +
                         "il pezzo nel quadro deve essere solo decorativo. Puoi rimuoverlo anche a mano per non vedere questo avviso.", this);
    }

    /// <summary>Imposta subito lo stato, senza animazione (usato all'avvio).</summary>
    public void Ripristina(bool pieno)
    {
        Pieno = pieno;
        if (pezzo == null) return;

        pezzo.SetActive(pieno);
        pezzo.transform.localPosition = posizionePezzo;
        pezzo.transform.localRotation = rotazionePezzo;
        pezzo.transform.localScale = scalaPezzo;
    }

    /// <summary>Inserisce il tassello con un piccolo effetto.</summary>
    public IEnumerator Posiziona(ParticleSystem prefabPuf, AudioClip suono)
    {
        Pieno = true;

        if (prefabPuf != null)
        {
            ParticleSystem puf = Instantiate(prefabPuf, transform.position, Quaternion.identity);
            puf.Play();
            Destroy(puf.gameObject, puf.main.duration + puf.main.startLifetime.constantMax + 0.5f);
        }
        if (suono != null && SFXPlayer.Instance != null) SFXPlayer.Instance.Play(suono);

        if (pezzo == null) yield break;

        // Il pezzo compare nella sua posizione esatta, crescendo con un piccolo rimbalzo
        pezzo.transform.localPosition = posizionePezzo;
        pezzo.transform.localRotation = rotazionePezzo;
        pezzo.transform.localScale = Vector3.zero;
        pezzo.SetActive(true);

        float durata = 0.3f, t = 0f;
        while (t < durata)
        {
            t += Time.deltaTime;
            float x = Mathf.Clamp01(t / durata);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float k = 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
            pezzo.transform.localScale = scalaPezzo * k;
            yield return null;
        }
        pezzo.transform.localScale = scalaPezzo;
    }
}