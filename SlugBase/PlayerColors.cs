using System;
using UnityEngine;
using MonoMod.RuntimeDetour;

namespace SlugBase
{
    // PlayerGraphics.SlugcatColor is ambiguous when using multi-instance characters since different characters have different colors for each slugcat index
    // This class contains hooks that find the slugcat associated with calls to SlugcatColor to correct that ambiguity
    internal static class PlayerColors
    {
        internal static SlugBaseCharacter drawingCharacter;
        
        public static void ApplyHooks()
        {
            On.ArenaBehaviors.SandboxEditor.EditCursor.DrawSprites += (orig, self, a, b, c, d) => DrawingPlayer(self.room.game, self.playerNumber, () => orig(self, a, b, c, d));
            On.HUD.PlayerSpecificMultiplayerHud.Update += (orig, self) => DrawingPlayer(self.RealizedPlayer, () => orig(self));
            On.HUD.PlayerSpecificMultiplayerHud.ctor += (orig, self, a, b, absPlayer) => DrawingPlayer(absPlayer.realizedObject as Player, () => orig(self, a, b, absPlayer));
            On.HUD.PlayerSpecificMultiplayerHud.Update += (orig, self) => DrawingPlayer(self.RealizedPlayer, () => orig(self));
            On.HUD.PlayerSpecificMultiplayerHud.Draw += (orig, self, a) => DrawingPlayer(self.RealizedPlayer, () => orig(self, a));
            //On.Menu.PlayerJoinButton.GrafUpdate += (orig, self, a) => DrawingPlayer(null, () => orig(self, a));
            //On.Menu.PlayerResultBox.GrafUpdate += (orig, self, a) => DrawingPlayer(null, () => orig(self, a));
            On.Menu.SandboxEditorSelector.ButtonCursor.GrafUpdate += (orig, self, a) => DrawingPlayer(self.roomCursor.room.game, self.roomCursor.playerNumber, () => orig(self, a));
            On.Player.Update += (orig, self, a) => DrawingPlayer(self, () => orig(self, a));
            On.Player.ShortCutColor += (orig, self) => DrawingPlayer(self, () => orig(self));
            On.PlayerGraphics.ApplyPalette += (orig, self, a, b, c) => DrawingPlayer(self.player, () => orig(self, a, b, c));
            On.PlayerGraphics.DrawSprites += (orig, self, a, b, c, d) => DrawingPlayer(self.player, () => orig(self, a, b, c, d));
            On.PlayerGraphics.Update += (orig, self) => DrawingPlayer(self.player, () => orig(self));
            On.Player.ShortCutColor += (orig, self) => DrawingPlayer(self, () => orig(self));

            new Hook(
                typeof(OverseerGraphics).GetProperty(nameof(OverseerGraphics.MainColor)).GetGetMethod(),
                new Func<Func<OverseerGraphics, Color>, OverseerGraphics, Color>((orig, self) => {
                    if (self.overseer.editCursor != null)
                        return DrawingPlayer(self.overseer.room.game, self.overseer.editCursor.playerNumber, () => orig(self));
                    else
                        return orig(self);
                })
            );
        }

        internal static void DrawingPlayer(RainWorldGame game, int playerNumber, Action orig)
        {
            var lastDrawing = drawingCharacter;

            if (playerNumber >= 0 && playerNumber < game.Players.Count)
                drawingCharacter = PlayerManager.GetCustomPlayer(game.Players[playerNumber]?.realizedObject as Player);
            else if (game.IsArenaSession)
                drawingCharacter = ArenaAdditions.GetSelectedArenaCharacter(game.manager.arenaSetup, playerNumber).player;
            else
                drawingCharacter = PlayerManager.GetCustomPlayer(game);

            try
            {
                orig();
            }
            finally
            {
                drawingCharacter = lastDrawing;
            }
        }

        internal static T DrawingPlayer<T>(RainWorldGame game, int playerNumber, Func<T> orig)
        {
            var lastDrawing = drawingCharacter;

            if (playerNumber >= 0 && playerNumber < game.Players.Count)
                drawingCharacter = PlayerManager.GetCustomPlayer(game.Players[playerNumber]?.realizedObject as Player);
            else if (game.IsArenaSession)
                drawingCharacter = ArenaAdditions.GetSelectedArenaCharacter(game.manager.arenaSetup, playerNumber).player;
            else
                drawingCharacter = PlayerManager.GetCustomPlayer(game);

            try
            {
                return orig();
            }
            finally
            {
                drawingCharacter = lastDrawing;
            }
        }

        internal static void DrawingPlayer(Player player, Action orig)
        {
            var lastDrawing = drawingCharacter;
            if (player != null)
                drawingCharacter = PlayerManager.GetCustomPlayer(player);
            try
            {
                orig();
            }
            finally
            {
                drawingCharacter = lastDrawing;
            }
        }

        internal static T DrawingPlayer<T>(Player player, Func<T> orig)
        {
            var lastDrawing = drawingCharacter;
            if (player != null)
                drawingCharacter = PlayerManager.GetCustomPlayer(player);
            try
            {
                return orig();
            }
            finally
            {
                drawingCharacter = lastDrawing;
            }
        }
    }
}
