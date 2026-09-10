using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Espectro.Prototype
{
    // Art-only composition, built after the imported architecture. Positions of gameplay
    // objects remain authoritative elsewhere. Decorative meshes are grouped by material
    // and 12 m cell so detail does not require one renderer per plank, leaf or stone.
    public sealed class BercoWorldArt : MonoBehaviour
    {
        private sealed class Batch
        {
            public Material material;
            public bool shadows;
            public readonly List<CombineInstance> parts = new();
        }

        private readonly Dictionary<string, Batch> batches = new();
        private readonly Dictionary<string, Material> palette = new();
        private readonly List<Mesh> ownedMeshes = new();
        private readonly List<Material> ownedMaterials = new();
        private readonly List<Bounds> houses = new();
        private readonly List<Vector3[]> routes = new();
        private Mesh cube;
        private Mesh sphere;
        private Mesh cylinder;
        private int detailCount;

        public static void Build(Transform parent)
        {
            if (parent.Find("O Berco - Arte Ambiental") != null) return;
            var root = new GameObject("O Berco - Arte Ambiental");
            root.transform.SetParent(parent, false);
            root.AddComponent<BercoWorldArt>().Construct();
        }

        private void Construct()
        {
            cube = BorrowMesh(PrimitiveType.Cube);
            sphere = BorrowMesh(PrimitiveType.Sphere);
            cylinder = BorrowMesh(PrimitiveType.Cylinder);
            RegisterPalette();
            RememberHouses();
            BuildRoutes();
            BuildRiver();
            BuildVillage();
            BuildMine();
            BuildForest();
            BuildHorizon();
            FlushBatches();
            gameObject.AddComponent<MineInteriorVisibility>().Initialize();
            Debug.Log($"[BercoWorldArt] {detailCount} detalhes agrupados em {batches.Count} lotes; composição determinística.");
        }

        private Mesh BorrowMesh(PrimitiveType type)
        {
            var item = GameObject.CreatePrimitive(type);
            item.SetActive(false);
            var mesh = item.GetComponent<MeshFilter>().sharedMesh;
            Destroy(item);
            return mesh;
        }

        private void RegisterPalette()
        {
            Mat("madeira", new Color(0.23f, 0.15f, 0.10f));
            Mat("tabua", new Color(0.42f, 0.29f, 0.17f));
            Mat("tabua clara", new Color(0.51f, 0.37f, 0.23f));
            Mat("pedra", new Color(0.43f, 0.46f, 0.41f));
            Mat("pedra quente", new Color(0.57f, 0.52f, 0.40f));
            Mat("pedra escura", new Color(0.25f, 0.30f, 0.31f));
            Mat("terra", new Color(0.43f, 0.35f, 0.23f));
            Mat("borda", new Color(0.32f, 0.33f, 0.21f));
            Mat("areia", new Color(0.51f, 0.48f, 0.33f));
            Mat("agua rasa", new Color(0.17f, 0.43f, 0.43f));
            Mat("agua funda", new Color(0.075f, 0.28f, 0.34f));
            Mat("reflexo", new Color(0.45f, 0.68f, 0.65f), true);
            Mat("folha", new Color(0.25f, 0.39f, 0.20f));
            Mat("junco", new Color(0.44f, 0.48f, 0.24f));
            Mat("musgo", new Color(0.28f, 0.36f, 0.20f));
            Mat("tecido azul", new Color(0.16f, 0.35f, 0.39f));
            Mat("tecido cru", new Color(0.78f, 0.69f, 0.49f));
            Mat("tecido vinho", new Color(0.43f, 0.24f, 0.22f));
            Mat("ferro", new Color(0.19f, 0.23f, 0.25f));
            Mat("cobre", new Color(0.61f, 0.34f, 0.18f));
            Mat("ceramica", new Color(0.56f, 0.32f, 0.22f));
            Mat("papel", new Color(0.78f, 0.72f, 0.55f));
            Mat("ambar", new Color(1f, 0.68f, 0.28f), true);
            Mat("brasa", new Color(1f, 0.34f, 0.10f), true);
            Mat("espectral", new Color(0.20f, 0.68f, 0.72f), true);
        }

        private Material Mat(string name, Color color, bool unlit = false)
        {
            var shader = unlit ? Shader.Find("Universal Render Pipeline/Unlit") : Shader.Find("Espectro/ToonLit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "Berco - " + name, color = color, enableInstancing = true };
            if (material.HasProperty("_OutlineWidth")) material.SetFloat("_OutlineWidth", 0.0025f);
            if (material.HasProperty("_Bands")) material.SetFloat("_Bands", 4f);
            palette[name] = material;
            ownedMaterials.Add(material);
            return material;
        }

        private void RememberHouses()
        {
            foreach (var name in new[] { "Casa Oeste", "Casa do Conselho", "Estalagem", "Forja" })
            {
                var original = GameObject.Find(name);
                if (original == null) continue;
                var renderers = original.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                houses.Add(bounds);
            }
        }

        private void BuildRoutes()
        {
            HideRenderer("Estrada Norte");
            HideRenderer("Estrada Mina");
            AddRoute("Caminho da Floresta", new[] {
                P(1f, 3f), P(3f, 7f), P(4.6f, 11f), P(5.8f, 16f), P(10f, 21f), P(15f, 26f)
            }, 2.7f);
            AddRoute("Caminho da Mina", new[] {
                P(0f, 0f), P(4f, -4f), P(10f, -5.1f), P(15.5f, -6.8f), P(18f, -11.7f), P(22f, -12f), P(22f, -10.3f)
            }, 2.5f);
            AddRoute("Caminho do Rio", new[] {
                P(-1f, 0f), P(-4.5f, -4.7f), P(-8.5f, -6f), P(-13f, -6f), P(-18f, -6f), P(-22f, 0f), P(-23f, 9f)
            }, 2.1f);

            // A broken ring and four branching inlays echo the landmark, leaving the
            // spawn and Mira's approach open. No solid object occupies these approaches.
            var center = new Vector3(-3.8f, 0.18f, 3.8f);
            for (int i = 0; i < 26; i++)
            {
                if (i % 7 == 0) continue;
                float angle = i * 360f / 26f;
                var direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Box("pedra quente", center + direction * 1.35f, new Vector3(0.26f, 0.06f, 0.38f), angle, false);
            }
            for (int arm = 0; arm < 4; arm++)
                for (int stone = 0; stone < 4; stone++)
                {
                    var direction = Quaternion.Euler(0f, arm * 90f + 45f, 0f) * Vector3.forward;
                    Box("pedra", center + direction * (1.7f + stone * 0.32f), new Vector3(0.12f, 0.025f, 0.27f), arm * 90f + 45f, false);
                }
            Sign(new Vector3(4.9f, 0f, -1.8f), "FLORESTA", "MINA", 0f);
            Sign(new Vector3(-18f, 0f, -7.8f), "O BERÇO", "TRILHA DO RIO", -24f);
        }

        private static Vector3 P(float x, float z) => new Vector3(x, 0f, z);

        private void AddRoute(string name, Vector3[] points, float width)
        {
            routes.Add(points);
            Ribbon(name + " - margem", points, width + 0.65f, "borda", 0.013f, true);
            Ribbon(name, points, width, "terra", 0.024f, true);
            var samples = Smooth(points, 6);
            for (int i = 2; i < samples.Count - 1; i += 3)
            {
                var p = samples[i];
                var tangent = (samples[i + 1] - samples[i - 1]).normalized;
                var side = new Vector3(tangent.z, 0f, -tangent.x) * (i % 2 == 0 ? 1f : -1f);
                p += side * (width * 0.52f + 0.15f);
                p.y = Ground(p.x, p.z) + 0.07f;
                Lump("pedra quente", p, new Vector3(0.3f, 0.13f, 0.23f), i * 31f, false);
            }
        }

        private static float RiverX(float z) => -13.4f + Mathf.Sin(z * 0.12f) * 0.8f;

        private void BuildRiver()
        {
            var points = new Vector3[14];
            for (int i = 0; i < points.Length; i++)
            {
                float z = -29f + i * 4.7f;
                points[i] = P(RiverX(z), z);
            }
            Ribbon("Leito e margens", points, 6.1f, "areia", 0.015f, false);
            Ribbon("Agua rasa", points, 4.9f, "agua rasa", 0.032f, false);
            Ribbon("Canal profundo", points, 3.25f, "agua funda", 0.038f, false);
            for (int i = 0; i < 28; i++)
            {
                float z = -27f + i * 2.12f;
                if (Mathf.Abs(z + 6f) < 2f) continue;
                float x = RiverX(z);
                Box("reflexo", new Vector3(x + Mathf.Sin(i * 2f), 0.044f, z),
                    new Vector3(0.45f + (i % 4) * 0.17f, 0.004f, 0.035f), i % 3 * 8f, false);
                for (int side = -1; side <= 1; side += 2)
                {
                    float edge = x + side * (2.65f + (i % 3) * 0.12f);
                    if (i % 3 == 0) Lump("pedra", new Vector3(edge, 0.11f, z), new Vector3(0.7f, 0.34f, 0.5f), i * 43f, false);
                    for (int reed = 0; reed < 3; reed++)
                    {
                        float height = 0.45f + ((i + reed) % 4) * 0.11f;
                        Part(cube, palette["junco"], new Vector3(edge + reed * 0.12f, height * 0.5f, z + reed * 0.16f),
                            new Vector3(0.038f, height, 0.07f), Quaternion.Euler(7f, i * 43f, side * 12f), false);
                    }
                }
            }
            float bridgeX = RiverX(-6f);
            for (int i = 0; i < 15; i++)
                Box(i % 3 == 0 ? "tabua clara" : "tabua", new Vector3(bridgeX - 3.5f + i * 0.5f, 0.18f, -6f),
                    new Vector3(0.47f, 0.16f, 2.45f), 0f);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("madeira", new Vector3(bridgeX, 0.83f, -6f + side * 1.18f), new Vector3(7.5f, 0.12f, 0.12f), 0f);
                for (int i = 0; i < 4; i++)
                    Box("madeira", new Vector3(bridgeX - 3.5f + i * 2.33f, 0.5f, -6f + side * 1.18f), new Vector3(0.16f, 1f, 0.16f), 0f);
            }
            var bridge = new GameObject("Piso caminhavel da ponte");
            bridge.transform.SetParent(transform, false);
            bridge.transform.position = new Vector3(bridgeX, 0.14f, -6f);
            bridge.AddComponent<BoxCollider>().size = new Vector3(7.5f, 0.20f, 2.45f);
        }

        private void BuildVillage()
        {
            string[] names = { "Casa Oeste", "Casa do Conselho", "Estalagem", "Forja" };
            foreach (var name in names)
            {
                var original = GameObject.Find(name);
                if (original == null) continue;
                var renderers = original.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                float front = bounds.min.z - 0.24f;
                float width = Mathf.Max(4.5f, bounds.size.x);
                float height = Mathf.Clamp(bounds.size.y * 0.67f, 2.4f, 3.2f);
                var center = bounds.center;
                // All facade attachments derive from the same bounds used by RebuildHouse.
                Box("madeira", new Vector3(center.x, height - 0.22f, front), new Vector3(width, 0.15f, 0.13f), 0f);
                Box("madeira", new Vector3(center.x, 0.72f, front - 0.015f), new Vector3(width * 0.92f, 0.12f, 0.14f), 0f);
                Box("madeira", new Vector3(center.x, 1.72f, front - 0.018f), new Vector3(width * 0.92f, 0.10f, 0.14f), 0f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("madeira", new Vector3(center.x + side * width * 0.43f, height * 0.5f, front),
                        new Vector3(0.14f, height, 0.15f), 0f);
                    Lantern(new Vector3(center.x + side * 0.9f, 1.8f, front - 0.24f));
                }
                // Soleira e pedras irregulares eliminam a sensação de fachada flutuando.
                Box("pedra quente", new Vector3(center.x, 0.10f, front - 0.42f), new Vector3(1.45f, 0.18f, 0.62f), 0f);
                for (int paver = 0; paver < 4; paver++)
                    Lump("pedra", new Vector3(center.x - 0.58f + paver * 0.39f, 0.07f, front - 0.82f - (paver % 2) * 0.16f),
                        new Vector3(0.32f, 0.10f, 0.28f), paver * 31f, false);
                if (name == "Estalagem")
                {
                    Awning(new Vector3(center.x, height - 0.08f, front), width * 0.72f, 1.4f, "tecido azul");
                    TextSign(new Vector3(center.x, height + 0.12f, front - 0.15f), "ESTALAGEM", width * 0.63f);
                    Table(new Vector3(center.x + width * 0.48f + 0.8f, 0f, front - 1.1f));
                    Barrel(new Vector3(center.x - width * 0.5f - 0.35f, 0f, front), 0.8f);
                }
                else if (name == "Forja")
                {
                    Awning(new Vector3(center.x, height - 0.03f, front), width * 0.7f, 1.2f, "tecido vinho");
                    TextSign(new Vector3(center.x, height + 0.12f, front - 0.13f), "FORJA", 1.8f);
                    Workbench(new Vector3(center.x + 1.15f, 0f, front - 0.7f));
                    Barrel(new Vector3(center.x - 1.7f, 0f, front - 0.55f), 0.62f);
                }
                else if (name == "Casa do Conselho")
                {
                    TextSign(new Vector3(center.x, height - 0.65f, front - 0.10f), "CASA DO CONSELHO", 3.1f);
                    // Mural separado da testeira: os dois títulos não disputam a mesma silhueta.
                    Noticeboard(new Vector3(center.x - width * 0.34f, 0f, front - 1.15f));
                }
                else
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var planter = new Vector3(center.x + side * width * 0.3f, 0.44f, front - 0.35f);
                        Box("tabua", planter, new Vector3(1.1f, 0.42f, 0.4f), 0f);
                        for (int f = 0; f < 5; f++)
                        {
                            var p = planter + new Vector3(-0.4f + f * 0.2f, 0.35f, 0f);
                            Lump("folha", p, new Vector3(0.27f, 0.33f, 0.28f), f * 29f, false);
                            Lump("tecido cru", p + Vector3.up * 0.13f, Vector3.one * 0.095f, 0f, false);
                        }
                    }
                }

                // Lenha seca e ferramenta simples: detalhe repetível com variação por edifício.
                float utilitySide = name == "Estalagem" ? -1f : 1f;
                var utility = new Vector3(center.x + utilitySide * (width * 0.42f), 0.18f, front - 0.34f);
                for (int log = 0; log < 6; log++)
                    Part(cylinder, palette[log % 2 == 0 ? "tabua" : "madeira"],
                        utility + new Vector3((log % 3) * 0.18f, (log / 3) * 0.22f, 0f),
                        new Vector3(0.12f, 0.32f, 0.12f), Quaternion.Euler(90f, 0f, 0f), false);
                Box("ferro", utility + new Vector3(-0.22f, 0.58f, 0f), new Vector3(0.07f, 0.48f, 0.08f), -12f, false);
            }
            Bench(new Vector3(-4.4f, 0.17f, 7.2f), 0f);
            Bench(new Vector3(4.4f, 0.17f, 4.6f), 90f);
            Market(new Vector3(8.6f, 0f, 1.8f));
            // Resource handling tells the story of work, without adding gathering gameplay.
            HideRenderer("Braseiro");
            var hearth = new Vector3(-5.2f, 0f, -3.8f);
            Part(cylinder, palette["pedra escura"], hearth + Vector3.up * 0.28f, new Vector3(0.9f, 0.28f, 0.9f), Quaternion.identity);
            Lump("brasa", hearth + Vector3.up * 0.5f, new Vector3(0.65f, 0.1f, 0.65f), 0f, false);
            for (int i = 0; i < 4; i++) Box("ferro", hearth + new Vector3(-0.25f + i * 0.17f, 0.56f, 0f), new Vector3(0.045f, 0.035f, 0.72f), 0f);
            WarmLight(hearth + Vector3.up * 0.8f, 3.6f, 0.75f);
        }

        private void Awning(Vector3 wall, float width, float depth, string cloth)
        {
            for (int stripe = 0; stripe < 7; stripe++)
            {
                float x0 = wall.x - width * 0.5f + stripe * width / 7f;
                float x1 = x0 + width / 7f;
                var verts = new[] {
                    new Vector3(x0, wall.y, wall.z), new Vector3(x1, wall.y, wall.z),
                    new Vector3(x1, wall.y - 0.28f, wall.z - depth), new Vector3(x0, wall.y - 0.28f, wall.z - depth),
                    new Vector3((x0+x1)*0.5f, wall.y - 0.46f, wall.z-depth)
                };
                Surface("Toldo tecido", verts, new[] {0,1,2,0,2,3,3,2,4,2,1,0,3,2,0,4,2,3}, palette[stripe % 2 == 0 ? cloth : "tecido cru"]);
            }
            for (int side = -1; side <= 1; side += 2)
                Box("madeira", new Vector3(wall.x + side * width * 0.5f, (wall.y - 0.28f) * 0.5f, wall.z - depth),
                    new Vector3(0.10f, wall.y - 0.28f, 0.10f), 0f);
            Box("madeira", wall + new Vector3(0f, -0.3f, -depth), new Vector3(width + 0.15f, 0.09f, 0.1f), 0f);
        }

        private void Bench(Vector3 p, float yaw)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            foreach (float side in new[] { -0.7f, 0.7f })
                Part(cube, palette["pedra"], p + rotation * new Vector3(side, 0.23f, 0f), new Vector3(0.22f, 0.46f, 0.45f), rotation);
            Part(cube, palette["tabua"], p + Vector3.up * 0.48f, new Vector3(1.85f, 0.12f, 0.56f), rotation);
            Part(cube, palette["madeira"], p + rotation * new Vector3(0f, 0.87f, 0.24f), new Vector3(1.85f, 0.16f, 0.1f), rotation);
        }

        private void Table(Vector3 p)
        {
            Box("tabua", p + Vector3.up * 0.72f, new Vector3(1.5f, 0.12f, 1f), 0f);
            for (int side = -1; side <= 1; side += 2)
                Box("madeira", p + new Vector3(side * 0.52f, 0.35f, 0f), new Vector3(0.18f, 0.7f, 0.75f), 0f);
            Bench(p + Vector3.back * 0.88f, 0f);
            Pot(p + new Vector3(0.3f, 0.8f, 0.1f), 0.22f);
        }

        private void Workbench(Vector3 p)
        {
            Box("tabua", p + Vector3.up * 0.72f, new Vector3(1.6f, 0.15f, 0.64f), 0f);
            for (int side = -1; side <= 1; side += 2)
                Box("madeira", p + new Vector3(side * 0.6f, 0.36f, 0f), new Vector3(0.18f, 0.72f, 0.52f), 0f);
            Box("ferro", p + new Vector3(-0.3f, 0.9f, 0f), new Vector3(0.52f, 0.22f, 0.32f), 0f);
            Box("ferro", p + new Vector3(-0.3f, 1.05f, 0f), new Vector3(0.83f, 0.12f, 0.34f), 0f);
            Box("madeira", p + new Vector3(0.44f, 0.84f, 0.03f), new Vector3(0.08f, 0.08f, 0.43f), -28f);
            Box("ferro", p + new Vector3(0.34f, 0.87f, -0.14f), new Vector3(0.27f, 0.16f, 0.17f), -28f);
            for (int i = 0; i < 5; i++)
                Box("cobre", p + new Vector3(-0.47f + (i % 3) * 0.28f, 0.1f + (i / 3) * 0.17f, -0.6f), new Vector3(0.24f, 0.15f, 0.4f), 0f);
        }

        private void Market(Vector3 p)
        {
            Awning(p + Vector3.up * 2.55f, 2.8f, 1.7f, "tecido azul");
            Box("tabua", p + new Vector3(0f, 0.85f, -0.8f), new Vector3(2.6f, 0.14f, 0.8f), 0f);
            for (int side = -1; side <= 1; side += 2)
                Box("madeira", p + new Vector3(side * 1.1f, 0.42f, -0.8f), new Vector3(0.15f, 0.84f, 0.6f), 0f);
            for (int i = 0; i < 9; i++)
                Lump(i % 3 == 0 ? "cobre" : "junco", p + new Vector3(-0.85f + (i % 5) * 0.36f, 1.02f, -0.7f - (i / 5) * 0.2f),
                    new Vector3(0.2f, 0.18f, 0.23f), i * 43f, false);
            Barrel(p + new Vector3(1.8f, 0f, -0.4f), 0.75f);
            Pot(p + new Vector3(-1.7f, 0f, -0.5f), 0.6f);
        }

        private void Barrel(Vector3 p, float height)
        {
            Part(cylinder, palette["tabua"], p + Vector3.up * height * 0.5f,
                new Vector3(height * 0.7f, height * 0.5f, height * 0.7f), Quaternion.identity);
            for (int i = 0; i < 2; i++)
                Part(cylinder, palette["ferro"], p + Vector3.up * height * (0.2f + i * 0.6f),
                    new Vector3(height * 0.73f, 0.025f, height * 0.73f), Quaternion.identity, false);
        }

        private void Pot(Vector3 p, float size)
        {
            Lump("ceramica", p + Vector3.up * size * 0.5f, new Vector3(size, size, size * 0.85f), 0f);
            Part(cylinder, palette["terra"], p + Vector3.up * size * 0.94f,
                new Vector3(size * 0.53f, 0.025f, size * 0.53f), Quaternion.identity, false);
        }

        private void Noticeboard(Vector3 p)
        {
            Box("madeira", p + Vector3.up * 1.5f, new Vector3(1.7f, 1.4f, 0.16f), 0f);
            for (int side = -1; side <= 1; side += 2)
                Box("madeira", p + new Vector3(side * 0.8f, 0.85f, 0f), new Vector3(0.12f, 1.7f, 0.18f), 0f);
            for (int i = 0; i < 5; i++)
                Box("papel", p + new Vector3(-0.52f + (i % 3) * 0.5f, 1.68f - (i / 3) * 0.47f, -0.10f),
                    new Vector3(0.36f, 0.4f, 0.025f), 0f, false);
            TextSign(p + new Vector3(0f, 2.02f, -0.09f), "MURAL", 0.88f);
            var blocker = new GameObject("Colisor do mural");
            blocker.transform.SetParent(transform, false);
            blocker.transform.position = p + Vector3.up * 1.15f;
            blocker.AddComponent<BoxCollider>().size = new Vector3(1.92f, 2.30f, 0.30f);
        }

        private void Sign(Vector3 p, string first, string second, float yaw)
        {
            Box("madeira", p + Vector3.up * 1.05f, new Vector3(0.12f, 2.1f, 0.12f), yaw);
            TextSign(p + new Vector3(0f, 1.85f, -0.07f), first, 1.85f);
            TextSign(p + new Vector3(0.1f, 1.45f, -0.08f), second, 1.85f);
        }

        private void TextSign(Vector3 p, string title, float width)
        {
            Box("madeira", p, new Vector3(width, 0.31f, 0.12f), 0f);
            var item = new GameObject(title);
            item.transform.SetParent(transform, false);
            item.transform.position = p + Vector3.back * 0.068f;
            var text = item.AddComponent<TextMesh>();
            text.text = title;
            text.fontSize = 48;
            text.characterSize = Mathf.Min(0.075f, width / Mathf.Max(1f, title.Length) * 0.65f);
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = palette["papel"].color;
            text.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private void Lantern(Vector3 p)
        {
            Box("madeira", p + new Vector3(0f, 0.2f, 0.15f), new Vector3(0.08f, 0.08f, 0.42f), 0f);
            Box("ferro", p, new Vector3(0.26f, 0.38f, 0.24f), 0f);
            Box("ambar", p + Vector3.back * 0.125f, new Vector3(0.17f, 0.25f, 0.012f), 0f, false);
        }

        private void BuildMine()
        {
            HideRenderer("Montanha da Mina");
            for (int i = 0; i < 7; i++)
            {
                float angle = Mathf.Lerp(-100f, 100f, i / 6f) * Mathf.Deg2Rad;
                var p = new Vector3(22f + Mathf.Sin(angle) * 4.6f, 0f, -5f + Mathf.Cos(angle) * 2.4f);
                Stamp("Nature/Rock_" + (i % 5 + 1), p, 5.4f + i % 3 * 1.15f, i * 37f, true);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                var p = new Vector3(22f + side * 2f, 0f, -9.45f);
                Lantern(p + new Vector3(side * 0.42f, 2f, -0.17f));
                for (int bolt = 0; bolt < 3; bolt++)
                    Box("ferro", p + new Vector3(0f, 0.6f + bolt * 1.15f, -0.255f), new Vector3(0.18f, 0.14f, 0.025f), 0f, false);
            }
            TextSign(new Vector3(22f, 3.85f, -9.6f), "MINA", 1.8f);
            for (int i = 0; i < 9; i++)
                Box("tabua", new Vector3(22f, 0.045f, -10f - i * 0.5f), new Vector3(1.6f, 0.08f, 0.17f), 0f, false);
            for (int side = -1; side <= 1; side += 2)
                Box("ferro", new Vector3(22f + side * 0.5f, 0.105f, -12f), new Vector3(0.07f, 0.08f, 4.2f), 0f, false);
            Stamp("Village/Prop_Crate", new Vector3(24.8f, 0f, -11.5f), 0.75f, 12f, true);
            for (int i = 0; i < 7; i++)
                Lump(i % 3 == 0 ? "cobre" : "pedra escura", new Vector3(25f + (i % 3) * 0.32f, 0.12f, -12.6f + (i / 3) * 0.3f),
                    new Vector3(0.45f, 0.29f, 0.35f), i * 37f, false);
            WarmLight(new Vector3(22f, 2.5f, -10f), 4f, 0.6f);

            // The existing cave is a separate teleport destination. Detail its edges;
            // keep the central corridor and both interaction destinations unobstructed.
            for (int z = -36; z >= -46; z -= 5)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("madeira", new Vector3(side * 5.5f, 2.1f, z), new Vector3(0.23f, 4.2f, 0.25f), 0f);
                    Stamp("Nature/Rock_" + (Mathf.Abs(z) % 5 + 1), new Vector3(side * 5.9f, 0.16f, z - 1f), 2.6f, z * 7f, false);
                    for (int i = 0; i < 3; i++)
                        Part(cube, palette[side < 0 ? "ferro" : "cobre"], new Vector3(side * (5.15f + i * 0.12f), 0.5f + i * 0.24f, z - 1f),
                            new Vector3(0.16f, 0.52f, 0.18f), Quaternion.Euler(12f, i * 32f, side * 23f), false);
                }
                Box("madeira", new Vector3(0f, 4.18f, z), new Vector3(11.3f, 0.26f, 0.3f), 0f);
            }
            // Teto e uma segunda camada de rocha fecham o cenário quando o jogador está dentro.
            for (int z = -36; z >= -47; z -= 2)
            {
                Lump("pedra escura", new Vector3(-3.2f, 5.05f, z), new Vector3(4.7f, 1.5f, 2.2f), z * 11f, false);
                Lump("pedra", new Vector3(3.1f, 5.0f, z - 0.5f), new Vector3(4.8f, 1.4f, 2.3f), z * 13f, false);
            }
        }

        private void BuildForest()
        {
            Vector3[] groves = {
                P(-25f, 15f), P(-24f, 25f), P(-30f, -3f), P(-23f, -17f),
                P(13f, 20f), P(22f, 23f), P(28f, 13f), P(6f, 28f)
            };
            var random = new System.Random(7301);
            for (int group = 0; group < groves.Length; group++)
            {
                for (int i = 0; i < 4; i++)
                {
                    var p = groves[group] + new Vector3(Range(random, -3.4f, 3.4f), 0f, Range(random, -3.4f, 3.4f));
                    if (!Plantable(p, 1.5f)) continue;
                    string model = group > 4 && i % 3 == 0 ? "PineTree_1" : "NormalTree_" + (i % 3 + 1);
                    Stamp("Nature/" + model, p, Range(random, 4.3f, 6.9f), Range(random, 0f, 360f), true);
                }
                for (int i = 0; i < 17; i++)
                {
                    var p = groves[group] + new Vector3(Range(random, -4.8f, 4.8f), 0f, Range(random, -4.8f, 4.8f));
                    if (!Plantable(p, 0.5f)) continue;
                    Stamp(i % 6 == 0 ? "Nature/Bush" : "Nature/Grass_Large_Extruded", p,
                        i % 6 == 0 ? Range(random, 0.55f, 0.85f) : Range(random, 0.16f, 0.34f), i * 47f, false);
                }
            }
            // Sparse roadside tufts frame the route without filling the walking lane.
            foreach (var route in routes)
            {
                var samples = Smooth(route, 5);
                for (int i = 1; i < samples.Count - 1; i += 4)
                {
                    var p = samples[i] + new Vector3(i % 2 == 0 ? 2.1f : -2.1f, 0f, 0f);
                    if (Plantable(p, 0.35f)) Stamp("Nature/Bush", p, 0.45f, i * 29f, false);
                }
            }
        }

        private bool Plantable(Vector3 p, float margin)
        {
            if (Mathf.Abs(p.x) > 36f || p.z > 32f || p.z < -28f) return false;
            if (Mathf.Abs(p.x - RiverX(p.z)) < 3.2f + margin) return false;
            if (p.x > -6.8f && p.x < 6.8f && p.z > -3.3f && p.z < 9.3f) return false;
            foreach (var bounds in houses)
                if (p.x > bounds.min.x - margin && p.x < bounds.max.x + margin &&
                    p.z > bounds.min.z - margin && p.z < bounds.max.z + margin) return false;
            if ((p - P(22f, -10f)).sqrMagnitude < 36f) return false;
            foreach (var route in routes)
                for (int i = 0; i < route.Length - 1; i++)
                    if (DistanceToSegment(p, route[i], route[i + 1]) < 1.5f + margin) return false;
            return true;
        }

        private void BuildHorizon()
        {
            Vector3[] positions = { P(-34f, 35f), P(-27f, 37f), P(-19f, 38f), P(-7f, 39f), P(5f, 40f), P(20f, 38f), P(30f, 36f), P(37f, 31f) };
            for (int i = 0; i < positions.Length; i++)
                Stamp("Nature/Rock_" + (i % 5 + 1), positions[i], 4.5f + (i % 3) * 1.2f, i * 53f, true);
        }

        private void WarmLight(Vector3 p, float range, float intensity)
        {
            var item = new GameObject("Luz quente de oficio");
            item.transform.SetParent(transform, false);
            item.transform.position = p;
            var light = item.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.57f, 0.27f);
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        private void Stamp(string resource, Vector3 position, float height, float yaw, bool shadows)
        {
            var item = StylizedVisualBootstrap.SpawnNormalized(resource, position, Quaternion.Euler(0f, yaw, 0f), height, transform);
            if (item == null) { Debug.LogWarning("[BercoWorldArt] Recurso ausente: " + resource); return; }
            // GPU-only imported meshes must retain their renderers in player builds.
            bool readable = true;
            foreach (var filter in item.GetComponentsInChildren<MeshFilter>())
                if (filter.sharedMesh != null && !filter.sharedMesh.isReadable) readable = false;
            if (!readable)
            {
                foreach (var renderer in item.GetComponentsInChildren<Renderer>())
                    renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                return;
            }
            foreach (var filter in item.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || filter.sharedMesh == null) continue;
                var materials = renderer.sharedMaterials;
                for (int sub = 0; sub < filter.sharedMesh.subMeshCount && sub < materials.Length; sub++)
                    if (materials[sub] != null)
                        AddToBatch(filter.sharedMesh, materials[sub], filter.transform.localToWorldMatrix, position, shadows, sub);
            }
            item.SetActive(false);
            Destroy(item);
        }

        private void Box(string material, Vector3 p, Vector3 scale, float yaw, bool shadows = true) =>
            Part(cube, palette[material], p, scale, Quaternion.Euler(0f, yaw, 0f), shadows);

        private void Lump(string material, Vector3 p, Vector3 scale, float yaw, bool shadows = true) =>
            Part(sphere, palette[material], p, scale, Quaternion.Euler(0f, yaw, 0f), shadows);

        private void Part(Mesh mesh, Material material, Vector3 p, Vector3 scale, Quaternion rotation, bool shadows = true) =>
            AddToBatch(mesh, material, Matrix4x4.TRS(p, rotation, scale), p, shadows, 0);

        private void AddToBatch(Mesh mesh, Material material, Matrix4x4 matrix, Vector3 position, bool shadows, int subMesh)
        {
            string key = material.GetEntityId() + ":" + Mathf.FloorToInt(position.x / 12f) + ":" + Mathf.FloorToInt(position.z / 12f) + ":" + shadows;
            if (!batches.TryGetValue(key, out var batch))
            {
                batch = new Batch { material = material, shadows = shadows };
                batches.Add(key, batch);
            }
            batch.parts.Add(new CombineInstance { mesh = mesh, subMeshIndex = subMesh, transform = matrix });
            detailCount++;
        }

        private void FlushBatches()
        {
            foreach (var batch in batches.Values)
            {
                var mesh = new Mesh { name = "Detalhes agrupados", indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(batch.parts.ToArray(), true, true);
                ownedMeshes.Add(mesh);
                var item = new GameObject("Detalhes - " + batch.material.name);
                item.transform.SetParent(transform, false);
                item.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = item.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = batch.material;
                renderer.shadowCastingMode = batch.shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }
        }

        private void Ribbon(string name, Vector3[] controls, float width, string material, float elevation, bool followGround)
        {
            var samples = Smooth(controls, 7);
            var vertices = new Vector3[samples.Count * 2];
            var triangles = new int[(samples.Count - 1) * 6];
            for (int i = 0; i < samples.Count; i++)
            {
                var tangent = (samples[Mathf.Min(i + 1, samples.Count - 1)] - samples[Mathf.Max(0, i - 1)]).normalized;
                var side = new Vector3(tangent.z, 0f, -tangent.x);
                float variation = 1f + Mathf.Sin(i * 1.17f) * 0.06f;
                for (int edge = 0; edge < 2; edge++)
                {
                    var p = samples[i] + side * (edge == 0 ? -1f : 1f) * width * 0.5f * variation;
                    p.y = elevation + (followGround ? Ground(p.x, p.z) : 0f);
                    vertices[i * 2 + edge] = p;
                }
                if (i == samples.Count - 1) continue;
                int t = i * 6, v = i * 2;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            Surface(name, vertices, triangles, palette[material]);
        }

        private void Surface(string name, Vector3[] vertices, int[] triangles, Material material)
        {
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            ownedMeshes.Add(mesh);
            var item = new GameObject(name);
            item.transform.SetParent(transform, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static List<Vector3> Smooth(Vector3[] points, int subdivisions)
        {
            var result = new List<Vector3>();
            for (int i = 0; i < points.Length - 1; i++)
                for (int sample = 0; sample < subdivisions; sample++)
                {
                    float t = sample / (float)subdivisions, t2 = t * t, t3 = t2 * t;
                    var a = points[Mathf.Max(0, i - 1)];
                    var b = points[i];
                    var c = points[i + 1];
                    var d = points[Mathf.Min(points.Length - 1, i + 2)];
                    result.Add(0.5f * ((2f * b) + (-a + c) * t + (2f * a - 5f * b + 4f * c - d) * t2 + (-a + 3f * b - 3f * c + d) * t3));
                }
            result.Add(points[points.Length - 1]);
            return result;
        }

        private static float Ground(float x, float z)
        {
            // A superfície visual é contínua; os antigos blocos permanecem apenas como colisores.
            // Isso elimina os triângulos verticais nos limites retangulares do protótipo.
            return 0.025f;
        }

        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            var direction = b - a;
            float t = direction.sqrMagnitude > 0 ? Mathf.Clamp01(Vector3.Dot(p - a, direction) / direction.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + direction * t);
        }

        private static float Range(System.Random random, float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        private static void HideRenderer(string name)
        {
            var item = GameObject.Find(name);
            if (item == null) return;
            foreach (var renderer in item.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
        }

        private void OnDestroy()
        {
            foreach (var mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
            foreach (var material in ownedMaterials) if (material != null) Destroy(material);
        }
    }

    // O interior está no mesmo protótipo, a 40 m da vila. Escondê-lo fora da mina impede
    // que paredes e cristais pareçam uma construção solta no campo, sem alterar teleporte.
    public sealed class MineInteriorVisibility : MonoBehaviour
    {
        private Renderer[] interior;
        private Transform player;

        public void Initialize()
        {
            var found = new List<Renderer>();
            foreach (var renderer in FindObjectsByType<Renderer>())
                if (renderer.bounds.center.z < -30f) found.Add(renderer);
            interior = found.ToArray();
            Apply(false);
        }

        private void LateUpdate()
        {
            if (player == null)
            {
                var controller = FindAnyObjectByType<PrototypePlayerController>();
                if (controller != null) player = controller.transform;
            }
            Apply(player != null && player.position.z < -28f);
        }

        private void Apply(bool visible)
        {
            if (interior == null) return;
            foreach (var renderer in interior)
                if (renderer != null && renderer.enabled != visible) renderer.enabled = visible;
        }
    }
}
