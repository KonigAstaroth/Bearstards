using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;

public class CreateAndJoinRooms : MonoBehaviourPunCallbacks
{
    private const int LargoMaximo = 24;
    private const string MsgCuentaRepetida = "Esa cuenta ya está dentro de la sala.";

    public InputField createInput;
    public InputField joinInput;

    [Tooltip("Texto (Legacy) donde se muestran los avisos de la sala")]
    [SerializeField] private Text aviso;
    [SerializeField] private Button botonCrear;
    [SerializeField] private Button botonUnir;

    // Hay una operacion de sala en curso (crear, unirse o salir por cuenta repetida).
    private bool esperando;

    private void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        esperando = false;
        ActivarBotones(true);
        MostrarAviso("");

        if (createInput != null) createInput.onValueChanged.AddListener(LimpiarAviso);
        if (joinInput != null) joinInput.onValueChanged.AddListener(LimpiarAviso);
    }

    private void OnDestroy()
    {
        if (createInput != null) createInput.onValueChanged.RemoveListener(LimpiarAviso);
        if (joinInput != null) joinInput.onValueChanged.RemoveListener(LimpiarAviso);
    }

    public void CreateRoom()
    {
        if (esperando) return;
        if (!PuedeUsarSalas()) return;

        string nombre = LeerNombre(createInput);
        if (nombre == null) return;

        // Por si una salida anterior lo apago.
        PhotonNetwork.AutomaticallySyncScene = true;

        RoomOptions opciones = new RoomOptions
        {
            MaxPlayers = 2,
            PublishUserId = true,
            IsVisible = true,
            IsOpen = true
        };

        if (PhotonNetwork.CreateRoom(nombre, opciones))
        {
            Esperar("Creando sala...");
        }
        else
        {
            MostrarAviso("No se pudo crear la sala. Intenta de nuevo.");
        }
    }

    public void JoinRoom()
    {
        if (esperando) return;
        if (!PuedeUsarSalas()) return;

        string nombre = LeerNombre(joinInput);
        if (nombre == null) return;

        PhotonNetwork.AutomaticallySyncScene = true;

        if (PhotonNetwork.JoinRoom(nombre))
        {
            Esperar("Entrando a la sala...");
        }
        else
        {
            MostrarAviso("No se pudo entrar a la sala. Intenta de nuevo.");
        }
    }

    // Sin sesion web o sin Photon listo no se puede crear ni unirse a una room.
    private bool PuedeUsarSalas()
    {
        if (SessionManager.Instance == null || !SessionManager.Instance.HaySesion)
        {
            Debug.LogWarning("No hay sesion iniciada, no se puede usar la room");
            MostrarAviso("Inicia sesión para jugar.");
            return false;
        }

        if (!PhotonNetwork.IsConnectedAndReady)
        {
            Debug.LogWarning("Photon todavia no esta listo");
            MostrarAviso("Todavía conectando con el servidor, espera un momento.");
            return false;
        }

        return true;
    }

    // Devuelve el nombre limpio o null si no es valido (y avisa).
    private string LeerNombre(InputField campo)
    {
        string nombre = campo != null && campo.text != null ? campo.text.Trim() : "";

        if (nombre.Length == 0)
        {
            MostrarAviso("Escribe el nombre de la sala.");
            return null;
        }

        if (nombre.Length > LargoMaximo)
        {
            MostrarAviso($"El nombre de la sala es muy largo (máximo {LargoMaximo}).");
            return null;
        }

        return nombre;
    }

    public override void OnCreatedRoom()
    {
        Debug.Log("Created Room: " + PhotonNetwork.CurrentRoom.Name);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"No se pudo crear la room | Code: {returnCode} | Message: {message}");
        Terminar(MensajeDeError(returnCode));
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"No se pudo entrar a la room | Code: {returnCode} | Message: {message}");
        Terminar(MensajeDeError(returnCode));
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Ya estamos en la room " + PhotonNetwork.CurrentRoom.Name);

        // Respaldo por si el servidor deja pasar la misma cuenta dos veces.
        if (HayCuentaRepetida())
        {
            Debug.LogWarning("La misma cuenta ya esta en la room, saliendo");
            // Que no siga la escena del master mientras salimos.
            PhotonNetwork.AutomaticallySyncScene = false;
            esperando = true;
            ActivarBotones(false);
            MostrarAviso(MsgCuentaRepetida);
            PhotonNetwork.LeaveRoom();
            return;
        }

        MostrarAviso("");
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel("Store");
        }
    }

    // Tras salir por cuenta repetida se vuelve al master: ya se puede intentar otra vez.
    public override void OnConnectedToMaster()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        if (!esperando) return;
        esperando = false;
        ActivarBotones(true);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        if (!esperando) return;
        Terminar("Se perdió la conexión con el servidor.");
    }

    private bool HayCuentaRepetida()
    {
        int miId = PhotonIdentidad.ApiIdDe(PhotonNetwork.LocalPlayer);
        if (miId < 0) return false;

        // El master al crear esta solo, asi que la lista viene vacia.
        foreach (Player otro in PhotonNetwork.PlayerListOthers)
        {
            if (PhotonIdentidad.ApiIdDe(otro) == miId) return true;
        }
        return false;
    }

    private static string MensajeDeError(short codigo)
    {
        switch ((int)codigo)
        {
            case ErrorCode.GameIdAlreadyExists: return "Ya existe una sala con ese nombre.";
            case ErrorCode.GameDoesNotExist: return "No existe esa sala (o ya terminó).";
            case ErrorCode.GameFull: return "La sala está llena.";
            case ErrorCode.GameClosed: return "La sala ya está en partida.";
            case ErrorCode.JoinFailedFoundActiveJoiner:
            case ErrorCode.JoinFailedPeerAlreadyJoined: return MsgCuentaRepetida;
            default: return $"No se pudo entrar a la sala ({codigo}).";
        }
    }

    private void Esperar(string mensaje)
    {
        esperando = true;
        ActivarBotones(false);
        MostrarAviso(mensaje);
    }

    private void Terminar(string mensaje)
    {
        esperando = false;
        ActivarBotones(true);
        MostrarAviso(mensaje);
    }

    private void ActivarBotones(bool activos)
    {
        if (botonCrear != null) botonCrear.interactable = activos;
        if (botonUnir != null) botonUnir.interactable = activos;
    }

    private void LimpiarAviso(string _)
    {
        if (!esperando) MostrarAviso("");
    }

    private void MostrarAviso(string mensaje)
    {
        if (aviso != null) aviso.text = mensaje;
    }
}
