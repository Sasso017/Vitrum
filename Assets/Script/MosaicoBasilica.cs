using UnityEngine;

/// <summary>
/// Gestisce l'enigma del mosaico nella Basilica: quando tutti i quadri sono completi,
/// mostra un messaggio e fa emergere la chiave (ChiaveRicompensa con Enigma = Mosaico),
/// che il giocatore raccoglie con E. La chiave viene assegnata al momento della raccolta.
/// Mettilo su un GameObject vuoto nella ScenaBasilica.
/// </summary>
public class MosaicoBasilica : MonoBehaviour
{
    [Tooltip("I quadri del mosaico da completare")]
    [SerializeField] private QuadroMosaico[] quadri;

    [Tooltip("La chiave che compare al centro della Basilica (ChiaveRicompensa con Enigma = Mosaico)")]
    [SerializeField] private ChiaveRicompensa chiave;

    [Header("Completamento")]
    [SerializeField] private string testoCompletato = "Il mosaico è completo! Una chiave è apparsa al centro della basilica";
    [SerializeField] private float durataMessaggio = 4f;
    [SerializeField] private AudioClip suonoCompletato;

    private bool completato = false;

    private void Start()
    {
        if (quadri == null || quadri.Length == 0)
            quadri = FindObjectsOfType<QuadroMosaico>();

        foreach (QuadroMosaico q in quadri)
            if (q != null) q.OnCompletato += ControllaCompletamento;

        // Se i quadri erano già completi (es. scena ricaricata) ma la chiave non è ancora stata raccolta,
        // la chiave ricompare senza ripetere il messaggio
        Invoke(nameof(ControllaAllAvvio), 0.1f);
    }

    private void OnDestroy()
    {
        if (quadri == null) return;
        foreach (QuadroMosaico q in quadri)
            if (q != null) q.OnCompletato -= ControllaCompletamento;
    }

    private void ControllaAllAvvio()
    {
        if (TuttiCompleti())
        {
            completato = true;
            if (chiave != null) chiave.Compari();
        }
    }

    private void ControllaCompletamento()
    {
        if (completato || !TuttiCompleti()) return;
        completato = true;

        MessaggiSchermo.MostraMessaggio(testoCompletato, durataMessaggio);
        if (suonoCompletato != null && SFXPlayer.Instance != null) SFXPlayer.Instance.Play(suonoCompletato);

        if (chiave != null) chiave.Compari();
        else Debug.LogWarning("[MosaicoBasilica] Nessuna chiave assegnata.", this);
    }

    private bool TuttiCompleti()
    {
        if (quadri == null || quadri.Length == 0) return false;
        foreach (QuadroMosaico q in quadri)
            if (q == null || !q.Completo) return false;
        return true;
    }
}
