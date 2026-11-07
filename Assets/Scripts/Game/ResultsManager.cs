
using Photon.Pun;
using TMPro;
using UnityEngine;
using ExitGames.Client.Photon;
using UnityEngine.UI;
using System.Collections;

public class ResultsManager : MonoBehaviourPunCallbacks
{
    [Header("UI")]
    [SerializeField] private TMP_Text resultText;
    [Header("Bears")]
    [SerializeField] private Animator localBearController;
    [SerializeField] private Animator enemyBearController;

    [Header("UI transition")]
    [SerializeField] private CanvasGroup homePanel;
    [SerializeField] private float fadeDuration = 1f;

    private const string WinnerKey = "WinnerActorNumber";
    private bool resultDisplayed;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        homePanel.alpha = 0;

        RefreshResult();

    }

    public override void OnRoomPropertiesUpdate(
        ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(WinnerKey))
        {
            RefreshResult();
        }
    }

    private void RefreshResult()
    {
        if (resultDisplayed) return;

        if (resultText == null)
            return;

        if (!PhotonNetwork.InRoom)
        {
            resultText.text = "MATCH FINISHED";
            return;
        }

        var properties = PhotonNetwork.CurrentRoom.CustomProperties;

        if (!properties.TryGetValue(WinnerKey, out object value))
        {
            resultText.text = "CALCULATING RESULT...";
            return;
        }

        int winnerActorNumber = (int)value;
        int myActorNumber = PhotonNetwork.LocalPlayer.ActorNumber;

        if (winnerActorNumber == -1)
        {
            resultText.text = "TIE!";
            resultText.color = Color.green;
        }
        else if (winnerActorNumber == myActorNumber)
        {
            resultText.text = "VICTORY!";
            resultText.color = HexToColor("#FFA400");
            localBearController.SetTrigger("Victory");
            enemyBearController.SetTrigger("Defeat");
        }
        else
        {
            resultText.text = "DEFEAT";
            resultText.color = Color.red;
            localBearController.SetTrigger("Defeat");
            enemyBearController.SetTrigger("Victory");
        }

        resultDisplayed = true;
        StartCoroutine(GoToLobby());
    }

    private IEnumerator GoToLobby()
    {
        yield return new WaitForSecondsRealtime(5f);

        if (homePanel == null) yield break;

        homePanel.gameObject.SetActive(true);
        homePanel.alpha = 0f;

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            homePanel.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);

            yield return null;
        }

        homePanel.alpha = 1f;
    }

    private Color HexToColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color color);
        return color;
    }
}
