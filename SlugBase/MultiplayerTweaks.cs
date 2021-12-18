using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SlugBase
{
    // Various mod-agnostic tweaks to multiplayer modes
    internal static class MultiplayerTweaks
    {
        public static void ApplyHooks()
        {
            On.Player.ctor += Player_ctor;
        }

        // Change the slugcat character of players 2 and on if they would look the same as player 1
        private static void Player_ctor(On.Player.orig_ctor orig, Player self, AbstractCreature abstractCreature, World world)
        {
            orig(self, abstractCreature, world);
            var state = self.playerState;
            if (!state.isGhost
                && state.playerNumber > 0
                && state.slugcatCharacter == (int)self.slugcatStats.name
                && PlayerManager.GetCustomPlayer(state.slugcatCharacter) != null)
            {
                state.slugcatCharacter = state.playerNumber;
            }
        }
    }
}
