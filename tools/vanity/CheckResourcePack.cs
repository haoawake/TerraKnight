// Read-only inspection/loading of the installed Terraria resource-pack API.
using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Security.Cryptography;

class CheckResourcePack {
 class Services:IServiceProvider {public object GetService(Type t){return null;}}
 static Assembly game;
 static Assembly Resolve(object s, ResolveEventArgs a) {
  string name=new AssemblyName(a.Name).Name;
  foreach(string resource in game.GetManifestResourceNames()) if(resource.EndsWith(name+".dll"))
   using(var stream=game.GetManifestResourceStream(resource)) using(var bytes=new MemoryStream()) {stream.CopyTo(bytes);return Assembly.Load(bytes.ToArray());}
  return null;
 }
 static int Main(string[] args) {
  try {
   AppDomain.CurrentDomain.AssemblyResolve+=Resolve;
   game=Assembly.LoadFrom(args[0]);
   var type=game.GetType("Terraria.IO.ResourcePack",true);
   if(args.Length<2) {
    foreach(var c in type.GetConstructors()) Console.WriteLine("CTOR "+c);
    foreach(var m in type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly)) Console.WriteLine("API "+m);
    return 0;
   }
   var ctor=type.GetConstructors()[0];var ps=ctor.GetParameters();
   object pack=ctor.Invoke(new object[]{new Services(),Path.GetFullPath(args[1]),Enum.ToObject(ps[2].ParameterType,0)});
   string packName=(string)type.GetProperty("Name").GetValue(pack,null);
   object source=type.GetMethod("GetContentSource").Invoke(pack,null);
   var st=source.GetType();var names=(IEnumerable)st.GetMethod("GetAllAssetsStartingWith").Invoke(source,new object[]{""});
   var records=new List<object>();int count=0;
   foreach(string name in names) {
    string ext=(string)st.GetMethod("GetExtension").Invoke(source,new object[]{name});
    if(ext!=".png") throw new Exception("Unexpected content asset: "+name+ext);
    byte[] bytes;
    using(var stream=(Stream)st.GetMethod("OpenStream").Invoke(source,new object[]{name})) using(var ms=new MemoryStream()){stream.CopyTo(ms);bytes=ms.ToArray();}
    if(bytes.Length<24||bytes[0]!=137||bytes[1]!=80||bytes[2]!=78||bytes[3]!=71)throw new Exception("Invalid PNG stream: "+name);
    int w=(bytes[16]<<24)|(bytes[17]<<16)|(bytes[18]<<8)|bytes[19];
    int h=(bytes[20]<<24)|(bytes[21]<<16)|(bytes[22]<<8)|bytes[23];
    string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
    records.Add(new {asset=name,extension=ext,width=w,height=h,sha256=hash});count++;
   }
   if(count==0)throw new Exception("Empty resource pack");
   if(args.Length>=3) File.WriteAllText(args[2],new JavaScriptSerializer().Serialize(new {packName,sourceType=st.FullName,count,installedGameVersion=System.Diagnostics.FileVersionInfo.GetVersionInfo(args[0]).FileVersion,gameResourcePackContentLoaderVerified=true,runtimeEquipmentVerified=false,assets=records}));
   Console.WriteLine("Terraria pack loader accepted "+count+" PNG streams: "+packName+" ("+st.Name+")");
   return 0;
  } catch(Exception e) {Console.Error.WriteLine(e);return 1;}
 }
}
