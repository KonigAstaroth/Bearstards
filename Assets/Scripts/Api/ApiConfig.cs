using System;
using UnityEngine;

// Configuración de la API de Bearstards. El asset vive en Assets/Resources/ApiConfig.asset
[CreateAssetMenu(menuName = "Bearstards/ApiConfig")]
public class ApiConfig : ScriptableObject
{
    public string urlProduccion = "https://littlebearstards.com/api";
    public string urlLocal = "http://localhost:8000/api";
    public bool usarLocal = false;
    public int timeoutSegundos = 15;

    // URL base sin la barra final
    public string UrlBase => ((usarLocal ? urlLocal : urlProduccion) ?? string.Empty).Trim().TrimEnd('/');

    private static ApiConfig actual;

    public static ApiConfig Actual
    {
        get
        {
            if (actual == null)
            {
                actual = Resources.Load<ApiConfig>("ApiConfig");
                if (actual == null)
                {
                    // Sin asset: usamos valores por defecto
                    actual = CreateInstance<ApiConfig>();
                    actual.hideFlags = HideFlags.DontSave;
                }

                // "-apiLocal" en la línea de comandos fuerza el servidor local
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-apiLocal") >= 0)
                {
                    actual.usarLocal = true;
                }

                Debug.Log("[API] URL base: " + actual.UrlBase);
            }
            return actual;
        }
    }
}
