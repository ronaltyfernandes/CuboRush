using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cria obstáculos, moedas e power-ups na frente do jogador, apaga o que ficou
/// para trás e verifica as colisões com o cubo.
///
/// Tipos de obstáculo:
///  - Baixo  (rosa):    pule por cima ou desvie.
///  - Alto   (vermelho): só desviando.
///  - Barra  (roxo):    deslize por baixo ou desvie.
///  - Móvel  (branco):  anda de um lado para o outro da pista.
///  - Parede de cor:    barreira luminosa; só atravessa com o cubo da mesma cor.
///  - Projétil:         disparado pelas naves inimigas (ver Enemy).
/// Sempre existe um caminho possível em cada fileira.
/// </summary>
public class ObstacleSpawner : MonoBehaviour
{
    public PlayerController jogador;

    [Header("Geração")]
    [Tooltip("Até que distância à frente os objetos são criados.")]
    public float distanciaVisao = 130f;
    [Tooltip("Trecho livre no começo da partida.")]
    public float inicioLivre = 45f;
    [Tooltip("Tempo (s) entre fileiras no começo e na dificuldade máxima.")]
    public float tempoReacaoInicial = 1.5f;
    public float tempoReacaoFinal = 0.95f;

    [Header("Moedas e power-ups")]
    [Range(0f, 1f)] public float chanceMoedas = 0.65f;
    public float distanciaMinPowerUp = 180f;
    public float distanciaMaxPowerUp = 320f;
    public float raioIma = 8f;

    [Header("Paredes de cor")]
    [Tooltip("Chance de uma fileira ser uma parede de cor.")]
    [Range(0f, 1f)] public float chanceParedeDeCor = 0.14f;

    [Header("Inimigos")]
    [Tooltip("Segundos de jogo até o primeiro inimigo aparecer.")]
    public float primeiroInimigo = 15f;
    [Tooltip("Intervalo (s) entre o surgimento de um inimigo e o do próximo.")]
    public float intervaloInimigoMin = 16f;
    public float intervaloInimigoMax = 26f;
    [Tooltip("Velocidade do projétil vindo contra o jogador.")]
    public float velocidadeProjetil = 8f;

    Enemy inimigoAtual;
    int inimigosCriados;
    float proximoInimigo;
    float zInicial;

    enum Obst { Nenhum, Baixo, Alto, Barra }

    static readonly Color corBaixo = new Color(1f, 0.2f, 0.7f);
    static readonly Color corAlto = new Color(0.9f, 0.15f, 0.2f);
    static readonly Color corBarra = new Color(0.55f, 0.3f, 1f);
    static readonly Color corMovel = new Color(0.9f, 0.9f, 0.95f);
    static readonly Color corMoeda = new Color(1f, 0.85f, 0.15f);
    static readonly Color corEscudo = new Color(0.3f, 0.8f, 1f);
    static readonly Color corIma = new Color(0.3f, 1f, 0.45f);
    static readonly Color corDobro = new Color(1f, 0.5f, 0.9f);

    readonly List<TrackItem> itens = new List<TrackItem>();
    float proximoZ;
    float proximoPowerUpZ;

    int Faixas => jogador.quantidadeFaixas;
    float MeiaPista => Faixas * jogador.larguraFaixa / 2f;

    void Start()
    {
        float z = jogador.transform.position.z;
        zInicial = z;
        proximoInimigo = primeiroInimigo;
        proximoZ = z + inicioLivre;
        proximoPowerUpZ = z + Random.Range(120f, 200f);
    }

    void Update()
    {
        GameManager jogo = GameManager.Instancia;
        if (jogo == null || jogador == null) return;

        float zJogador = jogador.transform.position.z;

        while (proximoZ < zJogador + distanciaVisao)
            proximoZ += CriarFileira(proximoZ, jogo);

        // De tempos em tempos chega uma nave inimiga (uma de cada vez).
        if (jogo.Estado == EstadoJogo.Jogando && inimigoAtual == null &&
            jogo.TempoDeJogo >= proximoInimigo)
        {
            // O primeiro é sempre o Atirador (mais simples); depois, qualquer tipo.
            TipoInimigo tipo = inimigosCriados == 0
                ? TipoInimigo.Atirador
                : (TipoInimigo)Random.Range(0, 4);
            inimigosCriados++;

            inimigoAtual = new GameObject("Inimigo" + tipo).AddComponent<Enemy>();
            inimigoAtual.Configurar(jogador, this, tipo);
            proximoInimigo = jogo.TempoDeJogo + Random.Range(intervaloInimigoMin, intervaloInimigoMax);
        }

        AtualizarItens(jogo, zJogador);
    }

