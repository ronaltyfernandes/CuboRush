using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controla o cubo: avança sempre para frente (na velocidade do GameManager),
/// rola de lado para trocar entre as 5 faixas, pula, desliza e troca de cor.
///
/// Teclado: A/D ou setas (faixa), W/seta cima/Espaço (pulo), S/seta baixo (deslize),
///          Q/E, 1-2-3 ou clique do mouse (cor).
/// Celular: deslizar o dedo (faixa, pulo, deslize) e toque rápido (cor).
/// </summary>
public class PlayerController : MonoBehaviour
{
    /// <summary>Cores que o cubo pode assumir. Paredes de cor usam as mesmas.</summary>
    public static readonly Color[] Cores =
    {
        new Color(1f, 0.55f, 0.1f),   // 0 - laranja
        new Color(0.2f, 1f, 0.45f),   // 1 - verde
        new Color(0.2f, 0.55f, 1f)    // 2 - azul
    };

    [Header("Faixas")]
    public int quantidadeFaixas = 5;
    public float larguraFaixa = 2f;
    [Tooltip("Velocidade lateral ao trocar de faixa (o cubo rola nesse ritmo).")]
    public float velocidadeTroca = 12f;

    [Header("Pulo")]
    public float forcaPulo = 10f;
    public float gravidade = 28f;
    [Tooltip("Velocidade de descida ao apertar para baixo no ar.")]
    public float quedaRapida = 22f;

    [Header("Deslize")]
    public float duracaoDeslize = 0.7f;

    [Header("Toque")]
    [Tooltip("Distância mínima (em pixels) para contar como deslize do dedo.")]
    public float deslizeMinimo = 50f;

    /// <summary>Faixa atual, de 0 (esquerda) até quantidadeFaixas - 1 (direita).</summary>
    public int FaixaAtual { get; private set; }
    /// <summary>Índice da cor atual dentro de "Cores".</summary>
    public int CorId { get; private set; }
    public Color CorAtual => Cores[CorId];
    public bool Deslizando { get; private set; }
    public bool NoChao => altura <= 0f && velocidadeY <= 0f;
    public bool Invencivel => Time.time < invencivelAte;

    /// <summary>Altura máxima do pulo (usada para posicionar moedas em arco).</summary>
    public float AlturaMaximaPulo => forcaPulo * forcaPulo / (2f * gravidade);
    /// <summary>Tempo total no ar em um pulo.</summary>
    public float TempoNoAr => 2f * forcaPulo / gravidade;

    float AlturaCorpo => Deslizando ? 0.5f : 1f;

    /// <summary>Caixa de colisão do cubo (um pouco menor que ele, para ser justo).</summary>
    public Bounds Limites => new Bounds(
        new Vector3(transform.position.x, altura + AlturaCorpo / 2f, transform.position.z),
        new Vector3(0.8f, AlturaCorpo * 0.9f, 0.8f));

    float altura;          // distância da base do cubo até o chão
    float velocidadeY;
    float tempoDeslize;
    float esperaPulo;      // guarda o comando de pulo por um instante
    bool deslizarAoPousar;
    float giro;
    float achatamento;     // 0 = cubo normal, 1 = totalmente achatado no deslize
    float giroDeslize;
    float invencivelAte;

    Renderer visual;
    Transform orbeEscudo;
    Vector2 inicioToque;
    bool toqueValido, toqueUsado;

    void Start()
    {
        visual = GetComponent<Renderer>();
        visual.sharedMaterial = Visual.Mat(CorAtual);

        FaixaAtual = quantidadeFaixas / 2;
        Vector3 p = transform.position;
        p.x = PosicaoXDaFaixa(FaixaAtual);
        p.y = 0.5f;
        transform.position = p;

        // Bolinha que gira em volta do cubo enquanto houver escudo.
        orbeEscudo = Visual.Forma(PrimitiveType.Sphere, "OrbeEscudo", null,
            Vector3.zero, Vector3.one * 0.3f, new Color(0.75f, 0.95f, 1f), true).transform;
        orbeEscudo.gameObject.SetActive(false);
    }

