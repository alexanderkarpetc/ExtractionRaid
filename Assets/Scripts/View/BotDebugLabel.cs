using System.Text;
using Dev;
using State;
using UnityEngine;

namespace View
{
    public class BotDebugLabel : MonoBehaviour
    {
        const float VerticalOffset = 2.8f;
        const float CharSize = 0.08f;
        const int FontSize = 32;

        TextMesh _textMesh;
        MeshRenderer _renderer;
        readonly StringBuilder _sb = new(64);

        public static BotDebugLabel Create(Transform parent)
        {
            var go = new GameObject("DebugLabel");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, VerticalOffset, 0f);

            var label = go.AddComponent<BotDebugLabel>();

            label._textMesh = go.AddComponent<TextMesh>();
            label._textMesh.alignment = TextAlignment.Center;
            label._textMesh.anchor = TextAnchor.LowerCenter;
            label._textMesh.characterSize = CharSize;
            label._textMesh.fontSize = FontSize;
            label._textMesh.color = Color.white;

            label._renderer = go.GetComponent<MeshRenderer>();

            return label;
        }

        public void UpdateLabel(BotEntityState bot, float currentHp, float maxHp)
        {
            var dbg       = ViewCheats.Config?.BotDebug;
            bool showHp     = dbg != null && dbg.ShowHpText;
            bool showStatus = dbg != null && dbg.ShowStatus;

            // Defensive corpse-hide: BotView shell should die with the bot, but if a
            // path drops out (ragdoll disabled, despawn order edge case) the label
            // would otherwise freeze on the corpse with stale HP. 0 HP → never show.
            bool dead   = currentHp <= 0f;
            bool fovOff = DevCheats.FOVEnabled && !DevCheats.ForceShowAllBots && !bot.IsVisibleToPlayer;

            bool hidden = dead || fovOff || (!showHp && !showStatus);

            if (hidden)
            {
                if (_renderer.enabled) _renderer.enabled = false;
                return;
            }

            if (!_renderer.enabled) _renderer.enabled = true;

            _sb.Clear();
            if (showStatus)
            {
                var bb = bot.Blackboard;
                var status = bb.DebugStatus ?? "Idle";
                _sb.Append('[').Append(bot.TypeId).Append("] ").Append(status);
                if (bb.CanSeeTarget) _sb.Append(" [SEE]");
                if (bb.HasTarget) _sb.Append("  Dist: ").Append(bb.DistanceToTarget.ToString("F1"));
                if (showHp) _sb.Append('\n');
            }
            if (showHp)
            {
                _sb.Append("HP: ").Append(currentHp.ToString("F0"))
                   .Append('/').Append(maxHp.ToString("F0"));
            }

            _textMesh.text = _sb.ToString();
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            transform.rotation = cam.transform.rotation;
        }
    }
}
