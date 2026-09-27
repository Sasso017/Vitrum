#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Crea materiali a partire da una cartella di texture organizzata così:
///     CartellaTexture/
///         NomeMateriale/
///             ..._Albedo.(png/jpg)
///             ..._MetallicSmoothness.png   (opzionale)
///             ..._Normal.png               (opzionale)
///             impostazioni.txt             (opzionale, vedi sotto)
///
/// Opzioni di impostazioni.txt (una per riga, tutte facoltative):
///     tiling=2               ripetizione uguale sui due assi
///     tilingx=0.29 / tilingy=1   ripetizione diversa sui due assi
///     metallic=0 / smoothness=0.2   valori usati se manca la mappa MetallicSmoothness
///     rendering=opaque | cutout | fade | transparent
///         cutout      = bordi netti, parti trasparenti completamente bucate (tessuti strappati, foglie)
///         fade        = sfumato, anche i riflessi svaniscono (fiamme, fumo)
///         transparent = vetro: trasparente ma con riflessi lucidi
///     emissione=2            il materiale emette luce (usa l'albedo come colore della luce)
///     doppiafaccia=1         visibile da entrambi i lati (solo con rendering=cutout,
///                            usa lo shader "Basilica/Tessuto Doppia Faccia")
///
/// Uso: tasto destro sulla cartella nella finestra Project → "Crea materiali da questa cartella".
/// I materiali vengono salvati in "Materiali_NomeCartella" accanto a quella delle texture.
/// Va messo in una cartella chiamata "Editor".
/// </summary>
public static class CreaMaterialiDaCartella
{
    private class Impostazioni
    {
        public Vector2 tiling = Vector2.one;
        public float metallic = 0f;
        public float smoothness = 0.2f;
        public string rendering = "opaque";
        public float emissione = 0f;
        public bool doppiaFaccia = false;
    }

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
        Shader doppiaFaccia = Shader.Find("Basilica/Tessuto Doppia Faccia");
        string[] sottocartelle = AssetDatabase.GetSubFolders(cartellaTexture);
        int creati = 0;
        var avvisi = new List<string>();

        try
        {
            for (int i = 0; i < sottocartelle.Length; i++)
            {
                string cartella = sottocartelle[i];
                string nomeMateriale = Path.GetFileName(cartella);
                EditorUtility.DisplayProgressBar("Crea materiali", nomeMateriale, (float)i / sottocartelle.Length);

                Impostazioni imp = LeggiImpostazioni(cartella);
                bool conAlfa = imp.rendering != "opaque";

                Texture2D albedo = null, metallic = null, normal = null;
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { cartella }))
                {
                    string percorso = AssetDatabase.GUIDToAssetPath(guid);
                    string nomeFile = Path.GetFileNameWithoutExtension(percorso);

                    if (nomeFile.EndsWith("_Albedo"))
                    {
                        ImpostaTexture(percorso, TextureImporterType.Default, true, conAlfa);
                        albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(percorso);
                    }
                    else if (nomeFile.EndsWith("_MetallicSmoothness"))
                    {
                        ImpostaTexture(percorso, TextureImporterType.Default, false, false);
                        metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(percorso);
                    }
                    else if (nomeFile.EndsWith("_Normal"))
                    {
                        ImpostaTexture(percorso, TextureImporterType.NormalMap, false, false);
                        normal = AssetDatabase.LoadAssetAtPath<Texture2D>(percorso);
                    }
                }

                // Shader: doppia faccia solo per i materiali "cutout" che la richiedono
                bool usaDoppiaFaccia = imp.doppiaFaccia && imp.rendering == "cutout";
                if (usaDoppiaFaccia && doppiaFaccia == null)
                {
                    avvisi.Add($"{nomeMateriale}: shader 'Basilica/Tessuto Doppia Faccia' non trovato, uso Standard (visibile da un solo lato)");
                    usaDoppiaFaccia = false;
                }
                Shader shader = usaDoppiaFaccia ? doppiaFaccia : standard;

                string percorsoMateriale = $"{cartellaMateriali}/{nomeMateriale}.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(percorsoMateriale);
                bool nuovo = mat == null;
                if (nuovo) mat = new Material(shader);
                else mat.shader = shader;

                mat.color = Color.white;
                mat.SetTexture("_MainTex", albedo);
                mat.SetTextureScale("_MainTex", imp.tiling);

                if (usaDoppiaFaccia)
                    ConfiguraDoppiaFaccia(mat, metallic, normal, imp);
                else
                    ConfiguraStandard(mat, albedo, metallic, normal, imp);

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

