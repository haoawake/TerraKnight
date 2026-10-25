// Read Terraria's own item defaults and fully expanded recipe database without launching a world.
// Compile here with .NET Framework csc /platform:x86 /r:System.Web.Extensions.dll DumpCrafting.cs.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

class DumpArmor
{
    static Assembly game;
    static Dictionary<string, Assembly> loaded = new Dictionary<string, Assembly>();
    static Assembly Resolve(object sender, ResolveEventArgs args)
    {
        string name = new AssemblyName(args.Name).Name;
        Assembly found;
        if (loaded.TryGetValue(name, out found)) return found;
        loaded[name] = null;
        foreach (string resource in game.GetManifestResourceNames())
            if (resource.EndsWith(name + ".dll"))
                using (var stream = game.GetManifestResourceStream(resource))
                {
                    var bytes = new byte[stream.Length];
                    int read = 0;
                    while (read < bytes.Length) read += stream.Read(bytes, read, bytes.Length - read);
                    return loaded[name] = Assembly.Load(bytes);
                }
        return null;
    }
    static object Get(object obj, string name)
    {
        var type = obj as Type ?? obj.GetType();
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        var field = type.GetField(name, flags);
        if (field != null) return field.GetValue(obj is Type ? null : obj);
        var property = type.GetProperty(name, flags);
        return property == null ? null : property.GetValue(obj is Type ? null : obj, null);
    }
    static int Number(object obj, string name) { return Convert.ToInt32(Get(obj, name)); }
    static void Call(Type type, string name) { type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null); }
    static Exception Root(Exception e) { while (e.InnerException != null) e = e.InnerException; return e; }
    static int Main(string[] args)
    {
        try { return Run(args); }
        catch (Exception ex)
        {
            var error = Root(ex);
            Console.WriteLine(error.GetType().FullName);
            Console.WriteLine(error.Message);
            File.WriteAllText(args[1] + ".error", error.Message + "\n" + error.StackTrace);
            return 2;
        }
    }
    static int Run(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        game = Assembly.LoadFrom(args[0]);
        var program = game.GetType("Terraria.Program");
        var path = program.GetField("SavePath", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (path != null) path.SetValue(null, Path.Combine(Path.GetTempPath(), "tk-crafting-audit"));
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(game.GetType("Terraria.Item").TypeHandle);
        Call(game.GetType("Terraria.Lang"), "InitializeLegacyLocalization");
        ((Array)Get(game.GetType("Terraria.Main"), "player")).SetValue(System.Runtime.Serialization.FormatterServices.GetUninitializedObject(game.GetType("Terraria.Player")), 0);
        var itemType = game.GetType("Terraria.Item");
        MethodInfo defaults = null;
        foreach (var m in itemType.GetMethods())
            if (m.Name == "SetDefaults" && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(int)) defaults = m;
        int count = Number(game.GetType("Terraria.ID.ItemID"), "Count");
        var samples = (IDictionary)Get(game.GetType("Terraria.ID.ContentSamples"), "ItemsByType");
        var items = new Dictionary<string, object>();
        string[] fields = { "damage", "defense", "accessory", "vanity", "headSlot", "bodySlot", "legSlot", "pick", "axe", "hammer", "melee", "ranged", "magic", "summon", "shoot", "createTile", "createWall", "value", "rare", "fishingPole", "bait", "ammo", "useTime", "useAnimation", "reuseDelay", "channel", "noMelee", "crit", "shootSpeed", "mana" };
        for (int id = 0; id < count; id++)
        {
            var item = Activator.CreateInstance(itemType);
            try { defaults.Invoke(item, new object[] { id, null }); }
            catch(Exception) { Console.WriteLine("Item failure: " + id); throw; }
            samples[id] = item;
            var row = new Dictionary<string, object>();
            foreach (string field in fields) row[field] = Get(item, field);
            items[id.ToString()] = row;
        }
        var playerType = game.GetType("Terraria.Player");
        var grant = playerType.GetMethod("GrantArmorBenefits", BindingFlags.Instance | BindingFlags.NonPublic);
        string[] stats = { "statDefense", "statManaMax2", "maxMinions", "maxTurrets", "lifeRegen", "meleeDamage", "rangedDamage", "magicDamage", "minionDamage", "meleeCrit", "rangedCrit", "magicCrit", "meleeSpeed", "moveSpeed", "manaCost", "pickSpeed", "endurance", "whipRangeMultiplier", "ammoCost80", "ammoCost75", "ammoCost90", "huntressAmmoCost90", "chloroAmmoCost80", "nightVision", "noKnockback", "fireWalk", "arrowDamage", "bulletDamage", "rocketDamage", "aggro", "fishingSkill", "whipUseTimeMultiplier" };
        var armors = new Dictionary<string, object>();
        for (int id = 1; id < count; id++)
        {
            var item = samples[id];
            if (Number(item,"headSlot") < 0 && Number(item,"bodySlot") < 0 && Number(item,"legSlot") < 0) continue;
            var player = Activator.CreateInstance(playerType);
            playerType.GetField("whipUseTimeMultiplier").SetValue(player,1f);
            var before = new Dictionary<string,object>();
            foreach(var key in stats) before[key]=Get(player,key);
            grant.Invoke(player,new[]{item});
            var row = new Dictionary<string,object>();
            foreach(var key in stats)
            {
                var value=Get(player,key);
                if(value is bool) row[key]=value;
                else if(value!=null) row[key]=Convert.ToDouble(value)-Convert.ToDouble(before[key]);
            }
            armors[id.ToString()]=row;
        }
        var sets = new Dictionary<string, object>();
        var benefits=game.GetType("Terraria.DataStructures.ArmorSetBonuses+Benefits");
        foreach(var method in benefits.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            var player=Activator.CreateInstance(playerType);
            playerType.GetField("whipUseTimeMultiplier").SetValue(player,1f);
            var before=new Dictionary<string,object>();
            foreach(var key in stats) before[key]=Get(player,key);
            string error=null;
            try {method.Invoke(null,new[]{player});} catch(Exception e) {error=Root(e).Message;}
            var row=new Dictionary<string,object>();
            foreach(var key in stats)
            {
                var value=Get(player,key);
                if(value is bool) row[key]=value;
                else if(value!=null) row[key]=Convert.ToDouble(value)-Convert.ToDouble(before[key]);
            }
            sets[method.Name]=new {values=row,error};
        }
        var json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        File.WriteAllText(args[1],json.Serialize(new { items=armors, sets, version=game.GetName().Version.ToString() }),new UTF8Encoding(false));
        Console.WriteLine("Original armor bonus rows: "+armors.Count);
        return 0;
    }
}
