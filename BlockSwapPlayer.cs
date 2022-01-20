using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    }
}
