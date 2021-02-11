using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Achievements;
using Terraria.ModLoader;
using static FunctionalBlockSwap.ToolType;
using Hook = On.Terraria;
using Terraria.ID;
using Terraria.ObjectData;
using Microsoft.Xna.Framework;
using Tyfyter.Utils;

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
            Chest chest = null;
            if(PlaceThingChecks(self)&&createTile>-1&&TileCompatCheck(tile, createTile, self.HeldItem.placeStyle)&&tile.active()) {
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
                    if(Main.tileContainer[tile.type]&&Main.tileContainer[createTile]) {
                        for(int i = 0; i < Main.chest.Length; i++) {
                            chest = Main.chest[i];
                            if(chest is null)
                                continue;
                            if(chest.x == Player.tileTargetX && chest.y == Player.tileTargetY - 1) {
                                chest.y++;
                                break;
                            }
                        }
                    }
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
            if(!(chest is null)) {
                chest.y--;
            }
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
        public static bool TileCompatCheck(Tile currentTile, int createTile, int createStyle) {
            int currentType = currentTile.type;
            int currentFrameX = currentTile.frameX;
            if(Main.tileSolid[createTile]!=Main.tileSolid[currentType]||Main.tileSolidTop[createTile]!=Main.tileSolidTop[currentType]) {
                return false;
            }
            TileObjectData currentData = TileObjectData.GetTileData(currentType, 0);
            if(!(currentData is null)) {
                currentFrameX/=currentData.CoordinateFullWidth;
                currentData = TileObjectData.GetTileData(currentType, currentFrameX);
            } else {
                currentFrameX/=16;
            }
            TileObjectData createData = TileObjectData.GetTileData(createTile, createStyle);
            if(!(currentData is null||createData is null)) {
                if(currentData.Width!=createData.Width||currentData.Height!=createData.Height) {
                    return false;
                }
                Point offset = MultiTileUtils.GetRelativeOriginCoordinates(currentData, currentTile);
                if(offset!=new Point(0,0)) {
                    return false;
                }
            }
            if(currentType==createTile&&(!(Main.tileFrameImportant[currentType]&&Main.tileFrameImportant[createTile])||currentFrameX==createStyle)){
                return false;
            }
            return !((TileID.Sets.Grass[currentType]&&createTile==TileID.Dirt)||
                (TileID.Sets.GrassSpecial[currentType]&&createTile==TileID.Mud)||
                (TileID.Sets.Grass[createTile]&&!TileID.Sets.Grass[currentType]||
                TileID.Sets.GrassSpecial[createTile]&&!TileID.Sets.GrassSpecial[currentType]));
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