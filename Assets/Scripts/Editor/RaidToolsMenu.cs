using Save;
using UnityEditor;

namespace Editor
{
    public static class RaidToolsMenu
    {
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
    }
}
