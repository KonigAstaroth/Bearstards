using System;

// DTOs de la API. Los nombres de campo coinciden exactamente con el JSON (snake_case).

[Serializable]
public class JugadorDto
{
    public int id;
    public string username;
    public int partidas;
    public int victorias;
    public int derrotas;
    public int empates;
    public int kills;
    public int puntos_totales;
    public int mejor_puntuacion;
}

[Serializable]
public class SesionDto
{
    public string token;
    public JugadorDto jugador;
}

[Serializable]
public class LoginPeticion
{
    public string username;
    public string password;
    public string cliente = "juego";
}

[Serializable]
public class RegistroPeticion
{
    public string username;
    public string email;
    public string password;
    public string cliente = "juego";
}

[Serializable]
public class ErrorDto
{
    public string detail;
}

// Resultado de una llamada a la API
public class ApiResultado<T>
{
    public bool Ok;
    public long Status;
    public T Datos;
    public string Error;

    public bool SinConexion => Status == 0;
}
