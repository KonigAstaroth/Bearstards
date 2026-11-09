using UnityEditor;
using UnityEngine;

// Crea Assets/Resources/ApiConfig.asset si no existe
public static class CrearApiConfig
{
    private const string Ruta = "Assets/Resources/ApiConfig.asset";

    [MenuItem("Bearstards/Crear ApiConfig")]
    public static void Crear()
    {
        var existente = AssetDatabase.LoadAssetAtPath<ApiConfig>(Ruta);
        if (existente != null)
        {
            Debug.Log("[CrearApiConfig] Ya existe: " + Ruta);
            Selection.activeObject = existente;
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        var config = ScriptableObject.CreateInstance<ApiConfig>();
        AssetDatabase.CreateAsset(config, Ruta);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CrearApiConfig] Creado: " + Ruta);
        Selection.activeObject = config;
    }
}
