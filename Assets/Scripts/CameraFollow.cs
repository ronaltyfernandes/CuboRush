using UnityEngine;

/// <summary>
/// Câmera atrás e acima do cubo, acompanhando o avanço dele.
/// Abre o campo de visão conforme a velocidade aumenta e treme nas batidas.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform alvo;

    [Tooltip("Posição da câmera em relação ao cubo.")]
    public Vector3 deslocamento = new Vector3(0f, 4.5f, -8f);

    [Tooltip("Inclinação da câmera para baixo, em graus.")]
    public float inclinacao = 10f;

    [Tooltip("0 = câmera fixa no meio da pista; 1 = acompanha o cubo totalmente de lado.")]
    [Range(0f, 1f)]
    public float acompanhamentoLateral = 0.35f;

    [Header("Distância de visão")]
    [Tooltip("Até onde a câmera enxerga. Além disso, tudo some no escuro.")]
    public float distanciaVisao = 70f;
    [Tooltip("Em que fração dessa distância as coisas começam a escurecer.")]
    [Range(0f, 1f)]
    public float inicioDaNevoa = 0.55f;

    [Header("Campo de visão")]
    public float fovNormal = 60f;
    public float fovRapido = 74f;

    static CameraFollow instancia;
    Camera cam;
    float tremorAte, tremorForca;

    void Awake()
    {
        instancia = this;
        cam = GetComponent<Camera>();
    }

    void Start()
    {
        // Alcance de visão curto sem mexer no ângulo: uma névoa preta esconde
        // o que está longe, e os objetos surgem aos poucos em vez de aparecerem de repente.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Color.black;
        RenderSettings.fogStartDistance = distanciaVisao * inicioDaNevoa;
        RenderSettings.fogEndDistance = distanciaVisao;
        if (cam != null) cam.farClipPlane = distanciaVisao + 5f;
    }

    /// <summary>Faz a câmera tremer por alguns instantes.</summary>
    public static void Tremer(float duracao, float forca)
    {
        if (instancia == null) return;
        instancia.tremorAte = Time.unscaledTime + duracao;
        instancia.tremorForca = forca;
    }

    void LateUpdate()
    {
        if (alvo == null) return;

        Vector3 p = new Vector3(
            alvo.position.x * acompanhamentoLateral + deslocamento.x,
            deslocamento.y,
            alvo.position.z + deslocamento.z);

        if (Time.unscaledTime < tremorAte && Time.timeScale > 0f)
            p += (Vector3)Random.insideUnitCircle * tremorForca;

        transform.position = p;
        transform.rotation = Quaternion.Euler(inclinacao, 0f, 0f);

        GameManager jogo = GameManager.Instancia;
        if (cam != null && jogo != null)
        {
            float alvoFov = Mathf.Lerp(fovNormal, fovRapido, jogo.Dificuldade);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, alvoFov, 2f * Time.deltaTime);
        }
    }
}
