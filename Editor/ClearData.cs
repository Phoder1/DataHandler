using UnityEditor;
using UnityEngine;

namespace DataSaving
{
    public static class DataHandlerMenuItems
    {
        [MenuItem("Phoder1/DataHandler/Clear Data")]
        public static void ClearData()
        {
            DataHandler.ClearAllSavedData();
        }
        [MenuItem("Phoder1/DataHandler/Open saves directory")]
        public static void OpenSavesDirectory()
        {
            var directoryPath = DataHandler.DirectoryPath;
            string path;
            if (DataHandler.DirectoryExists(directoryPath))
                path = directoryPath;
            else
                path = Application.persistentDataPath;

            path = path.Replace(@"/", @"\");
            System.Diagnostics.Process.Start("explorer.exe", "/open," + path);
        }
    }
}
