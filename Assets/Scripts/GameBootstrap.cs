using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Monta o jogo sozinho ao apertar Play (e de novo a cada reinício):
/// cria o GameManager, o cubo, o chão, os obstáculos, as estrelas e liga a câmera.
/// Se a cena já tiver um PlayerController, este script não faz nada
/// (assim dá para montar a cena à mão no futuro).
/// </summary>
public static class GameBootstrap
{
    const int QuantidadeFaixas = 5;
    const float LarguraFaixa = 2f;

    static bool inscrito;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        Montar();

        // Reiniciar o jogo recarrega a cena; monta tudo outra vez.
        if (!inscrito)
        {
            inscrito = true;
            SceneManager.sceneLoaded += (cena, modo) => Montar();
        }
    }

    static void Montar()
    {
        if (Object.FindAnyObjectByType<PlayerController>() != null) return;

        Time.timeScale = 1f;

        // Regras do jogo (precisa existir antes dos outros).
        new GameObject("GameManager").AddComponent<GameManager>();

        // Cubo do jogador, apoiado no chão, no meio da pista.
        GameObject cubo = Visual.Forma(PrimitiveType.Cube, "Jogador", null,
            new Vector3(0f, 0.5f, 0f), Vector3.one, PlayerController.Cores[0]);

        PlayerController jogador = cubo.AddComponent<PlayerController>();
        jogador.quantidadeFaixas = QuantidadeFaixas;
        jogador.larguraFaixa = LarguraFaixa;

        // Chão infinito.
        FloorGenerator chao = new GameObject("GeradorDeChao").AddComponent<FloorGenerator>();
        chao.jogador = cubo.transform;
        chao.quantidadeFaixas = QuantidadeFaixas;
        chao.larguraFaixa = LarguraFaixa;

        // Obstáculos, moedas e power-ups.
        ObstacleSpawner obstaculos = new GameObject("GeradorDeObstaculos").AddComponent<ObstacleSpawner>();
        obstaculos.jogador = jogador;

        // Estrelas de fundo.
        StarField estrelas = new GameObject("Estrelas").AddComponent<StarField>();
        estrelas.jogador = cubo.transform;

        // Câmera.
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject objCamera = new GameObject("Main Camera");
            objCamera.tag = "MainCamera";
            camera = objCamera.AddComponent<Camera>();
            objCamera.AddComponent<AudioListener>();
        }

        CameraFollow seguir = camera.GetComponent<CameraFollow>();
        if (seguir == null) seguir = camera.gameObject.AddComponent<CameraFollow>();
        seguir.alvo = cubo.transform;

        // Fundo preto, para a temática espacial.
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;

        // Brilho (bloom): é o que faz as partes luminosas parecerem neon.
        camera.allowHDR = true;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        Volume volume = new GameObject("BrilhoNeon").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        VolumeProfile perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        Bloom bloom = perfil.Add<Bloom>(true);
        bloom.threshold.value = 0.9f;
        bloom.intensity.value = 0.9f;
        bloom.scatter.value = 0.65f;
        volume.profile = perfil;
    }
}