    // ---------- Atualização e colisão ----------

    void AtualizarItens(GameManager jogo, float zJogador)
    {
        bool jogando = jogo.Estado == EstadoJogo.Jogando;
        Bounds caixaJogador = jogador.Limites;

        for (int i = itens.Count - 1; i >= 0; i--)
        {
            TrackItem item = itens[i];
            if (item == null) { itens.RemoveAt(i); continue; }

            Transform t = item.transform;

            // Ficou para trás: apaga.
            if (t.position.z < zJogador - 15f)
            {
                Remover(i);
                continue;
            }

            if (item.girar) t.Rotate(0f, 180f * Time.deltaTime, 0f, Space.World);

            if (!jogando) continue;

            // Obstáculo móvel: vai e volta entre as bordas da pista.
            if (item.velocidadeX != 0f)
            {
                Vector3 p = t.position;
                p.x += item.velocidadeX * Time.deltaTime;
                float limite = MeiaPista - item.tamanho.x / 2f;
                if (Mathf.Abs(p.x) > limite)
                {
                    p.x = Mathf.Sign(p.x) * limite;
                    item.velocidadeX = -item.velocidadeX;
                }
                t.position = p;
            }

            // Projétil: desce até a altura do cubo e voa contra o jogador.
            // Se bater em um obstáculo no caminho, some (dá para se proteger atrás deles).
            if (item.projetil)
            {
                Vector3 p = t.position;
                p.z += item.velocidadeZ * Time.deltaTime;
                p.y = Mathf.MoveTowards(p.y, item.alturaVoo, 10f * Time.deltaTime);

                float limiteX = MeiaPista - 0.5f;

                // Onda: balança em torno da faixa em que foi lançado.
                if (item.ondaAmplitude > 0f)
                {
                    item.fase += item.ondaFrequencia * Time.deltaTime;
                    p.x = Mathf.Clamp(item.xBase + Mathf.Sin(item.fase) * item.ondaAmplitude, -limiteX, limiteX);
                }

                // Teleguiado: persegue o cubo de lado, mas para de corrigir
                // quando chega perto, para dar chance de desviar.
                if (item.perseguicao > 0f && p.z - zJogador > 6f)
                    p.x = Mathf.MoveTowards(p.x, caixaJogador.center.x, item.perseguicao * Time.deltaTime);

                t.position = p;

                if (BateuEmObstaculo(item))
                {
                    Remover(i);
                    continue;
                }
            }

            // Ímã: puxa as moedas próximas.
            if (item.tipo == TipoItem.Moeda && jogo.TempoIma > 0f &&
                Vector3.Distance(t.position, caixaJogador.center) < raioIma)
            {
                t.position = Vector3.MoveTowards(t.position, caixaJogador.center,
                    (jogo.Velocidade + 18f) * Time.deltaTime);
            }

            if (!caixaJogador.Intersects(item.Limites)) continue;

            switch (item.tipo)
            {
                case TipoItem.Moeda: jogo.ColetarMoeda(); Remover(i); break;
                case TipoItem.Escudo: jogo.AtivarEscudo(); Remover(i); break;
                case TipoItem.Ima: jogo.AtivarIma(); Remover(i); break;
                case TipoItem.Dobro: jogo.AtivarDobro(); Remover(i); break;

                case TipoItem.Obstaculo:
                    // Parede da mesma cor do cubo: atravessa sem dano e ganha bônus.
                    if (item.corId >= 0 && item.corId == jogador.CorId)
                    {
                        if (!item.atravessado)
                        {
                            item.atravessado = true;
                            jogo.BonusDeCor();
                        }
                        break;
                    }

                    if (jogador.Invencivel) break;
                    if (jogo.ConsumirEscudo())
                    {
                        // O escudo quebra o obstáculo e dá um instante de proteção.
                        Remover(i);
                        jogador.FicarInvencivel(1.2f);
                        CameraFollow.Tremer(0.2f, 0.25f);
                    }
                    else
                    {
                        jogo.FimDeJogo();
                        jogador.Morrer();
                        CameraFollow.Tremer(0.45f, 0.5f);
                        return;
                    }
                    break;
            }
        }
    }

    bool BateuEmObstaculo(TrackItem projetil)
    {
        Bounds caixa = projetil.Limites;
        for (int i = 0; i < itens.Count; i++)
        {
            TrackItem outro = itens[i];
            if (outro == null || outro == projetil || outro.projetil) continue;
            if (outro.tipo == TipoItem.Obstaculo && caixa.Intersects(outro.Limites)) return true;
        }
        return false;
    }

