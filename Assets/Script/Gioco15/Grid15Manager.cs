using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class Grid15Manager : MonoBehaviour
{
    public static Grid15Manager instance;

    [Header("Riferimenti")]
    public Tile15[] tiles;                // Le 15 tessere (0 - 14)
    public RectTransform[] gridPositions; // I 16 spazi della griglia (0 - 15)

    [Header("Fine partita")]
    [Tooltip("La postazione del minigioco nella Basilica: alla vittoria fa emergere la chiave e riporta in prima persona")]
    public StazioneMinigioco stazione;
    [Tooltip("Usato solo se non c'è una postazione (vecchia scena separata del Gioco del 15)")]
    public string nomeScenaBasilica = "ScenaBasilica";

    [Header("Impostazioni Animazione")]
    public float moveSpeed = 25f; // Reattività elevata per gestire i click veloci

    [Header("Mescolamento")]
    [Tooltip("Numero di mosse casuali usate per mescolare le tessere")]
    public int shuffleSteps = 80;

    [Header("Trucchi")]
    [Tooltip("F9 risolve il puzzle, F10 lo lascia a una sola mossa dalla soluzione. Funzionano solo mentre si gioca al minigioco")]
    public bool abilitaTrucchi = true;

    private int emptyIndex = 15; // Lo slot 15 (il 16°) parte vuoto
    private bool isGameFinished = false;

    // Traccia le animazioni attive per consentire interruzioni fluide
    private Dictionary<Tile15, Coroutine> activeCoroutines = new Dictionary<Tile15, Coroutine>();

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        // Nella Basilica il cursore lo gestisce la StazioneMinigioco quando il giocatore inizia a giocare
        if (stazione == null)
        {
            // Vecchia scena separata: serve il cursore libero da subito
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        InizializzaGriglia();

        // Se il gioco del 15 è già stato completato, le tessere restano in ordine
        if (GameManager.instance != null && GameManager.instance.IsCompletato(Enigma.Gioco15))
        {
            isGameFinished = true;
            return;
        }

        ShuffleGrid();
    }

    private void Update()
    {
        if (!abilitaTrucchi || isGameFinished) return;

        // Nella Basilica i trucchi funzionano solo mentre si sta giocando al minigioco
        if (stazione != null && !StazioneMinigioco.InUso) return;

        if (TruccoPremuto(9)) RisolviSubito();
        else if (TruccoPremuto(10)) QuasiRisolto();
    }

    void InizializzaGriglia()
    {
        for (int i = 0; i < tiles.Length; i++)
        {
            tiles[i].SetupTile(i, i, this);
            tiles[i].GetComponent<RectTransform>().anchoredPosition = gridPositions[i].anchoredPosition;
        }
        emptyIndex = 15;
    }

    public void TryMoveTile(Tile15 tile)
    {
        if (isGameFinished) return;

        // Verifica l'adiacenza sulla posizione logica istantanea
        if (IsAdjacent(tile.currentIndex, emptyIndex))
        {
            int targetSlot = emptyIndex;
            emptyIndex = tile.currentIndex;
            tile.currentIndex = targetSlot; // La posizione logica cambia SUBITO

            MuoviTessera(tile, targetSlot);
        }
    }

    /// <summary>Avvia (o riavvia) l'animazione di una tessera verso uno slot.</summary>
    void MuoviTessera(Tile15 tile, int slot)
    {
        // Se la tessera si stava ancora muovendo, interrompe la vecchia animazione
        if (activeCoroutines.ContainsKey(tile) && activeCoroutines[tile] != null)
        {
            StopCoroutine(activeCoroutines[tile]);
        }

        activeCoroutines[tile] = StartCoroutine(AnimateMove(tile, gridPositions[slot].anchoredPosition));
    }

    bool IsAdjacent(int index1, int index2)
    {
        int row1 = index1 / 4;
        int col1 = index1 % 4;
        int row2 = index2 / 4;
        int col2 = index2 % 4;

        return (Mathf.Abs(row1 - row2) + Mathf.Abs(col1 - col2)) == 1;
    }

    IEnumerator AnimateMove(Tile15 tile, Vector2 targetPos)
    {
        RectTransform rect = tile.GetComponent<RectTransform>();

        while (Vector2.Distance(rect.anchoredPosition, targetPos) > 0.5f)
        {
            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, targetPos, Time.deltaTime * moveSpeed);
            yield return null;
        }

        rect.anchoredPosition = targetPos;
        activeCoroutines[tile] = null;

        CheckVictory();
    }

    void ShuffleGrid()
    {
        // Rimescola finché il risultato non è davvero in disordine
        do
        {
            Tile15 lastMoved = null;

            for (int i = 0; i < shuffleSteps; i++)
            {
                List<Tile15> validTiles = new List<Tile15>();
                foreach (Tile15 t in tiles)
                {
                    // Esclude la tessera appena mossa, per non annullare la mossa precedente
                    if (t != lastMoved && IsAdjacent(t.currentIndex, emptyIndex))
                    {
                        validTiles.Add(t);
                    }
                }

                Tile15 randomTile = validTiles[Random.Range(0, validTiles.Count)];
                int temp = emptyIndex;
                emptyIndex = randomTile.currentIndex;
                randomTile.currentIndex = temp;
                lastMoved = randomTile;

                // Posizionamento immediato senza animazione durante lo shuffle
                randomTile.GetComponent<RectTransform>().anchoredPosition = gridPositions[temp].anchoredPosition;
            }
        }
        while (IsSolved());
    }

    bool IsSolved()
    {
        if (emptyIndex != 15) return false;

        foreach (Tile15 t in tiles)
        {
            if (t.currentIndex != t.targetIndex) return false;
        }
        return true;
    }

    void CheckVictory()
    {
        if (isGameFinished || !IsSolved()) return;

        // --- VITTORIA CONFERMATA ---
        isGameFinished = true;
        Debug.Log("GIOCO DEL 15 COMPLETATO!");

        if (stazione != null)
        {
            // Gioco nella Basilica: la postazione riporta in prima persona e fa emergere la chiave
            stazione.CompletaMinigioco();
        }
        else
        {
            // Vecchia scena separata: salva il completamento e torna alla Basilica
            if (GameManager.instance != null)
                GameManager.instance.CompletaEnigma(Enigma.Gioco15);

            Invoke(nameof(TornaAllaBasilica), 1.2f);
        }
    }

    void TornaAllaBasilica()
    {
        SceneFader.Instance.CaricaScena(nomeScenaBasilica);
    }

    // ---------- Trucchi ----------

    /// <summary>F9: tutte le tessere scorrono al loro posto e la vittoria scatta normalmente.</summary>
    void RisolviSubito()
    {
        Debug.Log("[Grid15Manager] Trucco F9: puzzle risolto.");

        foreach (Tile15 t in tiles)
        {
            t.currentIndex = t.targetIndex;
            MuoviTessera(t, t.targetIndex);
        }
        emptyIndex = 15;
        // La vittoria viene controllata da AnimateMove quando le tessere arrivano a destinazione
    }

    /// <summary>F10: sistema tutte le tessere tranne l'ultima, così basta un click per vincere.</summary>
    void QuasiRisolto()
    {
        Debug.Log("[Grid15Manager] Trucco F10: manca una mossa (clicca la tessera in basso a destra).");

        foreach (Tile15 t in tiles)
        {
            // La tessera che andrebbe nello slot 14 viene lasciata nello slot 15 (in basso a destra)
            int slot = t.targetIndex == 14 ? 15 : t.targetIndex;
            t.currentIndex = slot;
            MuoviTessera(t, slot);
        }
        emptyIndex = 14;
    }

    private bool TruccoPremuto(int tastoF)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return false;
        return tastoF == 9 ? Keyboard.current.f9Key.wasPressedThisFrame
                           : Keyboard.current.f10Key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(tastoF == 9 ? KeyCode.F9 : KeyCode.F10);
#endif
    }
}