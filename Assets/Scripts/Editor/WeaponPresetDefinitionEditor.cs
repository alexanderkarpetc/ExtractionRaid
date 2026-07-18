using System;
using System.Collections.Generic;
using System.Linq;
using State;
using Systems;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    /// <summary>
    /// Custom inspector for <see cref="WeaponPresetDefinition"/>. Instead of a raw
    /// attachment list (which lets you add two grips, a locked slot, or an incompatible
    /// mod), it presents attachments <b>per slot</b> — mirroring the Weapon Builder's
    /// install rules:
    ///   • one mod per slot (structural: one popup per slot);
    ///   • only mods whose <see cref="AttachmentDefinition.CompatibleArchetype"/> matches
    ///     the chosen payload/delivery are offered (see <see cref="AttachmentInstallSystem.ArchetypeMatches"/>);
    ///   • slots locked at the current core rarity (<see cref="AttachmentSlots"/>) are
    ///     shown disabled.
    /// The underlying storage stays a <c>List&lt;AttachmentDefinition&gt;</c>; this editor
    /// keeps it canonical (one entry per unlocked slot).
    /// </summary>
    [CustomEditor(typeof(WeaponPresetDefinition))]
    public class WeaponPresetDefinitionEditor : UnityEditor.Editor
    {
        SerializedProperty _presetName, _weaponDefinitionId;
        SerializedProperty _payload, _payloadRarity;
        SerializedProperty _delivery, _deliveryRarity;
        SerializedProperty _exotic, _attachments;

        // All AttachmentDefinition assets, loaded once per selection. Cheap enough for an
        // inspector; refreshed on enable.
        AttachmentDefinition[] _allAttachments;

        void OnEnable()
        {
            _presetName         = serializedObject.FindProperty("_presetName");
            _weaponDefinitionId = serializedObject.FindProperty("_weaponDefinitionId");
            _payload            = serializedObject.FindProperty("_payload");
            _payloadRarity      = serializedObject.FindProperty("_payloadRarity");
            _delivery           = serializedObject.FindProperty("_delivery");
            _deliveryRarity     = serializedObject.FindProperty("_deliveryRarity");
            _exotic             = serializedObject.FindProperty("_exotic");
            _attachments        = serializedObject.FindProperty("_attachments");
            _allAttachments     = LoadAllAttachments();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_presetName);
            EditorGUILayout.PropertyField(_weaponDefinitionId);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Payload — required (what it fires)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_payload);
            EditorGUILayout.PropertyField(_payloadRarity);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Delivery — required (how it fires)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_delivery);
            EditorGUILayout.PropertyField(_deliveryRarity);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Exotic — optional", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_exotic);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Attachments — one per unlocked slot", EditorStyles.boldLabel);
            DrawAttachments();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawAttachments()
        {
            var payload  = (PayloadCoreDefinition)_payload.objectReferenceValue;
            var delivery = (DeliveryCoreDefinition)_delivery.objectReferenceValue;

            if (payload == null || delivery == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign both a Payload and a Delivery core to configure attachments — " +
                    "slots and compatibility depend on them.",
                    MessageType.Info);
                return;
            }

            // RarityTier is contiguous from 0, so enumValueIndex == (int)value.
            var payloadRarity  = (RarityTier)_payloadRarity.enumValueIndex;
            var deliveryRarity = (RarityTier)_deliveryRarity.enumValueIndex;
            int payloadSlots   = AttachmentSlots.UnlockedPayloadCount(payloadRarity);
            int deliverySlots  = AttachmentSlots.UnlockedDeliveryCount(deliveryRarity);

            // Current slot → chosen definition, read from the stored list.
            var current = ReadSlotMap();
            // Selections we'll persist — starts from current, edited by the popups below,
            // and finally filtered to unlocked slots on write.
            var next = new Dictionary<AttachmentSlot, AttachmentDefinition>(current);

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Payload slots (Optic → Magazine → Buttstock)", EditorStyles.miniBoldLabel);
            for (int i = 0; i < AttachmentSlots.PayloadOrder.Length; i++)
                DrawSlotRow(AttachmentSlots.PayloadOrder[i], i < payloadSlots, payload, delivery, next);

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Delivery slots (Muzzle → Grip)", EditorStyles.miniBoldLabel);
            for (int i = 0; i < AttachmentSlots.DeliveryOrder.Length; i++)
                DrawSlotRow(AttachmentSlots.DeliveryOrder[i], i < deliverySlots, payload, delivery, next);

            bool edited = EditorGUI.EndChangeCheck();

            // Canonical desired list: unlocked slots only, in slot order. Filtering here
            // also drops entries whose slot has since locked (rarity lowered).
            var desired = new List<AttachmentDefinition>();
            AddIfSet(desired, next, AttachmentSlots.PayloadOrder,  payloadSlots);
            AddIfSet(desired, next, AttachmentSlots.DeliveryOrder, deliverySlots);

            if (edited || !SameAsStored(desired))
                WriteList(desired);
        }

        void DrawSlotRow(AttachmentSlot slot, bool unlocked,
            PayloadCoreDefinition payload, DeliveryCoreDefinition delivery,
            Dictionary<AttachmentSlot, AttachmentDefinition> selections)
        {
            if (!unlocked)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.LabelField(slot.ToString(), "🔒 locked at this rarity");
                return;
            }

            // Compatible mods for this slot: right slot category + archetype match.
            var options = _allAttachments
                .Where(a => a != null && a.Slot == slot && ArchetypeMatches(a, payload, delivery))
                .OrderBy(a => a.DisplayName ?? a.name)
                .ToList();

            var labels = new List<string> { "(None)" };
            labels.AddRange(options.Select(a => string.IsNullOrEmpty(a.DisplayName) ? a.name : a.DisplayName));

            selections.TryGetValue(slot, out var chosen);
            int index = chosen != null ? options.IndexOf(chosen) + 1 : 0;
            // A previously-chosen mod that no longer matches (archetype/slot changed) shows
            // as a distinct entry so it's visible rather than silently dropped.
            if (chosen != null && index == 0)
            {
                labels.Add($"⚠ {chosen.name} (incompatible)");
                index = labels.Count - 1;
            }

            int newIndex = EditorGUILayout.Popup(slot.ToString(), index, labels.ToArray());
            if (newIndex == index) return;

            if (newIndex == 0)
                selections.Remove(slot);
            else if (newIndex - 1 < options.Count)
                selections[slot] = options[newIndex - 1];
            // Selecting the trailing "incompatible" sentinel is a no-op (keeps current).
        }

        // Mirrors AttachmentInstallSystem.ArchetypeMatches, resolving cores directly from
        // the preset's asset references (no runtime registry available in-editor).
        static bool ArchetypeMatches(AttachmentDefinition mod,
            PayloadCoreDefinition payload, DeliveryCoreDefinition delivery)
        {
            var token = mod.CompatibleArchetype;
            if (string.IsNullOrEmpty(token)) return true; // universal
            if (payload != null && string.Equals(token, payload.Archetype, StringComparison.OrdinalIgnoreCase))
                return true;
            if (delivery != null)
            {
                if (string.Equals(token, delivery.FormFactor, StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(token, delivery.Pattern.ToString(), StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        Dictionary<AttachmentSlot, AttachmentDefinition> ReadSlotMap()
        {
            var map = new Dictionary<AttachmentSlot, AttachmentDefinition>();
            for (int i = 0; i < _attachments.arraySize; i++)
            {
                var def = _attachments.GetArrayElementAtIndex(i).objectReferenceValue as AttachmentDefinition;
                if (def == null) continue;
                map[def.Slot] = def; // first-per-slot wins; later dupes overwrite harmlessly
            }
            return map;
        }

        static void AddIfSet(List<AttachmentDefinition> dest,
            Dictionary<AttachmentSlot, AttachmentDefinition> map, AttachmentSlot[] order, int unlocked)
        {
            for (int i = 0; i < order.Length && i < unlocked; i++)
                if (map.TryGetValue(order[i], out var def) && def != null)
                    dest.Add(def);
        }

        bool SameAsStored(List<AttachmentDefinition> desired)
        {
            if (_attachments.arraySize != desired.Count) return false;
            for (int i = 0; i < desired.Count; i++)
                if (_attachments.GetArrayElementAtIndex(i).objectReferenceValue != desired[i])
                    return false;
            return true;
        }

        void WriteList(List<AttachmentDefinition> defs)
        {
            _attachments.arraySize = defs.Count;
            for (int i = 0; i < defs.Count; i++)
                _attachments.GetArrayElementAtIndex(i).objectReferenceValue = defs[i];
        }

        static AttachmentDefinition[] LoadAllAttachments()
        {
            return AssetDatabase.FindAssets("t:AttachmentDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<AttachmentDefinition>)
                .Where(a => a != null)
                .ToArray();
        }
    }
}
