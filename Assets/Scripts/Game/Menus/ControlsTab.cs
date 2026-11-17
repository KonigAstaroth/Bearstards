using UnityEngine;

public class ControlsTab : MonoBehaviour
{
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private GameObject backgroundPanel;

    private void Awake()
    {
        if(controlsPanel != null) controlsPanel.SetActive(false);
    }

    public void OpenControls()
    {
        controlsPanel.SetActive(true);
        backgroundPanel.SetActive(true);
    }

    public void CloseControls()
    {
        controlsPanel.SetActive(false);
        backgroundPanel.SetActive(false);
    }
}
