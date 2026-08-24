using UnityEngine;
using UnityEngine.UI;

public class Tile15 : MonoBehaviour
{
    public int targetIndex; // Indice corretto della tessera (da 0 a 14)
    public int currentIndex; // Posizione attuale nella griglia

    private Grid15Manager manager;
    private Button button;

    public void SetupTile(int target, int startGridIndex, Grid15Manager gridManager)
    {
        targetIndex = target;
        currentIndex = startGridIndex;
        manager = gridManager;

        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClickTile);
        }
    }

    void OnClickTile()
    {
        // Chiede al manager se questa tessera può muoversi nello spazio vuoto
        manager.TryMoveTile(this);
    }
}