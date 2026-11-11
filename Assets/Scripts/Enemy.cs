using UnityEngine;

/// <summary>
/// Comportamentos das naves inimigas:
///  - Atirador (vermelho): se alinha com uma faixa, avisa e dá um tiro por vez,
///                         variando o tipo de projétil.
///  - Rajada (laranja):    avisa e dispara em três faixas vizinhas ao mesmo tempo.
///  - Varredor (roxo):     não mira; varre a pista de um lado ao outro soltando
///                         tiros rápidos.
///  - Kamikaze (amarelo):  persegue a faixa do jogador, trava a mira e se joga
///                         contra ele. Pule por cima ou saia da faixa.
/// </summary>
public enum TipoInimigo { Atirador, Rajada, Varredor, Kamikaze }

/// <summary>
/// Nave inimiga. Voa à frente do jogador, acima dos obstáculos, age conforme
/// o seu tipo e depois vai embora. A faixa marcada no chão avisa onde vem o
/// ataque: vermelha = pule ou desvie; roxa = deslize ou desvie.
/// </summary>
public class Enemy : MonoBehaviour
{
    [Tooltip("Distância à frente do jogador em que a nave fica.")]
    public float distancia = 30f;
    [Tooltip("Altura de voo (acima dos obstáculos).")]
    public float alturaVoo = 4.2f;
    public float velocidadeLateral = 9f;
    [Tooltip("Tempo de aviso antes de cada ataque.")]
    public float tempoMira = 0.55f;

    enum Fase { Entrando, Movendo, Mirando, Esperando, Saindo }

    TipoInimigo tipo;
    PlayerController jogador;
    ObstacleSpawner gerador;
    Fase fase = Fase.Entrando;
    float relogio;
    float afastamento = 75f;
    float alturaAtual;
    float x;
    int faixaAlvo;
    int ataquesRestantes;
    float direcaoVarredura = 1f;
    TipoProjetil proximoProjetil;

    Transform corpo;
    Vector3 tamanhoCorpo;
    // Marcas no chão: [0] na faixa da nave, [1] à esquerda, [2] à direita.
    readonly GameObject[] miras = new GameObject[3];

    static readonly Color corAvisoBaixo = new Color(1f, 0.1f, 0.1f);
    static readonly Color corAvisoAlto = new Color(0.65f, 0.3f, 1f);

    public void Configurar(PlayerController jogador, ObstacleSpawner gerador, TipoInimigo tipo)
    {
        this.jogador = jogador;
        this.gerador = gerador;
        this.tipo = tipo;
        alturaAtual = alturaVoo;

        switch (tipo)
        {
            case TipoInimigo.Rajada: ataquesRestantes = 3; break;
            case TipoInimigo.Varredor: ataquesRestantes = 7; break;
            case TipoInimigo.Kamikaze: ataquesRestantes = 1; break;
            default: ataquesRestantes = 4; break;
        }

        Construir();
        Posicionar();
    }

    // ---------- Aparência ----------

    void Construir()
    {
        Color cor;
        switch (tipo)
        {
            case TipoInimigo.Rajada:
                cor = new Color(1f, 0.45f, 0.1f);
                tamanhoCorpo = new Vector3(2.8f, 0.5f, 1.2f);
                break;
            case TipoInimigo.Varredor:
                cor = new Color(0.7f, 0.3f, 1f);
                tamanhoCorpo = new Vector3(1.1f, 0.45f, 1.9f);
                break;
            case TipoInimigo.Kamikaze:
                cor = new Color(1f, 0.9f, 0.2f);
                tamanhoCorpo = new Vector3(1.5f, 0.9f, 1.3f);
                break;
            default:
                cor = new Color(1f, 0.15f, 0.3f);
                tamanhoCorpo = new Vector3(1.7f, 0.5f, 1.3f);
                break;
        }

        corpo = Visual.Forma(PrimitiveType.Cube, "Corpo", transform, Vector3.zero, tamanhoCorpo, cor).transform;

        float meiaLargura = tamanhoCorpo.x / 2f;
        Visual.Forma(PrimitiveType.Cube, "AsaEsquerda", corpo,
            new Vector3(-meiaLargura - 0.25f, 0.15f, 0.2f), new Vector3(0.5f, 0.2f, 0.8f), cor);
        Visual.Forma(PrimitiveType.Cube, "AsaDireita", corpo,
            new Vector3(meiaLargura + 0.25f, 0.15f, 0.2f), new Vector3(0.5f, 0.2f, 0.8f), cor);

        if (tipo == TipoInimigo.Kamikaze)
        {
            // Sem canhão: a frente é toda de espinhos.
            Visual.Espinhos(corpo, new Vector3(0f, 0f, -tamanhoCorpo.z / 2f), Quaternion.identity,
                tamanhoCorpo.x - 0.2f, tamanhoCorpo.y - 0.1f, cor);
        }
        else
        {
            int canhoes = tipo == TipoInimigo.Rajada ? 3 : 1;
            for (int i = 0; i < canhoes; i++)
            {
                float cx = (i - (canhoes - 1) / 2f) * 0.9f;
                Visual.Forma(PrimitiveType.Sphere, "Canhao", corpo,
                    new Vector3(cx, -0.3f, -0.3f), Vector3.one * 0.45f, Color.white, true);
            }
        }

        // Faixas de aviso no chão.
        float largura = jogador.larguraFaixa;
        for (int i = 0; i < miras.Length; i++)
        {
            float deslocX = i == 0 ? 0f : (i == 1 ? -largura : largura);
            miras[i] = Visual.Forma(PrimitiveType.Cube, "Mira", transform,
                new Vector3(deslocX, -alturaVoo + 0.03f, -distancia / 2f),
                new Vector3(0.6f, 0.02f, distancia), corAvisoBaixo, true, raio: 0f);
            miras[i].SetActive(false);
        }
    }

