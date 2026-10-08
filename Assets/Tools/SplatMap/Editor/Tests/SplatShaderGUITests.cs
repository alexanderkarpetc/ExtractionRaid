using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtractionRaid.Editor.SplatMap.Tests
{
    public class SplatShaderGUITests
    {
        [Test]
        public void MaskToggleEnablesTransparencyAndRestoresOpaquePasses()
        {
            Shader shader = Shader.Find("ExtractShaders/SplatRGBA");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                material.SetFloat("_UseOpacityMask", 1);
                SplatShaderGUI.Configure(material);
                Assert.That(material.renderQueue, Is.EqualTo((int)RenderQueue.Transparent));
                Assert.That(material.GetFloat("_ZWrite"), Is.Zero);
                Assert.That(material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), Is.True);
                Assert.That(material.GetShaderPassEnabled("ShadowCaster"), Is.False);
                Assert.That(material.GetShaderPassEnabled("DepthOnly"), Is.False);
                Assert.That(material.GetShaderPassEnabled("DepthNormals"), Is.False);
                Assert.That(material.GetFloat("_SrcBlend"), Is.EqualTo((float)BlendMode.SrcAlpha));
                Assert.That(material.GetFloat("_DstBlend"), Is.EqualTo((float)BlendMode.OneMinusSrcAlpha));

                material.SetFloat("_UseOpacityMask", 0);
                SplatShaderGUI.Configure(material);
                Assert.That(material.renderQueue, Is.EqualTo((int)RenderQueue.Geometry));
                Assert.That(material.GetFloat("_ZWrite"), Is.EqualTo(1));
                Assert.That(material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), Is.False);
                Assert.That(material.GetShaderPassEnabled("ShadowCaster"), Is.True);
                Assert.That(material.GetShaderPassEnabled("DepthOnly"), Is.True);
                Assert.That(material.GetShaderPassEnabled("DepthNormals"), Is.True);
            }
            finally { Object.DestroyImmediate(material); }
        }
    }
}
