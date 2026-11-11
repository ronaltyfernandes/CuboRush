using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Aparência do jogo, toda gerada por código:
///  - cubos com cantos levemente arredondados;
///  - textura neon (centro escuro, bordas luminosas) tingida pela cor de cada objeto;
///  - materiais reaproveitados (um por cor) para não pesar no desempenho.
/// </summary>
public static class Visual
{
    /// <summary>Raio padrão do arredondamento dos cubos.</summary>
    public const float RaioPadrao = 0.12f;

    static readonly Dictionary<string, Material> materiais = new Dictionary<string, Material>();
    static readonly Dictionary<string, Mesh> malhas = new Dictionary<string, Mesh>();
    static Material materialBase;
    static Texture2D texturaBase, texturaBrilho;

    // ---------- Materiais ----------

    /// <summary>
    /// Material neon de uma cor. "brilhante" faz o objeto inteiro emitir luz
    /// (moedas, linhas, estrelas); senão, só as bordas brilham.
    /// </summary>
    public static Material Mat(Color cor, bool brilhante = false)
    {
        string chave = ColorUtility.ToHtmlStringRGBA(cor) + (brilhante ? "B" : "N");
        if (materiais.TryGetValue(chave, out Material pronto) && pronto != null)
            return pronto;

        Material m = NovoMaterial();
        m.color = cor;
        m.EnableKeyword("_EMISSION");

        if (brilhante)
        {
            m.SetColor("_EmissionColor", cor * 1.6f);
        }
        else
        {
            GarantirTexturas();
            m.mainTexture = texturaBase;
            m.SetTexture("_EmissionMap", texturaBrilho);
            m.SetColor("_EmissionColor", cor * 2.2f);
        }

        materiais[chave] = m;
        return m;
    }

    /// <summary>Material do piso: cor lisa com uma grade neon repetida.</summary>
    public static Material MatPiso(Color corPiso, Color corGrade, Vector2 repeticao)
    {
        string chave = "Piso" + ColorUtility.ToHtmlStringRGBA(corPiso) +
                       ColorUtility.ToHtmlStringRGBA(corGrade) + repeticao;
        if (materiais.TryGetValue(chave, out Material pronto) && pronto != null)
            return pronto;

        GarantirTexturas();
        Material m = NovoMaterial();
        m.color = corPiso;
        m.mainTextureScale = repeticao;
        m.EnableKeyword("_EMISSION");
        m.SetTexture("_EmissionMap", texturaBrilho);
        m.SetColor("_EmissionColor", corGrade * 0.9f);

        materiais[chave] = m;
        return m;
    }

