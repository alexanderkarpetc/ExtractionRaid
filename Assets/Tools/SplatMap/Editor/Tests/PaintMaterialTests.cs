using NUnit.Framework;
using UnityEngine;

namespace ExtractionRaid.Editor.SplatMap.Tests
{
    public class PaintMaterialTests
    {
        [TestCase(0, 0.1f)]
        [TestCase(1, 0.3f)]
        [TestCase(2, 0.6f)]
        [TestCase(3, 0.8f)]
        public void ExistingMaskUsesSelectedChannel(float channel, float expected)
        {
            Assert.That(PaintMaterial.Grayscale(new Color(0.1f, 0.3f, 0.6f, 0.8f), channel),
                Is.EqualTo(new Color(expected, expected, expected, 1)));
        }

        [Test]
        public void RoadMaskUsesIndependentUVAndPreservesBlendSettings()
        {
            var shader = Shader.Find("ExtractionRaid/Road Marking Unlit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            var mesh = new Mesh();
            try
            {
                mesh.vertices = new[] { Vector3.zero };
                mesh.uv = new[] { new Vector2(0, 10) };
                mesh.uv2 = new[] { new Vector2(0, 1) };
                material.SetFloat("_UseRoadMaskUV", 1);
                material.SetFloat("_MaskChannel", 3);
                material.SetFloat("_SrcBlend", 1);
                material.SetFloat("_DstBlend", 1);
                material.SetFloat("_DepthOffset", -2);
                PaintMaterial.Configure(material, true);
                Assert.That(PaintMaterial.UV(mesh, material)[0], Is.EqualTo(new Vector2(0, 1)));
                Assert.That(material.GetFloat("_MaskChannel"), Is.Zero);
                Assert.That(material.GetFloat("_SrcBlend"), Is.EqualTo(1));
                Assert.That(material.GetFloat("_DstBlend"), Is.EqualTo(1));
                Assert.That(material.GetFloat("_DepthOffset"), Is.EqualTo(-2));
                material.SetFloat("_UseRoadMaskUV", 0);
                Assert.That(PaintMaterial.UV(mesh, material)[0], Is.EqualTo(new Vector2(0, 10)));
            }
            finally { Object.DestroyImmediate(material); Object.DestroyImmediate(mesh); }
        }
    }
}