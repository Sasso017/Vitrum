using System.Collections;
using UnityEngine;

/// <summary>
/// Gestisce l'enigma del mosaico nella Basilica. Quando il giocatore ha raccolto tutti i tasselli
/// e interagisce con uno dei quadri, i tasselli vengono inseriti tutti insieme in tutti i quadri,
/// con un unico audio. Poi compare il messaggio e la chiave emerge (ChiaveRicompensa con Enigma = Mosaico),
/// che il giocatore raccoglie con E. La chiave viene assegnata al momento della raccolta.
/// Mettilo su un GameObject vuoto nella ScenaBasilica.
/// </summary>
public class MosaicoBasilica : MonoBehaviour
{
    [Tooltip("I quadri del mosaico. Se vuoto, vengono cercati nella scena")]
    [SerializeField] private QuadroMosaico[] quadri;

    [Tooltip("La chiave che compare al centro della Basilica (ChiaveRicompensa con Enigma = Mosaico)")]
    [SerializeField] private ChiaveRicompensa chiave;

    [Header("Ricomposizione")]
    [Tooltip("L'unico audio che accompagna la ricomposizione del mosaico")]
    [SerializeField] private AudioClip suonoRicomposizione;
    [Tooltip("Lo stesso prefab dell'effetto 'puf' dei lucchetti (opzionale)")]
    [SerializeField] private ParticleSystem prefabPuf;
    [Tooltip("Tempo tra la comparsa di un tassello e il successivo (effetto a cascata). 0 = tutti nello stesso istante")]
    [SerializeField] private float intervalloTraTasselli = 0.12f;
    [Tooltip("Attesa dopo l'ultimo tassello, prima del messaggio e della chiave")]
    [SerializeField] private float attesaFinale = 0.6f;

    [Header("Completamento")]
    [Tooltip("Audio del puzzle completato: suona alla fine della ricomposizione, insieme al messaggio")]
    [SerializeField] private AudioClip suonoCompletato;
    [SerializeField] private string testoCompletato = "Il mosaico è completo! Una chiave è apparsa al centro della basilica";
    [SerializeField] private float durataMessaggio = 4f;

    /// <summary>True quando il mosaico è stato ricomposto.</summary>
    public bool Completato { get; private set; }

    /// <summary>True mentre i tasselli vengono inseriti.</summary>
    public bool InCorso { get; private set; }

    private void Start()
    {
        if (quadri == null || quadri.Length == 0)
            quadri = FindObjectsOfType<QuadroMosaico>();

        // Se i quadri erano già completi (es. scena ricaricata) ma la chiave non è ancora stata raccolta,
        // la chiave ricompare senza ripetere l'animazione
        Invoke(nameof(ControllaAllAvvio), 0.1f);
    }

    private void ControllaAllAvvio()
    {
        if (TuttiIQuadriCompleti())
        {
            Completato = true;
            if (chiave != null) chiave.Compari();
        }
    }

    // ---------- Stato dei tasselli ----------

    /// <summary>Numero totale di posti nei quadri.</summary>
    public int TasselliTotali()
    {
        int n = 0;
        foreach (QuadroMosaico q in quadri) if (q != null) n += q.Slot.Length;
        return n;
    }

    /// <summary>Quanti dei tasselli destinati ai quadri il giocatore ha raccolto.</summary>
    public int TasselliRaccolti()
    {
        GameManager gm = GameManager.instance;
        if (gm == null) return 0;

        int n = 0;
        foreach (QuadroMosaico q in quadri)
        {
            if (q == null) continue;
            foreach (SlotMosaico s in q.Slot)
                if (gm.IsTasselloRaccolto(s.IdTassello)) n++;
        }
        return n;
    }

    /// <summary>True quando il giocatore ha raccolto tutti i tasselli di tutti i quadri.</summary>
    public bool TuttiITasselliRaccolti()
    {
        int totali = TasselliTotali();
        return totali > 0 && TasselliRaccolti() >= totali;
    }

    private bool TuttiIQuadriCompleti()
    {
        if (quadri == null || quadri.Length == 0) return false;
        foreach (QuadroMosaico q in quadri)
            if (q == null || !q.Completo) return false;
        return true;
    }

    // ---------- Ricomposizione ----------

    /// <summary>Inserisce tutti i tasselli in tutti i quadri. Chiamato dal QuadroMosaico premendo E.</summary>
    public void Ricomponi()
    {
        if (Completato || InCorso || !TuttiITasselliRaccolti()) return;
        StartCoroutine(Sequenza());
    }

    private IEnumerator Sequenza()
    {
        InCorso = true;
        GameManager gm = GameManager.instance;

        // Un unico audio per tutta la ricomposizione
        if (suonoRicomposizione != null && SFXPlayer.Instance != null)
            SFXPlayer.Instance.Play(suonoRicomposizione);

        // I tasselli compaiono a cascata in tutti i quadri (ognuno con il suo sbuffo, senza suono proprio)
        foreach (QuadroMosaico q in quadri)
        {
            if (q == null) continue;
            foreach (SlotMosaico s in q.Slot)
            {
                if (s.Pieno) continue;
                if (gm != null) gm.PosizionaTassello(s.IdTassello);
                StartCoroutine(s.Posiziona(prefabPuf, null));
                if (intervalloTraTasselli > 0f) yield return new WaitForSeconds(intervalloTraTasselli);
            }
        }

        yield return new WaitForSeconds(attesaFinale);

        Completato = true;
        InCorso = false;

        if (suonoCompletato != null && SFXPlayer.Instance != null)
            SFXPlayer.Instance.Play(suonoCompletato);

        MessaggiSchermo.MostraMessaggio(testoCompletato, durataMessaggio);

        if (chiave != null) chiave.Compari();
        else Debug.LogWarning("[MosaicoBasilica] Nessuna chiave assegnata.", this);
    }
}