using System;
using Adapters;
using UnityEngine;

namespace Tests.EditMode.Fakes
{
    public sealed class FakeCombatViewportAdapter : ICombatViewportAdapter
    {
        public Func<Vector3, float, bool> IsInsideHandler = (_, _) => true;

        public bool IsInside(Vector3 worldPosition, float normalizedMargin)
            => IsInsideHandler(worldPosition, normalizedMargin);
    }
}
