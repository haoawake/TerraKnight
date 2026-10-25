// Read native tile flags used to distinguish full building blocks from furniture/plants.
// Compile with Framework csc /platform:x86; args: Terraria.exe output.tsv.
using System;
using System.IO;
using System.Reflection;
using System.Text;
class DumpBlockTiles
{
    static Assembly game;
    static Assembly Resolve(object sender, ResolveEventArgs e)
    {
        string name = new AssemblyName(e.Name).Name;
        foreach (string r in game.GetManifestResourceNames())
            if (r.EndsWith(name + ".dll"))
                using (var s = game.GetManifestResourceStream(r))
                using (var b = new MemoryStream()) { s.CopyTo(b); return Assembly.Load(b.ToArray()); }
        return null;
    }
    static int Main(string[] args)
    {
        try {
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            game = Assembly.LoadFrom(args[0]);
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            game.GetType("Terraria.Program").GetField("SavePath", flags).SetValue(null, Path.GetTempPath());
            var main = game.GetType("Terraria.Main");
            foreach (var m in main.GetMethods(flags))
                if (m.Name == "Initialize_TileAndNPCData1" && m.GetParameters().Length == 0) {
                    Console.WriteLine(m.Name); m.Invoke(null, null);
                }
            main.GetField("rand", flags).SetValue(null, Activator.CreateInstance(game.GetType("Terraria.Utilities.UnifiedRandom")));
            try { main.GetMethod("Initialize_TileAndNPCData2", flags).Invoke(null, null); }
            catch (TargetInvocationException e) {
                if (!(e.InnerException is NullReferenceException) || !e.InnerException.StackTrace.Contains("AnimateTiles_CritterCages")) throw;
                Console.WriteLine("Skipped post-initialization critter-cage animation (no graphical game instance).");
            }
            var solid = (bool[])main.GetField("tileSolid", flags).GetValue(null);
            var top = (bool[])main.GetField("tileSolidTop", flags).GetValue(null);
            var important = (bool[])main.GetField("tileFrameImportant", flags).GetValue(null);
            if (!solid[0] || !solid[1] || !solid[30] || !solid[54] || !important[18]) throw new Exception("Native tile initialization incomplete");
            var sb = new StringBuilder("tile\tsolid\tsolidTop\tframeImportant\n");
            for (int i = 0; i < solid.Length; i++) sb.Append(i).Append('\t').Append(solid[i]).Append('\t').Append(top[i]).Append('\t').Append(important[i]).Append('\n');
            File.WriteAllText(args[1], sb.ToString());
            return 0;
        } catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
