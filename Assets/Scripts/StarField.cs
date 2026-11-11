using UnityEngine;

/// <summary>
/// Estrelas ao redor da pista. São sempre as mesmas: quando uma fica para
/// trás do jogador, volta lá para a frente em outra posição.
/// </summary>
public class StarField : MonoBehaviour
{
    public Transform jogador;
    public int quantidade = 160;
    public float profundidade = 95f;
    public float largura = 45f;

    Transform[] estrelas;

    void Start()
    {
        estrelas = new Transform[quantidade];
        float zBase = jogador != null ? jogador.position.z : 0f;

        for (int i = 0; i < quantidade; i++)
        {
            float brilho = Random.Range(0.6f, 1f);
            // Poucos tons diferentes, para reaproveitar materiais.
            Color cor = i % 3 == 0 ? new Color(0.7f, 0.85f, 1f) : (i % 3 == 1 ? Color.white : new Color(1f, 0.9f, 0.7f));

            GameObject estrela = Visual.Forma(PrimitiveType.Cube, "Estrela", transform,
                Vector3.zero, Vector3.one * Random.Range(0.12f, 0.35f) * brilho, cor, true, raio: 0f);

            estrelas[i] = estrela.transform;
            Posicionar(estrelas[i], zBase - 10f + Random.Range(0f, profundidade));
        }
    }

    void Update()
    {
        if (jogador == null) return;
        float z = jogador.position.z;

        for (int i = 0; i < estrelas.Length; i++)
        {
            if (estrelas[i].position.z < z - 15f)
                Posicionar(estrelas[i], estrelas[i].position.z + profundidade);
        }
    }

    void Posicionar(Transform estrela, float z)
    {
        // Mantém as estrelas fora do corredor da pista.
        float x = Random.Range(9f, largura) * (Random.value < 0.5f ? -1f : 1f);
        float y = Random.Range(-18f, 30f);

        // Algumas passam por cima da pista, bem no alto.
        if (Random.value < 0.25f)
        {
            x = Random.Range(-9f, 9f);
            y = Random.Range(12f, 30f);
        }

        estrela.position = new Vector3(x, y, z);
    }
}
