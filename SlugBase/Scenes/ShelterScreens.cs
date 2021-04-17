using System;
using System.Collections.Generic;
using UnityEngine;
using Menu;
using MonoMod.RuntimeDetour;
using RWCustom;
using System.Reflection;
using System.IO;

namespace SlugBase
{
    using static CustomSceneManager;

    internal static class ShelterScreens
    {
		private static Dictionary<SleepAndDeathScreen, Action> updateDelegates = new Dictionary<SleepAndDeathScreen, Action>();

        public static void ApplyHooks()
        {
            On.Menu.MenuScene.BuildScene += MenuScene_BuildScene;
            On.Menu.MenuScene.AddIllustration += MenuScene_AddIllustration;
            On.MainLoopProcess.ShutDownProcess += MainLoopProcess_ShutDownProcess;
            On.Menu.SleepAndDeathScreen.AddBkgIllustration += SleepAndDeathScreen_AddBkgIllustration;
            On.Menu.SleepAndDeathScreen.Update += SleepAndDeathScreen_Update;
        }

		// Image positions are loaded from positions.txt at the end of BuildScene
		// This gets around that by moving them after
        private static void MenuScene_BuildScene(On.Menu.MenuScene.orig_BuildScene orig, MenuScene self)
        {
			orig(self);
			if(moveImages.Count > 0)
            {
				foreach(var pair in moveImages)
                {
					pair.Key.lastPos = pair.Value;
					pair.Key.pos = pair.Value;
                }
				moveImages.Clear();
            }
        }

		// The default sleep screen is Hunter, which doesn't line up with the default select screen
		// Change Hunter to Survivor for the sleep screen
		private static List<KeyValuePair<MenuDepthIllustration, Vector2>> moveImages = new List<KeyValuePair<MenuDepthIllustration, Vector2>>();
        private static void MenuScene_AddIllustration(On.Menu.MenuScene.orig_AddIllustration orig, MenuScene self, MenuIllustration newIllu)
        {
			SlugBaseCharacter chara = PlayerManager.GetCustomPlayer(self.menu.manager.rainWorld.progression.miscProgressionData.currentlySelectedSinglePlayerSlugcat);
			if (newIllu.fileName == "Sleep - 2 - Red"
				&& chara != null
				&& !chara.HasScene("SleepScreen")
				&& ((self.menu as SleepAndDeathScreen)?.IsSleepScreen ?? false)
				&& newIllu is MenuDepthIllustration mdi)
			{
				string folder = string.Concat(new object[]
				{
					"Scenes",
					Path.DirectorySeparatorChar,
					"Sleep Screen - White",
				});
				newIllu.RemoveSprites();
				newIllu = new MenuDepthIllustration(newIllu.menu, newIllu.owner, folder, "Sleep - 2 - White", new Vector2(677f, 63f), mdi.depth, mdi.shader);
				moveImages.Add(new KeyValuePair<MenuDepthIllustration, Vector2>((MenuDepthIllustration)newIllu, new Vector2(677f, 63f)));
			}

			orig(self, newIllu);
        }

		// Plug a memory leak
        private static void MainLoopProcess_ShutDownProcess(On.MainLoopProcess.orig_ShutDownProcess orig, MainLoopProcess self)
        {
			orig(self);
			updateDelegates.Clear();
        }

		// The original method may crash when called, since it assumes that there are 3+ images in the scene
		// Replace the method entirely for modded slugcats
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

		// Parse FADE tag
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
