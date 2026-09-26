using UnityEngine;

/// <summary>
/// Accende una candela: crea sopra lo stoppino una fiammella animata (Particle System)
/// e una luce calda che tremola in modo naturale.
/// Mettilo su ogni candela (puoi selezionarle tutte insieme e aggiungere il componente in un colpo solo),
/// oppure direttamente sul prefab della candela.
/// </summary>
public class FiammaCandela : MonoBehaviour
{
    [Header("Posizione")]
    [Tooltip("Opzionale: punto esatto dello stoppino. Se vuoto, la fiamma viene messa in cima alla candela")]
    [SerializeField] private Transform puntoFiamma;
    [Tooltip("Spostamento verso l'alto rispetto alla cima della candela (metri)")]
    [SerializeField] private float altezzaExtra = 0.01f;

    [Header("Fiamma")]
    [Tooltip("Materiale delle particelle (MatFiamma)")]
    [SerializeField] private Material materialeFiamma;
    [Tooltip("Altezza della fiammella in metri")]
    [SerializeField] private float dimensioneFiamma = 0.05f;
    [SerializeField] private Color coloreInterno = new Color(1f, 0.95f, 0.7f, 1f);
    [SerializeField] private Color coloreEsterno = new Color(1f, 0.5f, 0.12f, 1f);

    [Header("Luce")]
    [SerializeField] private Color coloreLuce = new Color(1f, 0.62f, 0.3f);
    [SerializeField] private float intensita = 1.2f;
    [Tooltip("Raggio di illuminazione in metri")]
    [SerializeField] private float raggio = 3f;
    [Tooltip("Le ombre delle candele sono belle ma costose: attivale solo su poche candele importanti")]
    [SerializeField] private bool ombre = false;

    [Header("Tremolio")]
    [Tooltip("Quanto varia l'intensità (0 = luce fissa)")]
    [SerializeField, Range(0f, 1f)] private float variazione = 0.3f;
    [SerializeField] private float velocitaTremolio = 6f;
    [Tooltip("Piccolo movimento della luce, che fa oscillare le ombre")]
    [SerializeField] private float oscillazione = 0.005f;

    private Light luce;
    private Transform fiamma;
    private Vector3 posizioneBaseLuce;
    private float seme;

    private void Start()
    {
        seme = Random.Range(0f, 100f); // Ogni candela tremola in modo diverso

        fiamma = new GameObject("Fiamma").transform;
        fiamma.SetParent(transform, false);
        fiamma.position = CalcolaPosizioneFiamma();
        CompensaScala(fiamma);

        CreaParticelle();
        CreaLuce();
    }

    private void Update()
    {
        if (luce == null) return;

        float t = Time.time * velocitaTremolio;

        // Due rumori a velocità diverse: tremolio lento + sfarfallio rapido
        float lento = Mathf.PerlinNoise(seme, t * 0.3f);
        float rapido = Mathf.PerlinNoise(seme + 10f, t);
        float fattore = 1f - variazione + variazione * 2f * (lento * 0.6f + rapido * 0.4f);
        luce.intensity = intensita * fattore;

        // La luce si sposta di pochissimo: le ombre "danzano" come con una vera fiamma
        Vector3 offset = new Vector3(
            Mathf.PerlinNoise(seme + 20f, t) - 0.5f,
            Mathf.PerlinNoise(seme + 30f, t) - 0.5f,
            Mathf.PerlinNoise(seme + 40f, t) - 0.5f) * oscillazione * 2f;
        luce.transform.position = posizioneBaseLuce + offset;
    }

    // ---------- Creazione ----------

    private Vector3 CalcolaPosizioneFiamma()
    {
        if (puntoFiamma != null) return puntoFiamma.position;

        // Cima della candela, calcolata dalla forma del modello
        Renderer r = GetComponentInChildren<Renderer>();
        if (r == null) return transform.position + Vector3.up * altezzaExtra;

        Bounds b = r.bounds;
        return new Vector3(b.center.x, b.max.y + altezzaExtra, b.center.z);
    }

    private void CreaParticelle()
    {
        ParticleSystem ps = fiamma.gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        float d = dimensioneFiamma;

        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(d * 0.8f, d * 1.1f);
        main.startRotation = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = 40;
        main.playOnAwake = true;

        var emissione = ps.emission;
        emissione.rateOverTime = 30f;

        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = d * 0.05f;

        // Le particelle salgono leggermente, ondeggiando
        var velocita = ps.velocityOverLifetime;
        velocita.enabled = true;
        velocita.space = ParticleSystemSimulationSpace.World;
        velocita.x = new ParticleSystem.MinMaxCurve(-d * 0.3f, d * 0.3f);
        velocita.y = new ParticleSystem.MinMaxCurve(d * 0.6f, d * 1.2f);
        velocita.z = new ParticleSystem.MinMaxCurve(-d * 0.3f, d * 0.3f);

        // Si restringono mentre salgono
        var dimensione = ps.sizeOverLifetime;
        dimensione.enabled = true;
        dimensione.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.7f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.2f)));

        // Dal giallo chiaro all'arancione, poi svaniscono
        var colore = ps.colorOverLifetime;
        colore.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(coloreInterno, 0f), new GradientColorKey(coloreEsterno, 0.6f),
                    new GradientColorKey(new Color(0.8f, 0.2f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
        colore.color = g;

        var renderer = fiamma.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        if (materialeFiamma != null)
            renderer.sharedMaterial = materialeFiamma;
        else
            Debug.LogWarning("[FiammaCandela] Nessun materiale della fiamma assegnato: le particelle appariranno rosa.", this);

        ps.Play();
    }

    private void CreaLuce()
    {
        luce = new GameObject("LuceCandela").AddComponent<Light>();
        luce.transform.SetParent(fiamma, false);
        luce.transform.localPosition = Vector3.zero;

        luce.type = LightType.Point;
        luce.color = coloreLuce;
        luce.intensity = intensita;
        luce.range = raggio;
        luce.shadows = ombre ? LightShadows.Soft : LightShadows.None;
        luce.renderMode = LightRenderMode.ForcePixel; // Luce per pixel: tremolio più morbido sulle superfici

        posizioneBaseLuce = luce.transform.position;
    }

    /// <summary>Annulla la scala della candela, così la fiamma ha sempre le dimensioni indicate in metri.</summary>
    private static void CompensaScala(Transform t)
    {
        Vector3 s = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(
            s.x != 0f ? 1f / s.x : 1f,
            s.y != 0f ? 1f / s.y : 1f,
            s.z != 0f ? 1f / s.z : 1f);
    }

    private void OnDrawGizmosSelected()
    {
        // Nell'Editor mostra dove comparirà la fiamma
        Gizmos.color = new Color(1f, 0.6f, 0.2f);
        Vector3 p = Application.isPlaying && fiamma != null ? fiamma.position : CalcolaPosizioneFiamma();
        Gizmos.DrawWireSphere(p, dimensioneFiamma * 0.5f);
    }
}
