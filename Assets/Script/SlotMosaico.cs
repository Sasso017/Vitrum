using System.Collections;
using UnityEngine;

/// <summary>
/// Uno dei posti vuoti di un quadro del mosaico. Accetta un solo tassello, quello corretto.
/// Struttura:
///     Slot (questo script)
///     └── Pezzo (l'aspetto del tassello una volta inserito nel quadro: nascosto finché non viene posizionato)
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

    private void Awake()
    {
        if (pezzo == null && transform.childCount > 0)
            pezzo = transform.GetChild(0).gameObject;

        if (pezzo != null) scalaPezzo = pezzo.transform.localScale;

        if (tasselloCorretto == null)
            Debug.LogWarning($"[SlotMosaico] '{name}' non ha un tassello corretto assegnato.", this);
    }

    /// <summary>Imposta subito lo stato, senza animazione (usato all'avvio).</summary>
    public void Ripristina(bool pieno)
    {
        Pieno = pieno;
        if (pezzo != null)
        {
            pezzo.SetActive(pieno);
            pezzo.transform.localScale = scalaPezzo;
        }
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

        // Il pezzo compare crescendo con un piccolo rimbalzo
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
