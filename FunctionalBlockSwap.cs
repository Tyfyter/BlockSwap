using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Achievements;
using Terraria.ModLoader;
using static FunctionalBlockSwap.ToolType;
using Hook = On.Terraria;
using Terraria.ID;
using Terraria.ObjectData;

namespace FunctionalBlockSwap {
    public class FunctionalBlockSwap : Mod {
        public static ushort wallID = 0;

        public override void Load() {
            Hook.Player.PlaceThing+=PlaceThing;
        }
        public override void PostAddRecipes() {
            wallID = (ushort)ModContent.WallType<DummyWall>();
        }

        private void PlaceThing(Hook.Player.orig_PlaceThing orig, Player self) {
            int createTile = self.HeldItem.createTile;
            if(createTile==TileID.Torches||(TileLoader.GetTile(createTile)?.torch??false)) {
                orig(self);
                return;
            }
            Tile tile = Main.tile[Player.tileTargetX,Player.tileTargetY];
            Tile tile2 = Main.tile[Player.tileTargetX, Player.tileTargetY+1];
            ushort wall = tile2.wall;
            if(PlaceThingChecks(self)&&createTile>-1&&TileCompatCheck(tile.type, tile.frameX, createTile, self.HeldItem.placeStyle)&&tile.active()) {
                int selected = self.selectedItem;
                if(Main.tileHammer[tile.type]) {
                    bool breakTile = Main.tileNoFail[tile.type];
                    if(!breakTile&&WorldGen.CanKillTile(Player.tileTargetX, Player.tileTargetY)) {
                        self.selectedItem = GetBestToolSlot(self, out int power, toolType: Hammer);
                        if(tile.type == TileID.DemonAltar && (power < 80 || !Main.hardMode)) {
                            self.Hurt(PlayerDeathReason.ByOther(4), self.statLife / 2, -self.direction);
                        }
                    }
                    if(breakTile) {
                        WorldGen.KillTile(Player.tileTargetX, Player.tileTargetY);
                        SetWall(tile2);
                        if(Main.netMode == NetmodeID.MultiplayerClient) {
                            NetMessage.SendData(MessageID.TileChange, -1, -1, null, 0, Player.tileTargetX, Player.tileTargetY);
                        }
                    }
                }else if(!(Main.tileAxe[tile.type] || Main.tileHammer[tile.type])) {
                    if(!Main.tileContainer[tile.type]) {
                        self.selectedItem = GetBestToolSlot(self, out int power, toolType: Pickaxe);
                        self.PickTile(Player.tileTargetX, Player.tileTargetY, power);
                        if(self.hitTile.data[0].damage>0) {
                            AchievementsHelper.CurrentlyMining = true;
                            self.hitTile.Clear(0);
                            WorldGen.KillTile(Player.tileTargetX, Player.tileTargetY);
                            SetWall(tile2);
                            AchievementsHelper.HandleMining();
                            if(Main.netMode == NetmodeID.MultiplayerClient) {
                                NetMessage.SendData(MessageID.TileChange, -1, -1, null, 0, Player.tileTargetX, Player.tileTargetY);
                            }
                            AchievementsHelper.CurrentlyMining = false;
                        } else if(!tile.active()) {
                            tile.ResetToType(0);
                            tile.active(false);
                            SetWall(tile2);
                        }
                    }
                }
                self.selectedItem = selected;
            }
            if(Main.netMode == NetmodeID.MultiplayerClient) {
                ushort t = tile.type;
                orig(self);
				if(tile.type!=t)NetMessage.SendData(MessageID.TileChange, -1, -1, null, tile.type, Player.tileTargetX, Player.tileTargetY);
            } else {
                orig(self);
            }

            tile2.wall = wall;
        }
        static void SetWall(Tile tile) {
            tile.wall = wallID;
        }
        public static bool PlaceThingChecks(Player player) {
            int tileBoost = player.HeldItem.tileBoost;
            return !player.noBuilding&&
                (player.itemTime == 0 && player.itemAnimation > 0 && player.controlUseItem)&&(
                player.Left.X / 16f - Player.tileRangeX - tileBoost - player.blockRange <= Player.tileTargetX &&
                player.Right.X / 16f + Player.tileRangeX + tileBoost - 1f + player.blockRange >= Player.tileTargetX &&
                player.Top.Y / 16f - Player.tileRangeY - tileBoost - player.blockRange <= Player.tileTargetY &&
                player.Bottom.Y / 16f + Player.tileRangeY + tileBoost - 2f + player.blockRange >= Player.tileTargetY);
        }
        public static bool TileCompatCheck(int currentTile, int currentFrameX, int createTile, int createStyle) {
            if(Main.tileSolid[createTile]!=Main.tileSolid[currentTile]||Main.tileSolidTop[createTile]!=Main.tileSolidTop[currentTile]) {
                return false;
            }
            TileObjectData current = TileObjectData.GetTileData(currentTile, 0);
            if(!(current is null)) {
                currentFrameX/=current.CoordinateFullWidth;
                current = TileObjectData.GetTileData(currentTile, currentFrameX);
            } else {
                currentFrameX/=16;
            }
            TileObjectData create = TileObjectData.GetTileData(createTile, createStyle);
            if(!(current is null||create is null)) {
                if(current.Width!=create.Width||current.Height!=create.Height) {
                    return false;
                }
            }
            if(currentTile==createTile&&(!(Main.tileFrameImportant[currentTile]&&Main.tileFrameImportant[createTile])||currentFrameX==createStyle)){
                return false;
            }
            return !((TileID.Sets.Grass[currentTile]&&createTile==TileID.Dirt)||
                (TileID.Sets.GrassSpecial[currentTile]&&createTile==TileID.Mud)||
                (TileID.Sets.Grass[createTile]&&!TileID.Sets.Grass[currentTile]||
                TileID.Sets.GrassSpecial[createTile]&&!TileID.Sets.GrassSpecial[currentTile]));
        }
        public static int GetBestToolSlot(Player player, out int power, ToolType toolType = Pickaxe) {
            int slot = player.selectedItem;
            power = 0;
            int i;
            int length = player.inventory.Length;
            switch(toolType) {
                case Pickaxe:
                for(i = 0; i < length; i++)
                    if(player.inventory[i].pick>power) {
                        power = player.inventory[i].pick;
                        slot = i;
                    }
                break;
                case Axe:
                for(i = 0; i < length; i++)
                    if(player.inventory[i].axe>power) {
                        power = player.inventory[i].axe;
                        slot = i;
                    }
                break;
                case Hammer:
                for(i = 0; i < length; i++)
                    if(player.inventory[i].hammer>power) {
                        power = player.inventory[i].hammer;
                        slot = i;
                    }
                break;
            }
            return slot;
        }
	}
    public class DummyWall : ModWall {
        public override bool Autoload(ref string name, ref string texture) {
            texture = "Terraria/Wall_1";
            return true;
        }
    }
    public enum ToolType {
        Pickaxe,
        Axe,
        Hammer
    }
}