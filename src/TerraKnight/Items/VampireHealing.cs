using System.Collections.Generic;
using UnityEngine;
namespace TerraKnight.Items
{
    // Reserve when the orb is created, so many airborne orbs share the same cap.
    internal static class VampireHealing
    {
        internal const int PerHit = 5, PerSecond = 20;
        static readonly Queue<(float Time, int Amount)> reserved = new Queue<(float,int)>();
        static int inWindow;
        static readonly Queue<(float Time,int Amount)> delivered = new Queue<(float,int)>();
        static int deliveredWindow;
        internal static void DevReset() { reserved.Clear(); delivered.Clear(); inWindow=deliveredWindow=0; }
        internal static int Heal(int requested, float now)
        {
            while (delivered.Count>0 && now-delivered.Peek().Time>=1f) deliveredWindow-=delivered.Dequeue().Amount;
            var stats=TerraMod.Instance?.Stats;
            if (stats==null || stats.IsDead || PlayerChallenges.RecoveryLocked) return 0;
            int amount=Mathf.Min(PerHit,Mathf.Min(Mathf.Max(0,requested),PerSecond-deliveredWindow));
            int healed=stats.Heal(amount,recovery:true);
            if(healed>0){delivered.Enqueue((now,healed));deliveredWindow+=healed;}
            return healed;
        }
        internal static int Reserve(float requested, float now)
        {
            while (reserved.Count > 0 && now - reserved.Peek().Time >= 1f) inWindow -= reserved.Dequeue().Amount;
            if (PlayerChallenges.RecoveryLocked) return 0;
            int amount = Mathf.Min(PerHit, Mathf.Min(Mathf.Max(0,(int)requested), PerSecond-inWindow));
            if (amount <= 0) return 0;
            amount = ArmorEffects.TakeLifeSteal(amount);
            if (amount <= 0) return 0;
            reserved.Enqueue((now,amount)); inWindow += amount;
            return amount;
        }
    }
}
