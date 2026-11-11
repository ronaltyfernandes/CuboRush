using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gera o chão automaticamente conforme o jogador avança.
/// O piso é sempre o mesmo: um conjunto fixo de pedaços que, ao ficarem
/// para trás do jogador, são reposicionados lá na frente (pista infinita
/// sem criar/destruir objetos o tempo todo).
/// </summary>
public class FloorGenerator : MonoBehaviour
{
    [Tooltip("Quem o chão acompanha (o cubo).")]
    public Transform jogador;

    [Header("Pista")]
    public int quantidadeFaixas = 5;
    public float larguraFaixa = 2f;

    [Header("Pedaços de chão")]
    [Tooltip("Comprimento de cada pedaço.")]
    public float comprimentoPedaco = 20f;
    [Tooltip("Quantos pedaços existem ao mesmo tempo.")]
    public int quantidadePedacos = 9;

    [Header("Visual")]
    public Color corPiso = new Color(0.10f, 0.08f, 0.22f);
    public Color corLinhas = new Color(0.20f, 0.90f, 1.00f);
    public float larguraLinha = 0.08f;

    readonly Queue<Transform> pedacos = new Queue<Transform>();
    float proximoZ;

    void Start()
    {
        // Começa um pedaço atrás do jogador para a câmera não ver o vazio.
        float zInicial = jogador != null ? jogador.position.z : 0f;
        proximoZ = zInicial - comprimentoPedaco;

        for (int i = 0; i < quantidadePedacos; i++)
            ColocarNaFrente(CriarPedaco());
    }

    void Update()
    {
        if (jogador == null) return;

        // Enquanto o pedaço mais antigo estiver totalmente para trás, manda ele para a frente.
        while (pedacos.Count > 0 &&
               jogador.position.z - pedacos.Peek().position.z > comprimentoPedaco * 2f)
        {
            ColocarNaFrente(pedacos.Dequeue());
        }
    }

    void ColocarNaFrente(Transform pedaco)
    {
        // O pivô do pedaço fica no início dele (borda de trás).
        pedaco.position = new Vector3(0f, 0f, proximoZ);
        proximoZ += comprimentoPedaco;
        pedacos.Enqueue(pedaco);
    }

    Transform CriarPedaco()
    {
        float larguraTotal = quantidadeFaixas * larguraFaixa;

        Transform raiz = new GameObject("PedacoDeChao").transform;
        raiz.SetParent(transform, false);

        // Piso (topo na altura Y = 0).
        GameObject piso = Visual.Forma(PrimitiveType.Cube, "Piso", raiz,
            new Vector3(0f, -0.1f, comprimentoPedaco / 2f),
            new Vector3(larguraTotal, 0.2f, comprimentoPedaco), corPiso, false, true, 0.03f);

        // Grade neon: um quadrado por faixa, repetido ao longo do pedaço.
        piso.GetComponent<Renderer>().sharedMaterial = Visual.MatPiso(corPiso, corLinhas,
            new Vector2(quantidadeFaixas, comprimentoPedaco / larguraFaixa));

        // Linhas que separam as faixas (inclui as duas bordas).
        for (int i = 0; i <= quantidadeFaixas; i++)
        {
            Visual.Forma(PrimitiveType.Cube, "Linha", raiz,
                new Vector3(-larguraTotal / 2f + i * larguraFaixa, 0.01f, comprimentoPedaco / 2f),
                new Vector3(larguraLinha, 0.02f, comprimentoPedaco), corLinhas, true, raio: 0f);
        }

        return raiz;
    }
}
