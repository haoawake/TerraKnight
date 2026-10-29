// Read Terraria's vanity items (Item.vanity) and the look tables the player's drawing uses, without launching a world.
// Compile here with .NET Framework csc /platform:x86 /r:System.Web.Extensions.dll DumpVanity.cs; run DumpVanity.exe <Terraria.exe> <out.json>.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

class DumpVanity
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
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static object Get(object obj, string name)
    {
        var type = obj as Type ?? obj.GetType();
        var field = type.GetField(name, All);
        if (field != null) return field.GetValue(obj is Type ? null : obj);
        var property = type.GetProperty(name, All);
        return property == null ? null : property.GetValue(obj is Type ? null : obj, null);
    }
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

    // Every static array of a "Sets" class: bool arrays as the true indices, number arrays as index -> value where not the default.
    static Dictionary<string, object> Sets(Type sets)
    {
        var result = new Dictionary<string, object>();
        foreach (var f in sets.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var value = f.GetValue(null) as Array;
            if (value == null) continue;
            var element = value.GetType().GetElementType();
            if (element == typeof(bool))
            {
                var on = new List<int>();
                for (int i = 0; i < value.Length; i++) if ((bool)value.GetValue(i)) on.Add(i);
                result[f.Name] = on;
            }
            else if (element.IsPrimitive)
            {
                var map = new Dictionary<string, object>();
                long first = Convert.ToInt64(value.Length > 0 ? value.GetValue(value.Length - 1) : 0);
                // The set's default: the most common value.
                var counts = new Dictionary<long, int>();
                for (int i = 0; i < value.Length; i++) { long v = Convert.ToInt64(value.GetValue(i)); counts[v] = (counts.ContainsKey(v) ? counts[v] : 0) + 1; }
                long common = first; int best = -1;
                foreach (var kv in counts) if (kv.Value > best) { best = kv.Value; common = kv.Key; }
                for (int i = 0; i < value.Length; i++) { long v = Convert.ToInt64(value.GetValue(i)); if (v != common) map[i.ToString()] = v; }
                result[f.Name] = new Dictionary<string, object> { { "default", common }, { "values", map } };
            }
            else
            {
                // Structs (IncludeCapeFrontAndBack): their primitive fields, the valid ones.
                var map = new Dictionary<string, object>();
                for (int i = 0; i < value.Length; i++)
                {
                    var s = value.GetValue(i);
                    var invalid = Get(s, "Invalid");
                    if (invalid is bool && (bool)invalid) continue;
                    var row = new Dictionary<string, object>();
                    foreach (var sf in element.GetFields(BindingFlags.Public | BindingFlags.Instance)) row[sf.Name] = sf.GetValue(s);
                    map[i.ToString()] = row;
                }
                result[f.Name] = map;
            }
        }
        return result;
    }

    static int Run(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        game = Assembly.LoadFrom(args[0]);
        var program = game.GetType("Terraria.Program");
        var path = program.GetField("SavePath", All);
        if (path != null) path.SetValue(null, Path.Combine(Path.GetTempPath(), "tk-vanity-dump"));
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(game.GetType("Terraria.Item").TypeHandle);
        game.GetType("Terraria.Lang").GetMethod("InitializeLegacyLocalization", All).Invoke(null, null);
        ((Array)Get(game.GetType("Terraria.Main"), "player")).SetValue(System.Runtime.Serialization.FormatterServices.GetUninitializedObject(game.GetType("Terraria.Player")), 0);
        var itemType = game.GetType("Terraria.Item");
        MethodInfo defaults = null;
        foreach (var m in itemType.GetMethods())
            if (m.Name == "SetDefaults" && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(int)) defaults = m;
        var idType = game.GetType("Terraria.ID.ItemID");
        int count = Convert.ToInt32(Get(idType, "Count"));
        var names = new Dictionary<int, string>();
        foreach (var f in idType.GetFields(BindingFlags.Public | BindingFlags.Static))
            if (f.IsLiteral && (f.FieldType == typeof(short) || f.FieldType == typeof(int))) { int v = Convert.ToInt32(f.GetValue(null)); if (!names.ContainsKey(v)) names[v] = f.Name; }
        var lang = game.GetType("Terraria.Lang");
        var nameOf = lang.GetMethod("GetItemNameValue", All);
        string[] fields = { "headSlot", "bodySlot", "legSlot", "handOnSlot", "handOffSlot", "backSlot", "frontSlot", "shoeSlot", "waistSlot", "wingSlot",
            "shieldSlot", "neckSlot", "faceSlot", "balloonSlot", "beardSlot", "rare", "value", "maxStack", "accessory", "vanity", "glowMask", "defense", "width", "height" };
        var items = new List<object>();
        for (int id = 1; id < count; id++)
        {
            var item = Activator.CreateInstance(itemType);
            defaults.Invoke(item, new object[] { id, null });
            if (!(bool)Get(item, "vanity")) continue;
            var row = new Dictionary<string, object>();
            row["id"] = id;
            row["name"] = names.ContainsKey(id) ? names[id] : null;
            try { row["en"] = nameOf.Invoke(null, new object[] { id }); } catch (Exception) { row["en"] = null; }
            foreach (string field in fields) { var v = Get(item, field); if (v != null) row[field] = v; }
            items.Add(row);
        }
        var armor = game.GetType("Terraria.ID.ArmorIDs");
        var tables = new Dictionary<string, object>();
        foreach (var nested in armor.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            var sets = nested.GetNestedType("Sets", BindingFlags.Public | BindingFlags.NonPublic);
            if (sets == null) continue;
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(sets.TypeHandle);
            tables[nested.Name] = Sets(sets);
        }
        var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 64 };
        File.WriteAllText(args[1], serializer.Serialize(new { itemCount = count, items, armor = tables }), new UTF8Encoding(false));
        Console.WriteLine("Vanity items=" + items.Count + "; armor tables=" + tables.Count);
        return 0;
    }
}
