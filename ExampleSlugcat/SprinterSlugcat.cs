using SlugBase;
using UnityEngine;

namespace ExampleSlugcat
{
    // Describes the character you want to add
    internal class SprinterSlugcat : SlugBaseCharacter
    {
        public SprinterSlugcat() : base("Sprinter", FormatVersion.V1) { }

        // Custom //

        // Hooks are applied here
        protected override void Enable()
        {
            On.Player.MovementUpdate += Player_MovementUpdate;
            On.Player.ObjectEaten += Player_ObjectEaten;
            On.Room.Loaded += Room_Loaded;
            On.Player.Jump += Player_Jump;
        }

        // Hooks are disposed of here
        protected override void Disable()
        {
            On.Player.MovementUpdate -= Player_MovementUpdate;
            On.Player.ObjectEaten -= Player_ObjectEaten;
            On.Room.Loaded -= Room_Loaded;
            On.Player.Jump -= Player_Jump;
        }

        // Attach some extra information to the Sprinter's save file
        public override CustomSaveState CreateNewSave(PlayerProgression progression)
        {
            return new SprinterSaveState(progression, this);
        }

        // Update stats when in turbo move
        private void Player_MovementUpdate(On.Player.orig_MovementUpdate orig, Player self, bool eu)
        {
            if (self.room.world.game.GetStorySession?.saveState is SprinterSaveState css && css.isTurbo)
                self.slugcatStats.runspeedFac = 5f;
            orig(self, eu);
        }

        // Go absolutely wild once a mushroom is eaten
        private void Player_ObjectEaten(On.Player.orig_ObjectEaten orig, Player self, IPlayerEdible edible)
        {
            if (edible is Mushroom && self.room.world.game.GetStorySession?.saveState is SprinterSaveState css)
            {
                css.isTurbo = true;
            }
            orig(self, edible);
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
            if (self.room.world.game.GetStorySession?.saveState is SprinterSaveState css && css.isTurbo)
                self.jumpBoost += 9f;
            else
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
}
