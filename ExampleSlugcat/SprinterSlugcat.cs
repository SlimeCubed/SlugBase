using SlugBase;
using UnityEngine;

namespace ExampleSlugcat
{
    // Describes the character you want to add
    internal class SprinterSlugcat : SlugBaseCharacter
    {
        public SprinterSlugcat() : base("Sprinter", FormatVersion.V1, 0, true) { }

        // Custom //

        // Hooks are applied here
        protected override void Enable()
        {
            On.Player.MovementUpdate += Player_MovementUpdate;
            On.Player.ObjectEaten += Player_ObjectEaten;
            On.Player.Jump += Player_Jump;
        }

        // Hooks are disposed of here
        protected override void Disable()
        {
            On.Player.MovementUpdate -= Player_MovementUpdate;
            On.Player.ObjectEaten -= Player_ObjectEaten;
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
            if (IsMe(self) && self.TryGetSave<SprinterSaveState>(out var save) && save.isTurbo)
                self.slugcatStats.runspeedFac = 5f;
            orig(self, eu);
        }

        // Go absolutely wild once a mushroom is eaten
        private void Player_ObjectEaten(On.Player.orig_ObjectEaten orig, Player self, IPlayerEdible edible)
        {
            if (IsMe(self) && edible is Mushroom && self.TryGetSave<SprinterSaveState>(out var save))
            {
                save.isTurbo = true;
            }
            orig(self, edible);
        }

        // Add more height to all standard jumps
        private void Player_Jump(On.Player.orig_Jump orig, Player self)
        {
            orig(self);
            if (!IsMe(self)) return;

            if (self.TryGetSave<SprinterSaveState>(out var save) && save.isTurbo)
                self.jumpBoost += 9f;
            else
                self.jumpBoost += 3f;
        }


        // SlugBase //

        public override string DisplayName => "The Sprinter";
        public override string Description =>
@"A lightspeed rodent whose supernatural speed stems from chillidogs and a curious glowing fungus.
This is an example slugcat for the SlugBase framework.";

        public override Color? SlugcatColor(int slugcatCharacter)
        {
            Color col = new Color(0.37f, 0.36f, 0.91f);

            if (slugcatCharacter == -1)
                return col;
            else
                return Color.Lerp(PlayerGraphics.SlugcatColor(slugcatCharacter), col, 0.75f);
        }

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

        // Play a short "cutscene", forcing the player to climb a pole when starting a new game
        public override void StartNewGame(Room room)
        {
            base.StartNewGame(room);

            // Make sure this is the right room
            if (room.abstractRoom.name != StartRoom) return;

            room.AddObject(new SprinterStart(room));
        }

        public override CustomScene BuildScene(string sceneName)
        {
            RainWorld rw = Object.FindObjectOfType<RainWorld>();

            var scene = base.BuildScene(sceneName);

            // If not in turbo mode, hide some scene images
            if(sceneName == "SelectMenu")
            {
                bool turbo = false;
                try
                {
                    turbo = bool.Parse(GetSaveSummary(rw).CustomData["turbo"]);
                }
                catch { }

                if (!turbo)
                    scene.ApplyFilter(img => !img.HasTag("turboonly"));
            }

            return scene;
        }
    }
}