        string messaggio = $"{creati} materiali creati o aggiornati in {cartellaMateriali}.";
        if (avvisi.Count > 0) messaggio += "\n\nAttenzione:\n" + string.Join("\n", avvisi);
        EditorUtility.DisplayDialog("Crea materiali", messaggio, "OK");
    }

    // ---------- Materiale Standard ----------

    private static void ConfiguraStandard(Material mat, Texture2D albedo, Texture2D metallic, Texture2D normal, Impostazioni imp)
    {
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
            mat.SetFloat("_Metallic", imp.metallic);
            mat.SetFloat("_Glossiness", imp.smoothness);
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

        ImpostaModalita(mat, imp.rendering);

        if (imp.emissione > 0f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", albedo);
            mat.SetColor("_EmissionColor", Color.white * imp.emissione);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }
    }

    /// <summary>Imposta la modalità di rendering del materiale Standard (come il menu "Rendering Mode").</summary>
    private static void ImpostaModalita(Material mat, string modalita)
    {
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        switch (modalita)
        {
            case "cutout":
                mat.SetFloat("_Mode", 1f);
                mat.SetOverrideTag("RenderType", "TransparentCutout");
                mat.SetInt("_SrcBlend", (int)BlendMode.One);
                mat.SetInt("_DstBlend", (int)BlendMode.Zero);
                mat.SetInt("_ZWrite", 1);
                mat.SetFloat("_Cutoff", 0.5f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)RenderQueue.AlphaTest;
                break;

            case "fade":
                mat.SetFloat("_Mode", 2f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = (int)RenderQueue.Transparent;
                break;

            case "transparent":
                mat.SetFloat("_Mode", 3f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)BlendMode.One);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = (int)RenderQueue.Transparent;
                break;

            default: // opaque
                mat.SetFloat("_Mode", 0f);
                mat.SetOverrideTag("RenderType", "");
                mat.SetInt("_SrcBlend", (int)BlendMode.One);
                mat.SetInt("_DstBlend", (int)BlendMode.Zero);
                mat.SetInt("_ZWrite", 1);
                mat.renderQueue = -1;
                break;
        }
    }

    // ---------- Materiale a doppia faccia ----------

    private static void ConfiguraDoppiaFaccia(Material mat, Texture2D metallic, Texture2D normal, Impostazioni imp)
    {
        mat.SetFloat("_Cutoff", 0.5f);

        mat.SetTexture("_MetallicGlossMap", metallic);
        mat.SetFloat("_UsaMetallicMap", metallic != null ? 1f : 0f);
        mat.SetFloat("_Metallic", imp.metallic);
        mat.SetFloat("_Glossiness", imp.smoothness);
        mat.SetFloat("_GlossMapScale", 1f);

        mat.SetTexture("_BumpMap", normal);
        mat.SetFloat("_BumpScale", 1f);
    }

    // ---------- Utilità ----------

    private static Impostazioni LeggiImpostazioni(string cartella)
    {
        var imp = new Impostazioni();
        string file = Path.Combine(cartella, "impostazioni.txt");
        if (!File.Exists(file)) return imp;

        foreach (string riga in File.ReadAllLines(file))
        {
            string[] parti = riga.Split('=');
            if (parti.Length != 2) continue;

            string chiave = parti[0].Trim().ToLowerInvariant();
            string testo = parti[1].Trim().ToLowerInvariant();

            if (chiave == "rendering") { imp.rendering = testo; continue; }

            if (!float.TryParse(testo, NumberStyles.Float, CultureInfo.InvariantCulture, out float valore))
                continue;

            switch (chiave)
            {
                case "tiling": imp.tiling = new Vector2(valore, valore); break;
                case "tilingx": imp.tiling.x = valore; break;
                case "tilingy": imp.tiling.y = valore; break;
                case "metallic": imp.metallic = valore; break;
                case "smoothness": imp.smoothness = valore; break;
                case "emissione": imp.emissione = valore; break;
                case "doppiafaccia": imp.doppiaFaccia = valore > 0.5f; break;
            }
        }
        return imp;
    }

    private static void ImpostaTexture(string percorso, TextureImporterType tipo, bool sRGB, bool alfaTrasparenza)
    {
        TextureImporter imp = AssetImporter.GetAtPath(percorso) as TextureImporter;
        if (imp == null) return;

        bool modificato = false;
        if (imp.textureType != tipo) { imp.textureType = tipo; modificato = true; }
        if (tipo != TextureImporterType.NormalMap && imp.sRGBTexture != sRGB) { imp.sRGBTexture = sRGB; modificato = true; }
        if (tipo == TextureImporterType.Default && imp.alphaIsTransparency != alfaTrasparenza) { imp.alphaIsTransparency = alfaTrasparenza; modificato = true; }
        if (imp.wrapMode != TextureWrapMode.Repeat) { imp.wrapMode = TextureWrapMode.Repeat; modificato = true; }
        if (imp.maxTextureSize != 2048) { imp.maxTextureSize = 2048; modificato = true; }

        if (modificato) imp.SaveAndReimport();
    }
}
#endif
