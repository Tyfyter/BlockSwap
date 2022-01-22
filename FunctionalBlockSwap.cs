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
using Terraria.Enums;
using static Terraria.TileObject;
using System.Linq;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using System;

namespace FunctionalBlockSwap {
    public class FunctionalBlockSwap : Mod {
        //public static ushort wallID = 0;
        public const ushort wallID = WallID.DirtUnsafe;
        /// <summary>
        /// blocks SquareTileFrame and reroutes chest destruction packets
        /// </summary>
        internal static protected bool clientSwapping = false;
        internal static protected bool blockDestroyChest = false;
        internal static protected bool blockChestHooks = false;
        internal static protected bool disableMod = false;
        internal static protected bool testing = true;
        internal static protected int hitTileLastDamage = 0;
        public override void Load() {
            Hook.Player.PlaceThing += PlaceThing;
            Hook.WorldGen.SquareTileFrame += SquareTileFrame;
            Hook.Chest.FindEmptyChest += Chest_FindEmptyChest;
            Hook.Chest.AfterPlacement_Hook += Chest_AfterPlacement_Hook;
            Hook.Chest.DestroyChest += Chest_DestroyChest;
            //Hook.WorldGen.CanKillTile_int_int_refBoolean += CanKillTile;
            //Hook.HitTile.AddDamage += HitTile_AddDamage;
            //ILMod.WorldGen.KillTile += WorldGen_KillTile;
        }

        private void WorldGen_KillTile(ILContext il) {
            ILCursor c = new ILCursor(il);
            FieldInfo tileType = typeof(Tile).GetField("type", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo newText = typeof(Main).GetMethod("NewText", new Type[]{ typeof(object), typeof(Color), typeof(bool) });
            ILLabel jumpTarget = null;
            if (c.TryGotoNext(MoveType.After, i => i.MatchLdloc(0), i => i.MatchLdfld(tileType), i => i.MatchLdcI4(72), i => i.MatchBeq(out jumpTarget))) {
                c.Emit(OpCodes.Ldsfld, typeof(FunctionalBlockSwap).GetField("testing", BindingFlags.NonPublic | BindingFlags.Static));
                c.Emit(OpCodes.Brtrue, jumpTarget);
                Logger.Info("patched killtile with jump to " + jumpTarget.Target.OpCode.Name);
            } else {
                Logger.Info("failed to patch killtile");
            }
        }

        private int HitTile_AddDamage(Hook.HitTile.orig_AddDamage orig, HitTile self, int tileId, int damageAmount, bool updateAmount) {
            return hitTileLastDamage = orig(self, tileId, damageAmount, updateAmount);
        }

        private bool CanKillTile(Hook.WorldGen.orig_CanKillTile_int_int_refBoolean orig, int i, int j, out bool blockDamaged) {
            if (clientSwapping) {
	            blockDamaged = false;
	            if (i < 0 || j < 0 || i >= Main.maxTilesX || j >= Main.maxTilesY){
		            return false;
	            }
	            Tile tile = Main.tile[i, j];
	            if (tile == null) {
		            return false;
	            }
	            if (!tile.active()) {
		            return false;
	            }
	            if (!TileLoader.CanKillTile(i, j, tile.type, ref blockDamaged)) {
		            return false;
	            }
	            blockDamaged = true;
                return true;
            }
	        return orig(i, j, out blockDamaged);
        }

        private bool Chest_DestroyChest(Hook.Chest.orig_DestroyChest orig, int X, int Y) {
            return blockDestroyChest || orig(X, Y);
        }

        private int Chest_AfterPlacement_Hook(Hook.Chest.orig_AfterPlacement_Hook orig, int x, int y, int type, int style, int direction) {
            if (blockChestHooks) {
				return -1;
            }
			return orig(x, y, type, style, direction);
        }

        private int Chest_FindEmptyChest(Hook.Chest.orig_FindEmptyChest orig, int x, int y, int type, int style, int direction) {
            if (blockChestHooks) {
				return -2;
            }
			return orig(x, y, type, style, direction);
        }

        public override void HandlePacket(BinaryReader reader, int whoAmI) {
            byte msgType = reader.ReadByte();
            switch (msgType) {
                case 0: {
		            short x = reader.ReadInt16();
		            short y = reader.ReadInt16();
		            short width = reader.ReadInt16();
		            short height = reader.ReadInt16();
                    ModPacket packet = GetPacket(1 + (4 * 2) + (width * height * 3 * 2));
                    if (Main.netMode == NetmodeID.Server) {
                        packet.Write((byte)0);
                        packet.Write(x);
                        packet.Write(y);
                        packet.Write(width);
                        packet.Write(height);
                    }
                    try {
                        blockDestroyChest = true;
                        WorldGen.KillTile(x, y);
                    } finally {
                        blockDestroyChest = false;
                    }

                    for (int i = 0; i < width; i++) {
                        for (int j = 0; j < height; j++) {
                            Main.tile[x + i, y + j].active(true);
							ushort type = reader.ReadUInt16();
							short frameX = reader.ReadInt16();
							short frameY = reader.ReadInt16();
                            Main.tile[x + i, y + j].ResetToType(type);
                            Main.tile[x + i, y + j].frameX = frameX;
                            Main.tile[x + i, y + j].frameY = frameY;

							if (Main.netMode == NetmodeID.Server) {
								packet.Write(type);
								packet.Write(frameX);
								packet.Write(frameY);
							}
                        }
                    }
                    if (Main.netMode == NetmodeID.Server) {
						packet.Send();
                    }
                    for (int i = 0; i < width; i++) {
                        for (int j = 0; j < height; j++) {
							WorldGen.TileFrame(i, j);
                        }
                    }
                }
                break;
                case 1: {
                    if (Main.netMode == NetmodeID.MultiplayerClient) {
                        disableMod = reader.ReadBoolean();
                    }
                }
                break;
            }
        }
        
        private static bool iltest(Hook.WorldGen.orig_SquareTileFrame orig, int i, int j, bool resetFrame) {
            if (clientSwapping) {
                return true;
            }
            return false;
        }
        private void SquareTileFrame(Hook.WorldGen.orig_SquareTileFrame orig, int i, int j, bool resetFrame) {
            if (!clientSwapping) {
                orig(i, j, resetFrame);
            }
        }

        private void PlaceThing(Hook.Player.orig_PlaceThing orig, Player self) {
            if (disableMod && IsNetSynced) {
                return;
            }
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
            bool chestSwapping = false;
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
                            if((IsNetSynced || Main.netMode == 0) && Chest.UsingChest(cIndex) == -1 && !Chest.isLocked(Player.tileTargetX, Player.tileTargetY - 1)) {
                                chest = Main.chest[cIndex];
                                chest.y++;
                                chestSwapping = true;
                            } else {
                                orig(self);
                                return;
                            }
                        }
                    }
                    self.selectedItem = GetBestToolSlot(self, out int power, toolType: Pickaxe);
                    if(power <= 0) {
                        orig(self);
                        return;
                    }
                    clientSwapping = true;

