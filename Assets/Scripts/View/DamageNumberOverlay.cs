using System.Collections.Generic;
using Adapters;
using ApplicationCore;
using Dev;
using UnityEngine;

namespace View
{
    /// <summary>
    /// IMGUI overlay for floating damage numbers with selectable trajectory modes.
    /// 0=FloatUp, 1=Knockback, 2=ArcGravity, 3=Scatter
    /// </summary>
    public class DamageNumberOverlay : MonoBehaviour
    {
        struct DamagePopup
        {
            public float SpawnTime;
            public Vector3 WorldPos;
            public float Damage;
            public bool IsHeadshot;
            public bool IsKill;
            public float AbsorptionRatio; // 0 = full pen, 1 = full absorption
            public Vector2 FlyDir; // screen-space direction (normalized)
            public float FlyAngle; // for scatter: random angle
        }

        readonly List<DamagePopup> _popups = new();
        GUIStyle _style;

        void LateUpdate()
        {
            if (!DevCheats.DmgNumEnabled) return;

            var session = App.Instance?.RaidSession;
            if (session == null) return;

            var cam = Camera.main;

            var events = session.ConsumeEvents();
            foreach (var e in events.All)
            {
                if (e.Type != RaidEventType.DamageNumber) continue;

                // Compute screen-space fly direction from bullet world direction
                var flyDir = Vector2.up; // default: float up
                if (cam != null && e.Direction.sqrMagnitude > 0.001f)
                {
                    // Project bullet direction endpoint to screen, get screen-space dir
                    var hitScreen = cam.WorldToScreenPoint(e.Position);
                    var aheadScreen = cam.WorldToScreenPoint(e.Position + e.Direction);
                    var screenDir = new Vector2(aheadScreen.x - hitScreen.x, aheadScreen.y - hitScreen.y);
                    if (screenDir.sqrMagnitude > 0.001f)
                        flyDir = screenDir.normalized;
                }

                // Add random angular spread
                float spreadDeg = Random.Range(-DevCheats.DmgNumRandomSpread, DevCheats.DmgNumRandomSpread);

                _popups.Add(new DamagePopup
                {
                    SpawnTime = Time.time,
                    WorldPos = e.Position,
                    Damage = e.Damage,
                    IsHeadshot = e.CurrentHp > 0.5f,
                    IsKill = e.MaxHp > 0.5f,
                    AbsorptionRatio = e.Id.Value / 1000f, // unpacked from RaidEvent.Id
                    FlyDir = flyDir,
                    FlyAngle = spreadDeg,
                });
            }
        }

        void OnGUI()
        {
            if (!DevCheats.DmgNumEnabled || _popups.Count == 0) return;

            var cam = Camera.main;
            if (cam == null) return;

            EnsureStyle();

            float duration = DevCheats.DmgNumDuration;
            float popDur = DevCheats.DmgNumPopDuration;
            float popOver = DevCheats.DmgNumPopOvershoot;
            float flySpeed = DevCheats.DmgNumFlySpeed;
            float gravity = DevCheats.DmgNumGravityAccel;
            float baseFontSize = DevCheats.DmgNumBaseFontSize;
            float scaleFactor = DevCheats.DmgNumDamageScaleFactor;
            int mode = DevCheats.DmgNumTrajectoryMode;

            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                var p = _popups[i];
                float age = Time.time - p.SpawnTime;

                if (age > duration)
                {
                    _popups.RemoveAt(i);
                    continue;
                }

                // World → Screen → GUI (base position)
                var sp = cam.WorldToScreenPoint(p.WorldPos);
                if (sp.z < 0f) continue;

                float guiX = sp.x;
                float guiY = Screen.height - sp.y;

                // Movement starts after pop phase
                float moveAge = Mathf.Max(0f, age - popDur * 2f);

                // Compute trajectory offset based on mode
                Vector2 offset = ComputeTrajectoryOffset(mode, p.FlyDir, p.FlyAngle, moveAge, flySpeed, gravity);

                // GUI Y is inverted (up = negative)
                guiX += offset.x;
                guiY -= offset.y;

                // Bounce/Pop scale
                float scale;
                if (age < popDur)
                {
                    float t = age / popDur;
                    scale = Mathf.Lerp(0f, popOver, t);
                }
                else if (age < popDur * 2f)
                {
                    float t = (age - popDur) / popDur;
                    scale = Mathf.Lerp(popOver, 1f, t);
                }
                else
                {
                    scale = 1f;
                }

                // Fade out in last 40% of lifetime
                float fadeStart = duration * 0.6f;
                float alpha = age > fadeStart
                    ? 1f - (age - fadeStart) / (duration - fadeStart)
                    : 1f;

                // Color per type
                Color color;
                if (p.IsKill) color = DevCheats.DmgNumKillColor;
                else if (p.IsHeadshot) color = DevCheats.DmgNumHeadshotColor;
                else
                {
                    // Blend normal → armor gray by absorption ratio
                    color = Color.Lerp(DevCheats.DmgNumNormalColor, DevCheats.DmgNumArmorAbsorbColor, p.AbsorptionRatio);
                }
                color.a *= alpha;

                // Font size
                float fontSize = baseFontSize * Mathf.Sqrt(Mathf.Max(1f, p.Damage) / scaleFactor) * scale;
                fontSize = Mathf.Max(8f, fontSize);

                _style.fontSize = Mathf.RoundToInt(fontSize);

                string text = Mathf.RoundToInt(p.Damage).ToString();
                var size = _style.CalcSize(new GUIContent(text));
                var rect = new Rect(guiX - size.x * 0.5f, guiY - size.y * 0.5f, size.x, size.y);

                // Dark outline
                _style.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.8f);
                float o = 1.5f;
                GUI.Label(new Rect(rect.x - o, rect.y, rect.width, rect.height), text, _style);
                GUI.Label(new Rect(rect.x + o, rect.y, rect.width, rect.height), text, _style);
                GUI.Label(new Rect(rect.x, rect.y - o, rect.width, rect.height), text, _style);
                GUI.Label(new Rect(rect.x, rect.y + o, rect.width, rect.height), text, _style);

                // Main text
                _style.normal.textColor = color;
                GUI.Label(rect, text, _style);
            }
        }

        static Vector2 ComputeTrajectoryOffset(int mode, Vector2 flyDir, float spreadAngle, float t, float speed, float gravity)
        {
            switch (mode)
            {
                case 0: // FloatUp — straight up
                    return new Vector2(0f, t * speed);

                case 1: // Knockback — along bullet direction (away from shooter) + spread
                {
                    var dir = RotateDir(flyDir, spreadAngle);
                    return dir * (t * speed);
                }

                case 2: // ArcGravity — launch along bullet direction, then gravity pulls down
                {
                    var dir = RotateDir(flyDir, spreadAngle);
                    float x = dir.x * t * speed;
                    float y = dir.y * t * speed - 0.5f * gravity * t * t;
                    return new Vector2(x, y);
                }

                case 3: // Scatter — random direction
                {
                    float angle = spreadAngle * 6f; // wider spread for full scatter
                    var dir = RotateDir(Vector2.up, angle);
                    return dir * (t * speed);
                }

                default:
                    return new Vector2(0f, t * speed);
            }
        }

        static Vector2 RotateDir(Vector2 dir, float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(dir.x * cos - dir.y * sin, dir.x * sin + dir.y * cos);
        }

        void EnsureStyle()
        {
            if (_style != null) return;
            _style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
            };
        }
    }
}
