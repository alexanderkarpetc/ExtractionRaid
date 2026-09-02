using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Barmetler.RoadSystem
{
    public class EditorTests
    {
        [Test]
        public void Runs()
        {
            Assert.Pass();
        }

        [Test]
        public void RefreshEndPoints_WithUnchangedEnd_DoesNotInvalidateRoadMesh()
        {
            var roadObject = new GameObject("Road");
            var endObject = new GameObject("End");

            try
            {
                var road = roadObject.AddComponent<Road>();
                var generator = roadObject.AddComponent<RoadMeshGenerator>();
                var end = endObject.AddComponent<RoadAnchor>();
                road.end = end;
                end.SetRoad(road, false);

                SetField(road, "points", new List<Vector3>
                {
                    Vector3.zero,
                    Vector3.forward,
                    Vector3.forward * 6f,
                    Vector3.forward * 4f
                });
                SetField(road, "normals", new List<Vector3> { Vector3.up, Vector3.up });
                SetField(generator, "autoGenerate", true);
                SetPropertyBackingField(generator, "Valid", true);

                end.transform.position = Vector3.forward * 4f;
                end.transform.rotation = Quaternion.identity;

                road.RefreshEndPoints();

                Assert.IsTrue(generator.Valid,
                    "An unchanged endpoint must not invalidate or regenerate its road mesh.");
            }
            finally
            {
                Object.DestroyImmediate(roadObject);
                Object.DestroyImmediate(endObject);
            }
        }

        private static void SetField<T>(object target, string name, T value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
        }

        private static void SetPropertyBackingField<T>(object target, string propertyName, T value)
        {
            SetField(target, $"<{propertyName}>k__BackingField", value);
        }
    }
}
