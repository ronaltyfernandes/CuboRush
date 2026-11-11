using UnityEngine;

public enum TipoItem { Obstaculo, Moeda, Escudo, Ima, Dobro }

/// <summary>
/// Tipos de projétil dos inimigos:
///  - Normal:     esfera vermelha rente ao chão. Pule ou troque de faixa.
///  - Alto:       lâmina roxa alta. Deslize por baixo ou troque de faixa.
///  - Rapido:     esfera amarela pequena e veloz.
///  - Onda:       esfera rosa que balança entre as faixas vizinhas.
///  - Teleguiado: esfera azul-clara que persegue o cubo de lado (pule na hora certa).
/// </summary>
public enum TipoProjetil { Normal, Alto, Rapido, Onda, Teleguiado }

/// <summary>
/// Qualquer coisa colocada na pista: obstáculo, parede de cor, projétil,
/// moeda ou power-up. A colisão é verificada pelo ObstacleSpawner usando
/// "tamanho" como caixa de colisão.
/// </summary>
public class TrackItem : MonoBehaviour
{
    public TipoItem tipo;

    [Tooltip("Tamanho da caixa de colisão, centrada no objeto.")]
    public Vector3 tamanho = Vector3.one;

    [Tooltip("Velocidade lateral (obstáculos móveis). 0 = parado.")]
    public float velocidadeX;

    [Tooltip("Gira no lugar (moedas e power-ups).")]
    public bool girar;

    [Tooltip("Parede de cor: índice da cor que atravessa sem dano. -1 = obstáculo comum.")]
    public int corId = -1;

    [Header("Projétil")]
    [Tooltip("Voa contra o jogador e é barrado por obstáculos.")]
    public bool projetil;
    public float velocidadeZ;
    [Tooltip("Altura em que o projétil viaja depois de descer da nave.")]
    public float alturaVoo = 0.5f;
    [Tooltip("Balanço lateral (Onda): amplitude e velocidade.")]
    public float ondaAmplitude;
    public float ondaFrequencia;
    [Tooltip("Velocidade lateral com que persegue o jogador (Teleguiado).")]
    public float perseguicao;

    [HideInInspector] public float xBase;
    [HideInInspector] public float fase;
    /// <summary>Já deu o bônus de travessia (paredes de cor).</summary>
    [HideInInspector] public bool atravessado;

    public Bounds Limites => new Bounds(transform.position, tamanho);
}