    static Material NovoMaterial()
    {
        if (materialBase == null)
        {
            // Pega o material padrão do pipeline de renderização em uso.
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            materialBase = temp.GetComponent<Renderer>().sharedMaterial;
            Object.Destroy(temp);
        }

        Material m = new Material(materialBase);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.65f);
        return m;
    }

    // ---------- Textura neon ----------

    static void GarantirTexturas()
    {
        if (texturaBase == null) texturaBase = CriarTexturaNeon(0.32f);
        if (texturaBrilho == null) texturaBrilho = CriarTexturaNeon(0.03f);
    }

    /// <summary>
    /// Quadrado com borda clara e um halo suave para dentro.
    /// "centro" é o quanto o meio do quadrado fica claro (0 = preto).
    /// </summary>
    static Texture2D CriarTexturaNeon(float centro)
    {
        const int lado = 128;
        Texture2D tex = new Texture2D(lado, lado, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 4;

        Color[] pixels = new Color[lado * lado];
        for (int y = 0; y < lado; y++)
        {
            for (int x = 0; x < lado; x++)
            {
                float u = (x + 0.5f) / lado, v = (y + 0.5f) / lado;
                // Distância até a borda mais próxima (0 na borda, 0,5 no meio).
                float d = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));

                float linha = Mathf.Clamp01(1f - d / 0.05f);
                linha *= linha;
                float halo = 0.4f * Mathf.Exp(-d * 16f);

                float valor = Mathf.Lerp(centro, 1f, Mathf.Clamp01(linha + halo));
                pixels[y * lado + x] = new Color(valor, valor, valor, 1f);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(true);
        return tex;
    }

    // ---------- Formas ----------

    /// <summary>
    /// Cria uma forma já posicionada e colorida. Cubos saem com os cantos
    /// arredondados; use raio 0 para um cubo comum (linhas, estrelas).
    /// </summary>
    public static GameObject Forma(PrimitiveType tipo, string nome, Transform pai,
        Vector3 posicao, Vector3 escala, Color cor, bool brilhante = false,
        bool comColisor = false, float raio = RaioPadrao)
    {
        GameObject obj = GameObject.CreatePrimitive(tipo);
        obj.name = nome;
        if (pai != null) obj.transform.SetParent(pai, false);
        obj.transform.localPosition = posicao;
        obj.transform.localScale = escala;
        obj.GetComponent<Renderer>().sharedMaterial = Mat(cor, brilhante);

        if (tipo == PrimitiveType.Cube && raio > 0f)
        {
            MeshFilter filtro = obj.GetComponent<MeshFilter>();
            bool uniforme = Mathf.Approximately(escala.x, escala.y) && Mathf.Approximately(escala.y, escala.z);

            if (uniforme)
            {
                // Cubo "de verdade": usa a malha de tamanho 1 e mantém a escala.
                filtro.sharedMesh = CaixaArredondada(Vector3.one, raio);
            }
            else
            {
                // Caixa esticada: a malha já nasce no tamanho certo, para o
                // arredondamento não ficar deformado.
                filtro.sharedMesh = CaixaArredondada(escala, raio);
                obj.transform.localScale = Vector3.one;
                BoxCollider caixa = obj.GetComponent<BoxCollider>();
                if (caixa != null) caixa.size = escala;
            }
        }

        if (!comColisor)
        {
            Collider c = obj.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
        }
        return obj;
    }

    // ---------- Molduras geométricas (formas vazadas) ----------

    /// <summary>
    /// Cria um polígono vazado (só o contorno, com o miolo aberto) de frente
    /// para a pista, com um espinho apontando para fora em cada vértice.
    ///  - lados: 3 = triângulo, 4 = retângulo/losango, 6 = hexágono...
    ///  - anguloInicial: direção do primeiro vértice em graus (90 = ponta para cima;
    ///    com 4 lados, 45 dá um retângulo e 90 dá um losango).
    ///  - tamanho: largura (X), altura (Y) e profundidade (Z) ocupadas pela forma.
    ///  - espessura: grossura do contorno.
    /// </summary>
    public static GameObject Moldura(string nome, Transform pai, Vector3 posicao,
        int lados, float anguloInicial, Vector3 tamanho, float espessura, Color cor)
    {
        GameObject obj = new GameObject(nome);
        if (pai != null) obj.transform.SetParent(pai, false);
        obj.transform.localPosition = posicao;
        obj.AddComponent<MeshFilter>().sharedMesh = MalhaMoldura(lados, anguloInicial, tamanho, espessura);
        obj.AddComponent<MeshRenderer>().sharedMaterial = Mat(cor);
        return obj;
    }

    static Mesh MalhaMoldura(int lados, float anguloInicial, Vector3 tamanho, float espessura)
    {
        string chave = "M" + lados + "_" + anguloInicial.ToString("F0") + tamanho.ToString("F2") + espessura.ToString("F2");
        if (malhas.TryGetValue(chave, out Mesh pronta) && pronta != null)
            return pronta;

        // Vértices do polígono, esticados para ocupar exatamente largura x altura.
        Vector2[] fora = new Vector2[lados];
        Vector2 minimo = Vector2.one * float.MaxValue, maximo = Vector2.one * float.MinValue;
        for (int k = 0; k < lados; k++)
        {
            float a = (anguloInicial + 360f * k / lados) * Mathf.Deg2Rad;
            fora[k] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            minimo = Vector2.Min(minimo, fora[k]);
            maximo = Vector2.Max(maximo, fora[k]);
        }

        Vector2[] dentro = new Vector2[lados];
        float escalaX = Mathf.Max(0.05f, (tamanho.x - 2f * espessura) / tamanho.x);
        float escalaY = Mathf.Max(0.05f, (tamanho.y - 2f * espessura) / tamanho.y);
        for (int k = 0; k < lados; k++)
        {
            fora[k] = new Vector2(
                (Mathf.InverseLerp(minimo.x, maximo.x, fora[k].x) - 0.5f) * tamanho.x,
                (Mathf.InverseLerp(minimo.y, maximo.y, fora[k].y) - 0.5f) * tamanho.y);
            dentro[k] = new Vector2(fora[k].x * escalaX, fora[k].y * escalaY);
        }

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normais = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangulos = new List<int>();

        float zf = -tamanho.z / 2f, zt = tamanho.z / 2f; // frente e trás

        // Contorno: cada lado do polígono é uma barra com 4 faces.
        for (int k = 0; k < lados; k++)
        {
            int k1 = (k + 1) % lados;
            Vector3 fa = new Vector3(fora[k].x, fora[k].y, zf), fb = new Vector3(fora[k1].x, fora[k1].y, zf);
            Vector3 da = new Vector3(dentro[k].x, dentro[k].y, zf), db = new Vector3(dentro[k1].x, dentro[k1].y, zf);
            Vector3 fa2 = fa, fb2 = fb, da2 = da, db2 = db;
            fa2.z = fb2.z = da2.z = db2.z = zt;

            Vector3 miolo = (fa + fb + da + db + fa2 + fb2 + da2 + db2) / 8f;

            Quadrado(vertices, normais, uvs, triangulos, fa, fb, db, da, miolo);       // frente
            Quadrado(vertices, normais, uvs, triangulos, fa2, fb2, db2, da2, miolo);   // trás
            Quadrado(vertices, normais, uvs, triangulos, fa, fb, fb2, fa2, miolo);     // borda de fora
            Quadrado(vertices, normais, uvs, triangulos, da, db, db2, da2, miolo);     // borda de dentro
        }

        // Espinhos: uma pirâmide em cada vértice, apontando para fora.
        const float meiaBase = 0.13f, comprimento = 0.32f;
        for (int k = 0; k < lados; k++)
        {
            Vector3 direcao = new Vector3(fora[k].x, fora[k].y, 0f).normalized;
            Vector3 lado = new Vector3(-direcao.y, direcao.x, 0f);
            Vector3 centroBase = new Vector3(fora[k].x, fora[k].y, 0f) - direcao * 0.06f;
            Vector3 apice = centroBase + direcao * (comprimento + 0.06f);

            Vector3[] cantos =
            {
                centroBase - lado * meiaBase + Vector3.back * meiaBase,
                centroBase + lado * meiaBase + Vector3.back * meiaBase,
                centroBase + lado * meiaBase + Vector3.forward * meiaBase,
                centroBase - lado * meiaBase + Vector3.forward * meiaBase
            };

            for (int c = 0; c < 4; c++)
                Triangulo(vertices, normais, uvs, triangulos, apice, cantos[c], cantos[(c + 1) % 4], centroBase);
        }

        Mesh malha = new Mesh();
        malha.name = "Moldura" + lados;
        malha.SetVertices(vertices);
        malha.SetNormals(normais);
        malha.SetUVs(0, uvs);
        malha.SetTriangles(triangulos, 0);
        malha.RecalculateBounds();

        malhas[chave] = malha;
        return malha;
    }

    /// <summary>Face de 4 pontos (em ordem de contorno), virada para longe de "miolo".</summary>
    static void Quadrado(List<Vector3> vertices, List<Vector3> normais, List<Vector2> uvs, List<int> triangulos,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 miolo)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
        if (Vector3.Dot(normal, (a + b + c + d) / 4f - miolo) < 0f)
        {
            Vector3 troca = b; b = d; d = troca;
            normal = -normal;
        }

        int i = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        for (int n = 0; n < 4; n++) normais.Add(normal);

        // Cada face usa a textura inteira: as bordas neon contornam a barra.
        uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f));
        uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));

        triangulos.Add(i); triangulos.Add(i + 1); triangulos.Add(i + 2);
        triangulos.Add(i); triangulos.Add(i + 2); triangulos.Add(i + 3);
    }

    /// <summary>Triângulo virado para longe de "miolo". Usa a parte clara da textura (brilha inteiro).</summary>
    static void Triangulo(List<Vector3> vertices, List<Vector3> normais, List<Vector2> uvs, List<int> triangulos,
        Vector3 a, Vector3 b, Vector3 c, Vector3 miolo)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
        if (Vector3.Dot(normal, (a + b + c) / 3f - miolo) < 0f)
        {
            Vector3 troca = b; b = c; c = troca;
            normal = -normal;
        }

        int i = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c);
        for (int n = 0; n < 3; n++)
        {
            normais.Add(normal);
            uvs.Add(Vector2.zero);
            triangulos.Add(i + n);
        }
    }

    // ---------- Espinhos ----------

    /// <summary>
    /// Placa de espinhos luminosos para colar em uma face de um obstáculo.
    /// Sem rotação, a placa fica no plano XY e os espinhos apontam para -Z
    /// (na direção de quem vem pela pista).
    /// </summary>
    public static GameObject Espinhos(Transform pai, Vector3 posicao, Quaternion rotacao,
        float largura, float altura, Color cor)
    {
        GameObject obj = new GameObject("Espinhos");
        obj.transform.SetParent(pai, false);
        obj.transform.localPosition = posicao;
        obj.transform.localRotation = rotacao;
        obj.AddComponent<MeshFilter>().sharedMesh = MalhaEspinhos(largura, altura);
        obj.AddComponent<MeshRenderer>().sharedMaterial = Mat(Color.Lerp(cor, Color.white, 0.45f), true);
        return obj;
    }

    static Mesh MalhaEspinhos(float largura, float altura)
    {
        largura = Mathf.Max(largura, 0.3f);
        altura = Mathf.Max(altura, 0.3f);

        string chave = "E" + largura.ToString("F2") + "x" + altura.ToString("F2");
        if (malhas.TryGetValue(chave, out Mesh pronta) && pronta != null)
            return pronta;

        const float meiaBase = 0.16f, comprimento = 0.32f;
        int colunas = Mathf.Max(1, Mathf.FloorToInt(largura / 0.45f));
        int linhas = Mathf.Max(1, Mathf.FloorToInt(altura / 0.45f));

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normais = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangulos = new List<int>();
        Vector3[] cantos = new Vector3[4];

        for (int l = 0; l < linhas; l++)
        {
            for (int c = 0; c < colunas; c++)
            {
                float cx = -largura / 2f + (c + 0.5f) * largura / colunas;
                float cy = -altura / 2f + (l + 0.5f) * altura / linhas;
                Vector3 centro = new Vector3(cx, cy, 0f);
                Vector3 apice = new Vector3(cx, cy, -comprimento);

                cantos[0] = new Vector3(cx - meiaBase, cy - meiaBase, 0f);
                cantos[1] = new Vector3(cx + meiaBase, cy - meiaBase, 0f);
                cantos[2] = new Vector3(cx + meiaBase, cy + meiaBase, 0f);
                cantos[3] = new Vector3(cx - meiaBase, cy + meiaBase, 0f);

                // Quatro faces da pirâmide.
                for (int k = 0; k < 4; k++)
                {
                    Vector3 v1 = cantos[k], v2 = cantos[(k + 1) % 4];
                    Vector3 normal = Vector3.Cross(v1 - apice, v2 - apice).normalized;
                    Vector3 paraFora = (v1 + v2) / 2f - centro + new Vector3(0f, 0f, -meiaBase);

                    if (Vector3.Dot(normal, paraFora) < 0f)
                    {
                        Vector3 troca = v1; v1 = v2; v2 = troca;
                        normal = -normal;
                    }

                    int inicio = vertices.Count;
                    vertices.Add(apice); vertices.Add(v1); vertices.Add(v2);
                    for (int n = 0; n < 3; n++)
                    {
                        normais.Add(normal);
                        uvs.Add(new Vector2(0.5f, 0.5f));
                        triangulos.Add(inicio + n);
                    }
                }
            }
        }

        Mesh malha = new Mesh();
        malha.name = "Espinhos";
        malha.SetVertices(vertices);
        malha.SetNormals(normais);
        malha.SetUVs(0, uvs);
        malha.SetTriangles(triangulos, 0);
        malha.RecalculateBounds();

        malhas[chave] = malha;
        return malha;
    }

    // ---------- Malha de caixa arredondada ----------

    /// <summary>Malha de uma caixa com arestas e cantos arredondados (com cache por tamanho).</summary>
    public static Mesh CaixaArredondada(Vector3 tamanho, float raio, int segmentos = 4)
    {
        Vector3 metade = tamanho / 2f;
        raio = Mathf.Min(raio, Mathf.Min(metade.x, Mathf.Min(metade.y, metade.z)) * 0.9f);

        string chave = tamanho.ToString("F3") + raio.ToString("F3");
        if (malhas.TryGetValue(chave, out Mesh pronta) && pronta != null)
            return pronta;

        Vector3 interno = metade - Vector3.one * raio;
        float[][] coords =
        {
            Coordenadas(metade.x, raio, segmentos),
            Coordenadas(metade.y, raio, segmentos),
            Coordenadas(metade.z, raio, segmentos)
        };

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normais = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangulos = new List<int>();

        // Para cada face: eixo da normal, eixo U e eixo V (0 = X, 1 = Y, 2 = Z).
        int[,] faces = { { 0, 2, 1 }, { 1, 0, 2 }, { 2, 0, 1 } };

        for (int f = 0; f < 3; f++)
        {
            for (int sinal = -1; sinal <= 1; sinal += 2)
            {
                int eixoN = faces[f, 0], eixoU = faces[f, 1], eixoV = faces[f, 2];
                float[] cu = coords[eixoU], cv = coords[eixoV];
                int inicio = vertices.Count;

                Vector3 n = Vector3.zero, dirU = Vector3.zero, dirV = Vector3.zero;
                n[eixoN] = sinal;
                dirU[eixoU] = 1f;
                dirV[eixoV] = 1f;

                for (int j = 0; j < cv.Length; j++)
                {
                    for (int i = 0; i < cu.Length; i++)
                    {
                        // Ponto na face do cubo "reto"...
                        Vector3 p = Vector3.zero;
                        p[eixoN] = sinal * metade[eixoN];
                        p[eixoU] = cu[i];
                        p[eixoV] = cv[j];

                        // ...puxado para a superfície arredondada.
                        Vector3 dentro = new Vector3(
                            Mathf.Clamp(p.x, -interno.x, interno.x),
                            Mathf.Clamp(p.y, -interno.y, interno.y),
                            Mathf.Clamp(p.z, -interno.z, interno.z));
                        Vector3 normal = (p - dentro).normalized;

                        vertices.Add(dentro + normal * raio);
                        normais.Add(normal);
                        uvs.Add(new Vector2(
                            (cu[i] + metade[eixoU]) / tamanho[eixoU],
                            (cv[j] + metade[eixoV]) / tamanho[eixoV]));
                    }
                }

                // Ordem dos triângulos depende de para que lado a face olha.
                bool frente = Vector3.Dot(Vector3.Cross(dirV, dirU), n) > 0f;
                int largura = cu.Length;

                for (int j = 0; j < cv.Length - 1; j++)
                {
                    for (int i = 0; i < largura - 1; i++)
                    {
                        int a = inicio + j * largura + i;   // (i, j)
                        int b = a + 1;                      // (i+1, j)
                        int c = a + largura;                // (i, j+1)
                        int d = c + 1;                      // (i+1, j+1)

                        if (frente)
                        {
                            triangulos.Add(a); triangulos.Add(c); triangulos.Add(b);
                            triangulos.Add(b); triangulos.Add(c); triangulos.Add(d);
                        }
                        else
                        {
                            triangulos.Add(a); triangulos.Add(b); triangulos.Add(c);
                            triangulos.Add(b); triangulos.Add(d); triangulos.Add(c);
                        }
                    }
                }
            }
        }

        Mesh malha = new Mesh();
        malha.name = "CaixaArredondada";
        malha.SetVertices(vertices);
        malha.SetNormals(normais);
        malha.SetUVs(0, uvs);
        malha.SetTriangles(triangulos, 0);
        malha.RecalculateBounds();

        malhas[chave] = malha;
        return malha;
    }

    /// <summary>
    /// Posições ao longo de um eixo: mais pontos perto das pontas (onde fica a
    /// curva) e um trecho reto no meio.
    /// </summary>
    static float[] Coordenadas(float metade, float raio, int segmentos)
    {
        float[] c = new float[segmentos * 2 + 2];
        for (int i = 0; i <= segmentos; i++)
        {
            float tangente = Mathf.Tan(Mathf.PI / 4f * (segmentos - i) / segmentos);
            c[i] = -metade + raio - raio * tangente;        // de -metade até o início do trecho reto
            c[c.Length - 1 - i] = metade - raio + raio * tangente; // espelhado
        }
        return c;
    }
}
