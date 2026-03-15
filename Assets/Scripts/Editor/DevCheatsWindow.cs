using Dev;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class DevCheatsWindow : EditorWindow
    {
        [MenuItem("Window/Dev Cheats")]
        static void Open()
        {
            GetWindow<DevCheatsWindow>("Dev Cheats");
        }

        void OnGUI()
        {
            // ── Cheats ──────────────────────────────────────
            EditorGUILayout.LabelField("Cheats", EditorStyles.boldLabel);
            DevCheats.GodMode      = EditorGUILayout.Toggle("God Mode", DevCheats.GodMode);
            DevCheats.InfiniteAmmo = EditorGUILayout.Toggle("Infinite Ammo", DevCheats.InfiniteAmmo);
            DevCheats.NoRecoil     = EditorGUILayout.Toggle("No Recoil", DevCheats.NoRecoil);

            EditorGUILayout.Space(8);

            // ── Weapon Tweaks ───────────────────────────────
            EditorGUILayout.LabelField("Weapon Tweaks", EditorStyles.boldLabel);
            DevCheats.DamageMultiplier          = EditorGUILayout.Slider("Damage Multiplier", DevCheats.DamageMultiplier, 0.1f, 50f);
            DevCheats.ProjectileSpeedMultiplier = EditorGUILayout.Slider("Projectile Speed", DevCheats.ProjectileSpeedMultiplier, 0.1f, 10f);
            DevCheats.FireRateMultiplier        = EditorGUILayout.Slider("Fire Rate", DevCheats.FireRateMultiplier, 0.1f, 10f);
            DevCheats.RecoilMultiplier          = EditorGUILayout.Slider("Recoil", DevCheats.RecoilMultiplier, 0f, 5f);

            EditorGUILayout.Space(8);

            // ── Player Tweaks ───────────────────────────────
            EditorGUILayout.LabelField("Player Tweaks", EditorStyles.boldLabel);
            DevCheats.MoveSpeedMultiplier = EditorGUILayout.Slider("Move Speed", DevCheats.MoveSpeedMultiplier, 0.1f, 10f);

            EditorGUILayout.Space(12);

            // ── Reset ───────────────────────────────────────
            if (GUILayout.Button("Reset All to Defaults"))
            {
                DevCheats.Reset();
            }
        }
    }
}
