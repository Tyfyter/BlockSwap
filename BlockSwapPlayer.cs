using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FunctionalBlockSwap {
    public class BlockSwapPlayer : ModPlayer {
        internal static protected bool triggerItemTime = false;
        public override void PostItemCheck() {
            if (triggerItemTime) {
                player.itemTime = PlayerHooks.TotalUseTime(player.HeldItem.useTime * player.tileSpeed, player, player.HeldItem);
            }
            triggerItemTime = false;
        }
        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) {
            if (Main.netMode == NetmodeID.Server) {

            }
        }
    }
}
