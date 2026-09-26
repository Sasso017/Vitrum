using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// I due enigmi della basilica. Ognuno, una volta completato, dà una chiave.
/// </summary>
public enum Enigma
{
    Mosaico,
    Gioco15
}

/// <summary>
/// Cervello della partita: ricorda gli enigmi completati (e quindi le chiavi raccolte)
/// e la posizione del giocatore tra un cambio scena e l'altro.
/// Persiste tra le scene e si azzera automaticamente quando si torna al menu principale.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public const int ChiaviTotali = 2;

    [Header("Stato dei minigiochi (salvati tra un cambio scena e l'altro)")]
    public bool isMosaicoCompletato = false;
    public bool isGioco15Completato = false;

    [Header("Tasselli del mosaico")]
    [Tooltip("Numero totale di tasselli da trovare. Se 0, vengono contati automaticamente i tasselli presenti nella scena")]
    [SerializeField] private int tasselliTotali = 0;
    [Tooltip("Tasselli già raccolti (si riempie da solo durante il gioco)")]
    public List<string> tasselliRaccolti = new List<string>();
    [Tooltip("Tasselli già posizionati nei quadri (si riempie da solo durante il gioco)")]
    public List<string> tasselliPosizionati = new List<string>();

    [Header("Eventi della storia")]
    [Tooltip("True dopo che la cutscene di ingresso nella Basilica è stata vista")]
    public bool introBasilicaVista = false;

    [Header("Posizione del giocatore prima di entrare in un minigioco")]
    public Vector3 ultimaPosizioneGiocatore;
    public Quaternion ultimaRotazioneGiocatore;
    public bool haPosizioneSalvata = false;

    [Header("Scene")]
    [Tooltip("Quando si carica questa scena, la partita viene azzerata")]
    [SerializeField] private string scenaMenuPrincipale = "StartMenu";

    /// <summary>
    /// Evento lanciato quando un enigma viene completato (utile per HUD delle chiavi, suoni, ecc.).
    /// </summary>
    public event Action<Enigma> OnEnigmaCompletato;

    /// <summary>Evento lanciato quando viene raccolto un tassello del mosaico.</summary>
    public event Action<string> OnTasselloRaccolto;

    // ---------- Proprietà utili ----------

    /// <summary>Numero di chiavi raccolte (una per enigma completato).</summary>
    public int ChiaviRaccolte
    {
        get
        {
            int conteggio = 0;
            if (isMosaicoCompletato) conteggio++;
            if (isGioco15Completato) conteggio++;
            return conteggio;
        }
    }

    /// <summary>True quando il giocatore ha tutte le chiavi (può aprire la porta).</summary>
    public bool HaTutteLeChiavi => ChiaviRaccolte >= ChiaviTotali;

    /// <summary>Quanti tasselli del mosaico sono stati raccolti.</summary>
    public int NumeroTasselliRaccolti => tasselliRaccolti.Count;

    /// <summary>Tasselli totali: il valore impostato, oppure quelli presenti nella scena.</summary>
    public int TasselliTotali => tasselliTotali > 0 ? tasselliTotali : Mathf.Max(Tassello.NumeroInScena, tasselliRaccolti.Count);

    /// <summary>True quando tutti i tasselli sono stati raccolti.</summary>
    public bool HaTuttiITasselli => TasselliTotali > 0 && NumeroTasselliRaccolti >= TasselliTotali;

    // ---------- Ciclo di vita ----------

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }
    }

    private void OnSceneLoaded(Scene scena, LoadSceneMode modalita)
    {
        // Tornando al menu principale la partita ricomincia da zero
        if (scena.name == scenaMenuPrincipale)
            NuovaPartita();
    }

    // ---------- Enigmi e chiavi ----------

    /// <summary>Restituisce true se l'enigma indicato è già stato completato.</summary>
    public bool IsCompletato(Enigma enigma)
    {
        switch (enigma)
        {
            case Enigma.Mosaico: return isMosaicoCompletato;
            case Enigma.Gioco15: return isGioco15Completato;
            default: return false;
        }
    }

    /// <summary>
    /// Segna un enigma come completato e assegna la relativa chiave.
    /// Da chiamare alla vittoria di ogni minigioco: GameManager.instance.CompletaEnigma(Enigma.Mosaico);
    /// </summary>
    public void CompletaEnigma(Enigma enigma)
    {
        if (IsCompletato(enigma)) return; // Già completato: nessuna chiave doppia

        switch (enigma)
        {
            case Enigma.Mosaico: isMosaicoCompletato = true; break;
            case Enigma.Gioco15: isGioco15Completato = true; break;
        }

        Debug.Log($"[GameManager] Enigma '{enigma}' completato. Chiavi: {ChiaviRaccolte}/{ChiaviTotali}");
        OnEnigmaCompletato?.Invoke(enigma);
    }

    // ---------- Tasselli del mosaico ----------

    /// <summary>True se il tassello con questo identificativo è già stato raccolto.</summary>
    public bool IsTasselloRaccolto(string id) => tasselliRaccolti.Contains(id);

    /// <summary>Segna un tassello come raccolto (chiamato dallo script Tassello).</summary>
    public void RaccogliTassello(string id)
    {
        if (string.IsNullOrEmpty(id) || tasselliRaccolti.Contains(id)) return;

        tasselliRaccolti.Add(id);
        Debug.Log($"[GameManager] Tassello '{id}' raccolto. Tasselli: {NumeroTasselliRaccolti}/{TasselliTotali}");
        OnTasselloRaccolto?.Invoke(id);
    }

    /// <summary>True se il tassello è già stato messo nel suo quadro.</summary>
    public bool IsTasselloPosizionato(string id) => tasselliPosizionati.Contains(id);

    /// <summary>Segna un tassello come posizionato nel suo quadro.</summary>
    public void PosizionaTassello(string id)
    {
        if (string.IsNullOrEmpty(id) || tasselliPosizionati.Contains(id)) return;
        tasselliPosizionati.Add(id);
    }

    // ---------- Posizione del giocatore ----------

    /// <summary>
    /// Salva posizione e rotazione del giocatore prima di entrare in un minigioco.
    /// </summary>
    public void SalvaPosizioneGiocatore(Transform giocatore)
    {
        if (giocatore == null) return;
        ultimaPosizioneGiocatore = giocatore.position;
        ultimaRotazioneGiocatore = giocatore.rotation;
        haPosizioneSalvata = true;
    }

    /// <summary>True se esiste una posizione a cui riportare il giocatore.</summary>
    public bool PosizioneDaRipristinare()
    {
        // Il controllo su Vector3.zero mantiene la compatibilità con gli script
        // che scrivono direttamente ultimaPosizioneGiocatore senza usare SalvaPosizioneGiocatore
        return haPosizioneSalvata || ultimaPosizioneGiocatore != Vector3.zero;
    }

    // ---------- Nuova partita ----------

    /// <summary>Azzera tutti i progressi. Chiamato automaticamente al ritorno nel menu principale.</summary>
    public void NuovaPartita()
    {
        isMosaicoCompletato = false;
        isGioco15Completato = false;

        introBasilicaVista = false;
        tasselliRaccolti.Clear();
        tasselliPosizionati.Clear();

        ultimaPosizioneGiocatore = Vector3.zero;
        ultimaRotazioneGiocatore = Quaternion.identity;
        haPosizioneSalvata = false;

        Debug.Log("[GameManager] Nuova partita: progressi azzerati.");
    }
}