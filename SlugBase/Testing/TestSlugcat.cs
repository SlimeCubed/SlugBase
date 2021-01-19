using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SlugBase
{
    internal class TestSlugcatMod
    {
        public static bool enabled = false;

        public static void Register()
        {
            On.Player.Jump += Player_Jump;

            PlayerManager.RegisterPlayer(new TestSlugcat());
        }

        private static void Player_Jump(On.Player.orig_Jump orig, Player self)
        {
            orig(self);
            if (enabled)
                self.jumpBoost += 3f;
        }
    }

    internal class TestSlugcat : CustomPlayer
    {
        public TestSlugcat() : base("Firecat", PlayerFormatVersion.V_0_1, 0)
        {
            DevMode = true;
        }

        public override Color? SlugcatColor()
        {
            return Color.red;
        }

        protected internal override void GetStats(SlugcatStats stats)
        {
            stats.runspeedFac *= 1.5f;
            stats.poleClimbSpeedFac *= 1.5f;
            stats.corridorClimbSpeedFac *= 1.5f;
            stats.loudnessFac *= 2f;
            stats.name = (SlugcatStats.Name)slugcatIndex;
            stats.bodyWeightFac *= 0.75f;
        }

        protected internal override void Enable()
        {
            TestSlugcatMod.enabled = true;
        }

        protected internal override void Disable()
        {
            TestSlugcatMod.enabled = false;
        }

        public override string DisplayName => "Test Slugcat";
        public override string Description => 
@"This is a test of a framework I am making for adding in new slugcats. This screen
only exists for demonstration purposes.";
    }
}