    void Remover(int indice)
    {
        if (itens[indice] != null) Destroy(itens[indice].gameObject);
        itens.RemoveAt(indice);
    }

    // ---------- Geração ----------

    /// <summary>Cria uma fileira em Z e devolve a distância até a próxima.</summary>
    float CriarFileira(float z, GameManager jogo)
    {
        float d = jogo.Dificuldade;
        float vel = jogo.Velocidade;
        float espaco = vel * Mathf.Lerp(tempoReacaoInicial, tempoReacaoFinal, d) * Random.Range(1f, 1.35f);

        if (z > zInicial + 120f && Random.value < chanceParedeDeCor)
        {
            // Fileira especial: parede de cor ocupando a pista inteira.
            CriarParedeDeCor(z);
            espaco *= 1.15f;
        }
        else if (d > 0.25f && Random.value < 0.12f)
        {
            // Fileira especial: um obstáculo móvel sozinho.
            CriarMovel(z, d);
            espaco *= 1.15f;
        }
        else
        {
            Obst[] fila = MontarFila(d);

            for (int f = 0; f < Faixas; f++)
                if (fila[f] != Obst.Nenhum) CriarObstaculo(fila[f], f, z);

            // Moedas mostrando um caminho seguro por esta fileira.
            if (Random.value < chanceMoedas)
            {
                List<int> seguras = new List<int>();
                for (int f = 0; f < Faixas; f++)
                    if (fila[f] != Obst.Alto) seguras.Add(f);

                int faixa = seguras[Random.Range(0, seguras.Count)];
                CriarMoedas(faixa, z, fila[faixa], vel);
            }
        }

        // No meio do caminho até a próxima fileira: power-up ou mais moedas.
        float zMeio = z + espaco / 2f;
        if (zMeio >= proximoPowerUpZ)
        {
            CriarPowerUp(Random.Range(0, Faixas), zMeio);
            proximoPowerUpZ = zMeio + Random.Range(distanciaMinPowerUp, distanciaMaxPowerUp);
        }
        else if (espaco > 14f && Random.value < 0.3f)
        {
            CriarMoedas(Random.Range(0, Faixas), zMeio, Obst.Nenhum, vel);
        }

        return espaco;
    }

    /// <summary>Decide o que vai em cada faixa, sempre deixando passagem.</summary>
    Obst[] MontarFila(float d)
    {
        Obst[] fila = new Obst[Faixas];

        // Parede inteira que obriga a pular ou a deslizar.
        if (d > 0.15f && Random.value < 0.15f)
        {
            Obst tipo = Random.value < 0.5f ? Obst.Baixo : Obst.Barra;
            for (int f = 0; f < Faixas; f++) fila[f] = tipo;
            return fila;
        }

        // Bloqueia algumas faixas; pelo menos uma fica totalmente livre.
        int maximo = Mathf.Min(Faixas - 1, Mathf.RoundToInt(Mathf.Lerp(2f, 4f, d)));
        int quantidade = Random.Range(1, maximo + 1);

        List<int> livres = new List<int>();
        for (int f = 0; f < Faixas; f++) livres.Add(f);

        for (int i = 0; i < quantidade; i++)
        {
            int sorteio = Random.Range(0, livres.Count);
            int faixa = livres[sorteio];
            livres.RemoveAt(sorteio);

            float r = Random.value;
            fila[faixa] = r < 0.35f ? Obst.Baixo : (r < 0.75f ? Obst.Alto : Obst.Barra);
        }
        return fila;
    }

