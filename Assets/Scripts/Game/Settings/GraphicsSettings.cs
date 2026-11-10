
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;

public class GraphicsSettings : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown fpsDropdown;
    [SerializeField] private Toggle vSyncToggle;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject backgroundPanel;

    private Resolution[] resolutions;

    private const string ResolutionKey = "ResolutionIndex";
    private const string FPSKey = "TargetFPS";
    private const string VSyncKey = "VSync";

    private readonly int[] fpsOptions = { 30, 60, 120, 144, 165, 240, -1 };

    private bool isInitializing;

    private void Awake()
    {
        settingsPanel.SetActive(false);
        resolutions = Screen.resolutions;

        LoadSettings();
        ApplySettings();
    }

    private void Start()
    {
        InitializeUI();
    }

    private bool IsMenuScene()
    {
        string scene = SceneManager.GetActiveScene().name;

        return scene == "Login" ||
               scene == "Loading" ||
               scene == "Lobby" ||
               scene == "Results";
    }

    private void InitializeUI()
    {
        isInitializing = true;

        // RESOLUTIONS
        if (resolutionDropdown != null)
        {
            resolutionDropdown.ClearOptions();

            List<string> options = new List<string>();

            int currentResolutionIndex = 0;

            for (int i = 0; i < resolutions.Length; i++)
            {
                string option = resolutions[i].width + " x " +
                                resolutions[i].height;

                options.Add(option);

                if (resolutions[i].width == Screen.currentResolution.width &&
                    resolutions[i].height == Screen.currentResolution.height)
                {
                    currentResolutionIndex = i;
                }
            }

            resolutionDropdown.AddOptions(options);
            resolutionDropdown.value = currentResolutionIndex;
            resolutionDropdown.RefreshShownValue();
        }

        // FPS
        if (fpsDropdown != null)
        {
            fpsDropdown.ClearOptions();

            List<string> options = new List<string>();

            foreach (int fps in fpsOptions)
            {
                options.Add(fps == -1 ? "Unlimited" : fps + " FPS");
            }

            fpsDropdown.AddOptions(options);

            int savedFPS = PlayerPrefs.GetInt(FPSKey, 144);
            int fpsIndex = System.Array.IndexOf(fpsOptions, savedFPS);

            fpsDropdown.value = fpsIndex >= 0 ? fpsIndex : 3;
            fpsDropdown.RefreshShownValue();
        }

        // VSYNC
        if (vSyncToggle != null)
        {
            vSyncToggle.isOn = PlayerPrefs.GetInt(VSyncKey, 0) == 1;
        }

        isInitializing = false;
    }

    public void SetResolution(int index)
    {
        if (isInitializing || index < 0 || index >= resolutions.Length)
            return;

        PlayerPrefs.SetInt(ResolutionKey, index);

        Resolution resolution = resolutions[index];

        Screen.SetResolution(
            resolution.width,
            resolution.height,
            Screen.fullScreenMode
        );

        PlayerPrefs.Save();
    }

    public void SetFPS(int index)
    {
        if (isInitializing || index < 0 || index >= fpsOptions.Length)
            return;

        int fps = fpsOptions[index];

        PlayerPrefs.SetInt(FPSKey, fps);

        ApplySettings();
        PlayerPrefs.Save();
    }

    public void SetVSync(bool enabled)
    {
        if (isInitializing)
            return;

        PlayerPrefs.SetInt(VSyncKey, enabled ? 1 : 0);

        ApplySettings();
        PlayerPrefs.Save();
    }

    private void LoadSettings()
    {
        if (resolutions.Length == 0)
            return;

        int resolutionIndex = PlayerPrefs.GetInt(ResolutionKey, -1);

        if (resolutionIndex >= 0 && resolutionIndex < resolutions.Length)
        {
            Resolution resolution = resolutions[resolutionIndex];

            Screen.SetResolution(
                resolution.width,
                resolution.height,
                Screen.fullScreenMode
            );
        }
    }

    private void ApplySettings()
    {
        // MENUS ALWAYS RUN AT 60 FPS
        if (IsMenuScene())
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            return;
        }

        int targetFPS = PlayerPrefs.GetInt(FPSKey, 144);
        bool vSync = PlayerPrefs.GetInt(VSyncKey, 0) == 1;

        QualitySettings.vSyncCount = vSync ? 1 : 0;

        Application.targetFrameRate = vSync ? -1 : targetFPS;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySettings();
    }

    public void OpenSettings()
    {
        backgroundPanel.SetActive(true);
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        backgroundPanel.SetActive(false);
        settingsPanel.SetActive(false);
    }
}
