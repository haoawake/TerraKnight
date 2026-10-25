using System;
using System.Collections.Generic;

namespace TerraKnight.Items
{
    /// <summary>Read-only navigation over the real recipe and regional drop databases.</summary>
    internal static class RecipeTracking
    {
        internal sealed class Node
        {
            public ItemType Type;
            public int Required, Variant;
            public IReadOnlyList<Recipe> Options => For(Type);
            public Recipe Recipe => Options.Count>0?Options[Math.Min(Variant,Options.Count-1)]:null;
        }
        static readonly Dictionary<ItemType,List<Recipe>> byResult=new Dictionary<ItemType,List<Recipe>>();
        static readonly List<Recipe> empty=new List<Recipe>();
        static bool indexed;
        static HashSet<ItemType> farmable;
        static bool claimed;
        static IEnumerator<bool> farmWork;
        internal static bool SourcesReady => farmable != null && farmWork == null && claimed == EssentialDrops.ManipulatorGiven;
        internal static void TickSources()
        {
            if(farmable == null || claimed != EssentialDrops.ManipulatorGiven) {
                farmWork?.Dispose(); claimed=EssentialDrops.ManipulatorGiven;
                farmable=new HashSet<ItemType>(); farmWork=BuildSources().GetEnumerator();
            }
            if(farmWork==null)return;
            var budget=System.Diagnostics.Stopwatch.StartNew();
            do { if(!farmWork.MoveNext()){farmWork.Dispose();farmWork=null;break;} }
            while(budget.Elapsed.TotalMilliseconds<2);
        }
        static IEnumerable<bool> BuildSources()
        {
            foreach(string record in EnemyDrops.DevRecords) {
                if(EnemyDrops.KindOf(record)=="non_enemy"||EnemyDrops.KindOf(record)=="player_shade")continue;
                foreach(var home in BestiaryHabitats.Of(record))for(int stage=0;stage<=World.Areas.MaxLevel;stage++) {
                    foreach(var drop in EnemyDrops.At(record,stage,true,true,true,home.Scene))
                        if(drop.Type!=ItemType.None&&drop.Num>0)farmable.Add(drop.Type);
                    yield return true;
                }
            }
        }
        internal static readonly List<Node> Path=new List<Node>();
        public static bool Active => Path.Count>0;
        public static Node Current => Active?Path[Path.Count-1]:null;
        public static IReadOnlyList<Recipe> For(ItemType type)
        {
            if(!indexed) {
                foreach(var r in Recipes.All) {
                    if(r.IsBought)continue;
                    if(!byResult.TryGetValue(r.Result,out var list))byResult[r.Result]=list=new List<Recipe>();
                    list.Add(r);
                }
                indexed=true;
            }
            return byResult.TryGetValue(type,out var result)?result:empty;
        }
        public static void Clear()=>Path.Clear();
        public static void Start(Recipe recipe)
        {
            Clear();var node=new Node{Type=recipe.Result,Required=1};
            for(int i=0;i<node.Options.Count;i++)if(ReferenceEquals(node.Options[i],recipe)){node.Variant=i;break;}
            Path.Add(node);
        }
        public static void Enter(ItemType type,int required)
        {
            // Conversion recipes (wood/platforms, bars and their conversions...) must not make an endless path.
            for(int i=0;i<Path.Count;i++)if(Path[i].Type==type){Jump(i);return;}
            if(Path.Count>=64)return;
            Path.Add(new Node{Type=type,Required=Math.Max(1,required)});
        }
        public static void Jump(int index) { if(index>=0&&index<Path.Count)Path.RemoveRange(index+1,Path.Count-index-1); }
        public static void Back()=>Jump(Path.Count-2);
        public static void Variant(int delta)
        {
            var n=Current;if(n==null||n.Options.Count==0)return;
            n.Variant=(n.Variant+delta%n.Options.Count+n.Options.Count)%n.Options.Count;
        }
        internal static int Needed(Node node,int ingredientCount)
        {
            int output=Math.Max(1,node.Recipe?.Count??1);
            long batches=((long)node.Required+output-1)/output;
            return (int)Math.Min(int.MaxValue,batches*ingredientCount);
        }
        public static bool Farmable(ItemType type)
        {
            return SourcesReady && farmable.Contains(type);
        }
    }
}
