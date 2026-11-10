
using Photon.Pun;
using TMPro;
using UnityEngine;
using System.Collections;

public class ResultsManager : MonoBehaviourPunCallbacks
{
    [Header("UI")]
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text estadisticasText;
    [Header("Bears")]
    [SerializeField] private Animator localBearController;
    [SerializeField] private Animator enemyBearController;

    [Header("UI transition")]
    [SerializeField] private CanvasGroup homePanel;
    [SerializeField] private float fadeDuration = 1f;

    // Cuánto esperamos a que llegue MatchEnded antes de armar el reporte
    private const float EsperaTerminada = 3f;

    private bool resultDisplayed;
    private bool reporteIniciado;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (homePanel != null) homePanel.alpha = 0;
        MostrarEstadisticas(string.Empty);

        RefreshResult();
    }

    public override void OnRoomPropertiesUpdate(
        ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(EstadoPartida.ClaveGanador)
            || propertiesThatChanged.ContainsKey(EstadoPartida.ClaveTerminada)
            || propertiesThatChanged.ContainsKey(EstadoPartida.ClaveAbandono))
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

        // Las propiedades de la sala siguen ahí aunque el rival ya se haya ido
        var properties = PhotonNetwork.CurrentRoom.CustomProperties;
        bool hayGanador = properties.ContainsKey(EstadoPartida.ClaveGanador);
        if (!hayGanador && !EstadoPartida.PorAbandono)
        {
            resultText.text = "CALCULATING RESULT...";
            return;
        }

        int myActorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        int abandono = EstadoPartida.ActorAbandono;
        bool porAbandono = abandono != -1;

        // Con abandono gana el que se quedó, aunque el marcador diga otra cosa
        int winnerActorNumber;
        if (porAbandono)
            winnerActorNumber = abandono == myActorNumber ? -2 : myActorNumber;
        else
            winnerActorNumber = EstadoPartida.Ganador;

        if (winnerActorNumber == -1)
        {
            resultText.text = "IT'S A TIE!";
            resultText.color = Color.green;
            Animar(localBearController, "TIE");
            Animar(enemyBearController, "TIE");
        }
        else if (winnerActorNumber == myActorNumber)
        {
            resultText.text = porAbandono ? "VICTORY!\n<size=40%>(your opponent left)</size>" : "VICTORY!";
            resultText.color = HexToColor("#FFA400");
            Animar(localBearController, "Victory");
            Animar(enemyBearController, "Defeat");
        }
        else
        {
            resultText.text = porAbandono && abandono == myActorNumber
                ? "DEFEAT\n<size=40%>(you left the match)</size>"
                : "DEFEAT";
            resultText.color = Color.red;
            Animar(localBearController, "Defeat");
            Animar(enemyBearController, "Victory");
        }

        resultDisplayed = true;
        StartCoroutine(GoToLobby());
        ReportarPartida();
    }

    // Guarda la partida en la API una sola vez y muestra las estadísticas nuevas
    private async void ReportarPartida()
    {
        if (reporteIniciado) return;
        reporteIniciado = true;

        MostrarEstadisticas("Saving match...");

        // MatchEnded debería venir junto con el ganador; por si acaso esperamos un poco
        float limite = Time.realtimeSinceStartup + EsperaTerminada;
        while (!EstadoPartida.Terminada && PhotonNetwork.InRoom && Time.realtimeSinceStartup < limite)
        {
            await Awaitable.NextFrameAsync();
            if (this == null) return;
        }

        ResultadoEnvio estado;
        try
        {
            estado = await ReportePartida.ReportarPartidaActual();
        }
        catch (System.Exception e)
        {
            Debug.LogError("[ResultsManager] Error al reportar la partida: " + e);
            estado = ResultadoEnvio.SinDatos;
        }
        if (this == null) return;

        switch (estado)
        {
            case ResultadoEnvio.Guardada:
            case ResultadoEnvio.YaEnviada:
                await MostrarGuardada();
                break;
            case ResultadoEnvio.Pendiente:
                // Si la API invalidó la sesión al intentarlo, no fue falta de conexión
                bool sinSesion = SessionManager.Instance == null || !SessionManager.Instance.HaySesion;
                MostrarEstadisticas(sinSesion
                    ? "Session expired: log in again to save this match"
                    : "Offline: it will be saved later");
                break;
            default:
                MostrarEstadisticas("Could not save the match");
                break;
        }
    }

    private async Awaitable MostrarGuardada()
    {
        SessionManager sesion = SessionManager.Instance;
        if (sesion == null || !sesion.TieneTokenGuardado)
        {
            MostrarEstadisticas("Match saved");
            return;
        }

        try
        {
            await sesion.RefrescarJugador();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[ResultsManager] No se pudo refrescar el jugador: " + e.Message);
        }
        if (this == null) return;

        JugadorDto jugador = SessionManager.Instance != null ? SessionManager.Instance.Jugador : null;
        if (jugador == null)
        {
            MostrarEstadisticas("Match saved");
            return;
        }

        MostrarEstadisticas("Match saved\n"
            + $"Total score: {jugador.puntos_totales} · Matches: {jugador.partidas} · Victories: {jugador.victorias}");
    }

    private void MostrarEstadisticas(string texto)
    {
        if (estadisticasText != null) estadisticasText.text = texto;
    }

    private static void Animar(Animator animator, string trigger)
    {
        if (animator != null) animator.SetTrigger(trigger);
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
