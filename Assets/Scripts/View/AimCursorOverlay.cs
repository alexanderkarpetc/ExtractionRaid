using System.Collections.Generic;
using Adapters;
using ApplicationCore;
using Dev;
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
    /// Geometry/colors are tweakable via DevCheats (Crosshair section).
    /// </summary>
    public class AimCursorOverlay : MonoBehaviour
    {
        Texture2D _pixelTex;

        // ── Configurable via DevCheats ─────────────────────
        // Read each frame from DevCheats.CrosshairXxx properties
        float LineLength      => DevCheats.CrosshairLineLength;
        float LineThickness   => DevCheats.CrosshairLineThickness;
        float BaseGap         => DevCheats.CrosshairBaseGap;
        float CenterDotSize   => DevCheats.CrosshairCenterDotSize;
        float BloomExtraGap   => DevCheats.CrosshairBloomExtraGap;
        Color NormalColor     => DevCheats.CrosshairNormalColor;
        Color WarningColor    => DevCheats.CrosshairWarningColor;
        Color BloomColor      => DevCheats.CrosshairBloomColor;

        // ── Non-configurable constants ─────────────────────
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
        struct HitMarker { public float time; public bool isKill; public bool isHeadshot; public float absorptionRatio; public bool isRicochet; }
        readonly List<HitMarker> _markers = new();

        // ADS visual interpolant
        float _adsAmount;

        // Colors (non-configurable)
        static readonly Color RawDotColor = new Color(1f, 1f, 1f, 0.6f);
        static readonly Color ReloadFilledColor = new Color(1f, 0.65f, 0.1f, 0.9f);
        static readonly Color ReloadEmptyColor = new Color(0.4f, 0.4f, 0.4f, 0.4f);
        static readonly Color ChargeFilledColor = new Color(0.35f, 0.75f, 1f, 0.95f); // energy blue
        static readonly Color ChargeEmptyColor  = new Color(0.25f, 0.30f, 0.45f, 0.4f);
        static readonly Color UnarmedColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);

        void Awake()
        {
            _pixelTex = MakeTex(Color.white);
        }

        void Update()
        {
            var session = App.Instance?.RaidSession;
            var player = session?.RaidState?.PlayerEntity;
            bool inGameplay = player != null;
            bool inMenu = player != null && player.IsInMenu;
            Cursor.visible = !inGameplay || !DevCheats.CrosshairEnabled || inMenu;
        }

        void LateUpdate()
        {
            // Read events before AppBootstrap (order 1000) clears them.
            // AimCursorOverlay has default order (0) so LateUpdate runs first.
            var session = App.Instance?.RaidSession;
            if (session == null) return;

            var events = session.ConsumeEvents();
            foreach (var e in events.All)
            {
                if (e.Type == RaidEventType.HitConfirmed)
                    _markers.Add(new HitMarker
                    {
                        time = Time.time,
                        isKill = e.Damage > 0f,
                        isHeadshot = e.Direction.x > 0.5f,
                        absorptionRatio = e.CurrentHp,
                        isRicochet = e.MaxHp > 0.5f,
                    });
            }
        }

        void OnGUI()
        {
            if (!DevCheats.CrosshairEnabled) return;

            var session = App.Instance?.RaidSession;
            if (session == null) return;

            // While the end-of-raid screen is up the session is still live (the dead
            // body keeps ticking) but we must not run any IMGUI here — IMGUI processes
            // pointer events ahead of UI Toolkit and would swallow clicks on the
            // result-screen Next button. Same gate also covers extraction-from-screen,
            // where the session is already null so this check is a no-op.
            if (App.Instance.LastRaidOutcome != RaidOutcome.None) return;

            var state = session.RaidState;
            var player = state?.PlayerEntity;
            if (player == null) return;
            if (player.IsInMenu) return;

            var cam = Camera.main;
            if (cam == null) return;

            // ADS crosshair blend
            float adsTarget = player.IsADS ? 1f : 0f;
            float adsSpeed = 1f / Mathf.Max(0.01f, DevCheats.AdsTransitionTime);
            _adsAmount = Mathf.MoveTowards(_adsAmount, adsTarget, Time.deltaTime * adsSpeed);

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

            // ADS-interpolated crosshair params
            float adsGap = Mathf.Lerp(BaseGap, DevCheats.AdsBaseGap, _adsAmount);
            float adsBloomExtra = Mathf.Lerp(BloomExtraGap, DevCheats.AdsBloomExtraGap, _adsAmount);

            switch (weapon.Phase)
            {
                case WeaponPhase.Ready:
                    var readyColor = HasAmmo(weapon, state) ? NormalColor : WarningColor;
                    DrawCrosshairLines(pos, adsGap, readyColor, alphaMul, _adsAmount);
                    break;

                case WeaponPhase.Firing:
                    // Max bloom — Firing lasts 1 tick before becoming Cooldown
                    DrawCrosshairLines(pos, adsGap + adsBloomExtra, BloomColor, alphaMul, _adsAmount);
                    break;

                case WeaponPhase.Cooldown:
                    float cooldownT = weapon.Stats.FireInterval > 0f
                        ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / weapon.Stats.FireInterval))
                        : 1f;
                    float bloomGap = adsGap + adsBloomExtra * (1f - cooldownT);
                    var bloomLerp = Color.Lerp(BloomColor, NormalColor, cooldownT);
                    DrawCrosshairLines(pos, bloomGap, bloomLerp, alphaMul, _adsAmount);
                    break;

                case WeaponPhase.Reloading:
                    float reloadProgress = weapon.Stats.ReloadTime > 0f
                        ? Mathf.Clamp01(elapsed / weapon.Stats.ReloadTime)
                        : 1f;
                    DrawReloadRing(pos, reloadProgress, alphaMul);
                    break;

                case WeaponPhase.Charging:
                    // Charge progress ring (Laser & other charge-up payloads). Uses the same
                    // dot-ring layout as reload but in energy-blue to visually distinguish.
                    float chargeTime = Systems.WeaponChargeResolver.GetChargeTime(weapon);
                    float chargeProgress = chargeTime > 0f
                        ? Mathf.Clamp01((state.ElapsedTime - weapon.ChargeStartTime) / chargeTime)
                        : 1f;
                    DrawChargeRing(pos, chargeProgress, alphaMul);
                    break;

                case WeaponPhase.Equipping:
                    float equipAlpha = weapon.Stats.EquipTime > 0f
                        ? Mathf.Clamp01(elapsed / weapon.Stats.EquipTime)
                        : 1f;
                    DrawCrosshairLines(pos, adsGap, NormalColor, equipAlpha * alphaMul, _adsAmount);
                    break;

                case WeaponPhase.Unequipping:
                    float unequipAlpha = weapon.Stats.UnequipTime > 0f
                        ? 1f - Mathf.Clamp01(elapsed / weapon.Stats.UnequipTime)
                        : 0f;
                    DrawCrosshairLines(pos, adsGap, NormalColor, unequipAlpha * alphaMul, _adsAmount);
                    break;
            }
        }

        // ── Drawing primitives ───────────────────────────────────

        void DrawCrosshairLines(Vector2 center, float gap, Color color, float alpha, float adsBlend = 0f)
        {
            GUI.color = new Color(color.r, color.g, color.b, color.a * alpha);

            float halfThick = LineThickness * 0.5f;

            // Top — fades out during ADS
            float topAlpha = 1f - adsBlend;
            if (topAlpha > 0.01f)
            {
                GUI.color = new Color(color.r, color.g, color.b, color.a * alpha * topAlpha);
                GUI.DrawTexture(
                    new Rect(center.x - halfThick, center.y - gap - LineLength, LineThickness, LineLength),
                    _pixelTex);
                GUI.color = new Color(color.r, color.g, color.b, color.a * alpha);
            }

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

        void DrawChargeRing(Vector2 center, float progress, float alpha)
        {
            // Same dot-ring geometry as reload; distinct energy-blue palette so the
            // player can't confuse "weapon is charging" with "weapon is reloading".
            int filledCount = Mathf.FloorToInt(progress * ReloadDotCount);

            for (int i = 0; i < ReloadDotCount; i++)
            {
                float angle = i * (2f * Mathf.PI / ReloadDotCount) - Mathf.PI * 0.5f;
                float x = center.x + Mathf.Cos(angle) * ReloadRingRadius;
                float y = center.y + Mathf.Sin(angle) * ReloadRingRadius;

                var dotColor = i < filledCount ? ChargeFilledColor : ChargeEmptyColor;
                GUI.color = new Color(dotColor.r, dotColor.g, dotColor.b, dotColor.a * alpha);
                DrawRect(new Vector2(x, y), ReloadDotSize);
            }

            // Center dot pulses with charge intensity — more filled dots → brighter center.
            float centerAlpha = ChargeFilledColor.a * alpha * Mathf.Lerp(0.4f, 1f, progress);
            GUI.color = new Color(ChargeFilledColor.r, ChargeFilledColor.g, ChargeFilledColor.b, centerAlpha);
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
            float scale = DevCheats.HitMarkerScale;

            for (int i = _markers.Count - 1; i >= 0; i--)
            {
                var m = _markers[i];
                float duration = m.isRicochet ? DevCheats.RicochetDuration
                    : m.isHeadshot ? DevCheats.HeadshotDuration
                    : m.isKill ? DevCheats.KillDuration
                    : DevCheats.HitDuration;
                float age = Time.time - m.time;

                if (age >= duration)
                {
                    _markers.RemoveAt(i);
                    continue;
                }

                float t = age / duration;
                float alpha = 1f - t;

                // Ricochet: distinct short-lived spark marker
                if (m.isRicochet)
                {
                    float ricoLen = DevCheats.HitLineLength * scale * 0.5f;
                    float ricoGap = DevCheats.HitGapStart * scale * 0.6f;
                    float ricoThick = DevCheats.HitMarkerThickness * scale * 0.8f;
                    var ricoColor = DevCheats.RicochetColor;
                    ricoColor.a = alpha;
                    GUI.color = ricoColor;
                    DrawXLine(center, ricoGap, ricoLen, ricoThick, 1f, 1f);
                    DrawXLine(center, ricoGap, ricoLen, ricoThick, -1f, 1f);
                    DrawXLine(center, ricoGap, ricoLen, ricoThick, 1f, -1f);
                    DrawXLine(center, ricoGap, ricoLen, ricoThick, -1f, -1f);
                    continue;
                }

                // Proportional sizing: more absorption = smaller marker
                float absScale = 1f - m.absorptionRatio * 0.5f; // 1.0 at full pen, 0.5 at full absorption
                float lineLen = (m.isKill || m.isHeadshot ? DevCheats.KillLineLength : DevCheats.HitLineLength) * scale * absScale;
                float gap = DevCheats.HitGapStart * scale + DevCheats.HitGapExpand * scale * t;
                float thick = DevCheats.HitMarkerThickness * scale;

                // Color: lerp toward gray-blue for armored hits
                var baseColor = m.isHeadshot ? DevCheats.HeadshotColor
                    : m.isKill ? DevCheats.KillColor
                    : DevCheats.HitColor;
                var armorColor = DevCheats.ArmorHitColor;
                var color = (m.isKill || m.isHeadshot)
                    ? baseColor  // kill/headshot colors are never blended
                    : Color.Lerp(baseColor, armorColor, m.absorptionRatio);
                color.a = alpha;

                // Inner X
                GUI.color = color;
                DrawXLine(center, gap, lineLen, thick, 1f, 1f);
                DrawXLine(center, gap, lineLen, thick, -1f, 1f);
                DrawXLine(center, gap, lineLen, thick, 1f, -1f);
                DrawXLine(center, gap, lineLen, thick, -1f, -1f);

                // Outer X (headshot only)
                if (m.isHeadshot)
                {
                    float outerScale = DevCheats.HeadshotOuterScale;
                    float outerGap = DevCheats.HitGapStart * scale * outerScale
                        + DevCheats.HitGapExpand * scale * outerScale * t * DevCheats.HeadshotOuterExpandMul;
                    float outerLen = lineLen * outerScale;
                    float outerAlpha = alpha * 0.7f; // slightly more transparent
                    var outerColor = color;
                    outerColor.a = outerAlpha;

                    GUI.color = outerColor;
                    DrawXLine(center, outerGap, outerLen, thick, 1f, 1f);
                    DrawXLine(center, outerGap, outerLen, thick, -1f, 1f);
                    DrawXLine(center, outerGap, outerLen, thick, 1f, -1f);
                    DrawXLine(center, outerGap, outerLen, thick, -1f, -1f);
                }
            }

            GUI.color = Color.white;
        }

        /// <summary>
        /// Draws one arm of the X-marker at 45° diagonal.
        /// dirX/dirY: +1 or -1 to pick the quadrant.
        /// </summary>
        void DrawXLine(Vector2 center, float gap, float length, float thickness, float dirX, float dirY)
        {
            const float inv = 0.7071068f; // 1/sqrt(2)
            float dx = inv * dirX;
            float dy = inv * dirY;

            float x1 = center.x + dx * gap;
            float y1 = center.y + dy * gap;
            float x2 = center.x + dx * (gap + length);
            float y2 = center.y + dy * (gap + length);

            float midX = (x1 + x2) * 0.5f;
            float midY = (y1 + y2) * 0.5f;
            float halfLen = length * 0.5f;
            float halfThick = thickness * 0.5f;

            var savedMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f * dirX * dirY, new Vector2(midX, midY));
            GUI.DrawTexture(
                new Rect(midX - halfThick, midY - halfLen, thickness, length),
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
            return App.Instance?.Player?.Inventory != null
                && AmmoSystem.CountReserve(App.Instance.Player.Inventory, weapon.AmmoType) > 0;
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
