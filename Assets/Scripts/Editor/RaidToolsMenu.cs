using Save;
using UnityEditor;

namespace Editor
{
    public static class RaidToolsMenu
    {
        // EditorPref key — also referenced (string-literal duplicated) у App.Initialize
        // to honour the toggle on Play Mode entry. Default ON for dev convenience —
        // every Play Mode start gets a fresh player; toggle off if explicitly testing
        // save persistence.
        public const string RemoveSaveOnStartPrefKey = "ExtractionRaid.RemoveSaveOnStart";
        const string RemoveSaveOnStartMenuPath = "Raid/Remove Save On Start";

        [MenuItem("Raid/Delete Save")]
        static void DeleteSave()
        {
            if (!SaveManager.HasSave())
            {
                EditorUtility.DisplayDialog("Delete Save", "No save file found.", "OK");
                return;
            }

            if (EditorUtility.DisplayDialog("Delete Save",
                    "Are you sure you want to delete the save file?", "Delete", "Cancel"))
            {
                SaveManager.Delete();
            }
        }

        [MenuItem(RemoveSaveOnStartMenuPath)]
        static void ToggleRemoveSaveOnStart()
        {
            bool current = EditorPrefs.GetBool(RemoveSaveOnStartPrefKey, true);
            EditorPrefs.SetBool(RemoveSaveOnStartPrefKey, !current);
            Menu.SetChecked(RemoveSaveOnStartMenuPath, !current);
        }

        [MenuItem(RemoveSaveOnStartMenuPath, true)]
        static bool ToggleRemoveSaveOnStartValidate()
        {
            Menu.SetChecked(RemoveSaveOnStartMenuPath,
                EditorPrefs.GetBool(RemoveSaveOnStartPrefKey, true));
            return true;
        }
    }
}
