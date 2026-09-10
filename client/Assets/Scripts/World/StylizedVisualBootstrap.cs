using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Espectro.Prototype
{
    public static class StylizedVisualBootstrap
    {
        private static readonly Dictionary<int, Material> Materials = new();
        private static readonly Dictionary<string, Material> ImportedMaterials = new();

        // O FBX do Quaternius Nature (árvore/arbusto/grama/rocha) não resolve a textura embutida
        // ao importar (referência externa quebrada — confirmado: source.mainTexture vem null
        // para todo esse pacote). Já o FBX da Village resolve corretamente sozinho na maioria dos
        // materiais (confirmado extraindo em batchmode) — só falha onde a textura de origem nem
        // existe no pacote (pedra de acabamento "RockTrim", vidro de janela). Por isso a
        // prioridade em ConvertImportedMaterials é: 1) textura já resolvida pelo próprio FBX,
        // 2) override por nome de MATERIAL (abaixo) só quando o FBX não resolveu nada, 3) cor
        // fixa de último recurso. Um override "por recurso inteiro" (nome do arquivo) foi
        // removido de propósito: peças como parede/telhado têm mais de um material (viga de
        // madeira + reboco), e forçar uma textura única pro recurso inteiro apagava a textura de
        // madeira que o FBX já resolvia certinho, pintando a viga com reboco por cima.
        private static readonly Dictionary<string, string> MaterialTextureOverrides = new()
        {
            ["NormalTree_Bark"] = "NormalTree_Bark",
            ["NormalTree_Leaves"] = "NormalTree_Leaves",
            ["PineTree_Bark"] = "PineTree_Bark",
            ["PineTree_Leaves"] = "PineTree_Leaves",
            ["Bush_Leaves"] = "Bush_Leaves",
            ["Grass"] = "Grass",
            ["Rock"] = "Rocks",
            ["MI_Vine"] = "T_VineLeaf",
        };

        // Materiais da Village sem textura correspondente no pacote (confirmado: não existe
        // T_RockTrim_*.png nem textura de vidro) — cor fixa plausível em vez de ficar cinza
        // padrão do shader.
        private static readonly Dictionary<string, Color> MaterialColorOverrides = new()
        {
            ["MI_RockTrim"] = new Color(0.5f, 0.48f, 0.45f),
            ["MI_WindowGlass"] = new Color(0.62f, 0.74f, 0.78f),
        };
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallSafeVisualLayer()
        {
            if (GameObject.Find("Espectro Visual Upgrade") != null) return;
            var root = new GameObject("Espectro Visual Upgrade").transform;

            // Primeira camada validavel: apenas luz, atmosfera e paleta.
            // Geometria, camera e pos-processamento permanecem intocados.
            ConfigureAtmosphere();
            RecolorExistingWorld();
            CreateCrossroadsLandmark(root);
            UpgradePlayer();
            // Religado: troca as primitivas (CreateCliffs/CreateVegetation) pelos modelos reais
            // do Quaternius (árvores, rochas, casas com parede/telha/porta de verdade), agora com
            // textura de verdade em vez de cor lisa (ver ConvertImportedMaterials).
            UpgradeWithImportedModels(root);
            BercoWorldArt.Build(root);
            EspectroWorldExpansion.Build(root);
            CreatePostProcessing(root);
        }

        private static void ConfigureAtmosphere()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.48f, 0.61f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.47f, 0.51f, 0.43f);
            RenderSettings.ambientGroundColor = new Color(0.19f, 0.24f, 0.2f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.61f, 0.69f, 0.66f);
            RenderSettings.fogStartDistance = 38f;
            RenderSettings.fogEndDistance = 92f;

            var sun = GameObject.Find("Sol")?.GetComponent<Light>();
            if (sun != null)
            {
                sun.color = new Color(1f, 0.82f, 0.62f);
                sun.intensity = 1.25f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.72f;
                sun.transform.rotation = Quaternion.Euler(38f, -48f, 0f);
            }

        }

        private static void RecolorExistingWorld()
        {
            Recolor("Terreno do Berco", new Color(0.16f, 0.42f, 0.2f));
            Recolor("Praca", new Color(0.38f, 0.34f, 0.23f));
            Recolor("Estrada Norte", new Color(0.35f, 0.3f, 0.2f));
            Recolor("Estrada Mina", new Color(0.35f, 0.3f, 0.2f));
            Recolor("Montanha da Mina", new Color(0.48f, 0.29f, 0.19f));
            Recolor("Entrada Escura", new Color(0.12f, 0.14f, 0.16f));
            Recolor("Piso da Mina", new Color(0.22f, 0.24f, 0.25f));
        }

        private static void CreateRiver(Transform root)
        {
            var river = CreatePrimitive("Rio Azul", PrimitiveType.Cube, new Vector3(-12f, -0.05f, 3f), new Vector3(7f, 0.18f, 70f), new Color(0.04f, 0.38f, 0.55f), root);
            var material = river.GetComponent<Renderer>().material;
            material.SetFloat("_Smoothness", 0.88f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.01f, 0.12f, 0.2f));

            for (var i = -5; i <= 5; i++)
            {
                var stone = CreatePrimitive("Pedra da Margem", PrimitiveType.Sphere, new Vector3(-8.4f + (i % 2) * 0.4f, 0.28f, i * 5.4f), new Vector3(1.4f, 0.65f, 1.15f), new Color(0.32f, 0.35f, 0.31f), root);
                stone.transform.rotation = Quaternion.Euler(0f, i * 27f, 0f);
            }
        }

        private static void UpgradePlayer()
        {
            var player = GameObject.Find("Jogador");
            if (player == null || player.transform.Find("Acessorios Visuais") != null) return;
            var accessories = new GameObject("Acessorios Visuais").transform;
            accessories.SetParent(player.transform, false);
            var hair = CreatePrimitive("Cabelo", PrimitiveType.Sphere, player.transform.position + new Vector3(0f, 2.27f, -0.03f), new Vector3(0.6f, 0.36f, 0.62f), new Color(0.08f, 0.16f, 0.32f), accessories);
            hair.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            var shield = CreatePrimitive("Escudo", PrimitiveType.Cylinder, player.transform.position + new Vector3(-0.48f, 1.35f, -0.2f), new Vector3(0.58f, 0.12f, 0.58f), new Color(0.25f, 0.14f, 0.07f), accessories);
            shield.transform.localRotation = Quaternion.Euler(90f, 0f, 18f);
            var sword = CreatePrimitive("Espada", PrimitiveType.Cube, player.transform.position + new Vector3(0.55f, 1.18f, -0.2f), new Vector3(0.1f, 1.05f, 0.12f), new Color(0.62f, 0.68f, 0.7f), accessories);
            sword.transform.localRotation = Quaternion.Euler(0f, 0f, -22f);
            CreatePrimitive("Capa", PrimitiveType.Cube, player.transform.position + new Vector3(0f, 1.25f, -0.27f), new Vector3(0.72f, 1.05f, 0.08f), new Color(0.48f, 0.08f, 0.12f), accessories);
        }

        private static void CreateVillageIdentity(Transform root)
        {
            var architecture = new GameObject("Identidade da Vila").transform;
            architecture.SetParent(root, false);
            CreateFacadeAccents(architecture, new Vector3(-7f, 0f, 7f), 5.8f, new Color(0.24f, 0.12f, 0.055f));
            CreateFacadeAccents(architecture, new Vector3(0f, 0f, 12f), 7.2f, new Color(0.27f, 0.14f, 0.06f));
            CreateFacadeAccents(architecture, new Vector3(7f, 0f, 7f), 7.2f, new Color(0.22f, 0.11f, 0.05f));
            CreateFacadeAccents(architecture, new Vector3(-8f, 0f, -2f), 6.8f, new Color(0.18f, 0.09f, 0.04f));

            CreateBanner(architecture, new Vector3(-4.5f, 2.3f, 5.18f), new Color(0.12f, 0.34f, 0.48f));
            CreateBanner(architecture, new Vector3(4.6f, 2.35f, 4.98f), new Color(0.58f, 0.18f, 0.12f));
            CreateBanner(architecture, new Vector3(-1.9f, 2.65f, 8.02f), new Color(0.43f, 0.22f, 0.52f));
        }

        private static void CreateFacadeAccents(Transform parent, Vector3 center, float width, Color wood)
        {
            var frontZ = center.z - 2.05f;
            CreatePrimitive("Viga Horizontal", PrimitiveType.Cube, new Vector3(center.x, 2.45f, frontZ), new Vector3(width, 0.16f, 0.18f), wood, parent);
            CreatePrimitive("Viga Esquerda", PrimitiveType.Cube, new Vector3(center.x - width * 0.38f, 1.45f, frontZ - 0.02f), new Vector3(0.16f, 2.15f, 0.18f), wood, parent);
            CreatePrimitive("Viga Direita", PrimitiveType.Cube, new Vector3(center.x + width * 0.38f, 1.45f, frontZ - 0.02f), new Vector3(0.16f, 2.15f, 0.18f), wood, parent);

            var glow = new Color(1f, 0.55f, 0.16f);
            var leftWindow = CreatePrimitive("Janela Acesa", PrimitiveType.Cube, new Vector3(center.x - width * 0.23f, 1.55f, frontZ - 0.12f), new Vector3(0.72f, 0.78f, 0.08f), glow, parent);
            var rightWindow = CreatePrimitive("Janela Acesa", PrimitiveType.Cube, new Vector3(center.x + width * 0.23f, 1.55f, frontZ - 0.12f), new Vector3(0.72f, 0.78f, 0.08f), glow, parent);
            MakeEmissive(leftWindow, glow * 1.4f);
            MakeEmissive(rightWindow, glow * 1.4f);
        }

        private static void CreateBanner(Transform parent, Vector3 position, Color color)
        {
            var pole = CreatePrimitive("Mastro", PrimitiveType.Cylinder, position + Vector3.up * 0.25f, new Vector3(0.055f, 0.85f, 0.055f), new Color(0.2f, 0.11f, 0.05f), parent);
            var cloth = CreatePrimitive("Estandarte", PrimitiveType.Cube, position + new Vector3(0.34f, 0.38f, 0f), new Vector3(0.62f, 0.72f, 0.055f), color, parent);
            cloth.transform.rotation = Quaternion.Euler(0f, 0f, -4f);
            pole.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void CreatePathLanguage(Transform root)
        {
            var paths = new GameObject("Linguagem dos Caminhos").transform;
            paths.SetParent(root, false);
            var stoneA = new Color(0.46f, 0.42f, 0.32f);
            var stoneB = new Color(0.58f, 0.49f, 0.34f);

            for (var i = 0; i < 17; i++)
            {
                var side = i % 2 == 0 ? -1f : 1f;
                var stone = CreatePrimitive("Marco de Caminho", PrimitiveType.Cube,
                    new Vector3(side * (1.45f + (i % 3) * 0.12f), 0.09f, -1f + i * 1.9f),
                    new Vector3(0.65f, 0.12f, 0.85f), i % 3 == 0 ? stoneB : stoneA, paths);
                stone.transform.rotation = Quaternion.Euler(0f, i * 23f, 0f);
                stone.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            CreateSignpost(paths, new Vector3(3.8f, 0f, 1.2f), 18f);
            CreateSignpost(paths, new Vector3(8.5f, 0f, -3.4f), -34f);
        }

        private static void CreateSignpost(Transform parent, Vector3 position, float yaw)
        {
            var post = CreatePrimitive("Poste de Direcao", PrimitiveType.Cylinder, position + Vector3.up * 1.15f, new Vector3(0.11f, 1.2f, 0.11f), new Color(0.24f, 0.12f, 0.045f), parent);
            post.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            var board = CreatePrimitive("Placa de Direcao", PrimitiveType.Cube, position + Vector3.up * 1.9f, new Vector3(1.5f, 0.34f, 0.14f), new Color(0.38f, 0.2f, 0.075f), parent);
            board.transform.rotation = Quaternion.Euler(0f, yaw, -3f);
        }

        private static void CreateCrossroadsLandmark(Transform root)
        {
            var landmark = new GameObject("Marco dos Mil Caminhos").transform;
            landmark.SetParent(root, false);
            landmark.position = new Vector3(-3.8f, 0f, 3.8f);

            CreatePrimitive("Base de Pedra", PrimitiveType.Cylinder, landmark.position + Vector3.up * 0.18f, new Vector3(1.35f, 0.18f, 1.35f), new Color(0.29f, 0.3f, 0.27f), landmark);
            CreatePrimitive("Monolito", PrimitiveType.Cube, landmark.position + Vector3.up * 1.25f, new Vector3(0.62f, 2.25f, 0.62f), new Color(0.2f, 0.23f, 0.25f), landmark).transform.rotation = Quaternion.Euler(0f, 45f, 0f);

            var spectrum = new[]
            {
                new Color(0.12f, 0.58f, 0.72f),
                new Color(0.48f, 0.28f, 0.72f),
                new Color(0.86f, 0.38f, 0.16f),
                new Color(0.34f, 0.7f, 0.3f)
            };
            for (var i = 0; i < spectrum.Length; i++)
            {
                var angle = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                var shardPosition = landmark.position + new Vector3(Mathf.Cos(angle) * 0.68f, 1.55f + (i % 2) * 0.22f, Mathf.Sin(angle) * 0.68f);
                var shard = CreatePrimitive("Fragmento do Espectro", PrimitiveType.Cube, shardPosition, new Vector3(0.2f, 0.68f, 0.2f), spectrum[i], landmark);
                shard.transform.rotation = Quaternion.Euler(18f, -i * 38f, 28f);
                MakeEmissive(shard, spectrum[i] * 1.25f);
            }
        }

        private static void MakeEmissive(GameObject item, Color emission)
        {
            var renderer = item.GetComponent<Renderer>();
            var material = new Material(renderer.sharedMaterial);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
            renderer.sharedMaterial = material;
        }

        private static void UpgradeWithImportedModels(Transform root)
        {
            var imported = new GameObject("Modelos Organicos Importados").transform;
            imported.SetParent(root, false);

            // sizeMultiplier < 1: escalar pra bater com a altura da primitiva antiga (~5,7 m,
            // valor arbitrário do protótipo) deixava galho/tronco proporcionalmente grossos
            // demais — a árvore real do Quaternius não tem essa proporção. Reduzido até ficar
            // com aparência de árvore, não de escultura gigante.
            var treeModels = new[] { "NormalTree_1", "NormalTree_2", "NormalTree_3", "PineTree_1" };
            for (var i = 1; i <= 8; i++)
                ReplacePrimitiveVisual("Arvore " + i, "Nature/" + treeModels[(i - 1) % treeModels.Length], imported, 0.5f);

            for (var i = 1; i <= 8; i++)
                ReplacePrimitiveVisual("Rocha " + i, "Nature/Rock_" + (((i - 1) % 5) + 1), imported, 1.08f);

            RebuildVillage(imported);

            // Vector3.one aqui seria escala literal sobre o mesh cru (~0,01-0,08 unidade, ver nota
            // em SpawnNormalized) — deixava esses props praticamente invisíveis. Normalizados por
            // altura real como o resto do pacote Nature/Village.
            SpawnNormalized("Village/Prop_Wagon", new Vector3(5.1f, 0.02f, 0.8f), Quaternion.Euler(0f, -28f, 0f), 1.2f, imported);
            SpawnNormalized("Village/Prop_Crate", new Vector3(-5.3f, 0.02f, -1.1f), Quaternion.Euler(0f, 18f, 0f), 0.55f, imported);
            SpawnNormalized("Village/Prop_Crate", new Vector3(-4.65f, 0.02f, -1.35f), Quaternion.Euler(0f, -12f, 0f), 0.43f, imported);

            for (var i = 0; i < 5; i++)
                SpawnNormalized("Village/Prop_WoodenFence_Single", new Vector3(-10.5f + i * 1.9f, 0.02f, 15.8f), Quaternion.identity, 1f, imported);

            SpawnNormalized("Village/Prop_Vine1", new Vector3(-9.1f, 1.1f, 4.95f), Quaternion.Euler(0f, 0f, 0f), 1.7f, imported);
            SpawnNormalized("Village/Prop_Vine2", new Vector3(6.1f, 1.15f, 4.92f), Quaternion.Euler(0f, 0f, 0f), 1.5f, imported);
        }

        private static void RebuildVillage(Transform parent)
        {
            RebuildHouse("Casa Oeste", parent, new Color(0.78f, 0.62f, 0.4f));
            RebuildHouse("Casa do Conselho", parent, new Color(0.82f, 0.68f, 0.46f));
            RebuildHouse("Estalagem", parent, new Color(0.72f, 0.5f, 0.31f));
            RebuildHouse("Forja", parent, new Color(0.62f, 0.43f, 0.29f));
        }

        private static void RebuildHouse(string objectName, Transform parent, Color tint)
        {
            var original = GameObject.Find(objectName);
            if (original == null) return;
            var originalRenderers = original.GetComponentsInChildren<Renderer>();
            if (originalRenderers.Length == 0) return;

            var bounds = originalRenderers[0].bounds;
            for (var i = 1; i < originalRenderers.Length; i++) bounds.Encapsulate(originalRenderers[i].bounds);
            foreach (var renderer in originalRenderers) renderer.enabled = false;

            var house = new GameObject(objectName + " Visual Modular").transform;
            house.SetParent(parent, false);
            var width = Mathf.Max(4.5f, bounds.size.x);
            var depth = Mathf.Max(4.5f, bounds.size.z);
            var baseY = bounds.min.y;
            var wallHeight = Mathf.Clamp(bounds.size.y * 0.67f, 2.4f, 3.2f);
            var wallThickness = 0.28f;
            var center = new Vector3(bounds.center.x, baseY + wallHeight * 0.5f, bounds.center.z);

            // O kit modular possui aberturas e, em alguns ângulos, deixa enxergar o gramado
            // através da casa inteira. Um casco escuro logo atrás das peças dá espessura aos
            // vãos e garante uma silhueta fechada, sem mudar colisores ou permitir entrada.
            var innerWall = new Color(tint.r * 0.48f, tint.g * 0.45f, tint.b * 0.42f);
            var stone = new Color(0.42f, 0.43f, 0.38f);
            CreatePrimitive("Parede interna frente", PrimitiveType.Cube,
                new Vector3(center.x, center.y, bounds.min.z + 0.10f),
                new Vector3(width - 0.20f, wallHeight - 0.12f, 0.24f), innerWall, house, true);
            CreatePrimitive("Parede interna fundos", PrimitiveType.Cube,
                new Vector3(center.x, center.y, bounds.max.z - 0.10f),
                new Vector3(width - 0.20f, wallHeight - 0.12f, 0.24f), innerWall, house, true);
            CreatePrimitive("Parede interna esquerda", PrimitiveType.Cube,
                new Vector3(bounds.min.x + 0.10f, center.y, center.z),
                new Vector3(0.24f, wallHeight - 0.12f, depth - 0.20f), innerWall, house, true);
            CreatePrimitive("Parede interna direita", PrimitiveType.Cube,
                new Vector3(bounds.max.x - 0.10f, center.y, center.z),
                new Vector3(0.24f, wallHeight - 0.12f, depth - 0.20f), innerWall, house, true);
            CreatePrimitive("Teto interno", PrimitiveType.Cube,
                new Vector3(center.x, baseY + wallHeight - 0.05f, center.z),
                new Vector3(width - 0.18f, 0.16f, depth - 0.18f), innerWall, house);

            // Rodapé contínuo ancora a casa no chão e oculta frestas causadas pelos bounds
            // irregulares dos módulos importados.
            CreatePrimitive("Fundação frente", PrimitiveType.Cube,
                new Vector3(center.x, baseY + 0.20f, bounds.min.z - 0.08f),
                new Vector3(width + 0.16f, 0.40f, 0.34f), stone, house);
            CreatePrimitive("Fundação fundos", PrimitiveType.Cube,
                new Vector3(center.x, baseY + 0.20f, bounds.max.z + 0.08f),
                new Vector3(width + 0.16f, 0.40f, 0.34f), stone, house);
            CreatePrimitive("Fundação esquerda", PrimitiveType.Cube,
                new Vector3(bounds.min.x - 0.08f, baseY + 0.20f, center.z),
                new Vector3(0.34f, 0.40f, depth), stone, house);
            CreatePrimitive("Fundação direita", PrimitiveType.Cube,
                new Vector3(bounds.max.x + 0.08f, baseY + 0.20f, center.z),
                new Vector3(0.34f, 0.40f, depth), stone, house);

            SpawnFitted("Village/Wall_Plaster_Door_Round", new Bounds(
                new Vector3(center.x, center.y, bounds.min.z - 0.03f),
                new Vector3(width, wallHeight, wallThickness)), Quaternion.identity, house, tint);
            SpawnFitted("Village/Wall_Plaster_Straight", new Bounds(
                new Vector3(center.x, center.y, bounds.max.z + 0.03f),
                new Vector3(width, wallHeight, wallThickness)), Quaternion.Euler(0f, 180f, 0f), house, tint);
            SpawnFitted("Village/Wall_Plaster_Window_Wide_Round", new Bounds(
                new Vector3(bounds.min.x - 0.03f, center.y, center.z),
                new Vector3(wallThickness, wallHeight, depth)), Quaternion.Euler(0f, 90f, 0f), house, tint);
            SpawnFitted("Village/Wall_Plaster_Window_Wide_Round", new Bounds(
                new Vector3(bounds.max.x + 0.03f, center.y, center.z),
                new Vector3(wallThickness, wallHeight, depth)), Quaternion.Euler(0f, -90f, 0f), house, tint);

            SpawnFitted("Village/Floor_UnevenBrick", new Bounds(
                new Vector3(center.x, baseY + 0.06f, center.z),
                new Vector3(width * 0.98f, 0.12f, depth * 0.98f)), Quaternion.identity, house, Color.white);
            SpawnFitted(width > 7f ? "Village/Roof_RoundTiles_6x8" : "Village/Roof_RoundTiles_4x6", new Bounds(
                new Vector3(center.x, baseY + wallHeight + 0.8f, center.z),
                new Vector3(width * 1.16f, 1.65f, depth * 1.18f)), Quaternion.identity, house, Color.white);

            SpawnFitted("Village/Door_1_Round", new Bounds(
                new Vector3(center.x, baseY + 1.05f, bounds.min.z - 0.18f),
                new Vector3(1.05f, 2.1f, 0.16f)), Quaternion.identity, house, Color.white);
            SpawnFitted("Village/Prop_Chimney", new Bounds(
                new Vector3(center.x + width * 0.27f, baseY + wallHeight + 1.15f, center.z + depth * 0.12f),
                new Vector3(0.7f, 2.3f, 0.7f)), Quaternion.identity, house, Color.white);

            // Cantos com acabamento em pedra/tijolo (peça extra do mesmo pacote, só disponível em
            // OBJ — VillageOBJ/) quebram a repetição visual das quatro paredes lisas.
            var cornerSize = new Vector3(wallThickness * 1.4f, wallHeight, wallThickness * 1.4f);
            foreach (var cornerX in new[] { bounds.min.x, bounds.max.x })
                foreach (var cornerZ in new[] { bounds.min.z, bounds.max.z })
                    SpawnFitted("VillageOBJ/Corner_ExteriorWide_Brick", new Bounds(
                        new Vector3(cornerX, center.y, cornerZ), cornerSize), Quaternion.identity, house, Color.white);
        }

        private static void CreateImportedVegetation(Transform parent)
        {
            var random = new System.Random(7301);
            for (var i = 0; i < 52; i++)
            {
                var x = Mathf.Lerp(-26f, 26f, (float)random.NextDouble());
                var z = Mathf.Lerp(-20f, 30f, (float)random.NextDouble());
                if (Mathf.Abs(x) < 6.5f && z > -2f && z < 19f) continue;
                if (x < -8f && x > -16f) continue;
                var model = i % 7 == 0 ? "Nature/Bush" : "Nature/Grass_Large_Extruded";
                var variance = i % 7 == 0 ? 0.7f + (float)random.NextDouble() * 0.45f : 0.42f + (float)random.NextDouble() * 0.32f;
                var baseHeight = i % 7 == 0 ? 0.85f : 0.4f;
                var item = SpawnNormalized(model, new Vector3(x, 0.02f, z), Quaternion.Euler(0f, random.Next(0, 360), 0f), baseHeight * variance, parent);
                if (item == null) continue;
                foreach (var renderer in item.GetComponentsInChildren<Renderer>())
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = i % 7 == 0;
                }
            }
        }

        private static void CreateOrganicHorizon(Transform parent)
        {
            // "Terreno do Berco" (Corte0ProjectSetup.CreateEnvironment) e um Plane de escala 8 ->
            // vai de -40 a 40 em X/Z. Essas rochas estavam em z=43~46, alem da borda do chao —
            // pareciam flutuar porque nao havia nenhum terreno visivel embaixo delas. Movidas pra
            // dentro do limite (z=33~36), ainda no fundo do cenario, mas apoiadas no chao de verdade.
            for (var i = 0; i < 13; i++)
            {
                var x = -34f + i * 5.7f;
                var model = "Nature/Rock_" + ((i % 5) + 1);
                SpawnFitted(model, new Bounds(
                    new Vector3(x, 4.2f + (i % 3) * 1.1f, 33f + (i % 2) * 3f),
                    new Vector3(7.2f, 9f + (i % 3) * 2.2f, 6.5f)),
                    Quaternion.Euler(0f, i * 29f, 0f), parent, new Color(0.68f, 0.38f, 0.22f));
            }
        }

        private static GameObject SpawnFitted(string resourceName, Bounds desiredBounds, Quaternion rotation, Transform parent, Color tint)
        {
            var instance = SpawnImported(resourceName, Vector3.zero, rotation, Vector3.one, parent);
            if (instance == null) return null;
            var current = CalculateBounds(instance);
            if (current.size.x < 0.001f || current.size.y < 0.001f || current.size.z < 0.001f) return instance;

            var dx = desiredBounds.size.x / current.size.x;
            var dy = desiredBounds.size.y / current.size.y;
            var dz = desiredBounds.size.z / current.size.z;
            // A correção de eixo em SpawnImported (-90° em X, só pros pacotes Nature/Village) faz
            // o eixo local Z virar o eixo de altura no mundo, não mais o Y — e isso vale pra
            // QUALQUER yaw (rotação em Y não mexe em quem já ficou alinhado ao Y), então a altura
            // sempre vai pro canal Z local. Mas os dois eixos horizontais (X/Z do mundo) ainda
            // trocam de canal local conforme o yaw for 0°/180° ou 90°/270° — exatamente a mesma
            // troca que já existia antes da correção (só que agora entre X/Y locais, não X/Z).
            // Sem separar os dois casos, a parede lateral (90°/270°) recebia a proporção de
            // largura no canal errado e ficava esmagada de um lado e esticada de 22 m do outro.
            var needsAxisFix = resourceName.StartsWith("Nature/") || resourceName.StartsWith("Village/");
            var yawNearSide = needsAxisFix &&
                (Mathf.Abs(Mathf.DeltaAngle(rotation.eulerAngles.y, 90f)) < 45f ||
                 Mathf.Abs(Mathf.DeltaAngle(rotation.eulerAngles.y, 270f)) < 45f);
            instance.transform.localScale = !needsAxisFix ? new Vector3(dx, dy, dz)
                : yawNearSide ? new Vector3(dz, dx, dy)
                : new Vector3(dx, dz, dy);
            current = CalculateBounds(instance);
            instance.transform.position += desiredBounds.center - current.center;
            ApplyTint(instance, tint);
            return instance;
        }

        // Multiplica a cor do material por cima do que a peça já tem (textura real ou fallback).
        // Usado tanto para variar o tom de cada casa quanto para forçar um tom natural nas
        // peças de vegetação cujo material de folha não resolve textura (ver nota em
        // ReplacePrimitiveVisual) — sem isso, ficam brancas/quebradas em vez de verdes.
        private static void ApplyTint(GameObject instance, Color tint)
        {
            if (tint == Color.white) return;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                foreach (var material in renderer.materials) material.color *= tint;
        }

        private static void ReplacePrimitiveVisual(string targetName, string resourceName, Transform parent, float sizeMultiplier, Color? tint = null)
        {
            var target = GameObject.Find(targetName);
            if (target == null) return;
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            var targetBounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) targetBounds.Encapsulate(renderers[i].bounds);
            foreach (var renderer in renderers) renderer.enabled = false;

            var instance = SpawnImported(resourceName, Vector3.zero, Quaternion.Euler(0f, StableAngle(targetName), 0f), Vector3.one, parent);
            if (instance == null)
            {
                foreach (var renderer in renderers) renderer.enabled = true;
                return;
            }

            var importedBounds = CalculateBounds(instance);
            if (importedBounds.size.y < 0.01f) return;
            var uniformScale = targetBounds.size.y / importedBounds.size.y * sizeMultiplier;
            instance.transform.localScale = Vector3.one * uniformScale;
            importedBounds = CalculateBounds(instance);
            instance.transform.position += new Vector3(
                targetBounds.center.x - importedBounds.center.x,
                targetBounds.min.y - importedBounds.min.y,
                targetBounds.center.z - importedBounds.center.z);
            if (tint.HasValue) ApplyTint(instance, tint.Value);
        }

        // Igual ao ajuste de altura usado pelas árvores (ReplacePrimitiveVisual), mas sem exigir
        // uma primitiva antiga como referência: escala pra uma altura real em metros. Necessário
        // porque o mesh cru do pacote Nature é minúsculo (~0,01-0,08 unidade) — um scale fixo
        // (ex.: 0.7-1.15) aplicado direto deixava arbusto/grama praticamente invisíveis.
        internal static GameObject SpawnNormalized(string resourceName, Vector3 groundPosition, Quaternion rotation, float targetHeight, Transform parent)
        {
            var instance = SpawnImported(resourceName, groundPosition, rotation, Vector3.one, parent);
            if (instance == null) return null;
            var bounds = CalculateBounds(instance);
            if (bounds.size.y < 0.0001f) return instance;
            instance.transform.localScale = Vector3.one * (targetHeight / bounds.size.y);
            bounds = CalculateBounds(instance);
            instance.transform.position += new Vector3(0f, groundPosition.y - bounds.min.y, 0f);
            return instance;
        }

        private static GameObject SpawnImported(string resourceName, Vector3 position, Quaternion rotation, Vector3 scale, Transform parent)
        {
            var prefab = Resources.Load<GameObject>("EspectroModels/" + resourceName);
            if (prefab == null) return null;
            // Os pacotes Nature/ e Village/ (FBX) foram exportados com o "comprimento" do modelo
            // no eixo Z em vez do Y (confirmado medindo os bounds locais crus: a maior dimensão
            // cai em Z, não em Y, tanto pra árvore quanto pra parede). O pacote VillageOBJ/ (OBJ)
            // não tem esse problema — importa já correto. Corrigido girando -90° em X antes de
            // qualquer outra rotação, o que leva o "comprimento" de Z pra cima (Y). Sem isso: árvore
            // deitada no chão, e paredes/telhados com vigas diagonais distorcidas pelo ajuste não
            // uniforme de escala tentando encaixar o eixo errado na altura-alvo.
            var needsAxisFix = resourceName.StartsWith("Nature/") || resourceName.StartsWith("Village/");
            var correctedRotation = needsAxisFix ? rotation * Quaternion.Euler(-90f, 0f, 0f) : rotation;
            var instance = Object.Instantiate(prefab, position, correctedRotation, parent);
            instance.name = prefab.name;
            instance.transform.localScale = scale;
            ConvertImportedMaterials(instance);
            foreach (var collider in instance.GetComponentsInChildren<Collider>()) Object.Destroy(collider);
            return instance;
        }

        private static void ConvertImportedMaterials(GameObject instance)
        {
            var shader = Shader.Find("Espectro/ToonLit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                var sourceMaterials = renderer.sharedMaterials;
                var converted = new Material[sourceMaterials.Length];
                for (var i = 0; i < sourceMaterials.Length; i++)
                {
                    var source = sourceMaterials[i];
                    if (source == null) continue;

                    // Prioridade: 1) textura que o próprio FBX já resolveu (Village resolve certo
                    // por submesh); 2) override por nome de material, só quando o FBX não achou
                    // nada (Nature inteiro, mais alguns materiais isolados da Village); 3) cor fixa.
                    var nativeTexture = source.mainTexture as Texture2D;
                    string textureName = null;
                    if (nativeTexture == null && MaterialTextureOverrides.TryGetValue(source.name, out var byMaterial))
                        textureName = byMaterial;

                    var cacheKey = textureName != null ? "override:" + textureName
                        : nativeTexture != null ? "native:" + nativeTexture.GetEntityId()
                        : "flat:" + source.name;
                    if (!ImportedMaterials.TryGetValue(cacheKey, out var material))
                    {
                        material = new Material(shader) { name = (textureName ?? source.name) + " URP", enableInstancing = true };
                        if (textureName != null)
                        {
                            var texture = Resources.Load<Texture2D>("EspectroModels/Textures/" + textureName);
                            if (texture != null) material.mainTexture = texture;
                            if (IsFoliageTexture(textureName)) ConfigureAlphaClip(material);
                        }
                        else if (nativeTexture != null)
                        {
                            var nativeFoliage = IsFoliageTexture(nativeTexture.name) || IsFoliageTexture(source.name);
                            material.color = nativeFoliage
                                ? new Color(0.66f, 0.78f, 0.48f, 1f)
                                : Color.white;
                            material.mainTexture = nativeTexture;
                            if (nativeFoliage) ConfigureAlphaClip(material);
                        }
                        else
                        {
                            material.color = MaterialColorOverrides.TryGetValue(source.name, out var flatColor) ? flatColor
                                : source.HasProperty("_Color") ? source.color : Color.white;
                        }
                        ImportedMaterials[cacheKey] = material;
                    }
                    converted[i] = material;
                }
                renderer.sharedMaterials = converted;
            }
        }

        private static bool IsFoliageTexture(string textureName) =>
            !string.IsNullOrEmpty(textureName) &&
            (textureName.Contains("Leaves") || textureName.Contains("Leaf") ||
             textureName.Contains("Grass") || textureName.Contains("Bush") || textureName.Contains("Vine"));

        // Folhagem do Quaternius é modelada como cartões planos com formato de folha recortado
        // pelo alpha da textura (fundo branco = alpha 0). Sem isso, o cartão inteiro aparece como
        // um painel sólido — era a "teia" que aparecia no lugar da copa da árvore.
        private static void ConfigureAlphaClip(Material material)
        {
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            if (material.HasProperty("_OutlineWidth")) material.SetFloat("_OutlineWidth", 0f);
            if (material.HasProperty("_RimStrength")) material.SetFloat("_RimStrength", 0.06f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.AlphaTest;
        }

        private static Bounds CalculateBounds(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(target.transform.position, Vector3.zero);
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static float StableAngle(string value)
        {
            unchecked
            {
                var hash = 17;
                foreach (var character in value) hash = hash * 31 + character;
                return Mathf.Abs(hash % 360);
            }
        }

        private static void CreatePostProcessing(Transform root)
        {
            var volumeObject = new GameObject("Pos Processamento Espectro");
            volumeObject.transform.SetParent(root);
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 20f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.profile = profile;
            var color = profile.Add<ColorAdjustments>();
            color.active = true;
            color.contrast.Override(7f);
            color.saturation.Override(5f);
            color.postExposure.Override(0.04f);
            var bloom = profile.Add<Bloom>();
            bloom.active = true;
            bloom.intensity.Override(0.28f);
            bloom.threshold.Override(1.05f);
            var vignette = profile.Add<Vignette>();
            vignette.active = true;
            vignette.intensity.Override(0.14f);
            vignette.smoothness.Override(0.38f);

            if (Camera.main != null && Camera.main.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData))
                cameraData.renderPostProcessing = true;
        }

        private static GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color, Transform parent, bool keepCollider = false)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent);
            item.transform.position = position;
            item.transform.localScale = scale;
            var collider = item.GetComponent<Collider>();
            if (collider != null && !keepCollider) Object.Destroy(collider);
            item.GetComponent<Renderer>().sharedMaterial = GetMaterial(color);
            return item;
        }

        private static Material GetMaterial(Color color)
        {
            var key = ColorUtility.ToHtmlStringRGBA(color).GetHashCode();
            if (Materials.TryGetValue(key, out var existing)) return existing;
            var shader = Shader.Find("Espectro/ToonLit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color, enableInstancing = true };
            Materials[key] = material;
            return material;
        }

        private static void Recolor(string objectName, Color color)
        {
            var target = GameObject.Find(objectName);
            if (target == null) return;
            foreach (var renderer in target.GetComponentsInChildren<Renderer>()) renderer.material.color = color;
        }
    }
}
