using UnityEngine;

namespace ExampleSlugcat
{
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

                ply.eatCounter = 15;
                AbstractPhysicalObject shroom = new AbstractConsumable(room.world, AbstractPhysicalObject.AbstractObjectType.Mushroom, null, new WorldCoordinate(room.abstractRoom.index, 68, 30, 0), room.game.GetNewID(), -1, -1, null);
                room.abstractRoom.AddEntity(shroom);
                shroom.RealizeInRoom();
                shroom.realizedObject.firstChunk.HardSetPosition(ply.mainBodyChunk.pos + new Vector2(-30f, 0f));
                ply.SlugcatGrab(shroom.realizedObject, 0);

                room.game.cameras[0].hud.foodMeter.NewShowCount(ply.FoodInStomach);
                room.game.cameras[0].hud.foodMeter.visibleCounter = 0;
                room.game.cameras[0].hud.foodMeter.fade = 0f;
                room.game.cameras[0].hud.foodMeter.lastFade = 0f;
            }

            // End the cutscene
            if (timer == 180)
            {
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
                else if (owner.timer < 40) y = 0;
                else if (owner.timer < 165) y = 1;
                else y = 0;

                return new Player.InputPackage(false, 0, y, false, false, false, false, false);
            }
        }
    }
}
