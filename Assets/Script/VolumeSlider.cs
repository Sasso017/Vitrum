using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Collega uno Slider UI al VolumeManager.
/// Mettilo sullo stesso GameObject dello Slider e scegli il tipo nell'Inspector.
/// </summary>
[RequireComponent(typeof(Slider))]
public class VolumeSlider : MonoBehaviour
{
    public enum VolumeType { Master, Music }

    [SerializeField] private VolumeType volumeType = VolumeType.Master;

    private Slider slider;

    private void Awake()
    {
        slider = GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
    }

    private void OnEnable()
    {
        if (VolumeManager.Instance == null)
        {
            Debug.LogWarning("[VolumeSlider] VolumeManager non presente nella scena.", this);
            return;
        }

        // Mostra il valore salvato senza far scattare l'evento
        slider.SetValueWithoutNotify(GetCurrentValue());
        slider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void OnDisable()
    {
        slider.onValueChanged.RemoveListener(OnSliderChanged);

        // Salva quando il menu impostazioni viene chiuso
        if (VolumeManager.Instance != null)
            VolumeManager.Instance.Save();
    }

    private void OnSliderChanged(float value)
    {
        if (VolumeManager.Instance == null) return;

        switch (volumeType)
        {
            case VolumeType.Master:
                VolumeManager.Instance.SetMasterVolume(value);
                break;
            case VolumeType.Music:
                VolumeManager.Instance.SetMusicVolume(value);
                break;
        }
    }

    private float GetCurrentValue()
    {
        return volumeType == VolumeType.Master
            ? VolumeManager.Instance.MasterVolume
            : VolumeManager.Instance.MusicVolume;
    }
}