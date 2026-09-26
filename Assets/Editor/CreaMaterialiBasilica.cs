#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Crea automaticamente i materiali della Basilica a partire dalle texture.
/// Ogni sottocartella di "Assets/Texture_Basilica" diventa un materiale Standard
/// con lo stesso nome della cartella (= nome del materiale in Blender),
/// salvato in "Assets/Materiali_Basilica".
/// Uso: menu Tools → Basilica → Crea materiali dalle texture.
/// Va messo in una cartella chiamata "Editor" (es. Assets/Editor/).
/// </summary>
public static class CreaMaterialiBasilica
{
    private const string CartellaTexture = "Assets/Texture_Basilica";
    private const string CartellaMateriali = "Assets/Materiali_Basilica";

    [MenuItem("Tools/Basilica/Crea materiali dalle texture")]
    private static void CreaMateriali()
    {
        if (!AssetDatabase.IsValidFolder(CartellaTexture))
        {
            EditorUtility.DisplayDialog("Crea materiali",
                $"Cartella '{CartellaTexture}' non trovata.\nCopia la cartella Texture_Basilica dentro Assets.", "OK");
            return;
        }

        if (!AssetDatabase.IsValidFolder(CartellaMateriali))
            AssetDatabase.CreateFolder("Assets", "Materiali_Basilica");

        Shader standard = Shader.Find("Standard");
        string[] sottocartelle = AssetDatabase.GetSubFolders(CartellaTexture);
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
                        // Dati, non colore: niente sRGB
                        ImpostaTexture(percorso, TextureImporterType.Default, false);
                        metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(percorso);
                    }
                    else if (nomeFile.EndsWith("_Normal"))
                    {
                        ImpostaTexture(percorso, TextureImporterType.NormalMap, false);
                        normal = AssetDatabase.LoadAssetAtPath<Texture2D>(percorso);
                    }
                }

                // Crea il materiale o aggiorna quello esistente
                string percorsoMateriale = $"{CartellaMateriali}/{nomeMateriale}.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(percorsoMateriale);
                bool nuovo = mat == null;
                if (nuovo) mat = new Material(standard);
                else mat.shader = standard;

                mat.SetTexture("_MainTex", albedo);
                mat.color = Color.white;

                if (metallic != null)
                {
                    mat.SetTexture("_MetallicGlossMap", metallic);
                    mat.EnableKeyword("_METALLICGLOSSMAP");
                    mat.SetFloat("_GlossMapScale", 1f);
                    mat.SetFloat("_SmoothnessTextureChannel", 0f); // Levigatezza nel canale alfa della mappa metallic
                }
                else
                {
                    mat.SetTexture("_MetallicGlossMap", null);
                    mat.DisableKeyword("_METALLICGLOSSMAP");
                    mat.SetFloat("_Metallic", 0f);
                    mat.SetFloat("_Glossiness", 0.2f);
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
            $"{creati} materiali creati o aggiornati in {CartellaMateriali}.", "OK");
    }

    private static void ImpostaTexture(string percorso, TextureImporterType tipo, bool sRGB)
    {
        TextureImporter imp = AssetImporter.GetAtPath(percorso) as TextureImporter;
        if (imp == null) return;

        bool modificato = false;
        if (imp.textureType != tipo) { imp.textureType = tipo; modificato = true; }
        if (tipo != TextureImporterType.NormalMap && imp.sRGBTexture != sRGB) { imp.sRGBTexture = sRGB; modificato = true; }
        if (imp.maxTextureSize != 2048) { imp.maxTextureSize = 2048; modificato = true; }

        if (modificato) imp.SaveAndReimport();
    }
}
#endif
