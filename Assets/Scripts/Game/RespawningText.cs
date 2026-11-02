using UnityEngine;
using TMPro;

public class RespawningUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI countdownText;

    public void SetCountDown(int seconds)
    {
        if (seconds <= 0)
        {
            countdownText.text = "GO!";
        }
        else
        {
            countdownText.text = "Respawning in: " + seconds.ToString();
        }
    }
}
