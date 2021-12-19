using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using RWCustom;

namespace SlugBase.Compatibility
{
    internal static class FlatmodeFix
    {
        public static void Apply()
        {
            On.Menu.SleepAndDeathScreen.Update += SleepAndDeathScreen_Update;
        }

        private static void SleepAndDeathScreen_Update(On.Menu.SleepAndDeathScreen.orig_Update orig, Menu.SleepAndDeathScreen self)
        {
            // Catch exception thrown by the vanilla game trying to access depth illustrations
			try
            {
                orig(self);
            }
            catch(ArgumentOutOfRangeException)
            {
                foreach(var illust in self.scene.depthIllustrations)
                    illust.setAlpha = new float?(Mathf.Lerp(0.85f, 0.4f, self.fadeOutIllustration));
            }
		}
    }
}
