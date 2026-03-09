using Adapters;
using UnityEngine;

namespace Tests.EditMode.Fakes
{
    public class FakePhysicsAdapter : IPhysicsAdapter
    {
        public bool Blocked;

        public bool Linecast(Vector3 from, Vector3 to)
        {
            return Blocked;
        }
    }
}
