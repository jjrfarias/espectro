using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Espectro.Prototype
{
    // Primeira expansão explorável: amplia o chão sem alterar a cena-base ou o protocolo.
    // As regiões são composição ambiental; gameplay autoritativo pode ser ligado por cortes.
    public sealed class EspectroWorldExpansion : MonoBehaviour
    {
        private readonly List<Material> materials = new();
        private Material meadow, forest, highland, wetland, road, stone, wood, spectral;

        public static void Build(Transform parent)
        {
            if (parent.Find("Mundo Expandido") != null) return;
            var root = new GameObject("Mundo Expandido");
            root.transform.SetParent(parent, false);
            root.AddComponent<EspectroWorldExpansion>().Construct();
        }

        private void Construct()
        {
            meadow = Mat("Pradaria", new Color(0.16f, 0.42f, 0.20f));
            forest = Mat("Mata", new Color(0.19f, 0.33f, 0.18f));
            highland = Mat("Serra", new Color(0.34f, 0.38f, 0.31f));
            wetland = Mat("Baixio", new Color(0.25f, 0.39f, 0.31f));
            road = Mat("Estrada", new Color(0.46f, 0.37f, 0.25f));
            stone = Mat("Pedra", new Color(0.34f, 0.37f, 0.36f));
            wood = Mat("Madeira", new Color(0.29f, 0.19f, 0.11f));
            spectral = Mat("Veu", new Color(0.20f, 0.70f, 0.68f), true);
            BuildGround();
            BuildRoads();
            BuildWhisperingWood();
            BuildSplitRange();
            BuildVeilLowlands();
            BuildBoundary();
            Debug.Log("[WorldExpansion] mapa 180x180 m: Mata dos Sussurros, Serra Partida e Baixios do Veu.");
        }

        private void BuildGround()
        {
            for (int gx = -1; gx <= 1; gx++)
                for (int gz = -1; gz <= 1; gz++)
                {
                    if (gx == 0 && gz == 0) continue;
                    var p = new Vector3(gx * 60f, -0.025f, gz * 60f);
                    Primitive("Terreno " + gx + " " + gz, PrimitiveType.Plane, p, new Vector3(6f, 1f, 6f), meadow, true);
                }
        }

        private void BuildRoads()
        {
            Road(new[] { new Vector3(15,0,26), new Vector3(23,0,38), new Vector3(12,0,54), new Vector3(-8,0,68), new Vector3(-30,0,72) }, 3.2f);
            Road(new[] { new Vector3(28,0,3), new Vector3(42,0,8), new Vector3(56,0,18), new Vector3(70,0,31) }, 3f);
            Road(new[] { new Vector3(-22,0,-12), new Vector3(-35,0,-27), new Vector3(-42,0,-46), new Vector3(-31,0,-68) }, 2.8f);
            Road(new[] { new Vector3(18,0,-12), new Vector3(32,0,-28), new Vector3(42,0,-49), new Vector3(57,0,-67) }, 2.8f);
        }

        private void Road(Vector3[] points, float width)
        {
            for (int i = 0; i < points.Length - 1; i++)
            {
                var a = points[i]; var b = points[i + 1]; var delta = b - a;
                var item = Primitive("Trecho de estrada", PrimitiveType.Cube, (a + b) * 0.5f + Vector3.up * 0.015f,
                    new Vector3(width, 0.055f, delta.magnitude + 0.7f), road, false);
                item.transform.rotation = Quaternion.LookRotation(delta.normalized);
            }
        }

        private void BuildWhisperingWood()
        {
            var random = new System.Random(1709);
            for (int i = 0; i < 58; i++)
            {
                var p = new Vector3(Range(random,-76,-10), 0, Range(random,34,82));
                Spawn(i % 7 == 0 ? "Nature/PineTree_1" : "Nature/NormalTree_" + (i % 3 + 1), p, Range(random,5.5f,9f), random.Next(360));
                if (i % 2 == 0) Spawn("Nature/Bush", p + new Vector3(Range(random,-2,2),0,Range(random,-2,2)), Range(random,.5f,1f), random.Next(360));
            }
            BuildForestStream(random);
            BuildForestUnderstory(random);
            Landmark(new Vector3(-30,0,72), "MATA DOS SUSSURROS", new Color(0.34f,0.63f,0.36f));
            for (int i = 0; i < 7; i++) Spawn("Nature/Rock_" + (i%5+1), new Vector3(-5+i*1.7f,0,60+Mathf.Sin(i)*2), 2.2f+i%2, i*41);
        }

        private void BuildForestStream(System.Random random)
        {
            var points = new[] { new Vector3(-61,0,80), new Vector3(-54,0,73), new Vector3(-59,0,66), new Vector3(-49,0,59), new Vector3(-52,0,51), new Vector3(-43,0,43), new Vector3(-35,0,36) };
            for (int i = 0; i < points.Length - 1; i++)
            {
                var delta = points[i + 1] - points[i];
                var stream = Primitive("Riacho da Mata", PrimitiveType.Cube, (points[i] + points[i + 1]) * .5f + Vector3.up * .025f, new Vector3(2.2f + (i % 2) * .35f, .045f, delta.magnitude + 1.2f), wetland, false);
                stream.transform.rotation = Quaternion.LookRotation(delta.normalized);
                for (int s = 0; s < 4; s++)
                {
                    var t = (s + 1) / 5f;
                    var bank = Vector3.Cross(delta.normalized, Vector3.up) * (s % 2 == 0 ? 1.25f : -1.25f);
                    Spawn("Nature/Rock_" + ((i + s) % 5 + 1), Vector3.Lerp(points[i], points[i + 1], t) + bank, .35f + s * .08f, random.Next(360));
                }
            }
            for (int i = 0; i < 22; i++)
            {
                var p = new Vector3(-61 + i * 1.2f, .22f, 81 - i * 2f + Mathf.Sin(i * 1.4f) * 3f);
                Primitive("Reflexo do riacho", PrimitiveType.Cube, p, new Vector3(.42f,.012f,.08f), spectral, false).transform.rotation = Quaternion.Euler(0, i * 37f, 0);
            }
        }

        private void BuildForestUnderstory(System.Random random)
        {
            var clearings = new[] { new Vector3(-43,0,70), new Vector3(-25,0,57), new Vector3(-68,0,48) };
            foreach (var clearing in clearings)
            {
                for (int i = 0; i < 13; i++)
                {
                    float angle = i * 2.399f, radius = 4.2f + (i % 3) * .8f;
                    var p = clearing + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                    Spawn(i % 4 == 0 ? "Nature/Bush_Large" : "Nature/Grass_Large_Extruded", p, i % 4 == 0 ? .9f : .3f + (i % 3) * .08f, i * 29f);
                }
                for (int i = 0; i < 5; i++)
                {
                    var p = clearing + new Vector3(-2f + i * .9f, .08f, Mathf.Sin(i) * 1.4f);
                    Primitive("Cogumelo da clareira", PrimitiveType.Cylinder, p + Vector3.up * .12f, new Vector3(.07f,.24f,.07f), wood, false);
                    Primitive("Chapeu do cogumelo", PrimitiveType.Sphere, p + Vector3.up * .38f, new Vector3(.30f,.12f,.30f), i % 2 == 0 ? spectral : highland, false);
                }
            }
            for (int i = 0; i < 16; i++)
            {
                var p = new Vector3(Range(random,-75,-13), .18f, Range(random,38,80));
                var root = Primitive("Raiz exposta", PrimitiveType.Cylinder, p, new Vector3(.10f,.9f,.10f), wood, false);
                root.transform.rotation = Quaternion.Euler(68f, random.Next(360), 0f);
            }
            for (int i = 0; i < 9; i++)
            {
                var p = new Vector3(-70 + i * 6.7f, .3f, 43 + Mathf.Sin(i * 1.8f) * 6f);
                var log = Primitive("Tronco caído", PrimitiveType.Cylinder, p, new Vector3(.34f,2.2f,.34f), wood, true);
                log.transform.rotation = Quaternion.Euler(0, i * 23f, 90f);
                Primitive("Corte do tronco", PrimitiveType.Cylinder, p + Vector3.right * 1.1f, new Vector3(.36f,.035f,.36f), highland, false).transform.rotation = log.transform.rotation;
            }
        }

        private void BuildSplitRange()
        {
            for (int i = 0; i < 24; i++)
            {
                float z = -78f + i * 6.7f;
                float x = 72f + Mathf.Sin(i * 1.7f) * 8f;
                Spawn("Nature/Rock_" + (i % 5 + 1), new Vector3(x,0,z), 6f + (i%4)*1.4f, i*47);
            }
            for (int i = 0; i < 15; i++) Spawn("Nature/PineTree_1", new Vector3(44+i%5*6,0,5+i/5*10), 5f+i%3, i*31);
            Landmark(new Vector3(62,0,25), "SERRA PARTIDA", new Color(0.55f,0.59f,0.55f));
            // Portal de pedra quebrado como ponto de interesse futuro.
            Primitive("Pilar antigo A", PrimitiveType.Cube, new Vector3(55,2.4f,-58), new Vector3(1.2f,4.8f,1.2f), stone, true);
            Primitive("Pilar antigo B", PrimitiveType.Cube, new Vector3(60,2.4f,-58), new Vector3(1.2f,4.8f,1.2f), stone, true);
            var lintel = Primitive("Lintel partido", PrimitiveType.Cube, new Vector3(57.1f,5.0f,-58), new Vector3(3.2f,.8f,1.1f), stone, true);
            lintel.transform.rotation = Quaternion.Euler(0,0,8);
        }

        private void BuildVeilLowlands()
        {
            for (int i = 0; i < 28; i++)
            {
                float x = -68f + i * 5f;
                var water = Primitive("Canal do Veu", PrimitiveType.Cube, new Vector3(x,-.015f,-62+Mathf.Sin(i*.55f)*5), new Vector3(5.3f,.04f,12f), wetland, false);
                water.transform.rotation = Quaternion.Euler(0,Mathf.Sin(i)*12f,0);
                if (i % 3 == 0) Spawn("Nature/Bush", new Vector3(x,0,-53+Mathf.Sin(i)*7), .8f, i*29);
            }
            for (int i = 0; i < 18; i++)
                Primitive("Junco", PrimitiveType.Cylinder, new Vector3(-65+i*7.2f,.55f,-55-Mathf.Sin(i)*8), new Vector3(.10f,.55f,.10f), wood, false);
            Landmark(new Vector3(-31,0,-68), "BAIXIOS DO VEU", new Color(0.32f,0.64f,0.61f));
            for (int i = 0; i < 5; i++)
                Primitive("Luz do Veu", PrimitiveType.Sphere, new Vector3(-24+i*3.2f,1.1f,-70+Mathf.Sin(i)*2), Vector3.one*.32f, spectral, false);
        }

        private void BuildBoundary()
        {
            for (int i = 0; i < 36; i++)
            {
                float angle = i * Mathf.PI * 2f / 36f;
                var p = new Vector3(Mathf.Cos(angle)*88f,0,Mathf.Sin(angle)*88f);
                Spawn(i%3==0 ? "Nature/PineTree_1" : "Nature/Rock_"+(i%5+1), p, i%3==0?8f:5.5f, i*37);
            }
        }

        private void Landmark(Vector3 p, string label, Color accent)
        {
            Primitive(label+" marco", PrimitiveType.Cylinder, p+Vector3.up*1.25f, new Vector3(.55f,1.25f,.55f), stone, true);
            Primitive(label+" faixa", PrimitiveType.Cube, p+new Vector3(0,2.05f,-.4f), new Vector3(3.8f,.42f,.12f), wood, false);
            Primitive(label+" luz", PrimitiveType.Sphere, p+Vector3.up*2.75f, Vector3.one*.38f, Mat(label,accent,true), false);
        }

        private void Spawn(string resource, Vector3 p, float height, float yaw)
        {
            var item = StylizedVisualBootstrap.SpawnNormalized(resource,p,Quaternion.Euler(0,yaw,0),height,transform);
            if (item == null) return;
            foreach (var r in item.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = height>3 ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        private GameObject Primitive(string name, PrimitiveType type, Vector3 p, Vector3 scale, Material mat, bool collider)
        {
            var item=GameObject.CreatePrimitive(type); item.name=name; item.transform.SetParent(transform,false); item.transform.position=p; item.transform.localScale=scale;
            item.GetComponent<Renderer>().sharedMaterial=mat; if(!collider) Destroy(item.GetComponent<Collider>()); return item;
        }

        private Material Mat(string name, Color color, bool unlit=false)
        {
            var shader=Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Espectro/ToonLit") ?? Shader.Find("Universal Render Pipeline/Lit");
            var mat=new Material(shader){name="Mundo - "+name,color=color}; if(mat.HasProperty("_OutlineWidth")) mat.SetFloat("_OutlineWidth",.0015f); materials.Add(mat); return mat;
        }
        private static float Range(System.Random r,float a,float b)=>Mathf.Lerp(a,b,(float)r.NextDouble());
        private void OnDestroy(){foreach(var mat in materials) if(mat!=null) Destroy(mat);}
    }
}
