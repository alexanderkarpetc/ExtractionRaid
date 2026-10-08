using ExtractionRaid.View.Authoring;
using NUnit.Framework;
using UnityEngine;

namespace ExtractionRaid.Editor.Roads.Tests
{
    public sealed class RoadOverlayTests
    {
        [Test]
        public void OverlayCopiesWorldPathWithoutSharingPointsOrAssets()
        {
            var parent = new GameObject("Overlay test");
            SplatRoad overlay = null;
            var mesh = new Mesh();
            try
            {
                parent.transform.localScale = new Vector3(2, 3, 4);
                parent.transform.rotation = Quaternion.Euler(0, 35, 0);
                var go = new GameObject("Source");
                go.transform.SetParent(parent.transform, false);
                go.transform.localPosition = new Vector3(1, 2, 3);
                var source = go.AddComponent<SplatRoad>();
                source.points.Add(Vector3.zero);
                source.points.Add(new Vector3(3, 1, 12));
                source.generatedMesh = mesh;
                source.assetFolder = "Assets/ExistingRoad";
                overlay = RoadOverlay.Create(source);
                Assert.That(overlay.transform.TransformPoint(overlay.points[1]),
                    Is.EqualTo(source.transform.TransformPoint(source.points[1])));
                Assert.That(overlay.generatedMesh, Is.Null);
                Assert.That(overlay.generatedMaterial, Is.Null);
                Assert.That(overlay.materialTemplate, Is.Null);
                Assert.That(string.IsNullOrEmpty(overlay.assetFolder), Is.True);
                Assert.That(overlay.useSplatMaps, Is.False);
                Assert.That(overlay.surfaceLift, Is.GreaterThan(source.surfaceLift));
                overlay.points[1] = Vector3.zero;
                Assert.That(source.points[1], Is.EqualTo(new Vector3(3, 1, 12)));
                Assert.That(source.generatedMesh, Is.SameAs(mesh));
            }
            finally
            {
                if (overlay) Object.DestroyImmediate(overlay.gameObject);
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(mesh);
            }
        }
    }
}