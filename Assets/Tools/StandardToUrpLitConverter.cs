// StandardToShaderConverter.cs
// Положи файл в Assets/Editor/
// В Unity: Tools > Materials > Convert Standard Materials

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class StandardToShaderConverter : EditorWindow
{
    private bool convertSelectionOnly = true;
    private bool includeSubfolders = true;
    private bool forceReconvert = false;

    private Shader targetShader;
    private string targetShaderName = "Universal Render Pipeline/Lit";

    [MenuItem("Tools/Materials/Convert Standard Materials")]
    public static void ShowWindow()
    {
        GetWindow<StandardToShaderConverter>("Material Converter");
    }

    private void OnEnable()
    {
        if (targetShader == null && !string.IsNullOrEmpty(targetShaderName))
            targetShader = Shader.Find(targetShaderName);
    }

    private void OnGUI()
    {
        GUILayout.Label("Standard Material Converter", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        convertSelectionOnly = EditorGUILayout.ToggleLeft(
            "Convert only selected materials / folders",
            convertSelectionOnly);

        includeSubfolders = EditorGUILayout.ToggleLeft(
            "Include subfolders",
            includeSubfolders);

        forceReconvert = EditorGUILayout.ToggleLeft(
            "Force reconvert materials already using target shader",
            forceReconvert);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Target Shader", EditorStyles.boldLabel);
        targetShader = (Shader)EditorGUILayout.ObjectField("Shader Asset", targetShader, typeof(Shader), false);

        if (targetShader != null)
            targetShaderName = targetShader.name;

        targetShaderName = EditorGUILayout.TextField("Or Shader Name", targetShaderName);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Find Shader By Name"))
        {
            Shader found = Shader.Find(targetShaderName);
            if (found != null)
            {
                targetShader = found;
            }
            else
            {
                EditorUtility.DisplayDialog("Shader not found", $"Shader '{targetShaderName}' not found.", "OK");
            }
        }

        if (GUILayout.Button("Use URP Lit"))
        {
            targetShaderName = "Universal Render Pipeline/Lit";
            targetShader = Shader.Find(targetShaderName);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        string shaderInfo = targetShader != null
            ? $"Selected: {targetShader.name}"
            : "No shader selected";

        EditorGUILayout.HelpBox(
            shaderInfo + "\n\n" +
            "Если выбран URP Lit — переносится большинство параметров Standard.\n" +
            "Если выбран другой шейдер — будет выполнен базовый перенос совместимых свойств.",
            MessageType.Info);

        EditorGUILayout.Space();

        if (GUILayout.Button("Convert", GUILayout.Height(32)))
        {
            Convert();
        }
    }

    private void Convert()
    {
        if (targetShader == null)
        {
            if (!string.IsNullOrEmpty(targetShaderName))
                targetShader = Shader.Find(targetShaderName);
        }

        if (targetShader == null)
        {
            EditorUtility.DisplayDialog(
                "Shader not found",
                "Не выбран целевой шейдер или Unity не может его найти.",
                "OK");
            return;
        }

        List<Material> materials = GatherMaterials(convertSelectionOnly, includeSubfolders);
        if (materials.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Nothing found",
                "Не найдено материалов для конвертации.",
                "OK");
            return;
        }

        int converted = 0;
        int skipped = 0;
        bool isUrpLit = targetShader.name == "Universal Render Pipeline/Lit";

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < materials.Count; i++)
            {
                Material mat = materials[i];
                if (mat == null)
                    continue;

                EditorUtility.DisplayProgressBar(
                    "Converting Materials",
                    $"{i + 1}/{materials.Count}: {mat.name}",
                    (float)(i + 1) / materials.Count);

                if (!forceReconvert && mat.shader != null && mat.shader.name == targetShader.name)
                {
                    skipped++;
                    continue;
                }

                if (mat.shader == null)
                {
                    skipped++;
                    continue;
                }

                string shaderName = mat.shader.name;
                bool isStandard = shaderName == "Standard";
                bool isStandardSpec = shaderName == "Standard (Specular setup)";

                if (!isStandard && !isStandardSpec)
                {
                    skipped++;
                    continue;
                }

                if (isUrpLit)
                    ConvertToUrpLit(mat, targetShader, isStandardSpec);
                else
                    ConvertToGenericShader(mat, targetShader);

                EditorUtility.SetDirty(mat);
                converted++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        EditorUtility.DisplayDialog(
            "Done",
            $"Target Shader: {targetShader.name}\nConverted: {converted}\nSkipped: {skipped}",
            "OK");
    }

    private static List<Material> GatherMaterials(bool selectionOnly, bool includeSubfolders)
    {
        var result = new List<Material>();
        var added = new HashSet<string>();

        if (selectionOnly)
        {
            Object[] selection = Selection.objects;
            foreach (var obj in selection)
            {
                if (obj == null) continue;

                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                if (obj is Material mat)
                {
                    if (added.Add(path))
                        result.Add(mat);
                    continue;
                }

                if (AssetDatabase.IsValidFolder(path))
                {
                    string[] searchFolders = new[] { path };
                    string[] guids = AssetDatabase.FindAssets("t:Material", searchFolders);

                    foreach (string guid in guids)
                    {
                        string matPath = AssetDatabase.GUIDToAssetPath(guid);

                        if (!includeSubfolders)
                        {
                            string dir = System.IO.Path.GetDirectoryName(matPath)?.Replace("\\", "/");
                            if (dir != path.Replace("\\", "/"))
                                continue;
                        }

                        if (!added.Add(matPath))
                            continue;

                        Material folderMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                        if (folderMat != null)
                            result.Add(folderMat);
                    }
                }
            }
        }
        else
        {
            string[] guids = AssetDatabase.FindAssets("t:Material");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!added.Add(path))
                    continue;

                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null)
                    result.Add(mat);
            }
        }

        return result;
    }

    private static void ConvertToUrpLit(Material mat, Shader targetShader, bool fromSpecularWorkflow)
    {
        Texture mainTex = GetTexture(mat, "_MainTex");
        Color color = GetColor(mat, "_Color", Color.white);

        Texture bumpMap = GetTexture(mat, "_BumpMap");
        float bumpScale = GetFloat(mat, "_BumpScale", 1f);

        Texture parallaxMap = GetTexture(mat, "_ParallaxMap");
        float parallax = GetFloat(mat, "_Parallax", 0.005f);

        Texture occlusionMap = GetTexture(mat, "_OcclusionMap");
        float occlusionStrength = GetFloat(mat, "_OcclusionStrength", 1f);

        Texture emissionMap = GetTexture(mat, "_EmissionMap");
        Color emissionColor = GetColor(mat, "_EmissionColor", Color.black);

        Texture detailMask = GetTexture(mat, "_DetailMask");
        Texture detailAlbedo = GetTexture(mat, "_DetailAlbedoMap");
        Texture detailNormal = GetTexture(mat, "_DetailNormalMap");
        float detailNormalScale = GetFloat(mat, "_DetailNormalMapScale", 1f);

        Texture metallicGlossMap = GetTexture(mat, "_MetallicGlossMap");
        float metallic = GetFloat(mat, "_Metallic", 0f);

        Texture specGlossMap = GetTexture(mat, "_SpecGlossMap");
        Color specColor = GetColor(mat, "_SpecColor", Color.grey);

        float glossiness = GetFloat(mat, "_Glossiness", 0.5f);
        float smoothnessTextureChannel = GetFloat(mat, "_SmoothnessTextureChannel", 0f);

        float cutoff = GetFloat(mat, "_Cutoff", 0.5f);
        int mode = Mathf.RoundToInt(GetFloat(mat, "_Mode", 0f));

        Vector2 mainScale = mat.HasProperty("_MainTex") ? mat.GetTextureScale("_MainTex") : Vector2.one;
        Vector2 mainOffset = mat.HasProperty("_MainTex") ? mat.GetTextureOffset("_MainTex") : Vector2.zero;

        mat.shader = targetShader;

        if (mat.HasProperty("_WorkflowMode"))
            mat.SetFloat("_WorkflowMode", fromSpecularWorkflow ? 0f : 1f);

        SetTexture(mat, "_BaseMap", mainTex);
        SetColor(mat, "_BaseColor", color);
        SetScaleOffset(mat, "_BaseMap", mainScale, mainOffset);

        SetTexture(mat, "_BumpMap", bumpMap);
        SetFloat(mat, "_BumpScale", bumpScale);

        if (bumpMap != null) mat.EnableKeyword("_NORMALMAP");
        else mat.DisableKeyword("_NORMALMAP");

        SetTexture(mat, "_ParallaxMap", parallaxMap);
        SetFloat(mat, "_Parallax", parallax);

        SetTexture(mat, "_OcclusionMap", occlusionMap);
        SetFloat(mat, "_OcclusionStrength", occlusionStrength);

        SetTexture(mat, "_EmissionMap", emissionMap);
        SetColor(mat, "_EmissionColor", emissionColor);

        bool hasEmission = emissionMap != null || emissionColor.maxColorComponent > 0.0001f;
        if (hasEmission) mat.EnableKeyword("_EMISSION");
        else mat.DisableKeyword("_EMISSION");

        SetTexture(mat, "_DetailMask", detailMask);
        SetTexture(mat, "_DetailAlbedoMap", detailAlbedo);
        SetTexture(mat, "_DetailNormalMap", detailNormal);
        SetFloat(mat, "_DetailNormalMapScale", detailNormalScale);

        if (detailAlbedo != null || detailNormal != null) mat.EnableKeyword("_DETAIL_MULX2");
        else mat.DisableKeyword("_DETAIL_MULX2");

        SetFloat(mat, "_Smoothness", glossiness);

        if (mat.HasProperty("_SmoothnessTextureChannel"))
            mat.SetFloat("_SmoothnessTextureChannel", smoothnessTextureChannel);

        if (fromSpecularWorkflow)
        {
            SetTexture(mat, "_SpecGlossMap", specGlossMap);
            SetColor(mat, "_SpecColor", specColor);

            if (specGlossMap != null) mat.EnableKeyword("_SPECGLOSSMAP");
            else mat.DisableKeyword("_SPECGLOSSMAP");
        }
        else
        {
            SetTexture(mat, "_MetallicGlossMap", metallicGlossMap);
            SetFloat(mat, "_Metallic", metallic);

            if (metallicGlossMap != null) mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            else mat.DisableKeyword("_METALLICSPECGLOSSMAP");
        }

        ApplySurfaceMode(mat, mode, cutoff);
    }

    private static void ConvertToGenericShader(Material mat, Shader targetShader)
    {
        Texture mainTex = GetTexture(mat, "_MainTex");
        Color color = GetColor(mat, "_Color", Color.white);
        Texture bumpMap = GetTexture(mat, "_BumpMap");
        Texture emissionMap = GetTexture(mat, "_EmissionMap");
        Color emissionColor = GetColor(mat, "_EmissionColor", Color.black);
        Texture occlusionMap = GetTexture(mat, "_OcclusionMap");
        Texture metallicGlossMap = GetTexture(mat, "_MetallicGlossMap");
        Texture specGlossMap = GetTexture(mat, "_SpecGlossMap");

        Vector2 mainScale = mat.HasProperty("_MainTex") ? mat.GetTextureScale("_MainTex") : Vector2.one;
        Vector2 mainOffset = mat.HasProperty("_MainTex") ? mat.GetTextureOffset("_MainTex") : Vector2.zero;

        mat.shader = targetShader;

        TrySetFirstExistingTexture(mat, new[] { "_BaseMap", "_MainTex", "_BaseColorMap", "_AlbedoMap" }, mainTex, mainScale, mainOffset);
        TrySetFirstExistingColor(mat, new[] { "_BaseColor", "_Color", "_Tint", "_AlbedoColor" }, color);
        TrySetFirstExistingTexture(mat, new[] { "_BumpMap", "_NormalMap" }, bumpMap);
        TrySetFirstExistingTexture(mat, new[] { "_EmissionMap", "_EmissiveMap" }, emissionMap);
        TrySetFirstExistingColor(mat, new[] { "_EmissionColor", "_EmissiveColor" }, emissionColor);
        TrySetFirstExistingTexture(mat, new[] { "_OcclusionMap", "_AOMap" }, occlusionMap);
        TrySetFirstExistingTexture(mat, new[] { "_MetallicGlossMap", "_MetallicMap" }, metallicGlossMap);
        TrySetFirstExistingTexture(mat, new[] { "_SpecGlossMap", "_SpecularMap" }, specGlossMap);

        if ((bumpMap != null) && (mat.HasProperty("_BumpMap") || mat.HasProperty("_NormalMap")))
            mat.EnableKeyword("_NORMALMAP");

        if ((emissionMap != null || emissionColor.maxColorComponent > 0.0001f) &&
            (mat.HasProperty("_EmissionMap") || mat.HasProperty("_EmissiveMap")))
            mat.EnableKeyword("_EMISSION");
    }

    private static void ApplySurfaceMode(Material mat, int standardMode, float cutoff)
    {
        SetFloat(mat, "_Cutoff", cutoff);

        switch (standardMode)
        {
            case 0:
                SetFloat(mat, "_Surface", 0f);
                SetFloat(mat, "_AlphaClip", 0f);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                break;

            case 1:
                SetFloat(mat, "_Surface", 0f);
                SetFloat(mat, "_AlphaClip", 1f);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                break;

            case 2:
            case 3:
                SetFloat(mat, "_Surface", 1f);
                SetFloat(mat, "_AlphaClip", 0f);
                SetFloat(mat, "_Blend", 0f);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                break;

            default:
                SetFloat(mat, "_Surface", 0f);
                SetFloat(mat, "_AlphaClip", 0f);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                break;
        }
    }

    private static Texture GetTexture(Material mat, string property)
    {
        return mat.HasProperty(property) ? mat.GetTexture(property) : null;
    }

    private static Color GetColor(Material mat, string property, Color fallback)
    {
        return mat.HasProperty(property) ? mat.GetColor(property) : fallback;
    }

    private static float GetFloat(Material mat, string property, float fallback)
    {
        return mat.HasProperty(property) ? mat.GetFloat(property) : fallback;
    }

    private static void SetTexture(Material mat, string property, Texture value)
    {
        if (mat.HasProperty(property))
            mat.SetTexture(property, value);
    }

    private static void SetColor(Material mat, string property, Color value)
    {
        if (mat.HasProperty(property))
            mat.SetColor(property, value);
    }

    private static void SetFloat(Material mat, string property, float value)
    {
        if (mat.HasProperty(property))
            mat.SetFloat(property, value);
    }

    private static void SetScaleOffset(Material mat, string property, Vector2 scale, Vector2 offset)
    {
        if (!mat.HasProperty(property))
            return;

        mat.SetTextureScale(property, scale);
        mat.SetTextureOffset(property, offset);
    }

    private static void TrySetFirstExistingTexture(Material mat, string[] propertyNames, Texture texture, Vector2? scale = null, Vector2? offset = null)
    {
        if (texture == null) return;

        foreach (string prop in propertyNames)
        {
            if (mat.HasProperty(prop))
            {
                mat.SetTexture(prop, texture);

                if (scale.HasValue) mat.SetTextureScale(prop, scale.Value);
                if (offset.HasValue) mat.SetTextureOffset(prop, offset.Value);
                return;
            }
        }
    }

    private static void TrySetFirstExistingColor(Material mat, string[] propertyNames, Color color)
    {
        foreach (string prop in propertyNames)
        {
            if (mat.HasProperty(prop))
            {
                mat.SetColor(prop, color);
                return;
            }
        }
    }
}