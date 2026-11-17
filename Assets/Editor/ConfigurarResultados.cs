using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Agrega a Results el texto con el estado del guardado y las estadísticas.
// Batch: -batchmode -executeMethod ConfigurarResultados.Configurar -quit
public static class ConfigurarResultados
{
    private const string RutaEscena = "Assets/Scenes/Results.unity";
    private const string NombreTexto = "EstadisticasText";

    [MenuItem("Bearstards/Configurar Results")]
    public static void Configurar()
    {
        try
        {
            Scene escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);

            ResultsManager manager = null;
            foreach (GameObject raiz in escena.GetRootGameObjects())
            {
                manager = raiz.GetComponentInChildren<ResultsManager>(true);
                if (manager != null) break;
            }
            if (manager == null) throw new Exception("No se encontró ResultsManager en " + RutaEscena);

            SerializedObject so = new SerializedObject(manager);
            SerializedProperty propResultado = so.FindProperty("resultText");
            if (propResultado == null) throw new Exception("ResultsManager no tiene el campo 'resultText'");
            TMP_Text resultado = propResultado.objectReferenceValue as TMP_Text;
            if (resultado == null) throw new Exception("ResultsManager.resultText no está asignado");

            Transform padre = resultado.transform.parent;
            if (padre == null) throw new Exception("resultText no tiene padre");

            // Idempotente: se borra y se vuelve a crear
            foreach (GameObject raiz in escena.GetRootGameObjects())
            {
                Transform viejo = BuscarRecursivo(raiz.transform, NombreTexto);
                while (viejo != null)
                {
                    UnityEngine.Object.DestroyImmediate(viejo.gameObject);
                    viejo = BuscarRecursivo(raiz.transform, NombreTexto);
                }
            }

            GameObject go = UnityEngine.Object.Instantiate(resultado.gameObject, padre, false);
            go.name = NombreTexto;
            go.transform.SetSiblingIndex(resultado.transform.GetSiblingIndex() + 1);
            for (int i = go.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
            }

            TMP_Text texto = go.GetComponent<TMP_Text>();
            texto.text = string.Empty;
            texto.enableAutoSizing = false;
            texto.fontSize = 34;
            texto.fontStyle = FontStyles.Normal;
            texto.color = new Color(1f, 1f, 1f, 0.9f);
            texto.horizontalAlignment = HorizontalAlignmentOptions.Center;
            texto.verticalAlignment = VerticalAlignmentOptions.Top;
            texto.textWrappingMode = TextWrappingModes.Normal;
            texto.overflowMode = TextOverflowModes.Overflow;
            texto.margin = Vector4.zero;
            texto.raycastTarget = false;

            // El resultado está centrado en y=106 con alto 70 (de 71 a 141).
            // Este va debajo, con espacio para la segunda línea de "abandono".
            RectTransform rt = texto.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 1f);
            Vector2 posResultado = resultado.rectTransform.anchoredPosition;
            float bordeInferior = posResultado.y - resultado.rectTransform.sizeDelta.y * 0.5f;
            rt.anchoredPosition = new Vector2(posResultado.x, bordeInferior - 40f);
            rt.sizeDelta = new Vector2(1000f, 120f);
            rt.localScale = Vector3.one;

            AsignarCampo(so, "estadisticasText", texto);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(escena);
            if (!EditorSceneManager.SaveScene(escena)) throw new Exception("No se pudo guardar " + RutaEscena);

            Debug.Log("[ConfigurarResultados] OK");
        }
        catch (Exception e)
        {
            Debug.LogError("[ConfigurarResultados] Error: " + e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    private static void AsignarCampo(SerializedObject so, string campo, UnityEngine.Object valor)
    {
        SerializedProperty prop = so.FindProperty(campo);
        if (prop == null) throw new Exception("ResultsManager no tiene el campo '" + campo + "'");
        prop.objectReferenceValue = valor;
    }

    private static Transform BuscarRecursivo(Transform padre, string nombre)
    {
        if (padre.name == nombre) return padre;
        foreach (Transform hijo in padre)
        {
            Transform t = BuscarRecursivo(hijo, nombre);
            if (t != null) return t;
        }
        return null;
    }
}
