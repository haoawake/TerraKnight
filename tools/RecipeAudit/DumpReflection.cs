// Export the installed Terraria build's actual CanBeReflected result, without starting a world.
// Compile with .NET Framework csc /platform:x86 /r:System.Web.Extensions.dll.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;

class DumpReflection
{
    static Assembly game;
    static Assembly Resolve(object sender, ResolveEventArgs args)
    {
        string name = new AssemblyName(args.Name).Name;
        foreach (string resource in game.GetManifestResourceNames())
            if (resource.EndsWith(name + ".dll"))
                using (var stream = game.GetManifestResourceStream(resource))
                using (var bytes = new MemoryStream()) { stream.CopyTo(bytes); return Assembly.Load(bytes.ToArray()); }
        return null;
    }
    static int Main(string[] args)
    {
        try {
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            game = Assembly.LoadFrom(args[0]);
            game.GetType("Terraria.Program").GetField("SavePath", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, Path.GetTempPath());
            game.GetType("Terraria.Lang").GetMethod("InitializeLegacyLocalization", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            var rng = Activator.CreateInstance(game.GetType("Terraria.Utilities.UnifiedRandom"));
            game.GetType("Terraria.Main").GetField("rand", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, rng);
            var type = game.GetType("Terraria.Projectile");
            var set = type.GetMethod("SetDefaults", new[] { typeof(int) });
            var check = type.GetMethod("CanBeReflected");
            int count = Convert.ToInt32(game.GetType("Terraria.ID.ProjectileID").GetField("Count").GetValue(null));
            var rows = new List<object>();
            for (int id = 0; id < count; id++) {
                var p = Activator.CreateInstance(type);
                try { set.Invoke(p, new object[] { id }); } catch { Console.Error.WriteLine("Projectile " + id); throw; }
                type.GetField("damage").SetValue(p, 100);
                rows.Add(new { Id = id, AiStyle = type.GetField("aiStyle").GetValue(p),
                    Friendly = type.GetField("friendly").GetValue(p),
                    Hostile = type.GetField("hostile").GetValue(p),
                    Reflectable = check.Invoke(p, null) });
            }
            File.WriteAllText(args[1], new JavaScriptSerializer().Serialize(new { AssemblyVersion = game.GetName().Version.ToString(), Count = count, Rows = rows }));
            Console.WriteLine("Exported " + count + " projectile defaults and reflection checks.");
            return 0;
        } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