    void Update()
    {
        GameManager jogo = GameManager.Instancia;
        if (jogo == null || jogo.Estado != EstadoJogo.Jogando) return;

        // Ignora o primeiro instante para o comando que inicia o jogo não virar ação.
        if (jogo.TempoDeJogo > 0.1f) LerEntrada(jogo);

        Vector3 p = transform.position;

        // Sempre para frente.
        p.z += jogo.Velocidade * Time.deltaTime;

        // Troca de faixa.
        float alvoX = PosicaoXDaFaixa(FaixaAtual);
        p.x = Mathf.MoveTowards(p.x, alvoX, velocidadeTroca * Time.deltaTime);

        // Pulo.
        if (esperaPulo > 0f) esperaPulo -= Time.deltaTime;
        if (NoChao && esperaPulo > 0f)
        {
            esperaPulo = 0f;
            velocidadeY = forcaPulo;
            Deslizando = false;
            deslizarAoPousar = false;
            jogo.SomDePulo();
        }

        if (!NoChao)
        {
            velocidadeY -= gravidade * Time.deltaTime;
            altura += velocidadeY * Time.deltaTime;
            giro += 360f / TempoNoAr * Time.deltaTime;

            if (altura <= 0f)
            {
                altura = 0f;
                velocidadeY = 0f;
                giro = 0f;
                if (deslizarAoPousar) IniciarDeslize();
            }
        }

        // Deslize.
        if (Deslizando)
        {
            tempoDeslize -= Time.deltaTime;
            if (tempoDeslize <= 0f) Deslizando = false;
        }

        // Rolagem lateral: o cubo dá meia-volta a cada faixa, como se rolasse
        // sobre as arestas, e sobe um pouco quando está "de quina".
        //
        // Esquiva para baixo: o cubo se achata e alarga como um disco e gira
        // feito pião rente ao chão; ao terminar, estica um pouco ao voltar.
        achatamento = Mathf.MoveTowards(achatamento, Deslizando ? 1f : 0f, 9f * Time.deltaTime);

        if (Deslizando)
        {
            giroDeslize += 900f * Time.deltaTime;
        }
        else if (giroDeslize != 0f)
        {
            // Termina a volta no próximo "encaixe" de 90° para não dar tranco.
            float encaixe = Mathf.Ceil(giroDeslize / 90f) * 90f;
            giroDeslize = Mathf.MoveTowards(giroDeslize, encaixe, 900f * Time.deltaTime);
            if (Mathf.Approximately(giroDeslize, encaixe) && achatamento <= 0f) giroDeslize = 0f;
        }

        float alturaVisual = Mathf.Lerp(1f, 0.45f, achatamento);
        float larguraVisual = Mathf.Lerp(1f, 1.3f, achatamento);
        if (!Deslizando && achatamento > 0f)
            alturaVisual *= 1f + 0.25f * Mathf.Sin(Mathf.PI * (1f - achatamento));

        float elevacao = 0f;
        Quaternion rotacao;

        if (achatamento > 0f || giroDeslize != 0f)
        {
            rotacao = Quaternion.Euler(0f, giroDeslize, 0f);
        }
        else
        {
            float graus = -p.x / larguraFaixa * 180f;
            float fase = Mathf.Repeat(Mathf.Abs(graus), 90f);
            elevacao = Mathf.Cos((fase - 45f) * Mathf.Deg2Rad) * 0.70711f - 0.5f;
            rotacao = Quaternion.Euler(giro, 0f, graus);
        }

        p.y = altura + alturaVisual / 2f + elevacao;
        transform.position = p;
        transform.localScale = new Vector3(larguraVisual, alturaVisual, larguraVisual);
        transform.rotation = rotacao;

        // Pisca-pisca depois de perder o escudo.
        visual.enabled = !Invencivel || Mathf.FloorToInt(Time.time * 14f) % 2 == 0;

        // Orbe do escudo.
        if (orbeEscudo.gameObject.activeSelf != jogo.TemEscudo)
            orbeEscudo.gameObject.SetActive(jogo.TemEscudo);
        if (jogo.TemEscudo)
        {
            float a = Time.time * 6f;
            orbeEscudo.position = Limites.center + new Vector3(Mathf.Cos(a), 0.15f, Mathf.Sin(a)) * 0.95f;
        }
    }

