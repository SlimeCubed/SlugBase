using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Menu;
using MonoMod.RuntimeDetour;
using RWCustom;
using System.Reflection;

namespace SlugBase
{
    using static CustomSceneManager;

    internal static class ShelterScreens
    {
		private static Dictionary<SleepAndDeathScreen, Action> updateDelegates = new Dictionary<SleepAndDeathScreen, Action>();

        public static void ApplyHooks()
        {
            On.MainLoopProcess.ShutDownProcess += MainLoopProcess_ShutDownProcess;
            On.Menu.SleepAndDeathScreen.AddBkgIllustration += SleepAndDeathScreen_AddBkgIllustration;
            On.Menu.SleepAndDeathScreen.Update += SleepAndDeathScreen_Update;
        }

        private static void MainLoopProcess_ShutDownProcess(On.MainLoopProcess.orig_ShutDownProcess orig, MainLoopProcess self)
        {
			orig(self);
			updateDelegates.Clear();
        }

        private static void SleepAndDeathScreen_Update(On.Menu.SleepAndDeathScreen.orig_Update orig, SleepAndDeathScreen self)
        {
            if(self.scene.sceneFolder != resourceFolderName)
            {
                orig(self);
                return;
            }

			if (self.starvedWarningCounter >= 0)
			{
				self.starvedWarningCounter++;
			}

			// base.Update();
			if(!updateDelegates.TryGetValue(self, out Action baseUpdate))
            {
				MethodInfo m = typeof(KarmaLadderScreen).GetMethod("Update", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				baseUpdate = (Action)Activator.CreateInstance(typeof(Action), self, m.MethodHandle.GetFunctionPointer());
				updateDelegates[self] = baseUpdate;
            }
			baseUpdate();

			if (self.exitButton != null)
			{
				self.exitButton.buttonBehav.greyedOut = self.ButtonsGreyedOut;
			}
			if (self.passageButton != null)
			{
				self.passageButton.buttonBehav.greyedOut = (self.ButtonsGreyedOut || self.goalMalnourished);
				self.passageButton.black = Mathf.Max(0f, self.passageButton.black - 0.0125f);
			}
			if (self.endGameSceneCounter >= 0)
			{
				self.endGameSceneCounter++;
				if (self.endGameSceneCounter > 140)
				{
					self.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.CustomEndGameScreen);
				}
			}
			if (self.RevealMap)
			{
				self.fadeOutIllustration = Custom.LerpAndTick(self.fadeOutIllustration, 1f, 0.02f, 0.025f);
			}
			else
			{
				self.fadeOutIllustration = Custom.LerpAndTick(self.fadeOutIllustration, 0f, 0.02f, 0.025f);
			}

			for(int i = 0; i < self.scene.subObjects.Count; i++)
            {
				ImageSettings settings = null;
				
				if (!(self.scene.subObjects[i] is MenuIllustration illust)) continue;
				if(customRep.TryGet(illust, out SceneImage csi)) {
					settings = csi.GetTempProperty<ImageSettings>("ShelterSettings");
                }

				illust.setAlpha = Mathf.Lerp(settings?.baseAlpha ?? 1f, settings?.fadeAlpha ?? 1f, self.fadeOutIllustration);
			}
		}

        private static void SleepAndDeathScreen_AddBkgIllustration(On.Menu.SleepAndDeathScreen.orig_AddBkgIllustration orig, SleepAndDeathScreen self)
        {
            orig(self);
			for(int i = 0; i < self.scene.subObjects.Count; i++)
            {
				if (!(self.scene.subObjects[i] is MenuIllustration illust)) continue;

				if (!customRep.TryGet(illust, out SceneImage csi)) continue;

				ImageSettings settings = new ImageSettings();
				settings.baseAlpha = illust.setAlpha ?? illust.alpha;
				settings.fadeAlpha = settings.baseAlpha;

				// Fade property
				// The image's alpha will lerp to this when the map is open
				settings.fadeAlpha = csi.GetProperty<float?>("FADE") ?? settings.baseAlpha;

				csi.SetTempProperty("ShelterSettings", settings);
            }
        }

		private class ImageSettings
		{
			public float baseAlpha;
			public float fadeAlpha;
		}
	}
}
