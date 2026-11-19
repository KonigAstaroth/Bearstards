
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

    [Header("Resolution")]
    [SerializeField] private FullScreenMode screenMode = FullScreenMode.Windowed;

    private Resolution[] resolutions;
    private readonly List<Resolution> dropdownResolutions = new List<Resolution>();

    private const string ResolutionWidthKey = "ResolutionWidth";
    private const string ResolutionHeightKey = "ResolutionHeight";
    private const string FPSKey = "TargetFPS";
    private const string VSyncKey = "VSync";

    private readonly int[] fpsOptions =
    {
        30, 60, 120, 144, 165, 240, -1
    };

    private int selectedResolutionIndex;
    private bool isInitializing;

    private void Awake()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        resolutions = Screen.resolutions;
        DontDestroyOnLoad(transform.gameObject);

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

            dropdownResolutions.Clear();

            List<string> options = new List<string>();

            int savedWidth = PlayerPrefs.GetInt(
                ResolutionWidthKey, Screen.width);

            int savedHeight = PlayerPrefs.GetInt(
                ResolutionHeightKey, Screen.height);

            selectedResolutionIndex = 0;

            for (int i = 0; i < resolutions.Length; i++)
            {
                Resolution resolution = resolutions[i];

                bool existe = dropdownResolutions.Exists(r =>
                    r.width == resolution.width &&
                    r.height == resolution.height);

                if (existe)
                    continue;

                dropdownResolutions.Add(resolution);

                options.Add(resolution.width + " x " +
                            resolution.height);

                if (resolution.width == savedWidth &&
                    resolution.height == savedHeight)
                {
                    selectedResolutionIndex = dropdownResolutions.Count - 1;
                }
            }

            resolutionDropdown.AddOptions(options);
            resolutionDropdown.value = selectedResolutionIndex;
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
            int index = System.Array.IndexOf(fpsOptions, savedFPS);

            fpsDropdown.value = index >= 0 ? index : 3;
            fpsDropdown.RefreshShownValue();
        }

        // VSYNC
        if (vSyncToggle != null)
        {
            vSyncToggle.isOn =
                PlayerPrefs.GetInt(VSyncKey, 0) == 1;
        }

        isInitializing = false;
    }

    // Called by Resolution Dropdown
    public void SetResolution(int index)
    {
        if (isInitializing || index < 0)
            return;

        selectedResolutionIndex = index;
    }

    // Called by FPS Dropdown
    public void SetFPS(int index)
    {
        if (isInitializing || index < 0 ||
            index >= fpsOptions.Length)
            return;

        PlayerPrefs.SetInt(FPSKey, fpsOptions[index]);
    }

    // Called by VSync Toggle
    public void SetVSync(bool enabled)
    {
        if (isInitializing)
            return;

        PlayerPrefs.SetInt(VSyncKey, enabled ? 1 : 0);
    }

    private void ApplySettings()
    {
        // RESOLUTION: APPLY IN ALL SCENES
        int width = PlayerPrefs.GetInt(
            ResolutionWidthKey, Screen.width);

        int height = PlayerPrefs.GetInt(
            ResolutionHeightKey, Screen.height);

        if (width > 0 && height > 0 &&
            (Screen.width != width || Screen.height != height))
        {
            Screen.SetResolution(width, height, screenMode);

            Debug.Log($"Applying resolution: {width}x{height}");
        }

        // MENUS: ALWAYS 60 FPS
        if (IsMenuScene())
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            return;
        }

        // FPS AND VSYNC
        int targetFPS = PlayerPrefs.GetInt(FPSKey, 144);
        bool vSync = PlayerPrefs.GetInt(VSyncKey, 0) == 1;

        QualitySettings.vSyncCount = vSync ? 1 : 0;

        Application.targetFrameRate = vSync ? -1 : targetFPS;

        Debug.Log(
            $"GRAPHICS SETTINGS | FPS: {Application.targetFrameRate} | " +
            $"VSync: {QualitySettings.vSyncCount} | " +
            $"Resolution: {Screen.width}x{Screen.height}"
        );
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
        // SAVE SELECTED RESOLUTION
        if (dropdownResolutions.Count > 0)
        {
            int index = Mathf.Clamp(
                selectedResolutionIndex,
                0,
                dropdownResolutions.Count - 1);

            Resolution resolution = dropdownResolutions[index];

            PlayerPrefs.SetInt(ResolutionWidthKey, resolution.width);
            PlayerPrefs.SetInt(ResolutionHeightKey, resolution.height);
        }

        // APPLY ALL SETTINGS
        PlayerPrefs.Save();
        ApplySettings();

        backgroundPanel.SetActive(false);
        settingsPanel.SetActive(false);
    }
}