                    int hitID = -1;
                    if (!chestSwapping) {
                        self.PickTile(Player.tileTargetX, Player.tileTargetY, power);
                    }
                    
                    if(chestSwapping || hitTileLastDamage > 0 || (Sets.Grass[oldType] && tile.type == Dirt)) {
                        AchievementsHelper.CurrentlyMining = true;
                        //if(hitID > -1)self.hitTile.Clear(hitID);
                        clientSwapping = false;
                        if(tile.active())self.PickTile(Player.tileTargetX, Player.tileTargetY, ushort.MaxValue);
                        //WorldGen.KillTile(Player.tileTargetX, Player.tileTargetY);
                        SetWall(tile2);
                        AchievementsHelper.HandleMining();
                        AchievementsHelper.CurrentlyMining = false;
                    }/* else if(!tile.active()) {
                        SetWall(tile2);
                    }*/ else {
                        self.itemTime = 0;
                        BlockSwapPlayer.triggerItemTime = true;
                    }
                    clientSwapping = false;
                }
                self.selectedItem = selected;
            }
            if(Main.netMode == NetmodeID.MultiplayerClient) {
                if (chestSwapping) {
                    int tSX = targetSizeX + 1;
                    int tSY = targetSizeY + 1;
                    int x = Player.tileTargetX + targetOffsetX;
                    int y = Player.tileTargetY + targetOffsetY;
                    for (int i = 0; i < tSX; i++) {
                        for (int j = 0; j < tSY; j++) {
                            if (Main.tile[x + i, y + j].type == oldType) {
                                Main.tile[x + i, y + j].type = 0;
                                Main.tile[x + i, y + j].active(false);
                            }
                        }
                    }
                }
                try {
                    blockChestHooks = true;
                    orig(self);
                } finally {
                    blockChestHooks = false;
                }
                if (tile.type != oldType || (Main.tileFrameImportant[tile.type] && (oldFrameX == tile.frameX || oldFrameY == tile.frameY))) {
                    tile2.wall = wall;
                    tile.slope(oldSlope);
                    tile.halfBrick(oldHalfBrick);
                    if (TileID.Sets.Platforms[tile.type]) {
                        tile.frameX = oldFrameX;
                    }
                    WorldGen.SquareTileFrame(Player.tileTargetX, Player.tileTargetY);

                    NetMessage.SendTileRange(Main.myPlayer, Player.tileTargetX + targetOffsetX, Player.tileTargetY + targetOffsetY, targetSizeX, targetSizeY);
                    if (chestSwapping) {
                        targetSizeX++;
                        targetSizeY++;
                        ModPacket packet = GetPacket(1 + (4 * 2) + (targetSizeX * targetSizeY * 3 * 2));
                        packet.Write((byte)0);
                        int x = Player.tileTargetX + targetOffsetX;
                        int y = Player.tileTargetY + targetOffsetY;
                        packet.Write((short)x);
                        packet.Write((short)y);
                        packet.Write((short)targetSizeX);
                        packet.Write((short)targetSizeY);
                        for (int i = 0; i < targetSizeX; i++) {
                            for (int j = 0; j < targetSizeY; j++) {
                                packet.Write(Main.tile[x + i, y + j].type);
                                packet.Write(Main.tile[x + i, y + j].frameX);
                                packet.Write(Main.tile[x + i, y + j].frameY);
								Main.tile[x + i, y + j].active(true);
                            }
                        }
                        packet.Send();
                    }
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