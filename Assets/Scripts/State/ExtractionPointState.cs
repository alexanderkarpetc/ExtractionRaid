using UnityEngine;

namespace State
{
    public class ExtractionPointState
    {
        public EId Id;
        public Vector3 Position;
        public float Radius;
        public string Label;

        public static ExtractionPointState Create(EId id, Vector3 position, float radius, string label)
        {
            return new ExtractionPointState
            {
                Id = id,
                Position = position,
                Radius = radius,
                Label = label,
            };
        }
    }
}
