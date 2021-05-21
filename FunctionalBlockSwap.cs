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
using static Terraria.ID.TileID;

namespace FunctionalBlockSwap {
    public class FunctionalBlockSwap : Mod {
        public static ushort wallID = 0;

        public override void Load() {
            Hook.Player.PlaceThing+=PlaceThing;
        }
        public override void PostAddRecipes() {
            wallID = (ushort)ModContent.WallType<DummyWall>();
        }
        static bool verticalNormalOrigin(ushort type) {
            switch(type) {
                case Firework:
                case FireworkFountain:
                case LavaLamp:
                return true;
                default:
                return false;
            }
        }

        private void PlaceThing(Hook.Player.orig_PlaceThing orig, Player self) {
            int createTile = self.HeldItem.createTile;
            if(createTile==TileID.Torches||(TileLoader.GetTile(createTile)?.torch??false)) {
                orig(self);
                return;
            }
            Tile tile = Main.tile[Player.tileTargetX,Player.tileTargetY];
            Tile tile2 = Main.tile[Player.tileTargetX, Player.tileTargetY+1];
            if(!tile.active()) {
                orig(self);
                return;
            }
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
                        int cIndex = Chest.FindChest(Player.tileTargetX, Player.tileTargetY - 1);
                        if(cIndex!=-1) {
                            if(Chest.UsingChest(cIndex) == -1 && !Chest.isLocked(Player.tileTargetX, Player.tileTargetY - 1)) {
                                chest = Main.chest[cIndex];
                                chest.y++;
                            } else {
                                orig(self);
                                return;
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
            int currentStyle = currentTile.frameX;
            int xStyle = currentTile.frameX;
            if(Main.tileSolid[createTile]!=Main.tileSolid[currentType]||Main.tileSolidTop[createTile]!=Main.tileSolidTop[currentType]) {
                return false;
            }
            TileObjectData currentData = TileObjectData.GetTileData(currentType, 0);
            if(currentType == Chairs) {
                currentStyle = GetChairStyle(currentTile.frameY);
                if(currentStyle == -1)currentStyle = createStyle;
            } else if(!(currentData is null)) {
                if(!currentData.StyleHorizontal) {
                    currentStyle = currentTile.frameY;
                    int frameY = 0;
                    for(int y = currentStyle; y > 0; y -= currentData.CoordinateHeights[frameY % currentData.Height] + currentData.CoordinatePadding) {
                        frameY++;
                        if(frameY % currentData.Height == 0 && !currentData.StyleHorizontal) {
                            currentData = TileObjectData.GetTileData(currentType, frameY / currentData.Height);
                            //y -= currentData.CoordinatePadding * (currentType == Chairs ? 2 : 1);
                        }
                    }
                    currentStyle = frameY / currentData.Height;
                    xStyle /= currentData.CoordinateFullWidth;
                } else if(TileID.Sets.Platforms[currentType]) {
                    currentStyle = currentTile.frameY/18;
                } else {
                    currentStyle /= currentData.CoordinateFullWidth;
                    xStyle /= currentData.CoordinateFullWidth;
                    xStyle &= 1;
                }

                currentData = TileObjectData.GetTileData(currentType, currentStyle);
            } else {
                currentStyle /= 18;//vert?18:16;
                xStyle /= 18;
            }
            if(UsesPlayerDirection(currentType, out bool rev) &&
                (((xStyle>0) != (Main.LocalPlayer.direction>0))^rev)) {
                currentStyle = -1;
            }
            TileObjectData createData = TileObjectData.GetTileData(createTile, createStyle);
            if(!(currentData is null||createData is null) && currentType != Chairs) {
                if(currentData.Width!=createData.Width||currentData.Height!=createData.Height) {
                    return false;
                }
                Point offset = MultiTileUtils.GetRelativeOriginCoordinates(currentData, currentTile);
                if(offset!=new Point(0,0)) {//vert^verticalNormalOrigin(currentTile.type)?currentData.Height-1:
                    return false;
                }
            }
            if(currentType==createTile&&(!(Main.tileFrameImportant[currentType]&&Main.tileFrameImportant[createTile])||currentStyle==createStyle)){
                return false;
            }
            return !((TileID.Sets.Grass[currentType]&&createTile==TileID.Dirt)||
                (TileID.Sets.GrassSpecial[currentType]&&createTile==TileID.Mud)||
                (TileID.Sets.Grass[createTile]&&!TileID.Sets.Grass[currentType]||
                TileID.Sets.GrassSpecial[createTile]&&!TileID.Sets.GrassSpecial[currentType]));
        }
        public static bool UsesPlayerDirection(int type, out bool reverse) {
            reverse = false;
            switch(type) {
                case Beds:
                case Bathtubs:
                case Womannequin:
                case Mannequin:
                case Chairs:
                return true;

                default:
                return false;
            }
        }
        /// <summary>
        /// I implemented an entire bidirectional dictionary class to hardcode this, and it's just чертов (frameY-18)/40...
        /// </summary>
        public static int GetChairStyle(short frameY) {
            frameY -= 18;
            if(frameY%40 == 0) {
                return frameY / 40;
            }
            return -1;
            #region suffering
            /*switch(frameY){
                case 18:
                return 0;
                case 58:
                return 1;
                case 98:
                return 2;
                case 138:
                return 3;
                case 178:
                return 4;
                case 218:
                return 5;
                case 258:
                return 6;
                case 298:
                return 7;
                case 338:
                return 8;
                case 378:
                return 9;
                case 418:
                return 10;
                case 458:
                return 11;
                case 498:
                return 12;
                case 538:
                return 13;
                case 578:
                return 14;
                case 618:
                return 15;
                case 658:
                return 16;
                case 698:
                return 17;
                case 738:
                return 18;
                case 778:
                return 19;
                case 858:
                return 20;
                case 818:
                return 21;
                case 898:
                return 22;
                case 938:
                return 23;
                case 978:
                return 24;
                case 1018:
                return 25;
                case 1058:
                return 26;
                case 1098:
                return 27;
                case 1138:
                return 28;
                case 1178:
                return 29;
                case 1218:
                return 30;
                case 1258:
                return 31;
                case 1298:
                return 32;
                case 1338:
                return 33;
                case 1378:
                return 34;
                case 1418:
                return 35;
                case 1458:
                return 36;
            }
            return -1;*/
            #endregion suffering
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