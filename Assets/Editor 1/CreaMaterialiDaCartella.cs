#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Crea materiali Standard a partire da una cartella di texture organizzata così:
///     CartellaTexture/
///         NomeMateriale/
///             ..._Albedo.(png/jpg)
///             ..._MetallicSmoothness.png   (opzionale)
///             ..._Normal.png               (opzionale)
///             impostazioni.txt             (opzionale: tiling, metallic, smoothness)
/// Uso: tasto destro sulla cartella nella finestra Project → "Crea materiali da questa cartella".
/// I materiali vengono salvati in una cartella "Materiali_NomeCartella" accanto a quella delle texture,
/// con gli stessi nomi dei materiali in Blender (così "Search and Remap" li abbina da solo).
/// Va messo in una cartella chiamata "Editor".
/// </summary>
public static class CreaMaterialiDaCartella
{
    [MenuItem("Assets/Crea materiali da questa cartella", true)]
    private static bool Valida()
    {
        string percorso = AssetDatabase.GetAssetPath(Selection.activeObject);
        return !string.IsNullOrEmpty(percorso) && AssetDatabase.IsValidFolder(percorso);
    }

    [MenuItem("Assets/Crea materiali da questa cartella", false, 1000)]
    private static void Crea()
    {
        string cartellaTexture = AssetDatabase.GetAssetPath(Selection.activeObject);
        string nomeCartella = Path.GetFileName(cartellaTexture);
        string genitore = Path.GetDirectoryName(cartellaTexture).Replace('\\', '/');
        string cartellaMateriali = $"{genitore}/Materiali_{nomeCartella}";

        if (!AssetDatabase.IsValidFolder(cartellaMateriali))
            AssetDatabase.CreateFolder(genitore, $"Materiali_{nomeCartella}");

        Shader standard = Shader.Find("Standard");
        string[] sottocartelle = AssetDatabase.GetSubFolders(cartellaTexture);
        int creati = 0;

        try
        {
            for (int i = 0; i < sottocartelle.Length; i++)
            {
                string cartella = sottocartelle[i];
                string nomeMateriale = Path.GetFileName(cartella);
                EditorUtility.DisplayProgressBar("Crea materiali", nomeMateriale, (float)i / sottocartelle.Length);

                Texture2D albedo = null, metallic = null, normal = null;

                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { cartella }))
                {
                    string percorso = AssetDatabase.GUIDToAssetPath(guid);
                    string nomeFile = Path.GetFileNameWithoutExtension(percorso);

                    if (nomeFile.EndsWith("_Albedo"))
                    {
                        ImpostaTexture(percorso, TextureImporterType.Default, true);
                        albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(percorso);
                    }
                    else if (nomeFile.EndsWith("_MetallicSmoothness"))
                    {
                        ImpostaTexture(percorso, TextureImporterType.Default, false);
                        metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(percorso);
                    }
                    else if (nomeFile.EndsWith("_Normal"))
                    {
                        ImpostaTexture(percorso, TextureImporterType.NormalMap, false);
                        normal = AssetDatabase.LoadAssetAtPath<Texture2D>(percorso);
                    }
                }

                // Impostazioni opzionali (tiling, metallic, smoothness)
                float tiling = 1f, valoreMetallic = 0f, valoreSmoothness = 0.2f;
                LeggiImpostazioni(cartella, ref tiling, ref valoreMetallic, ref valoreSmoothness);

                string percorsoMateriale = $"{cartellaMateriali}/{nomeMateriale}.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(percorsoMateriale);
                bool nuovo = mat == null;
                if (nuovo) mat = new Material(standard);
                else mat.shader = standard;

                mat.color = Color.white;
                mat.SetTexture("_MainTex", albedo);
                mat.SetTextureScale("_MainTex", new Vector2(tiling, tiling)); // Vale anche per le altre mappe

                if (metallic != null)
                {
                    mat.SetTexture("_MetallicGlossMap", metallic);
                    mat.EnableKeyword("_METALLICGLOSSMAP");
                    mat.SetFloat("_GlossMapScale", 1f);
                    mat.SetFloat("_SmoothnessTextureChannel", 0f);
                }
                else
                {
                    mat.SetTexture("_MetallicGlossMap", null);
                    mat.DisableKeyword("_METALLICGLOSSMAP");
                    mat.SetFloat("_Metallic", valoreMetallic);
                    mat.SetFloat("_Glossiness", valoreSmoothness);
                }

                if (normal != null)
                {
                    mat.SetTexture("_BumpMap", normal);
                    mat.SetFloat("_BumpScale", 1f);
                    mat.EnableKeyword("_NORMALMAP");
                }
                else
                {
                    mat.SetTexture("_BumpMap", null);
                    mat.DisableKeyword("_NORMALMAP");
                }

                if (nuovo) AssetDatabase.CreateAsset(mat, percorsoMateriale);
                else EditorUtility.SetDirty(mat);
                creati++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Crea materiali",
            $"{creati} materiali creati o aggiornati in {cartellaMateriali}.", "OK");
    }

    private static void LeggiImpostazioni(string cartella, ref float tiling, ref float metallic, ref float smoothness)
    {
        string file = Path.Combine(cartella, "impostazioni.txt");
        if (!File.Exists(file)) return;

        foreach (string riga in File.ReadAllLines(file))
        {
            string[] parti = riga.Split('=');
            if (parti.Length != 2) continue;

            string chiave = parti[0].Trim().ToLowerInvariant();
            if (!float.TryParse(parti[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float valore))
                continue;

            if (chiave == "tiling") tiling = valore;
            else if (chiave == "metallic") metallic = valore;
            else if (chiave == "smoothness") smoothness = valore;
        }
    }

    private static void ImpostaTexture(string percorso, TextureImporterType tipo, bool sRGB)
    {
        TextureImporter imp = AssetImporter.GetAtPath(percorso) as TextureImporter;
        if (imp == null) return;

        bool modificato = false;
        if (imp.textureType != tipo) { imp.textureType = tipo; modificato = true; }
        if (tipo != TextureImporterType.NormalMap && imp.sRGBTexture != sRGB) { imp.sRGBTexture = sRGB; modificato = true; }
        if (imp.wrapMode != TextureWrapMode.Repeat) { imp.wrapMode = TextureWrapMode.Repeat; modificato = true; }
        if (imp.maxTextureSize != 2048) { imp.maxTextureSize = 2048; modificato = true; }

        if (modificato) imp.SaveAndReimport();
    }
}
#endif
