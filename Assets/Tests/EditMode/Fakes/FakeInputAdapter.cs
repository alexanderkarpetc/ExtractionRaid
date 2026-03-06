using Adapters;
using UnityEngine;

namespace Tests.EditMode.Fakes
{
    public class FakeInputAdapter : IInputAdapter
    {
        public Vector2 MoveInput { get; set; }
        public bool SprintPressed { get; set; }
        public bool AttackPressed { get; set; }
        public Vector3 AimWorldPoint { get; set; }
        public Vector3 MuzzleWorldPoint { get; set; }
    }
}