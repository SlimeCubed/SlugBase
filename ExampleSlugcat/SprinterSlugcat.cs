﻿using UnityEngine;
using SlugBase;

/*
 * This is a basic example of a SlugBase character.
 * 
 * It adds The Sprinter, a slugcat with faster movement and higher jumps.
 * Some scenes have been overridden so then Sprinter appears instead of Survivor.
 * To install, copy ExampleSlugcat.dll and SlugBase into the Mods folder.
 */

namespace ExampleSlugcat
{
    // Your mod class
    // This does not have to be a PartialityMod
    internal class ExampleSlugcatMod : Partiality.Modloader.PartialityMod
    {
        public ExampleSlugcatMod()
        {
            ModID = "Example Slugcat";
            Version = "1.0";
            author = "Slime_Cubed";
        }

        public override void OnEnable()
        {
            PlayerManager.RegisterCharacter(new SprinterSlugcat());
        }
    }
    
    // Describes the character you want to add
    internal class SprinterSlugcat : SlugBaseCharacter
    {
        public SprinterSlugcat() : base("Sprinter", FormatVersion.V1) { }

        // Custom //

        protected override void Enable()
        {
            // Hooks are applied here
            On.Room.Loaded += Room_Loaded;
            On.Player.Jump += Player_Jump;
        }

        protected override void Disable()
        {
            // Hooks are disposed of here
            On.Room.Loaded -= Room_Loaded;
            On.Player.Jump -= Player_Jump;
        }

        // Play a short "cutscene", forcing the player to climb a pole when starting a new game
        private void Room_Loaded(On.Room.orig_Loaded orig, Room self)
        {
            bool firstTimeRealized = self.abstractRoom.firstTimeRealized;
            orig(self);

            if (self.game == null) return;

            // Make sure this is the right room
            if (!self.game.IsStorySession) return;
            if (!firstTimeRealized) return;
            if (self.abstractRoom.name != StartRoom) return;
            if (self.game.GetStorySession.saveState.denPosition != StartRoom) return;

            self.AddObject(new SprinterStart(self));
        }

        // Add more height to all standard jumps
        private static void Player_Jump(On.Player.orig_Jump orig, Player self)
        {
            orig(self);
            self.jumpBoost += 3f;
        }


        // SlugBase //

        public override Color? SlugcatColor() => new Color(0.37f, 0.36f, 0.91f);

        public override bool HasGuideOverseer => false;

        public override string StartRoom => "UW_I01";

        protected override void GetStats(SlugcatStats stats)
        {
            stats.runspeedFac *= 1.5f;
            stats.poleClimbSpeedFac *= 1.5f;
            stats.corridorClimbSpeedFac *= 1.5f;
            stats.loudnessFac *= 2f;
        }

        public override void GetFoodMeter(out int maxFood, out int foodToSleep)
        {
            maxFood = 8;
            foodToSleep = 5;
        }

        public override string DisplayName => "The Sprinter";
        public override string Description =>
@"A lightspeed rodent whose supernatural speed stems from chillidogs and a curious glowing fungus.
This is an example slugcat for the SlugBase framework.";
    }


    // Plays a small "cutscene" at the start of the game
    internal class SprinterStart : UpdatableAndDeletable
    {
        private Player Sprinter => (room.game.Players.Count <= 0) ? null : (room.game.Players[0].realizedCreature as Player);
        private int timer = 0;
        private StartController startController;

        public SprinterStart(Room room)
        {
            this.room = room;
        }

        public override void Update(bool eu)
        {
            Player ply = Sprinter;
            if (ply == null) return;
            if (room.game.cameras[0].room != room) return;

            // Spawn the player at the correct place
            if (timer == 0)
            {
                room.game.cameras[0].MoveCamera(4);

                room.game.cameras[0].followAbstractCreature = null;

                if (room.game.cameras[0].hud == null)
                    room.game.cameras[0].FireUpSinglePlayerHUD(ply);

                for (int i = 0; i < 2; i++)
                {
                    ply.bodyChunks[i].HardSetPosition(room.MiddleOfTile(68, 30));
                }

                ply.graphicsModule?.Reset();

                startController = new StartController(this);
                ply.controller = startController;
                ply.playerState.foodInStomach = 5;

                room.game.cameras[0].hud.foodMeter.NewShowCount(ply.FoodInStomach);
                room.game.cameras[0].hud.foodMeter.visibleCounter = 0;
                room.game.cameras[0].hud.foodMeter.fade = 0f;
                room.game.cameras[0].hud.foodMeter.lastFade = 0f;
            }

            // End the cutscene
            if (timer == 180)
            {
                Debug.Log("Done!");
                ply.controller = null;
                ply.room.game.cameras[0].followAbstractCreature = ply.abstractCreature;
                Destroy();
            }

            timer++;
        }

        // Makes Sprinter climb a pole without player input
        public class StartController : Player.PlayerController
        {
            public SprinterStart owner;
            
            public StartController(SprinterStart owner)
            {
                this.owner = owner;
            }

            public override Player.InputPackage GetInput()
            {
                int y;
                if (owner.timer < 5) y = 1;
                if (owner.timer < 40) y = 0;
                else if (owner.timer < 170) y = 1;
                else y = 0;

                return new Player.InputPackage(false, 0, y, false, false, false, false, false);
            }
        }
    }
}
