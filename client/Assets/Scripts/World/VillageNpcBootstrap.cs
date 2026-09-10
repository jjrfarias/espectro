using UnityEngine;

namespace Espectro.Prototype
{
    // "Ancia Mira" (Corte0ProjectSetup.CreateInteractions) era só cápsula+esfera genérica —
    // visualmente muito abaixo do personagem real (KayKit). Troca pelo mesmo pipeline usado pro
    // jogador, com outro modelo (ver AdventurerCharacterSetup.BuildNpcPrefab), mantendo o
    // indicador de interação (esfera amarela flutuante) que já funciona.
    public static class VillageNpcBootstrap
    {
        private const string PrefabPath = "EspectroModels/Adventurers/Vila_NPC";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var npc = GameObject.Find("Ancia Mira");
            if (npc == null || npc.transform.Find("Vila NPC") != null) return;

            var prefab = Resources.Load<GameObject>(PrefabPath);
            if (prefab == null) return; // Prefab ainda não gerado pelo Editor — mantém a cápsula.

            var body = npc.transform.Find("Corpo");
            if (body != null) body.gameObject.SetActive(false);
            var head = npc.transform.Find("Cabeca");
            if (head != null) head.gameObject.SetActive(false);

            var instance = Object.Instantiate(prefab, npc.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // olhando pro jogador/praça.
        }
    }
}