    void CriarObstaculo(Obst tipo, int faixa, float z)
    {
        float x = jogador.PosicaoXDaFaixa(faixa);
        float largura = jogador.larguraFaixa;
        Vector3 tamanho;
        float centroY;
        Color cor;

        switch (tipo)
        {
            case Obst.Baixo:
                tamanho = new Vector3(largura * 0.9f, 0.7f, 1f);
                centroY = 0.35f;
                cor = corBaixo;
                break;
            case Obst.Barra:
                // Painel suspenso: vão de 0,65 embaixo para passar deslizando.
                tamanho = new Vector3(largura, 2.2f, 0.6f);
                centroY = 0.65f + 1.1f;
                cor = corBarra;
                break;
            default:
                tamanho = new Vector3(largura * 0.9f, 2.6f, 1f);
                centroY = 1.3f;
                cor = corAlto;
                break;
        }

        // Cada tipo é uma forma geométrica vazada, com espinhos nos vértices:
        // baixo = triângulo, alto = hexágono, barra = retângulo suspenso.
        int lados;
        float angulo, espessura;
        switch (tipo)
        {
            case Obst.Baixo: lados = 3; angulo = 90f; espessura = 0.13f; break;
            case Obst.Barra: lados = 4; angulo = 45f; espessura = 0.18f; break;
            default: lados = 6; angulo = 90f; espessura = 0.18f; break;
        }

        // A forma é mais fina que a caixa de colisão, que continua a mesma.
        Vector3 tamanhoVisual = new Vector3(tamanho.x, tamanho.y, 0.5f);
        GameObject obj = Visual.Moldura("Obstaculo" + tipo, transform,
            new Vector3(x, centroY, z), lados, angulo, tamanhoVisual, espessura, cor);
        Registrar(obj, TipoItem.Obstaculo, tamanho);
    }

    void CriarMovel(float z, float d)
    {
        Vector3 tamanho = new Vector3(1.8f, 2.2f, 0.9f);
        float limite = MeiaPista - tamanho.x / 2f;

        // Losango vazado.
        TrackItem item = Registrar(Visual.Moldura("ObstaculoMovel", transform,
            new Vector3(Random.Range(-limite, limite), tamanho.y / 2f, z),
            4, 90f, new Vector3(tamanho.x, tamanho.y, 0.5f), 0.2f, corMovel),
            TipoItem.Obstaculo, tamanho);

        item.velocidadeX = Mathf.Lerp(3f, 6f, d) * (Random.value < 0.5f ? -1f : 1f);    }

    /// <summary>
    /// Barreira luminosa em todas as faixas. Só passa quem estiver da mesma cor.
    /// Pode vir de uma cor só (troque a cor do cubo) ou com uma cor por faixa
    /// (troque a cor ou vá para a faixa certa).
    /// </summary>
    void CriarParedeDeCor(float z)
    {
        int quantidadeCores = PlayerController.Cores.Length;
        bool corUnica = Random.value < 0.6f;
        int corBase = Random.Range(0, quantidadeCores);
        Vector3 tamanho = new Vector3(jogador.larguraFaixa, 3.2f, 0.3f);

        for (int f = 0; f < Faixas; f++)
        {
            int cor = corUnica ? corBase : Random.Range(0, quantidadeCores);

            TrackItem item = Registrar(Visual.Forma(PrimitiveType.Cube, "ParedeDeCor", transform,
                new Vector3(jogador.PosicaoXDaFaixa(f), tamanho.y / 2f, z), tamanho,
                PlayerController.Cores[cor], true, false, 0.05f),
                TipoItem.Obstaculo, tamanho);
            item.corId = cor;
        }
    }

    /// <summary>Projétil disparado por um inimigo a partir de "origem".</summary>
    public void CriarProjetil(Vector3 origem, TipoProjetil tipo = TipoProjetil.Normal)
    {
        GameObject obj;
        Vector3 tamanho;
        float velocidade = velocidadeProjetil;
        float alturaVoo = 0.5f;

        switch (tipo)
        {
            case TipoProjetil.Alto:
                // Lâmina alta: do vão de 0,65 para cima. Só passa deslizando.
                tamanho = new Vector3(0.9f, 1.9f, 0.35f);
                alturaVoo = 0.65f + tamanho.y / 2f;
                obj = Visual.Forma(PrimitiveType.Cube, "ProjetilAlto", transform,
                    origem, tamanho, new Color(0.65f, 0.3f, 1f), true);
                break;

            case TipoProjetil.Rapido:
                tamanho = Vector3.one * 0.42f;
                velocidade *= 1.7f;
                obj = Visual.Forma(PrimitiveType.Sphere, "ProjetilRapido", transform,
                    origem, tamanho, new Color(1f, 0.9f, 0.2f), true);
                // Rastro esticado para passar a ideia de velocidade.
                Visual.Forma(PrimitiveType.Cube, "Rastro", obj.transform,
                    new Vector3(0f, 0f, 1.6f), new Vector3(0.35f, 0.35f, 2.4f), new Color(1f, 0.9f, 0.2f), true, raio: 0f);
                break;

            case TipoProjetil.Onda:
                tamanho = Vector3.one * 0.55f;
                obj = Visual.Forma(PrimitiveType.Sphere, "ProjetilOnda", transform,
                    origem, tamanho, new Color(1f, 0.3f, 0.8f), true);
                break;

            case TipoProjetil.Teleguiado:
                tamanho = Vector3.one * 0.6f;
                velocidade *= 0.8f;
                obj = Visual.Forma(PrimitiveType.Sphere, "ProjetilTeleguiado", transform,
                    origem, tamanho, new Color(0.5f, 1f, 0.95f), true);
                // Anel em volta para diferenciar dos outros.
                Visual.Forma(PrimitiveType.Cube, "Anel", obj.transform,
                    Vector3.zero, new Vector3(1.7f, 0.15f, 1.7f), new Color(0.5f, 1f, 0.95f), true);
                break;

            default:
                tamanho = Vector3.one * 0.55f;
                obj = Visual.Forma(PrimitiveType.Sphere, "Projetil", transform,
                    origem, tamanho, new Color(1f, 0.1f, 0.15f), true);
                break;
        }

        TrackItem item = Registrar(obj, TipoItem.Obstaculo, tamanho);
        item.projetil = true;
        item.velocidadeZ = -velocidade;
        item.alturaVoo = alturaVoo;
        item.xBase = origem.x;

        if (tipo == TipoProjetil.Onda)
        {
            item.ondaAmplitude = jogador.larguraFaixa;
            item.ondaFrequencia = 3.2f;
        }
        else if (tipo == TipoProjetil.Teleguiado)
        {
            item.perseguicao = 2.5f;
            item.girar = true;
        }
    }

