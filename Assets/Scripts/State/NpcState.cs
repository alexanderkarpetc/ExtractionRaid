using UnityEngine;

namespace State
{
    public class NpcState
    {
        public EId Id;
        public Vector3 Position;
        public string NpcId;

        public static NpcState Create(EId id, Vector3 position, string npcId)
        {
            return new NpcState
            {
                Id = id,
                Position = position,
                NpcId = npcId,
            };
        }
    }
}
