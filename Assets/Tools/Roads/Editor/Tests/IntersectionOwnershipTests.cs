using System;
using ExtractionRaid.View.Authoring;
using NUnit.Framework;
using UnityEngine;

namespace ExtractionRaid.Editor.Roads.Tests
{
    public class IntersectionOwnershipTests
    {
        [Test]
        public void OnlyBuiltConnectionsClaimRoads()
        {
            var roadObject = new GameObject("Ownership test road");
            var nodeObject = new GameObject("Ownership test intersection");
            try
            {
                var road = roadObject.AddComponent<SplatRoad>();
                var node = nodeObject.AddComponent<RoadIntersection>();
                node.approaches.Add(new RoadIntersectionEnd { road = road, start = true });
                Assert.That(IntersectionAssets.Owner(road), Is.Null);
                node.builtRoads.Add(road);
                Assert.That(IntersectionAssets.Owner(road), Is.SameAs(node));
                node.builtRoads.Clear();
                Assert.That(IntersectionAssets.Owner(road), Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(nodeObject); UnityEngine.Object.DestroyImmediate(roadObject); }
        }
    }
}