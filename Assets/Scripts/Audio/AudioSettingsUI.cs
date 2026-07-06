using UnityEngine;
using UnityEngine.UI;

public class AudioSettingsUI : MonoBehaviour
{
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider ambientSlider;
    [SerializeField] private Slider beachSlider;

    void Start()
    {
        AudioManager manager = AudioManager.EnsureInstance();
        if (manager == null)
        {
            return;
        }

        BindSlider(masterSlider, manager.MasterVolume, manager.SetMasterVolume);
        BindSlider(musicSlider, manager.MusicVolume, manager.SetMusicVolume);
        BindSlider(sfxSlider, manager.SfxVolume, manager.SetSfxVolume);
        BindSlider(ambientSlider, manager.AmbientVolume, manager.SetAmbientVolume);
        BindSlider(beachSlider, manager.BeachVolume, manager.SetBeachVolume);
    }

    static void BindSlider(Slider slider, float initialValue, System.Action<float> onChanged)
    {
        if (slider == null || onChanged == null)
        {
            return;
        }

        slider.SetValueWithoutNotify(initialValue);
        slider.onValueChanged.AddListener(value => onChanged(value));
    }
}