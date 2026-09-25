using System.Collections;
using UnityEngine;

/// <summary>
/// Gestisce l'uscita da un minigioco verso la Basilica, con dissolvenza.
/// Mettilo su un GameObject in ogni scena di enigma e scegli l'enigma nell'Inspector.
///
/// Alla vittoria, dallo script del minigioco:
///     ritornoBasilica.CompletaETorna();
/// Per uscire senza aver risolto l'enigma (es. pulsante "Torna indietro"):
///     ritornoBasilica.TornaSenzaCompletare();
/// Entrambe le funzioni si possono anche collegare all'On Click di un pulsante.
/// </summary>
public class RitornoBasilica : MonoBehaviour
{
    [Tooltip("Quale enigma rappresenta questa scena")]
    [SerializeField] private Enigma enigma = Enigma.Mosaico;

    [Tooltip("Nome esatto della scena della Basilica")]
    [SerializeField] private string nomeScenaBasilica = "ScenaBasilica";

    [Tooltip("Secondi di attesa dopo la vittoria prima di iniziare la dissolvenza (per mostrare un messaggio)")]
    [SerializeField] private float ritardoDopoVittoria = 2f;

    [Tooltip("Opzionale: oggetto da mostrare alla vittoria (es. testo 'Hai ottenuto una chiave!')")]
    [SerializeField] private GameObject messaggioVittoria;

    [Tooltip("Opzionale: suono della chiave ottenuta (usa lo SFXPlayer)")]
    [SerializeField] private AudioClip suonoChiave;

    private bool inUscita = false;

    private void Start()
    {
        if (messaggioVittoria != null) messaggioVittoria.SetActive(false);
    }

    /// <summary>Segna l'enigma come completato (assegna la chiave) e torna nella Basilica.</summary>
    public void CompletaETorna()
    {
        if (inUscita) return; // Evita chiamate doppie
        inUscita = true;

        if (GameManager.instance != null)
            GameManager.instance.CompletaEnigma(enigma);
        else
            Debug.LogWarning("[RitornoBasilica] GameManager non trovato: la chiave non verrà salvata. " +
                             "Per un test completo avvia il gioco dalla StartMenu.", this);

        if (messaggioVittoria != null) messaggioVittoria.SetActive(true);
        if (suonoChiave != null && SFXPlayer.Instance != null) SFXPlayer.Instance.Play(suonoChiave);

        StartCoroutine(Torna(ritardoDopoVittoria));
    }

    /// <summary>Torna nella Basilica senza completare l'enigma.</summary>
    public void TornaSenzaCompletare()
    {
        if (inUscita) return;
        inUscita = true;
        StartCoroutine(Torna(0f));
    }

    private IEnumerator Torna(float ritardo)
    {
        // Tempo reale: funziona anche se il minigioco ha messo in pausa il tempo
        if (ritardo > 0f)
            yield return new WaitForSecondsRealtime(ritardo);

        // Dissolvenza al nero, caricamento della Basilica, dissolvenza in entrata
        SceneFader.Instance.CaricaScena(nomeScenaBasilica);
    }
}
