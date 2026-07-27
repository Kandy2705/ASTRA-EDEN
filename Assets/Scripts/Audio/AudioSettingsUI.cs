using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AudioSettingsUI : MonoBehaviour
{
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider ambientSlider;
    [SerializeField] private Slider beachSlider;

    private TMP_Text masterValueText;
    private TMP_Text sfxValueText;
    private AudioManager manager;

    private void Start()
    {
        ResolveReferences();
        manager = AudioManager.EnsureInstance();
        if (manager == null)
        {
            return;
        }

        BindSlider(masterSlider, manager.MasterVolume, HandleMasterChanged);
        BindSlider(musicSlider, manager.MusicVolume, manager.SetMusicVolume);
        BindSlider(sfxSlider, manager.SfxVolume, HandleSfxChanged);
        BindSlider(ambientSlider, manager.AmbientVolume, manager.SetAmbientVolume);
        BindSlider(beachSlider, manager.BeachVolume, manager.SetBeachVolume);

        UpdateValueText(masterValueText, manager.MasterVolume);
        UpdateValueText(sfxValueText, manager.SfxVolume);
    }

    private void OnDestroy()
    {
        if (masterSlider != null)
        {
            masterSlider.onValueChanged.RemoveListener(HandleMasterChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveListener(HandleSfxChanged);
        }
    }

    private void ResolveReferences()
    {
        SettingsPanelController settings = GetComponent<SettingsPanelController>();
        Transform audioRoot =
            settings != null && settings.audioContent != null
                ? settings.audioContent.transform
                : transform;

        if (masterSlider == null)
        {
            Transform row = FindChildByName(audioRoot, "MasterVolumeRow");
            masterSlider = row != null ? row.GetComponentInChildren<Slider>(true) : null;
            masterValueText = FindValueText(row);
        }

        if (sfxSlider == null)
        {
            Transform row = FindChildByName(audioRoot, "SFXVolumeRow");
            sfxSlider = row != null ? row.GetComponentInChildren<Slider>(true) : null;
            sfxValueText = FindValueText(row);
        }
    }

    private void HandleMasterChanged(float value)
    {
        manager?.SetMasterVolume(value);
        UpdateValueText(masterValueText, value);
    }

    private void HandleSfxChanged(float value)
    {
        manager?.SetSfxVolume(value);
        UpdateValueText(sfxValueText, value);
    }

    public void ResetToDefaults()
    {
        manager = AudioManager.EnsureInstance();
        if (manager == null)
        {
            return;
        }

        manager.SetMasterVolume(1f);
        manager.SetSfxVolume(1f);

        masterSlider?.SetValueWithoutNotify(1f);
        sfxSlider?.SetValueWithoutNotify(1f);
        UpdateValueText(masterValueText, 1f);
        UpdateValueText(sfxValueText, 1f);
    }

    private static void BindSlider(Slider slider, float initialValue, UnityEngine.Events.UnityAction<float> onChanged)
    {
        if (slider == null || onChanged == null)
        {
            return;
        }

        slider.SetValueWithoutNotify(initialValue);
        slider.onValueChanged.AddListener(onChanged);
    }

    private static TMP_Text FindValueText(Transform row)
    {
        if (row == null)
        {
            return null;
        }

        TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name == "ValueText")
            {
                return texts[i];
            }
        }

        return null;
    }

    private static void UpdateValueText(TMP_Text text, float value)
    {
        if (text != null)
        {
            text.text = $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
        }
    }

    private static Transform FindChildByName(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == objectName)
            {
                return children[i];
            }
        }

        return null;
    }
}
