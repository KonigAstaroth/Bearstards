
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
    [SerializeField] private Button homeButton;

    private const string WinnerKey = "WinnerActorNumber";
    private bool resultDisplayed;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        RefreshResult();

        if(homeButton != null) homeButton.gameObject.SetActive(false);
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
            resultText.color = Color.yellow;
        }
        else
        {
            resultText.text = "DEFEAT";
            resultText.color = Color.red;
        }

        resultDisplayed = true;
        StartCoroutine(GoToLobby());
    }

    private IEnumerator GoToLobby()
    {
        yield return new WaitForSecondsRealtime(5f);
        homeButton.gameObject.SetActive(true);
    }
}
