using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Tooltip("Scena da caricare premendo GIOCA. Se vuoto, carica la scena successiva nelle Build Settings")]
    [SerializeField] private string nomeScenaDaCaricare = "IntroCutscene";

    private bool caricamentoAvviato = false;

    public void PlayGame()
    {
        // Evita doppi click durante la dissolvenza
        if (caricamentoAvviato) return;
        caricamentoAvviato = true;

        string scena = nomeScenaDaCaricare;

        if (string.IsNullOrEmpty(scena))
        {
            // Comportamento precedente: la scena successiva nelle Build Settings
            int indice = SceneManager.GetActiveScene().buildIndex + 1;
            scena = System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(indice));
        }

        // Dissolvenza al nero, caricamento in sottofondo, dissolvenza in entrata
        SceneFader.Instance.CaricaScena(scena);
    }

    public void QuitGame()
    {
        Debug.Log("Quit!");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