    /// <summary>
    /// A nave Kamikaze, ao mergulhar, passa a se comportar como um projétil:
    /// desce até a pista, voa contra o jogador e explode se bater em obstáculo.
    /// </summary>
    public void AdotarKamikaze(GameObject nave, Vector3 tamanho)
    {
        TrackItem item = Registrar(nave, TipoItem.Obstaculo, tamanho);
        item.projetil = true;
        item.velocidadeZ = -velocidadeProjetil * 1.5f;
        item.alturaVoo = tamanho.y / 2f + 0.1f;
        item.perseguicao = 1.5f;
    }

    void CriarMoedas(int faixa, float z, Obst sobre, float vel)
    {
        const int quantidade = 5;
        float x = jogador.PosicaoXDaFaixa(faixa);

        // Sobre obstáculo baixo as moedas formam o arco do pulo.
        bool arco = sobre == Obst.Baixo;
        float passo = arco ? vel * jogador.TempoNoAr / 6f : 1.6f;

        for (int i = 0; i < quantidade; i++)
        {
            float dz = (i - (quantidade - 1) / 2f) * passo;
            float y;

            if (arco)
            {
                float t = dz / vel;
                float subida = Mathf.Max(0f, jogador.AlturaMaximaPulo - 0.5f * jogador.gravidade * t * t);
                y = 0.5f + subida;
            }
            else if (sobre == Obst.Barra) y = 0.3f;  // rente ao chão, por baixo da barra
            else y = 0.7f;

            GameObject moeda = Visual.Forma(PrimitiveType.Cube, "Moeda", transform,
                new Vector3(x, y, z + dz), Vector3.one * 0.4f, corMoeda, true);
            moeda.transform.rotation = Quaternion.Euler(45f, 0f, 45f);

            TrackItem item = Registrar(moeda, TipoItem.Moeda, Vector3.one * 0.9f);
            item.girar = true;
        }
    }

    void CriarPowerUp(int faixa, float z)
    {
        TipoItem tipo;
        Color cor;
        switch (Random.Range(0, 3))
        {
            case 0: tipo = TipoItem.Escudo; cor = corEscudo; break;
            case 1: tipo = TipoItem.Ima; cor = corIma; break;
            default: tipo = TipoItem.Dobro; cor = corDobro; break;
        }

        GameObject obj = Visual.Forma(PrimitiveType.Sphere, "PowerUp" + tipo, transform,
            new Vector3(jogador.PosicaoXDaFaixa(faixa), 0.9f, z), Vector3.one * 0.9f, cor, true);

        // Anel em volta, para diferenciar bem das moedas.
        Visual.Forma(PrimitiveType.Cube, "Anel", obj.transform,
            Vector3.zero, new Vector3(1.5f, 0.12f, 1.5f), cor, true);

        TrackItem item = Registrar(obj, tipo, Vector3.one * 1.2f);
        item.girar = true;
    }

    TrackItem Registrar(GameObject obj, TipoItem tipo, Vector3 tamanho)
    {
        TrackItem item = obj.AddComponent<TrackItem>();
        item.tipo = tipo;
        item.tamanho = tamanho;
        itens.Add(item);
        return item;
    }
}