    void LerEntrada(GameManager jogo)
    {
        Keyboard t = Keyboard.current;
        if (t != null)
        {
            if (t.aKey.wasPressedThisFrame || t.leftArrowKey.wasPressedThisFrame) MudarFaixa(-1);
            if (t.dKey.wasPressedThisFrame || t.rightArrowKey.wasPressedThisFrame) MudarFaixa(+1);
            if (t.wKey.wasPressedThisFrame || t.upArrowKey.wasPressedThisFrame || t.spaceKey.wasPressedThisFrame) Pular();
            if (t.sKey.wasPressedThisFrame || t.downArrowKey.wasPressedThisFrame) Deslizar();

            if (t.eKey.wasPressedThisFrame) TrocarCor(+1);
            if (t.qKey.wasPressedThisFrame) TrocarCor(-1);
            if (t.digit1Key.wasPressedThisFrame) DefinirCor(0);
            if (t.digit2Key.wasPressedThisFrame) DefinirCor(1);
            if (t.digit3Key.wasPressedThisFrame) DefinirCor(2);
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) TrocarCor(+1);

        Touchscreen tela = Touchscreen.current;
        if (tela != null)
        {
            var toque = tela.primaryTouch;
            if (toque.press.wasPressedThisFrame)
            {
                inicioToque = toque.position.ReadValue();
                toqueValido = true;
                toqueUsado = false;
            }
            else if (toque.press.isPressed && toqueValido && !toqueUsado)
            {
                // Reage assim que o dedo andou o suficiente, sem esperar soltar.
                Vector2 d = toque.position.ReadValue() - inicioToque;
                if (d.magnitude >= deslizeMinimo)
                {
                    toqueUsado = true;
                    if (Mathf.Abs(d.x) > Mathf.Abs(d.y)) MudarFaixa(d.x > 0f ? +1 : -1);
                    else if (d.y > 0f) Pular();
                    else Deslizar();
                }
            }
            else if (toque.press.wasReleasedThisFrame && toqueValido)
            {
                // Soltou sem deslizar: toque rápido troca a cor.
                if (!toqueUsado) TrocarCor(+1);
                toqueValido = false;
            }
        }
    }

    /// <summary>Move uma faixa para a esquerda (-1) ou direita (+1), sem sair da pista.</summary>
    public void MudarFaixa(int direcao)
    {
        FaixaAtual = Mathf.Clamp(FaixaAtual + direcao, 0, quantidadeFaixas - 1);
    }

    /// <summary>Passa para a próxima cor (+1) ou a anterior (-1).</summary>
    public void TrocarCor(int direcao)
    {
        DefinirCor((CorId + direcao + Cores.Length) % Cores.Length);
    }

    public void DefinirCor(int indice)
    {
        if (indice == CorId) return;
        CorId = indice;
        visual.sharedMaterial = Visual.Mat(CorAtual);
        if (GameManager.Instancia != null) GameManager.Instancia.SomDeCor();
    }

    public void Pular()
    {
        // Se apertar um pouco antes de pousar, o pulo sai assim que tocar o chão.
        esperaPulo = 0.15f;
    }

    public void Deslizar()
    {
        if (NoChao) IniciarDeslize();
        else
        {
            // No ar: desce rápido e já pousa deslizando.
            velocidadeY = -quedaRapida;
            deslizarAoPousar = true;
        }
    }

    void IniciarDeslize()
    {
        Deslizando = true;
        deslizarAoPousar = false;
        tempoDeslize = duracaoDeslize;
    }

    public void FicarInvencivel(float segundos)
    {
        invencivelAte = Time.time + segundos;
    }

    /// <summary>Esconde o cubo e espalha pedaços dele.</summary>
    public void Morrer()
    {
        visual.enabled = false;
        orbeEscudo.gameObject.SetActive(false);
        Vector3 centro = Limites.center;

        for (int i = 0; i < 14; i++)
        {
            GameObject pedaco = Visual.Forma(PrimitiveType.Cube, "Pedaco", null,
                centro + Random.insideUnitSphere * 0.4f, Vector3.one * Random.Range(0.18f, 0.35f),
                CorAtual, false, true);
            Rigidbody corpo = pedaco.AddComponent<Rigidbody>();
            Vector3 impulso = new Vector3(Random.Range(-4f, 4f), Random.Range(3f, 8f), Random.Range(2f, 8f));
            corpo.AddForce(impulso, ForceMode.VelocityChange);
            corpo.AddTorque(Random.insideUnitSphere * 10f, ForceMode.VelocityChange);
        }
    }

    /// <summary>Posição X do centro de uma faixa (a pista é centrada em X = 0).</summary>
    public float PosicaoXDaFaixa(int faixa)
    {
        float meio = (quantidadeFaixas - 1) / 2f;
        return (faixa - meio) * larguraFaixa;
    }
}
