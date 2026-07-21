using UnityEngine;

namespace View.SpawnPoints
{
    public class NpcSpawnPoint : MonoBehaviour
    {
        public string npcId;

        void Start()
        {
            // Floating "!" badge above head when this NPC has a quest offer.
            // Built code-side so existing NPC prefabs need no edits.
            NpcQuestIndicator.Create(transform, npcId);

            // Interact highlight — the SAME screen-space outline stack containers/loot use
            // (InteractableOutlineTarget → InteractableOutlineFeature), so the quest-giver
            // reads as interactable when the player is in range. Added AFTER the quest
            // indicator so the target's one-shot renderer cache (OnEnable) sees only the
            // character mesh — the beam/ground-glow start inactive and are excluded.
            gameObject.AddComponent<InteractableOutlineTarget>();
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 0.9f, 0.5f);
            Gizmos.DrawCube(transform.position + Vector3.up * 0.5f, new Vector3(0.6f, 1f, 0.6f));
            Gizmos.color = new Color(0.2f, 0.6f, 0.9f, 1f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, new Vector3(0.6f, 1f, 0.6f));

            string label = string.IsNullOrEmpty(npcId) ? "NPC" : $"NPC: {npcId}";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 1.3f, label);
        }
#endif
    }
}
