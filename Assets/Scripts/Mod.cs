using System;
using Assets.Scripts.DevConsole;
using ModApi.Common.Events;
using ModApi.Mods;
using ModApi.Scenes.Events;
using ModApi.Ui;
using UnityEngine;

namespace Assets.Scripts {
    /// <summary>
    /// A singleton object representing this mod that is instantiated and initialize when the mod is loaded.
    /// </summary>
    public class Mod : GameMod {
        /// <summary>
        /// Gets the singleton instance of the mod object.
        /// </summary>
        /// <value>The singleton instance of the mod object.</value>
        public static Mod Instance { get; } = GetModInstance<Mod>();

        /// <summary>
        /// Prevents a default instance of the <see cref="Mod"/> class from being created.
        /// </summary>
        private Mod() : base() {
        }

        public override void OnModLoaded() {
            base.OnModLoaded();

#if DEBUG
            if (!DevConsoleManagerScript.OpenedThisSession) {
                Game.Instance.DevConsole.OpenConsole();
            }

            // For Testing
            // Screen.SetResolution(3840, 2160, Screen.fullScreen, Screen.currentResolution.refreshRate);
#endif

            if (Game.InMenuScene) {
                SceneManagerOnSceneLoaded(this, new SceneTransitionEventArgs(null, "Menu"));
            } else {
                // We have to use TransitionCompleted not Loaded because of SceneManager.HackFixResolution ಠ_ಠ
                Game.Instance.SceneManager.SceneTransitionCompleted += SceneManagerOnSceneLoaded;
            }
        }

        private void SceneManagerOnSceneLoaded(object sender, SceneTransitionEventArgs e) {
            if (Game.InMenuScene) {
                // Run once
                Game.Instance.SceneManager.SceneTransitionCompleted -= SceneManagerOnSceneLoaded;

                if (!Screen.fullScreen) {
                    Debug.Log("Not in fullscreen mode.");
                    return;
                }

                var oldResolution = Screen.currentResolution;
                Resolution? bestMatch = null;
                foreach (var resolution in Screen.resolutions) {
                    if (!bestMatch.HasValue) {
                        bestMatch = resolution;
                    } else {
                        var previousDelta = Math.Abs(oldResolution.width - bestMatch.Value.width);
                        if (Math.Abs(oldResolution.width - bestMatch.Value.width) <= previousDelta) {
                            bestMatch = resolution;
                        }
                    }
                }

                if (bestMatch.HasValue &&
                    (oldResolution.width != bestMatch.Value.width || oldResolution.height != bestMatch.Value.height)) {
                    var newResolution = bestMatch.Value;

                    Debug.Log($"Change Resolution: {Screen.currentResolution} --> {newResolution}");
                    Screen.SetResolution(newResolution.width, newResolution.height, Screen.fullScreen, newResolution.refreshRate);

                    var resolutionConfirmationDialog =
                        Game.Instance.UserInterface.CreateMessageDialog(
                            MessageDialogType.OkayCancel,
                            Game.Instance.UserInterface.Transform,
                            fadeIn: true
                        );

                    void resetResolution() {
                        Debug.Log($"Reset Resolution: {Screen.currentResolution} --> {oldResolution}");
                        Screen.SetResolution(oldResolution.width, oldResolution.height, Screen.fullScreen, oldResolution.refreshRate);
                    }

                    var baseText =
                        $"Changed resolution to {newResolution.width} x {newResolution.height}\n How does this look?\n" +
                        $"Reverting resolution change in [t] seconds, just in case something went wrong.";
                    var endTime = Time.time + 10f;

                    resolutionConfirmationDialog.MessageText = baseText.Replace("[t]", (endTime - Time.time).ToString("0"));
                    resolutionConfirmationDialog.CancelClicked += dialog => {
                        resetResolution();
                        dialog.Close();
                    };
                    resolutionConfirmationDialog.OkayClicked += dialog => {
                        var resolutionSetting = Game.Instance.Settings.Quality.Display.Resolution;
                        resolutionSetting.Value = newResolution;
                        resolutionSetting.CommitChanges();
                        Game.Instance.Settings.Save();

                        dialog.Close();
                    };

                    UnityEventDispatcher.Instance.ExecuteCustomYield(
                        () => {
                            if (resolutionConfirmationDialog == null) {
                                return false;
                            }

                            resolutionConfirmationDialog.MessageText = baseText.Replace("[t]", (endTime - Time.time).ToString("0"));
                            return Time.time < endTime;
                        },
                        () => {
                            if (!(resolutionConfirmationDialog != null)) {
                                return;
                            }

                            // Timeout
                            resetResolution();
                            resolutionConfirmationDialog.Close();
                        }
                    );
                }
            }
        }
    }
}
