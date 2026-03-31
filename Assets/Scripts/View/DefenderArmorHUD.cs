using Adapters;
using Dev;
using State;
using UnityEngine;

namespace View
{
    /// <summary>
    /// IMGUI overlay showing player's equipped armor status in bottom-left corner.
    /// Two stacked bars: helmet (top) and body armor (bottom).
    /// Color-coded by durability zone, flashes on damage, shows "BROKEN" on break.
    /// </summary>
    public class DefenderArmorHUD : MonoBehaviour
    {
        Texture2D _pixelTex;
        GUIStyle _labelStyle;
        GUIStyle _brokenStyle;

        // Track previous durability for flash-on-damage
        float _prevHelmetDur = -1f;
        float _prevVestDur = -1f;

        // Flash timers (countdown from FlashDuration to 0)
        float _helmetFlashTimer;
        float _vestFlashTimer;
        const float FlashDuration = 0.15f;

        // Broken timers (countdown from BrokenDuration to 0)
        float _helmetBrokenTimer;
        float _vestBrokenTimer;
        const float BrokenDuration = 1f;

        void Awake()
        {
            _pixelTex = MakeTex(Color.white);
        }

        void LateUpdate()
        {
            var session = App.App.Instance?.RaidSession;
            if (session == null) return;

            // Detect ArmorBroken events
            var events = session.ConsumeEvents();
            foreach (var e in events.All)
            {
                if (e.Type != RaidEventType.ArmorBroken) continue;

                var player = session.RaidState?.PlayerEntity;
                if (player == null || e.Id != player.Id) continue;

                bool isHelmet = e.Damage > 0.5f;
                if (isHelmet)
                    _helmetBrokenTimer = BrokenDuration;
                else
                    _vestBrokenTimer = BrokenDuration;
            }

            // Tick timers
            float dt = Time.deltaTime;
            if (_helmetFlashTimer > 0f) _helmetFlashTimer -= dt;
            if (_vestFlashTimer > 0f) _vestFlashTimer -= dt;
            if (_helmetBrokenTimer > 0f) _helmetBrokenTimer -= dt;
            if (_vestBrokenTimer > 0f) _vestBrokenTimer -= dt;
        }

        void OnGUI()
        {
            if (!DevCheats.ArmorHUDEnabled) return;

            var session = App.App.Instance?.RaidSession;
            if (session == null) return;

            var player = session.RaidState?.PlayerEntity;
            if (player == null) return;

            if (!session.RaidState.ArmorMap.TryGetValue(player.Id, out var armorSlots))
                return;

            if (armorSlots.Helmet == null && armorSlots.BodyArmor == null)
                return;

            EnsureStyles();

            float marginX = DevCheats.ArmorHUDMarginX;
            float marginY = DevCheats.ArmorHUDMarginY;
            float barW = DevCheats.ArmorHUDBarWidth;
            float barH = DevCheats.ArmorHUDBarHeight;
            float gap = 6f;
            float labelW = 30f;
            float statsW = 200f;

            // Position from top-left (below stamina bar)
            float curY = marginY;

            // Helmet bar (top)
            if (armorSlots.Helmet != null)
            {
                DrawArmorSlot(marginX, curY, labelW, barW, barH, statsW,
                    "H", armorSlots.Helmet,
                    ref _prevHelmetDur, ref _helmetFlashTimer, _helmetBrokenTimer);
                curY += barH + gap;
            }

            // Body armor bar (bottom)
            if (armorSlots.BodyArmor != null)
            {
                DrawArmorSlot(marginX, curY, labelW, barW, barH, statsW,
                    "V", armorSlots.BodyArmor,
                    ref _prevVestDur, ref _vestFlashTimer, _vestBrokenTimer);
            }
        }

        void DrawArmorSlot(float x, float y, float labelW, float barW, float barH, float statsW,
            string label, ArmorState armor,
            ref float prevDur, ref float flashTimer, float brokenTimer)
        {
            float durPercent = armor.DurabilityPercent;

            // Detect damage → trigger flash
            if (prevDur >= 0f && armor.CurrentDurability < prevDur - 0.01f)
                flashTimer = FlashDuration;
            prevDur = armor.CurrentDurability;

            // "BROKEN" overlay
            if (armor.IsBroken && brokenTimer > 0f)
            {
                float alpha = Mathf.Clamp01(brokenTimer / BrokenDuration);
                _brokenStyle.normal.textColor = new Color(1f, 0.2f, 0.15f, alpha);
                GUI.Label(new Rect(x, y, labelW + barW + statsW, barH), "BROKEN", _brokenStyle);
                return;
            }

            if (armor.IsBroken) return; // fully faded

            // Label "[H]" / "[V]"
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, labelW, barH), label, _labelStyle);

            float barX = x + labelW;

            // Bar background
            GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.7f);
            GUI.DrawTexture(new Rect(barX, y, barW, barH), _pixelTex);

            // Bar fill — color by durability zone
            Color fillColor;
            if (durPercent >= 0.7f)
                fillColor = new Color(0.2f, 0.85f, 0.2f, 0.9f);
            else if (durPercent >= 0.4f)
                fillColor = new Color(1f, 0.8f, 0f, 0.9f);
            else
            {
                // Low durability pulse
                float pulse = 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(Time.time * 3f));
                fillColor = new Color(0.9f, 0.2f, 0.15f, pulse);
            }

            GUI.color = fillColor;
            GUI.DrawTexture(new Rect(barX, y, barW * durPercent, barH), _pixelTex);

            // Flash overlay (white)
            if (flashTimer > 0f)
            {
                float flashAlpha = Mathf.Clamp01(flashTimer / FlashDuration) * 0.6f;
                GUI.color = new Color(1f, 1f, 1f, flashAlpha);
                GUI.DrawTexture(new Rect(barX, y, barW, barH), _pixelTex);
            }

            // Stats text: "65pts 75%"
            GUI.color = Color.white;
            string stats = $"{armor.ArmorPoints:0}pts {durPercent * 100f:0}%";
            GUI.Label(new Rect(barX + barW + 4f, y, statsW, barH), stats, _labelStyle);

            GUI.color = Color.white;
        }

        void EnsureStyles()
        {
            if (_labelStyle != null) return;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
            };
            _labelStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 0.9f);

            _brokenStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
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
            if (_pixelTex != null) Destroy(_pixelTex);
        }
    }
}
