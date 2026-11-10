using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// Claves de la sala de Photon y lectura del estado de la partida.
// Solo lee: quien escribe cada clave está anotado junto a ella.
// Todo sale de las propiedades de la sala, así que vale igual en los dos clientes.
public static class EstadoPartida
{
    // --- Claves que ya existían (MatchManager / ScoreManager) ---
    public const string ClaveInicio = "MatchStartTime";       // MatchManager (master), double
    public const string ClaveTerminada = "MatchEnded";        // MatchManager (master), bool
    public const string ClaveGanador = "WinnerActorNumber";   // MatchManager (master), int; -1 = empate
    public const string ClaveTiempoAgotado = "TimeIsUp";      // MatchManager (master), bool

    // --- Claves nuevas de la fase 2 ---
    public const string ClaveMatchId = "MatchId";             // MatchManager (master) al iniciar, string <= 64
    public const string ClaveActores = "Actores";             // MatchManager (master) al iniciar, int[] con los ActorNumber
    public const string ClaveAbandono = "AbandonoActor";      // MatchManager (master) si alguien se va, int ActorNumber

    // Por jugador (sufijo = ActorNumber)
    public static string ClavePuntos(int actor) => $"Score_{actor}";   // ScoreManager (dueño), int
    public static string ClaveKills(int actor) => $"Kills_{actor}";    // ScoreManager (dueño), int
    public static string ClaveApiId(int actor) => $"ApiId_{actor}";    // MatchManager (master) al iniciar, int
    public static string ClaveNombre(int actor) => $"Nombre_{actor}";  // MatchManager (master) al iniciar, string

    private static Hashtable Props =>
        PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.CustomProperties : null;

    private static T Leer<T>(string clave, T porDefecto)
    {
        Hashtable props = Props;
        if (props != null && props.TryGetValue(clave, out object valor) && valor is T t) return t;
        return porDefecto;
    }

    public static bool Iniciada => Props != null && Props.ContainsKey(ClaveInicio);
    public static bool Terminada => Leer(ClaveTerminada, false);
    public static string MatchId => Leer<string>(ClaveMatchId, null);

    // ActorNumber del ganador, o -1 si fue empate (o aún no hay ganador).
    public static int Ganador => Leer(ClaveGanador, -1);

    // ActorNumber de quien abandonó, o -1 si nadie abandonó.
    public static int ActorAbandono => Leer(ClaveAbandono, -1);
    public static bool PorAbandono => ActorAbandono != -1;

    // Los jugadores que empezaron la partida, aunque alguno ya se haya ido.
    public static IReadOnlyList<int> Actores
    {
        get
        {
            int[] actores = Leer<int[]>(ClaveActores, null);
            return actores ?? new int[0];
        }
    }

    public static int ApiId(int actor) => Leer(ClaveApiId(actor), -1);
    public static string Nombre(int actor) => Leer<string>(ClaveNombre(actor), "");
    public static int Puntos(int actor) => Leer(ClavePuntos(actor), 0);
    public static int Kills(int actor) => Leer(ClaveKills(actor), 0);

    // Resultado de un jugador según el ganador guardado: "victoria", "derrota" o "empate".
    public static string Resultado(int actor)
    {
        int ganador = Ganador;
        if (ganador == -1) return "empate";
        return ganador == actor ? "victoria" : "derrota";
    }

    // ActorNumber del jugador local, o -1 si no está en una sala.
    public static int ActorLocal =>
        PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1;

    public static Player BuscarJugador(int actor) =>
        PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.GetPlayer(actor) : null;
}
