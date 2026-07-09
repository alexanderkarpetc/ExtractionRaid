using Dev;
using UnityEditor;
using UnityEngine;

namespace GameEditor
{
    /// <summary>
    /// Friendly, grouped editor for the sniper-scope tuning section. Replaces the default
    /// text-field inspector with labelled sliders (clear ranges), per-group help, and hover
    /// tooltips. Picked up automatically by DevCheatsWindow.DrawSection via Editor.CreateEditor.
    /// </summary>
    [CustomEditor(typeof(DevCheatsScopeSection))]
    public class DevCheatsScopeSectionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var s = (DevCheatsScopeSection)target;
            EditorGUI.BeginChangeCheck();

            Group("Увімкнення (за відстанню)",
                "Курсор ближче за Near до персонажа — звичайна точка. Далі за Far — повний снайп-режим. Між ними плавний бленд.");
            s.NearDistance = Slider("Near — звичайна точка", s.NearDistance, 0f, 20f,
                "Поки курсор ближче за це (м) — скоуп вимкнений, лише точка.");
            s.FarDistance = Slider("Far — повний скоуп", Mathf.Max(s.FarDistance, s.NearDistance + 0.5f),
                1f, 40f, "Далі за це (м) — скоуп повністю розкритий.");

            Group("Вага прицілу (пружина)",
                "Приціл їде до курсора як вантаж на пружині. «Низьке / Високе ergo» = краї для поганої / доброї ергономіки зброї.");
            s.SpringStiffnessLow = Slider("Жорсткість · низьке ergo", s.SpringStiffnessLow, 20f, 2000f,
                "Нижче = приціл мляво доїжджає до курсора (важка зброя).");
            s.SpringStiffnessHigh = Slider("Жорсткість · високе ergo", s.SpringStiffnessHigh, 20f, 2000f,
                "Вище = приціл різко «клацає» на курсор (легка зброя).");
            s.SpringDampingLow = Slider("Відскок ζ · низьке ergo", s.SpringDampingLow, 0.2f, 1.2f,
                "Менше за 1 = приціл перелітає ціль і робить bounce назад. 0.35 = помітно, 0.5 = легко.");
            s.SpringDampingHigh = Slider("Відскок ζ · високе ergo", s.SpringDampingHigh, 0.2f, 1.2f,
                "1 = критичне демпфування, без відскоку (чистий снап).");
            s.ErgoImpact = Slider("Вплив ergo (крива)", s.ErgoImpact, 0.25f, 4f,
                "1 = лінійно між краями. Більше 1 = «тугим» стає лише при високому ergo (більшість зброї важча).");

            Group("Камера",
                "Як камера поводиться, коли скоуп активний.");
            s.CursorInfluenceMul = Slider("Зсув до курсора ×", s.CursorInfluenceMul, 1f, 6f,
                "Наскільки далі камера тягнеться до точки прицілу (1 = як без скоупа).");
            s.ZoomMul = Slider("Зум ×", s.ZoomMul, 0.5f, 1.2f,
                "Нижче за 1 = камера ближче (сильніший зум). 1 = без додаткового зуму.");

            Group("Приціл / затемнення",
                "Вигляд scoped-кола, обідка та затемнення поза колом.");
            s.CircleRadius = Slider("Радіус кола (частка висоти)", s.CircleRadius, 0.05f, 0.5f,
                "Розмір видимого круга навколо курсора (частка висоти екрана).");
            s.CircleDark = Slider("Затемнення зовні", s.CircleDark, 0f, 1f,
                "Наскільки темніє все поза колом (1 = майже чорне).");
            s.RingThickness = Slider("Товщина обідка", s.RingThickness, 0.001f, 0.02f,
                "Товщина світлого кільця по краю кола.");
            s.RingBright = Slider("Яскравість обідка/хреста", s.RingBright, 0f, 1f,
                "Сила підсвітки кільця та снайперського хреста.");

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Reset to defaults"))
                ResetToDefaults(s);

            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(s);
        }

        // ── layout helpers ──
        static void Group(string title, string help)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(help, MessageType.None);
        }

        static float Slider(string label, float value, float min, float max, string tooltip)
            => EditorGUILayout.Slider(new GUIContent(label, tooltip), value, min, max);

        static void ResetToDefaults(DevCheatsScopeSection s)
        {
            var def = CreateInstance<DevCheatsScopeSection>();
            s.NearDistance = def.NearDistance;
            s.FarDistance = def.FarDistance;
            s.SpringStiffnessLow = def.SpringStiffnessLow;
            s.SpringStiffnessHigh = def.SpringStiffnessHigh;
            s.SpringDampingLow = def.SpringDampingLow;
            s.SpringDampingHigh = def.SpringDampingHigh;
            s.ErgoImpact = def.ErgoImpact;
            s.CursorInfluenceMul = def.CursorInfluenceMul;
            s.ZoomMul = def.ZoomMul;
            s.CircleRadius = def.CircleRadius;
            s.CircleDark = def.CircleDark;
            s.RingThickness = def.RingThickness;
            s.RingBright = def.RingBright;
            DestroyImmediate(def);
        }
    }
}
