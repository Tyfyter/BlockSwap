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
            //Hook.Chest.DestroyChest += DestroyChest;

            ///replace chest descruction netmessages while blockswapping

            //new DynamicMethodDefinition(typeof(WorldGen).GetMethod("KillTile", BindingFlags.Instance|BindingFlags.Public));
        }
        public override void HandlePacket(BinaryReader reader, int whoAmI) {
            if (Main.netMode == NetmodeID.Server) {
                byte msgType = reader.ReadByte();
                switch (msgType) {
                    case 0: {
		                int x = reader.ReadInt16();
		                int y = reader.ReadInt16();
		                int width = reader.ReadInt16();
		                int height = reader.ReadInt16();
                        StringBuilder builder = new StringBuilder();
                        for (int i = 0; i < width; i++) {
                            for (int j = 0; j < height; j++) {
                                Main.tile[x + i, y + j].active(true);
                                Main.tile[x + i, y + j].ResetToType(reader.ReadUInt16());
                                builder.Append(Main.tile[x + i, y + j].type);
                                builder.Append(", ");
                                Main.tile[x + i, y + j].frameX = reader.ReadInt16();
                                Main.tile[x + i, y + j].frameY = reader.ReadInt16();
                            }
                        }
                        NetMessage.BroadcastChatMessage(NetworkText.FromLiteral($"goat pack, {width * height} much, {builder}"), Color.CornflowerBlue);
                        break;
                    }
                }
            }
        }
        /*
        private bool DestroyChest(Hook.Chest.orig_DestroyChest orig, int X, int Y) {
            if (Main.netMode == NetmodeID.Server) {
                NetMessage.BroadcastChatMessage(NetworkText.FromLiteral(blockDestroyChest+""), Color.CornflowerBlue);
            } else {
                Main.NewText(blockDestroyChest, Color.IndianRed);
            }
            return blockDestroyChest || orig(X,Y);
        }

        /*public override bool HijackGetData(ref byte messageType, ref BinaryReader reader, int playerNumber) {
           if (Main.netMode == NetmodeID.Server && messageType == 34) {
               NetMessage.BroadcastChatMessage(NetworkText.FromLiteral("got 34"), Color.DodgerBlue);
           }
           return false;
        }* /
        public override void HandlePacket(BinaryReader reader, int whoAmI) {
            if (Main.netMode == NetmodeID.Server) {
                byte msgType = reader.ReadByte();
                switch (msgType) {
                    case 0: {
		                byte type = reader.ReadByte();
		                int x = reader.ReadInt16();
		                int y = reader.ReadInt16();
                        int width = 0;
                        int height = 0;
				        //*
				        Tile targetTile = Main.tile[x, y];
                        switch (type % 100) {
                            case 5:
                            case 1:
				            if (targetTile.frameX % 36 != 0) {
					            x--;
				            }
				            if (targetTile.frameY % 36 != 0) {
					            y--;
				            }
                            width = 1;
                            height = 1;
                            break;
                            case 3:
				            x -= targetTile.frameX % 54 / 18;
				            if (targetTile.frameY % 36 != 0) {
					            y--;
				            }
                            width = 2;
                            height = 1;
                            break;
                        }
                        try {
                            blockDestroyChest = true;
				            WorldGen.KillTile(x, y);
                            NetMessage.SendTileRange(Main.myPlayer, x, y, width, height);
                        } finally {
                            blockDestroyChest = false;
                        }//* /

                        break;
                    }
                }
            }
        }

        public override bool HijackSendData(int whoAmI, int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7) {
            if (clientSwapping && IsNetSynced && Main.netMode == NetmodeID.MultiplayerClient && msgType == 34 && number % 100 == 1) {
                Main.NewText("Hijacked 34", Color.DodgerBlue);
                ModPacket packet = GetPacket(1+(7*4));
                packet.Write((byte)0);
			    packet.Write((byte)number);
			    packet.Write((short)number2);
			    packet.Write((short)number3);
                packet.Send();
                return true;
            }
            return false;
        }//*/

        private void SquareTileFrame(Hook.WorldGen.orig_SquareTileFrame orig, int i, int j, bool resetFrame) {
            if (!clientSwapping) {
                orig(i, j, resetFrame);
            }
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
                if (tile.active() && Main.tileContainer[tile.type]) {
                    int tSX = 1 + 1;
                    int tSY = 1 + 1;
                    int x = Player.tileTargetX + 0;
                    int y = Player.tileTargetY + -1;
                    StringBuilder builder = new StringBuilder();
                    for (int i = 0; i < tSX; i++) {
                        for (int j = 0; j < tSY; j++) {
                            builder.Append(Main.tile[x + i, y + j].active()? "[c/6495ed:" : "[c/aaaaaa:");
                            builder.Append(Main.tile[x + i, y + j].type);
                            builder.Append("], ");
                        }
                    }
                    Main.NewText(NetworkText.FromLiteral($"sandn't packn't, {builder}"), Color.CornflowerBlue);
                }
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
                /*if (Main.tileHammer[tile.type]) {
                    bool breakTile = Main.tileNoFail[tile.type];
                    if (!breakTile && WorldGen.CanKillTile(Player.tileTargetX, Player.tileTargetY)) {
                        self.selectedItem = GetBestToolSlot(self, out int power, toolType: Hammer);
                        if (tile.type == TileID.DemonAltar && (power < 80 || !Main.hardMode)) {
                            self.Hurt(PlayerDeathReason.ByOther(4), self.statLife / 2, -self.direction);
                        }
                    }
                    if (breakTile) {
                        WorldGen.KillTile(Player.tileTargetX, Player.tileTargetY);
                        SetWall(tile2);
                        if (Main.netMode == NetmodeID.MultiplayerClient) {
                            NetMessage.SendData(MessageID.TileChange, -1, -1, null, 0, Player.tileTargetX, Player.tileTargetY);
                        }
                    }
                } else */
                if(!(Main.tileAxe[tile.type] || Main.tileHammer[tile.type])) {
                    Main.NewText("pick maybe");
                    if(Main.tileContainer[tile.type]&&Main.tileContainer[createTile]) {
                        TileObjectData objectData = TileObjectData.GetTileData(tile.type, 0);
                        int cIndex = Chest.FindChest(Player.tileTargetX, Player.tileTargetY - (objectData.Height - 1));
                        if(cIndex!=-1) {
                            targetSizeX = objectData.Width-1;
                            targetSizeY = objectData.Height-1;
                            targetOffsetY = -targetSizeY;
                            if(Chest.UsingChest(cIndex) == -1 && !Chest.isLocked(Player.tileTargetX, Player.tileTargetY - 1)) {
                                chest = Main.chest[cIndex];
                                chest.y++;
                                chestSwapping = true;
                                Main.NewText("chest yes");
                            } else {
                                orig(self);
                                Main.NewText("chest no");
                                return;
                            }
                        }
                    }
                    self.selectedItem = GetBestToolSlot(self, out int power, toolType: Pickaxe);
                    clientSwapping = true;
                    if(!chestSwapping)self.PickTile(Player.tileTargetX, Player.tileTargetY, power);
                    Main.LocalPlayer.chatOverhead.NewMessage(self.hitTile.data[0].damage+"", 30);
                    if(self.hitTile.data[0].damage>0 || chestSwapping) {
                        Main.NewText("pick");
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
                        Main.NewText("pick much");
                        /*for (int oX = targetOffsetX; oX <= targetOffsetX+targetSizeX; oX++) {
                            for (int oY = targetOffsetY; oY <= targetOffsetY+targetSizeY; oY++) {
                                Main.tile[Player.tileTargetX + oX, Player.tileTargetY + oY].active(false);
                            }
                        }*/
                        /*tile.ResetToType(TileID.Dirt);
                        tile.active(false);*/
                        SetWall(tile2);
                    } else {
                        Main.NewText("pick no");
					    self.itemTime = PlayerHooks.TotalUseTime((float)self.HeldItem.useTime * self.tileSpeed, self, self.HeldItem);
                    }
                    if (!tile.active()) {
                        Main.NewText("pick succ");
                        /*for (int oX = targetOffsetX; oX <= targetOffsetX+targetSizeX; oX++) {
                            for (int oY = targetOffsetY; oY <= targetOffsetY+targetSizeY; oY++) {
                                Main.tile[Player.tileTargetX + oX, Player.tileTargetY + oY].active(false);
                            }
                        }*/
                    }
                    Main.NewText($"{tile.type} != {oldType}");
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
                    Main.netMode = NetmodeID.SinglePlayer;
                }
                try {
                    orig(self);
                } finally {
                    Main.netMode = NetmodeID.MultiplayerClient;
                }
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
                    if (chestSwapping && IsNetSynced) {
                        targetSizeX++;
                        targetSizeY++;
                        ModPacket packet = GetPacket(1 + (4 * 2) + (targetSizeX * targetSizeY * 3 * 2));
                        Main.NewText("pack succ");
                        packet.Write((byte)0);
                        int x = Player.tileTargetX + targetOffsetX;
                        int y = Player.tileTargetY + targetOffsetY;
                        packet.Write((short)x);
                        packet.Write((short)y);
                        packet.Write((short)targetSizeX);
                        packet.Write((short)targetSizeY);
                        StringBuilder builder = new StringBuilder();
                        for (int i = 0; i < targetSizeX; i++) {
                            for (int j = 0; j < targetSizeY; j++) {
                                packet.Write(Main.tile[x + i, y + j].type);
                                builder.Append(Main.tile[x + i, y + j].active()? "[c/6495ed:" : "[c/aaaaaa:");
                                builder.Append(Main.tile[x + i, y + j].type);
                                builder.Append("], ");
                                packet.Write(Main.tile[x + i, y + j].frameX);
                                packet.Write(Main.tile[x + i, y + j].frameY);
                            }
                        }
                        packet.Send();
                        Main.NewText(NetworkText.FromLiteral($"sand pack, {targetSizeX * targetSizeY} much, {builder}"), Color.CornflowerBlue);
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