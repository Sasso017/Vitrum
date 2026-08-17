using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Grid15Manager : MonoBehaviour
{
    public static Grid15Manager instance;

    [Header("Riferimenti")]
    public Tile15[] tiles;                // Le 15 tessere (0 - 14)
    public RectTransform[] gridPositions; // I 16 spazi della griglia (0 - 15)
    public string nomeScenaBasilica = "ScenaBasilica";

    [Header("Impostazioni Animazione")]
    public float moveSpeed = 25f; // Velocità aumentata per risposte super reattive

    private int emptyIndex = 15; // Lo slot 15 (il 16°) parte vuoto
    private bool isGameFinished = false;

    // Traccia la Coroutine di ciascuna tessera per poterla interrompere se viene ri-cliccata o mossa
    private Dictionary<Tile15, Coroutine> activeCoroutines = new Dictionary<Tile15, Coroutine>();

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        InizializzaGriglia();
        ShuffleGrid();
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

        // Verifica l'adiacenza sulla posizione LOGICA (che è già aggiornata)
        if (IsAdjacent(tile.currentIndex, emptyIndex))
        {
            int targetSlot = emptyIndex;
            emptyIndex = tile.currentIndex;
            tile.currentIndex = targetSlot; // La posizione interna cambia ALL'ISTANTE

            // Se la tessera si stava già muovendo, interrompi la vecchia animazione
            if (activeCoroutines.ContainsKey(tile) && activeCoroutines[tile] != null)
            {
                StopCoroutine(activeCoroutines[tile]);
            }

            // Avvia la nuova animazione verso la nuova destinazione
            Coroutine newAnim = StartCoroutine(AnimateMove(tile, gridPositions[targetSlot].anchoredPosition));
            activeCoroutines[tile] = newAnim;
        }
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
        int shuffleSteps = 80;
        for (int i = 0; i < shuffleSteps; i++)
        {
            List<Tile15> validTiles = new List<Tile15>();
            foreach (Tile15 t in tiles)
            {
                if (IsAdjacent(t.currentIndex, emptyIndex))
                {
                    validTiles.Add(t);
                }
            }

            Tile15 randomTile = validTiles[Random.Range(0, validTiles.Count)];
            int temp = emptyIndex;
            emptyIndex = randomTile.currentIndex;
            randomTile.currentIndex = temp;

            randomTile.GetComponent<RectTransform>().anchoredPosition = gridPositions[temp].anchoredPosition;
        }
    }

    void CheckVictory()
    {
        if (emptyIndex != 15) return;

        foreach (Tile15 t in tiles)
        {
            if (t.currentIndex != t.targetIndex) return;
        }

        isGameFinished = true;
        Debug.Log("GIOCO DEL 15 COMPLETATO!");

        if (GameManager.instance != null)
        {
            GameManager.instance.isMosaicoCompletato = true;
        }

        Invoke("TornaAllaBasilica", 1.2f);
    }

    void TornaAllaBasilica()
    {
        SceneManager.LoadScene(nomeScenaBasilica);
    }
}