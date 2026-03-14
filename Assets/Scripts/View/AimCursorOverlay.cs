using System.Collections.Generic;
using Adapters;
using State;
using Systems;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Hides the system cursor and draws a weapon-state crosshair:
    /// 1. Raw aim dot (white) — instant mouse position (player intent)
    /// 2. Weapon crosshair — shape/color reflects weapon phase
    /// 3. Hit/kill X-markers on crosshair (COD-style)
    /// </summary>
    public class AimCursorOverlay : MonoBehaviour
    {
        Texture2D _pixelTex;

        // Crosshair geometry
        const float LineLength = 24f;
        const float LineThickness = 6f;
        const float BaseGap = 15f;
        const float CenterDotSize = 9f;

        // Bloom animation
        const float BloomExtraGap = 30f;

        // Reload ring
        const int ReloadDotCount = 12;
        const float ReloadRingRadius = 42f;
        const float ReloadDotSize = 9f;

        // Sizes
        const float RawDotSize = 6f;
        const float UnarmedDotSize = 15f;

        // Rolling
        const float RollingAlpha = 0.3f;

        // Hit markers
        struct HitMarker { public float time; public bool isKill; }
        readonly List<HitMarker> _markers = new();
        const float HitDuration = 0.3f;
        const float KillDuration = 0.5f;
        const float HitMarkerThickness = 4f;
        const float HitLineLength = 14f;
        const float KillLineLength = 18f;
        const float HitGapStart = 8f;
        const float HitGapExpand = 14f;

        // Colors
        static readonly Color RawDotColor = new Color(1f, 1f, 1f, 0.6f);
        static readonly Color NormalColor = new Color(0.2f, 1f, 0.3f, 0.9f);
        static readonly Color WarningColor = new Color(1f, 0.25f, 0.2f, 0.9f);
        static readonly Color BloomColor = new Color(1f, 1f, 1f, 0.95f);
        static readonly Color ReloadFilledColor = new Color(1f, 0.65f, 0.1f, 0.9f);
        static readonly Color ReloadEmptyColor = new Color(0.4f, 0.4f, 0.4f, 0.4f);
        static readonly Color UnarmedColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
        static readonly Color HitColor = Color.white;
        static readonly Color KillColor = new Color(1f, 0.15f, 0.15f, 1f);

        void Awake()
        {
            _pixelTex = MakeTex(Color.white);
        }

        void Update()
        {
            // Force-hide system cursor every frame during active gameplay.
            // Unity resets Cursor.visible when editor regains focus.
            var session = App.App.Instance?.RaidSession;
            bool inGameplay = session?.RaidState?.PlayerEntity != null;
            Cursor.visible = !inGameplay;
        }

        void LateUpdate()
        {
            // Read events before AppBootstrap (order 1000) clears them.
            // AimCursorOverlay has default order (0) so LateUpdate runs first.
            var session = App.App.Instance?.RaidSession;
            if (session == null) return;

            var events = session.ConsumeEvents();
            foreach (var e in events.All)
            {
                if (e.Type == RaidEventType.HitConfirmed)
                    _markers.Add(new HitMarker { time = Time.time, isKill = e.Damage > 0f });
            }
        }

        void OnGUI()
        {
            var session = App.App.Instance?.RaidSession;
            if (session == null) return;

            var state = session.RaidState;
            var player = state?.PlayerEntity;
            if (player == null) return;

            var cam = Camera.main;
            if (cam == null) return;

            if (WorldToGUI(cam, player.RawAimPoint, out var rawPos))
                DrawRawCursor(rawPos);

            if (WorldToGUI(cam, player.WeaponAimPoint, out var weaponPos))
            {
                DrawWeaponCrosshair(weaponPos, player, state);
                DrawHitMarkers(weaponPos);
            }
        }

        // ── Raw cursor ──────────────────────────────────────────

        void DrawRawCursor(Vector2 pos)
        {
            GUI.color = RawDotColor;
            DrawRect(pos, RawDotSize);
            GUI.color = Color.white;
        }

        // ── Weapon crosshair state router ────────────────────────

        void DrawWeaponCrosshair(Vector2 pos, PlayerEntityState player, RaidState state)
        {
            var weapon = player.EquippedWeapon;
            float alphaMul = player.IsRolling ? RollingAlpha : 1f;

            if (weapon == null)
            {
                DrawUnarmedDot(pos, alphaMul);
                return;
            }

            float elapsed = state.ElapsedTime - weapon.PhaseStartTime;

            switch (weapon.Phase)
            {
                case WeaponPhase.Ready:
                    var readyColor = HasAmmo(weapon, state) ? NormalColor : WarningColor;
                    DrawCrosshairLines(pos, BaseGap, readyColor, alphaMul);
                    break;

                case WeaponPhase.Firing:
                    // Max bloom — Firing lasts 1 tick before becoming Cooldown
                    DrawCrosshairLines(pos, BaseGap + BloomExtraGap, BloomColor, alphaMul);
                    break;

                case WeaponPhase.Cooldown:
                    float cooldownT = weapon.FireInterval > 0f
                        ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / weapon.FireInterval))
                        : 1f;
                    float bloomGap = BaseGap + BloomExtraGap * (1f - cooldownT);
                    var bloomLerp = Color.Lerp(BloomColor, NormalColor, cooldownT);
                    DrawCrosshairLines(pos, bloomGap, bloomLerp, alphaMul);
                    break;

                case WeaponPhase.Reloading:
                    float reloadProgress = weapon.ReloadTime > 0f
                        ? Mathf.Clamp01(elapsed / weapon.ReloadTime)
                        : 1f;
                    DrawReloadRing(pos, reloadProgress, alphaMul);
                    break;

                case WeaponPhase.Equipping:
                    float equipAlpha = weapon.EquipTime > 0f
                        ? Mathf.Clamp01(elapsed / weapon.EquipTime)
                        : 1f;
                    DrawCrosshairLines(pos, BaseGap, NormalColor, equipAlpha * alphaMul);
                    break;

                case WeaponPhase.Unequipping:
                    float unequipAlpha = weapon.UnequipTime > 0f
                        ? 1f - Mathf.Clamp01(elapsed / weapon.UnequipTime)
                        : 0f;
                    DrawCrosshairLines(pos, BaseGap, NormalColor, unequipAlpha * alphaMul);
                    break;
            }
        }

        // ── Drawing primitives ───────────────────────────────────

        void DrawCrosshairLines(Vector2 center, float gap, Color color, float alpha)
        {
            GUI.color = new Color(color.r, color.g, color.b, color.a * alpha);

            float halfThick = LineThickness * 0.5f;

            // Top
            GUI.DrawTexture(
                new Rect(center.x - halfThick, center.y - gap - LineLength, LineThickness, LineLength),
                _pixelTex);
            // Bottom
            GUI.DrawTexture(
                new Rect(center.x - halfThick, center.y + gap, LineThickness, LineLength),
                _pixelTex);
            // Left
            GUI.DrawTexture(
                new Rect(center.x - gap - LineLength, center.y - halfThick, LineLength, LineThickness),
                _pixelTex);
            // Right
            GUI.DrawTexture(
                new Rect(center.x + gap, center.y - halfThick, LineLength, LineThickness),
                _pixelTex);

            // Center dot
            DrawRect(center, CenterDotSize);

            GUI.color = Color.white;
        }

        void DrawReloadRing(Vector2 center, float progress, float alpha)
        {
            int filledCount = Mathf.FloorToInt(progress * ReloadDotCount);

            for (int i = 0; i < ReloadDotCount; i++)
            {
                // Start from 12 o'clock, go clockwise
                float angle = i * (2f * Mathf.PI / ReloadDotCount) - Mathf.PI * 0.5f;
                float x = center.x + Mathf.Cos(angle) * ReloadRingRadius;
                float y = center.y + Mathf.Sin(angle) * ReloadRingRadius;

                var dotColor = i < filledCount ? ReloadFilledColor : ReloadEmptyColor;
                GUI.color = new Color(dotColor.r, dotColor.g, dotColor.b, dotColor.a * alpha);
                DrawRect(new Vector2(x, y), ReloadDotSize);
            }

            // Center dot in reload color
            GUI.color = new Color(ReloadFilledColor.r, ReloadFilledColor.g, ReloadFilledColor.b,
                ReloadFilledColor.a * alpha);
            DrawRect(center, CenterDotSize);

            GUI.color = Color.white;
        }

        void DrawUnarmedDot(Vector2 center, float alpha)
        {
            GUI.color = new Color(UnarmedColor.r, UnarmedColor.g, UnarmedColor.b, UnarmedColor.a * alpha);
            DrawRect(center, UnarmedDotSize);
            GUI.color = Color.white;
        }

        void DrawRect(Vector2 center, float size)
        {
            float half = size * 0.5f;
            GUI.DrawTexture(new Rect(center.x - half, center.y - half, size, size), _pixelTex);
        }

        // ── Hit markers ────────────────────────────────────────────

        void DrawHitMarkers(Vector2 center)
        {
            for (int i = _markers.Count - 1; i >= 0; i--)
            {
                var m = _markers[i];
                float duration = m.isKill ? KillDuration : HitDuration;
                float age = Time.time - m.time;

                if (age >= duration)
                {
                    _markers.RemoveAt(i);
                    continue;
                }

                float t = age / duration;
                float alpha = 1f - t;
                float lineLen = m.isKill ? KillLineLength : HitLineLength;
                float gap = HitGapStart + HitGapExpand * t;
                var color = m.isKill ? KillColor : HitColor;
                color.a = alpha;

                GUI.color = color;
                DrawXLine(center, gap, lineLen, 1f, 1f);   // ↘
                DrawXLine(center, gap, lineLen, -1f, 1f);  // ↙
                DrawXLine(center, gap, lineLen, 1f, -1f);  // ↗
                DrawXLine(center, gap, lineLen, -1f, -1f); // ↖
            }

            GUI.color = Color.white;
        }

        /// <summary>
        /// Draws one arm of the X-marker at 45° diagonal.
        /// dirX/dirY: +1 or -1 to pick the quadrant.
        /// </summary>
        void DrawXLine(Vector2 center, float gap, float length, float dirX, float dirY)
        {
            // Diagonal direction (normalized 45°)
            const float inv = 0.7071068f; // 1/sqrt(2)
            float dx = inv * dirX;
            float dy = inv * dirY;

            float startDist = gap;
            float endDist = gap + length;

            float x1 = center.x + dx * startDist;
            float y1 = center.y + dy * startDist;
            float x2 = center.x + dx * endDist;
            float y2 = center.y + dy * endDist;

            // Approximate diagonal line with a thin rotated rect
            float midX = (x1 + x2) * 0.5f;
            float midY = (y1 + y2) * 0.5f;
            float halfLen = length * 0.5f;
            float halfThick = HitMarkerThickness * 0.5f;

            // Use GUIUtility.RotateAroundPivot for diagonal drawing
            var savedMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f * dirX * dirY, new Vector2(midX, midY));
            GUI.DrawTexture(
                new Rect(midX - halfThick, midY - halfLen, HitMarkerThickness, length),
                _pixelTex);
            GUI.matrix = savedMatrix;
        }

        // ── Helpers ──────────────────────────────────────────────

        static bool WorldToGUI(Camera cam, Vector3 worldPoint, out Vector2 guiPos)
        {
            var sp = cam.WorldToScreenPoint(worldPoint);
            if (sp.z < 0f)
            {
                guiPos = default;
                return false;
            }
            guiPos = new Vector2(sp.x, Screen.height - sp.y);
            return true;
        }

        bool HasAmmo(WeaponEntityState weapon, RaidState state)
        {
            // Infinite ammo weapons (bots, melee)
            if (string.IsNullOrEmpty(weapon.AmmoType)) return true;
            // Has rounds in magazine
            if (weapon.AmmoInMagazine > 0) return true;
            // Has reserve ammo in inventory
            return state.Inventory != null
                && AmmoSystem.CountReserve(state.Inventory, weapon.AmmoType) > 0;
        }

        static Texture2D MakeTex(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        void OnDestroy()
        {
            Cursor.visible = true;
            if (_pixelTex != null) Destroy(_pixelTex);
        }
    }
}
