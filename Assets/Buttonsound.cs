using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Aggiunge un suono al click (e opzionalmente al passaggio del mouse) di un pulsante.
/// Mettilo sullo stesso GameObject del componente Button.
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonSound : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] private AudioClip clickSound;
    [Tooltip("Opzionale: suono quando il mouse passa sopra il pulsante")]
    [SerializeField] private AudioClip hoverSound;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        button.onClick.AddListener(PlayClick);
    }

    private void OnDisable()
    {
        button.onClick.RemoveListener(PlayClick);
    }

    private void PlayClick()
    {
        if (SFXPlayer.Instance != null)
            SFXPlayer.Instance.Play(clickSound, volume);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverSound == null || !button.interactable) return;

        if (SFXPlayer.Instance != null)
            SFXPlayer.Instance.Play(hoverSound, volume);
    }
}