using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;
using WebSocketSharp;

public class CreateAndJoinRooms : MonoBehaviourPunCallbacks
{
    public InputField createInput;
    public InputField joinInput;

    private void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    public void CreateRoom()
    {
        Debug.Log("Create Room Clicked");
        if (!HaySesion()) return;
        Debug.Log("Nombre del room: " + createInput.text);

        Debug.Log("Photon connected: " + PhotonNetwork.IsConnected);
        Debug.Log("Photon ready: " + PhotonNetwork.IsConnectedAndReady);

        string createRoomName = createInput.text.Trim();

        if (!createRoomName.IsNullOrEmpty())
        {
            PhotonNetwork.CreateRoom(createRoomName);
        }
    }

    public void JoinRoom()
    {
        if (!HaySesion()) return;

        if (!PhotonNetwork.IsConnectedAndReady)
        {
            Debug.LogWarning("Photon todavia no esta listo");
            return;
        }

        string joinRoomName = joinInput.text.Trim();

        if (!joinRoomName.IsNullOrEmpty())
        {
            PhotonNetwork.JoinRoom(joinRoomName);
        }
    }

    // Sin sesion web no se puede crear ni unirse a una room.
    private bool HaySesion()
    {
        if (SessionManager.Instance != null && SessionManager.Instance.HaySesion) return true;

        Debug.LogWarning("No hay sesion iniciada, no se puede usar la room");
        return false;
    }

    public override void OnCreatedRoom()
    {
        Debug.Log("Created Room: " + PhotonNetwork.CurrentRoom.Name);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError("FAIL ON CREATING ROOM " + returnCode);
        Debug.LogError("Mensaje " + message);
    }

     public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"FAIL ON JOINING ROOM | Code: {returnCode} | Message: {message}");
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Ya estamos en la room " + PhotonNetwork.CurrentRoom.Name);
        if (PhotonNetwork.IsMasterClient)
        { 
            PhotonNetwork.LoadLevel("Store");
        }
    }
}
