// Reflect field layouts only. Does not instantiate game types or invoke game methods.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Web.Script.Serialization;

class EnemyAttackSchema
{
    static string managed;
    static Dictionary<string, object> layouts = new Dictionary<string, object>();
    static bool Pointer(Type t)
    {
        for (var p=t; p!=null; p=p.BaseType) if (p.FullName=="UnityEngine.Object") return true;
        return false;
    }
    static string Describe(Type t, bool forceClass=false)
    {
        string key=t.FullName;
        if (layouts.ContainsKey(key) && !forceClass) return key;
        if(t.IsEnum) { layouts[key]=new {kind="enum", element=Describe(Enum.GetUnderlyingType(t))}; return key; }
        if(t.IsPrimitive || t==typeof(string)) { layouts[key]=new{kind="primitive"}; return key; }
        if(Pointer(t) && !forceClass) { layouts[key]=new{kind="pointer"}; return key; }
        if(t.IsArray) {layouts[key]=new{kind="array",element=Describe(t.GetElementType())};return key;}
        if(t.IsGenericType && t.GetGenericTypeDefinition()==typeof(List<>)) {layouts[key]=new{kind="array",element=Describe(t.GetGenericArguments()[0])};return key;}
        layouts[key]=null;
        var fields=new List<object>();
        var chain=new List<Type>();
        for(var p=t;p!=null && p!=typeof(object);p=p.BaseType)chain.Insert(0,p);
        foreach(var p in chain) foreach(var f in p.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly).OrderBy(f=>f.MetadataToken))
        {
            if(f.IsStatic||f.IsInitOnly||f.IsNotSerialized)continue;
            if(!f.IsPublic && !f.GetCustomAttributesData().Any(a=>a.AttributeType.FullName=="UnityEngine.SerializeField"))continue;
            if(f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition()!=typeof(List<>))continue;
            fields.Add(new{name=f.Name,type=Describe(f.FieldType)});
        }
        layouts[key]=new{kind="class",fields=fields};
        return key;
    }
    static int Main(string[] args)
    {
        managed=args[0];
        AppDomain.CurrentDomain.AssemblyResolve+=(sender,e)=>{
            var path=Path.Combine(managed,new AssemblyName(e.Name).Name+".dll");
            return File.Exists(path)?Assembly.LoadFrom(path):null;
        };
        var play=Assembly.LoadFrom(Path.Combine(managed,"PlayMaker.dll"));
        var t=play.GetType("PlayMakerFSM",true);
        // Native MonoBehaviour header is parsed independently, so only declared fields.
        var top=t.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly)
            .Where(f=>!f.IsStatic&&!f.IsNotSerialized&&(f.IsPublic||f.GetCustomAttributesData().Any(a=>a.AttributeType.FullName=="UnityEngine.SerializeField")))
            .OrderBy(f=>f.MetadataToken).Select(f=>new{name=f.Name,type=Describe(f.FieldType)}).ToArray();
        var game=Assembly.LoadFrom(Path.Combine(managed,"Assembly-CSharp.dll"));
        foreach(var type in game.GetTypes().Where(x=>x.Name.StartsWith("EnemyDeathEffects")||x.Name=="HealthManager"||x.Name=="DamageHero"))Describe(type,true);
        File.WriteAllText(args[1],new JavaScriptSerializer{MaxJsonLength=int.MaxValue,RecursionLimit=500}.Serialize(new{fields=top,layouts=layouts}));
        Console.WriteLine("Layouts: "+layouts.Count);
        return 0;
    }
}