    void MostrarMiras(bool laterais, Color cor)
    {
        Material material = Visual.Mat(cor, true);
        for (int i = 0; i < miras.Length; i++)
        {
            bool ativa = i == 0 || (laterais && FaixaValida(faixaAlvo + (i == 1 ? -1 : 1)));
            miras[i].SetActive(ativa);
            if (ativa) miras[i].GetComponent<Renderer>().sharedMaterial = material;
        }
    }

    void EsconderMiras()
    {
        for (int i = 0; i < miras.Length; i++) miras[i].SetActive(false);
    }

    bool FaixaValida(int faixa) => faixa >= 0 && faixa < jogador.quantidadeFaixas;

    // ---------- Comportamento ----------

    // LateUpdate: roda depois do jogador se mover, para acompanhar sem atraso.
    void LateUpdate()
    {
        GameManager jogo = GameManager.Instancia;
        if (jogo == null || jogador == null) return;

        if (jogo.Estado == EstadoJogo.Jogando) Agir(jogo);
        Posicionar();
    }

    void Posicionar()
    {
        transform.position = new Vector3(x, alturaAtual, jogador.transform.position.z + afastamento);
    }

    void Agir(GameManager jogo)
    {
        float dt = Time.deltaTime;

        switch (fase)
        {
            case Fase.Entrando:
                afastamento = Mathf.MoveTowards(afastamento, distancia, 45f * dt);
                if (Mathf.Approximately(afastamento, distancia)) ComecarAtaque(jogo);
                break;

            case Fase.Movendo:
                if (tipo == TipoInimigo.Varredor) Varrer(jogo, dt);
                else if (tipo == TipoInimigo.Kamikaze) Perseguir(dt);
                else Alinhar(dt);
                break;

            case Fase.Mirando:
                relogio -= dt;
                // Pulsa enquanto carrega o ataque.
                corpo.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(Time.time * 40f));
                if (relogio <= 0f)
                {
                    corpo.localScale = Vector3.one;
                    EsconderMiras();

                    if (tipo == TipoInimigo.Kamikaze)
                    {
                        Mergulhar(jogo);
                        return;
                    }

                    Disparar(jogo);
                    ataquesRestantes--;
                    fase = ataquesRestantes > 0 ? Fase.Esperando : Fase.Saindo;
                    relogio = Mathf.Lerp(0.9f, 0.45f, jogo.Dificuldade);
                }
                break;

            case Fase.Esperando:
                relogio -= dt;
                if (relogio <= 0f) ComecarAtaque(jogo);
                break;

            case Fase.Saindo:
                alturaAtual += 10f * dt;
                afastamento += 25f * dt;
                if (alturaAtual > alturaVoo + 18f) Destroy(gameObject);
                break;
        }
    }

    void ComecarAtaque(GameManager jogo)
    {
        fase = Fase.Movendo;

        switch (tipo)
        {
            case TipoInimigo.Varredor:
                relogio = 0.5f;
                break;

            case TipoInimigo.Kamikaze:
                relogio = 1.6f; // tempo perseguindo antes de travar a mira
                faixaAlvo = jogador.FaixaAtual;
                MostrarMiras(false, corAvisoBaixo);
                break;

            default:
                // Quase sempre mira na faixa em que o jogador está.
                faixaAlvo = Random.value < 0.7f
                    ? jogador.FaixaAtual
                    : Random.Range(0, jogador.quantidadeFaixas);
                proximoProjetil = SortearProjetil(jogo);
                break;
        }
    }

    TipoProjetil SortearProjetil(GameManager jogo)
    {
        float r = Random.value;

        if (tipo == TipoInimigo.Rajada)
            return r < 0.65f ? TipoProjetil.Normal : TipoProjetil.Alto;

        // Atirador: os tipos mais difíceis só entram depois de um tempo de jogo.
        if (jogo.TempoDeJogo < 30f)
            return r < 0.65f ? TipoProjetil.Normal : TipoProjetil.Alto;

        if (r < 0.35f) return TipoProjetil.Normal;
        if (r < 0.60f) return TipoProjetil.Alto;
        if (r < 0.80f) return TipoProjetil.Onda;
        return TipoProjetil.Teleguiado;
    }

    /// <summary>Atirador e Rajada: vai até a faixa escolhida e começa a mirar.</summary>
    void Alinhar(float dt)
    {
        float alvoX = jogador.PosicaoXDaFaixa(faixaAlvo);
        x = Mathf.MoveTowards(x, alvoX, velocidadeLateral * dt);
        if (!Mathf.Approximately(x, alvoX)) return;

        fase = Fase.Mirando;
        relogio = tempoMira;
        MostrarMiras(tipo == TipoInimigo.Rajada,
            proximoProjetil == TipoProjetil.Alto ? corAvisoAlto : corAvisoBaixo);
    }

    void Disparar(GameManager jogo)
    {
        Vector3 origem = transform.position + Vector3.down * 0.4f;
        gerador.CriarProjetil(origem, proximoProjetil);

        if (tipo == TipoInimigo.Rajada)
        {
            for (int lado = -1; lado <= 1; lado += 2)
            {
                if (!FaixaValida(faixaAlvo + lado)) continue;
                gerador.CriarProjetil(origem + Vector3.right * (lado * jogador.larguraFaixa), proximoProjetil);
            }
        }

        jogo.SomDeTiro();
    }

    /// <summary>Varredor: vai e volta pela pista soltando tiros rápidos.</summary>
    void Varrer(GameManager jogo, float dt)
    {
        float limite = jogador.PosicaoXDaFaixa(jogador.quantidadeFaixas - 1);
        x += direcaoVarredura * 5f * dt;
        if (Mathf.Abs(x) >= limite)
        {
            x = Mathf.Sign(x) * limite;
            direcaoVarredura = -direcaoVarredura;
        }

        relogio -= dt;
        if (relogio > 0f) return;

        // Solta o tiro na faixa mais próxima de onde está passando.
        int faixa = Mathf.Clamp(
            Mathf.RoundToInt(x / jogador.larguraFaixa + (jogador.quantidadeFaixas - 1) / 2f),
            0, jogador.quantidadeFaixas - 1);

        gerador.CriarProjetil(
            new Vector3(jogador.PosicaoXDaFaixa(faixa), alturaAtual - 0.4f, transform.position.z),
            TipoProjetil.Rapido);
        jogo.SomDeTiro();

        relogio = Mathf.Lerp(0.85f, 0.5f, jogo.Dificuldade);
        ataquesRestantes--;
        if (ataquesRestantes <= 0) fase = Fase.Saindo;
    }

    /// <summary>Kamikaze: acompanha o jogador de lado e depois trava a mira.</summary>
    void Perseguir(float dt)
    {
        x = Mathf.MoveTowards(x, jogador.transform.position.x, 7f * dt);
        relogio -= dt;
        if (relogio > 0f) return;

        fase = Fase.Mirando;
        relogio = 0.45f;
    }

    /// <summary>Kamikaze: vira um "projétil" e passa a ser controlado pelo gerador da pista.</summary>
    void Mergulhar(GameManager jogo)
    {
        for (int i = 0; i < miras.Length; i++) Destroy(miras[i]);
        jogo.SomDeTiro();
        gerador.AdotarKamikaze(gameObject, tamanhoCorpo);
        Destroy(this);
    }
}
