using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Achievements;
using Terraria.ModLoader;
using static FunctionalBlockSwap.ToolType;
using Hook = On.Terraria;
using ILMod = IL.Terraria;
using Terraria.ID;
using Terraria.ObjectData;
using Microsoft.Xna.Framework;
using Tyfyter.Utils;
using static Terraria.ID.TileID;
using MonoMod.Utils;
using System.Reflection;
using System.IO;
using Terraria.Localization;
using System.Text;

namespace FunctionalBlockSwap {
    public class FunctionalBlockSwap : Mod {
        //public static ushort wallID = 0;
        public const ushort wallID = WallID.DirtUnsafe;
        /// <summary>
        /// blocks SquareTileFrame and reroutes chest destruction packets
        /// </summary>
        internal protected bool clientSwapping = false;
        internal protected bool blockDestroyChest = false;
        public override void Load() {
            Hook.Player.PlaceThing += PlaceThing;
            Hook.WorldGen.SquareTileFrame += SquareTileFrame;
        }

        private void SquareTileFrame(Hook.WorldGen.orig_SquareTileFrame orig, int i, int j, bool resetFrame) {
            if (!clientSwapping) {
                orig(i, j, resetFrame);
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
            ushort oldType = tile.type;
            ushort wall = tile2.wall;
            Chest chest = null;
            int targetOffsetX = 0;
            int targetOffsetY = 0;
            int targetSizeX = 0;
            int targetSizeY = 0;
            byte oldSlope = tile.slope();
            bool oldHalfBrick = tile.halfBrick();
            short oldFrameX = -1;
            short oldFrameY = -1;
            if (Main.tileFrameImportant[tile.type]) {//TileID.Sets.Platforms[tile.type]
                oldFrameX = tile.frameX;
                oldFrameY = tile.frameY;
            }
            if(PlaceThingChecks(self)&&createTile>-1&&TileCompatCheck(tile, createTile, self.HeldItem.placeStyle)&&tile.active()) {
                int selected = self.selectedItem;
                if(!(Main.tileAxe[tile.type] || Main.tileHammer[tile.type])) {
                    if(Main.tileContainer[tile.type]&&Main.tileContainer[createTile]) {
                        TileObjectData objectData = TileObjectData.GetTileData(tile.type, 0);
                        int cIndex = Chest.FindChest(Player.tileTargetX, Player.tileTargetY - (objectData.Height - 1));
                        if(cIndex!=-1) {
                            targetSizeX = objectData.Width-1;
                            targetSizeY = objectData.Height-1;
                            targetOffsetY = -targetSizeY;
                            if(Main.netMode == NetmodeID.SinglePlayer && Chest.UsingChest(cIndex) == -1 && !Chest.isLocked(Player.tileTargetX, Player.tileTargetY - 1)) {
                                chest = Main.chest[cIndex];
                                chest.y++;
                            } else {
                                orig(self);
                                return;
                            }
                        }
                    }
                    self.selectedItem = GetBestToolSlot(self, out int power, toolType: Pickaxe);
                    clientSwapping = true;
                    self.PickTile(Player.tileTargetX, Player.tileTargetY, power);
                    if(self.hitTile.data[0].damage>0) {
                        AchievementsHelper.CurrentlyMining = true;
                        self.hitTile.Clear(0);
                        WorldGen.KillTile(Player.tileTargetX, Player.tileTargetY);
                        SetWall(tile2);
                        AchievementsHelper.HandleMining();
                        if(Main.netMode == NetmodeID.MultiplayerClient) {
                            //NetMessage.SendData(MessageID.TileChange, -1, -1, null, 0, Player.tileTargetX, Player.tileTargetY);
                        }
                        AchievementsHelper.CurrentlyMining = false;
                    } else if(!tile.active()) {
                        SetWall(tile2);
                    } else {
					    self.itemTime = PlayerHooks.TotalUseTime((float)self.HeldItem.useTime * self.tileSpeed, self, self.HeldItem);
                    }
                    clientSwapping = false;
                }
                self.selectedItem = selected;
            }
            if(Main.netMode == NetmodeID.MultiplayerClient) {
                orig(self);
                if (tile.type != oldType || (Main.tileFrameImportant[tile.type] && (oldFrameX == tile.frameX || oldFrameY == tile.frameY))) {
                    tile2.wall = wall;
                    tile.slope(oldSlope);
                    tile.halfBrick(oldHalfBrick);
                    if (TileID.Sets.Platforms[tile.type]) {
                        tile.frameX = oldFrameX;
                    }
                    WorldGen.SquareTileFrame(Player.tileTargetX, Player.tileTargetY);
                    //Main.LocalPlayer.chatOverhead.NewMessage(tile.type +":"+ oldType, 30);
                    NetMessage.SendTileRange(Main.myPlayer, Player.tileTargetX + targetOffsetX, Player.tileTargetY + targetOffsetY, targetSizeX, targetSizeY);
                    //NetMessage.SendData(MessageID.TileChange, -1, -1, null, GetTileNetType(tile.type), Player.tileTargetX, Player.tileTargetY);
                }
            } else {
                orig(self);
                if (tile.type != oldType || (Main.tileFrameImportant[tile.type] && (oldFrameX == tile.frameX || oldFrameY == tile.frameY))) {
                    tile2.wall = wall;
                    tile.slope(oldSlope);
                    tile.halfBrick(oldHalfBrick);
                    if (TileID.Sets.Platforms[tile.type]) {
                        tile.frameX = oldFrameX;
                    }
                    WorldGen.SquareTileFrame(Player.tileTargetX, Player.tileTargetY);
                }
            }

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
        public static int GetTileNetType(int type) {
            switch (type) {
                case Iron:
                return Iron;
                default:
                return type + 1;
            }
        }
        public static bool TileCompatCheck(Tile currentTile, int createTile, int createStyle) {
            int currentType = currentTile.type;
            int currentStyle = currentTile.frameX;
            int xStyle = currentTile.frameX;
            if(Main.tileSolid[createTile]!=Main.tileSolid[currentType]||
                Main.tileSolidTop[createTile]!=Main.tileSolidTop[currentType]||
                (Sets.Falling[createTile] && !Sets.Falling[createTile])) {
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
    public enum ToolType {
        Pickaxe,
        Axe,
        Hammer
    }
}