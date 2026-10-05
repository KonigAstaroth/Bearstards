using UnityEngine;
using TMPro;

public class RespawningUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI countdownText;

    public void SetCountDown(int seconds)
    {
        if (seconds <= 0)
        {
            countdownText.text = "VAMOS!";
        }
        else
        {
            countdownText.text = "Reapareciendo en: " + seconds.ToString();
        }
    }
}
