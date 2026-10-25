// Dumps every placeable wall from Item.SetDefaults (args: Terraria.exe out.tsv). Build with .NET Framework csc /platform:x86.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

class DumpItems
{
    static Assembly terraria;
    static readonly Dictionary<string, Assembly> loaded = new Dictionary<string, Assembly>();

    static Assembly Resolve(object sender, ResolveEventArgs e)
    {
        string name = new AssemblyName(e.Name).Name;
        Assembly a;
        if (loaded.TryGetValue(name, out a)) return a;
        loaded[name] = null;
        foreach (string r in terraria.GetManifestResourceNames())
        {
            if (!r.EndsWith(name + ".dll")) continue;
            using (Stream s = terraria.GetManifestResourceStream(r))
            {
                byte[] buf = new byte[s.Length];
                s.Read(buf, 0, buf.Length);
                a = Assembly.Load(buf);
                loaded[name] = a;
                return a;
            }
        }
        return null;
    }

    static readonly string[] fields = { "value", "rare", "maxStack", "damage", "knockBack", "useTime", "useAnimation", "useStyle", "hammer", "pick", "axe",
        "shoot", "shootSpeed", "fishingPole", "bait", "accessory", "consumable", "material", "questItem", "width", "height", "createTile", "createWall", "headSlot", "bodySlot", "legSlot", "defense",
        "buffType", "buffTime", "healLife", "noMelee", "noUseGraphic", "autoReuse", "melee", "ranged", "magic", "crit", "holdStyle" };

    static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        terraria = Assembly.LoadFrom(args[0]);
        System.Threading.Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;
        var prog = terraria.GetType("Terraria.Program");
        var sp = prog.GetField("SavePath", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (sp != null) sp.SetValue(null, Path.Combine(Path.GetTempPath(), "tkdump"));
        else { var pp = prog.GetProperty("SavePath", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static); if (pp != null && pp.CanWrite) pp.SetValue(null, Path.Combine(Path.GetTempPath(), "tkdump"), null); }
        try { System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(terraria.GetType("Terraria.Item").TypeHandle); }
        catch (Exception ex) { Exception e = ex; while (e.InnerException != null) e = e.InnerException; File.WriteAllText(args[1] + ".err", e.ToString()); }
        Type itemType = terraria.GetType("Terraria.Item");
        MethodInfo setDefaults = null;
        foreach (var m in itemType.GetMethods()) if (m.Name == "SetDefaults" && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(int)) setDefaults = m;
        var sb = new StringBuilder();
        sb.Append("id");
        foreach (var f in fields) sb.Append('\t').Append(f);
        sb.Append('\n');
        int count = Convert.ToInt32(terraria.GetType("Terraria.ID.ItemID").GetField("Count").GetValue(null));
        for (int id = 1; id < count; id++)
        {
            object item = Activator.CreateInstance(itemType);
            try { setDefaults.Invoke(item, new object[] { id, null }); }
            catch (Exception ex) { sb.Append(id).Append("\tERR ").Append((ex.InnerException != null ? ex.InnerException.ToString() : ex.ToString()).Replace('\n', ' ').Replace('\r', ' ')).Append('\n'); continue; }
            if ((int)itemType.GetField("createWall").GetValue(item) <= 0) continue;
            sb.Append(id);
            foreach (var f in fields)
            {
                var fi = itemType.GetField(f);
                object v = null;
                if (fi != null) v = fi.GetValue(item);
                else
                {
                    var pi = itemType.GetProperty(f);
                    if (pi != null) v = pi.GetValue(item, null);
                }
                sb.Append('\t').Append(v == null ? "" : v.GetType().Name == "DamageClass" ? v.ToString() : v.ToString());
            }
            sb.Append('\n');
        }
        File.WriteAllText(args[1], sb.ToString());
        return 0;
    }
}
