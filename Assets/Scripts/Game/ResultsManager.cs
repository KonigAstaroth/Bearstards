
using Photon.Pun;
using TMPro;
using UnityEngine;
using ExitGames.Client.Photon;

public class ResultsManager : MonoBehaviourPunCallbacks
{
    [Header("UI")]
    [SerializeField] private TMP_Text resultText;

    private const string WinnerKey = "WinnerActorNumber";

    private void Start()
    {
        RefreshResult();
    }

    public override void OnRoomPropertiesUpdate(
        Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(WinnerKey))
        {
            RefreshResult();
        }
    }

    private void RefreshResult()
    {
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
            resultText.text = "¡TIE!";
        }
        else if (winnerActorNumber == myActorNumber)
        {
            resultText.text = "¡VICTORY!";
        }
        else
        {
            resultText.text = "DEFEAT";
        }
    }
}
