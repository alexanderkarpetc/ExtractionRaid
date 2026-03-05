using Cysharp.Threading.Tasks;
using UnityEngine;

namespace App
{
    public static class GameLauncher
    {
        public static async UniTaskVoid Launch(LaunchOptions options)
        {
            Debug.Log($"[GameLauncher] Launching with mode={options.Mode}, level={options.LevelId}");

            switch (options.Mode)
            {
                case LaunchMode.Menu:
                    Debug.Log("[GameLauncher] Menu mode — not yet implemented.");
                    break;

                case LaunchMode.Raid:
                    App.Instance.StartRaid(options.LevelId);
                    break;

                case LaunchMode.TestScenario:
                    App.Instance.StartRaid(options.LevelId);
                    break;
            }

            await UniTask.CompletedTask;
        }
    }
}
