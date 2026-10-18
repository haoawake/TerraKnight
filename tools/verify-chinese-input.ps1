$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
function Method([string]$file,[string]$marker){
 $s=Get-Content (Join-Path $root $file) -Raw;$start=$s.IndexOf($marker);if($start -lt 0){throw "Missing $marker"}
 $brace=$s.IndexOf('{',$start);$depth=1;$end=$brace+1
 while($depth -gt 0){if($s[$end] -eq '{'){$depth++};if($s[$end] -eq '}'){$depth--};$end++}
 $s.Substring($start,$end-$start)
}
$methods=@('static void StartRename(','static void TypedChar(','static void EndRename(','static void TickRename(','static string ChestTitle(') | ForEach-Object {Method 'src/TerraKnight/Items/InventoryGuiChest.cs' $_}
$methods+=@('static void StartBookSearch(','static void BookAppend(','static void EndBookSearch(','static void TickBookSearch(','static void SetBookSearch(') | ForEach-Object {Method 'src/TerraKnight/Items/InventoryGuiBestiary.cs' $_}
$stub=@'
using System;using UnityEngine;using UnityEngine.InputSystem;using TerraKnight.World;
namespace UnityEngine {
 public struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}}
 public enum IMECompositionMode{Auto,On,Off}
 public static class Time {public static int frameCount;public static float unscaledTime;}
 public static class Input {public static bool unavailable;public static string inputString="",compositionString="";static IMECompositionMode mode=IMECompositionMode.Auto;public static IMECompositionMode imeCompositionMode{get{if(unavailable)throw new InvalidOperationException();return mode;}set{if(unavailable)throw new InvalidOperationException();mode=value;}}public static Vector2 compositionCursorPos;}
}
namespace UnityEngine.InputSystem.LowLevel {public struct IMECompositionString {string value;public IMECompositionString(string s){value=s;}public override string ToString()=>value;}}
namespace UnityEngine.InputSystem {
 public class Key {public bool wasPressedThisFrame;}
 public class Keyboard {
  public static Keyboard current;public bool enabled;public Vector2 cursor;Action<char> text;Action<LowLevel.IMECompositionString> composition;
  public int listeners=>text?.GetInvocationList().Length??0;
  public event Action<char> onTextInput{add{text+=value;}remove{text-=value;}}public event Action<LowLevel.IMECompositionString> onIMECompositionChange{add{composition+=value;}remove{composition-=value;}}
  public Key backspaceKey=new Key(),enterKey=new Key(),numpadEnterKey=new Key(),escapeKey=new Key();
  public void SetIMEEnabled(bool b){enabled=b;}public void SetIMECursorPosition(Vector2 p){cursor=p;}
  public void Type(string s){foreach(char c in s)text?.Invoke(c);}public void Compose(string s){composition?.Invoke(new LowLevel.IMECompositionString(s));}
  public void ClearKeys(){backspaceKey.wasPressedThisFrame=enterKey.wasPressedThisFrame=numpadEnterKey.wasPressedThisFrame=escapeKey.wasPressedThisFrame=false;}
 }
}
namespace TerraKnight {static class Lang{public static string T(string s)=>s;}}
namespace TerraKnight.World {static class Chests{public static bool IsOpen=true,BankOpen;public static string OpenName="",BankTitle="bank";public const int MaxNameLength=20;public static int saves;public static void SetOpenName(string s){OpenName=s;saves++;}}}
namespace TerraKnight.Items {
public static class ChineseInputScenarios {
 static bool renaming,bookTyping,Open=true,bestiaryOpen=true;static string nameDraft="",bookSearch="",bookComposing="";static int renameEnded=-10,bookTypingEnded=-10,filters;
 static readonly UI.ImeTextEntry nameInput=new UI.ImeTextEntry(TypedChar),bookInput=new UI.ImeTextEntry(BookAppend);
 static void Sound(string s){}static void ResetBookSearchFilters(){filters++;}
 static int checks;static void Check(bool b,string s){checks++;if(!b)throw new Exception(s);}
 static void Next(){Time.frameCount++;Keyboard.current?.ClearKeys();Input.inputString="";Input.compositionString="";}
 static void Settle(){Next();TickRename();TickBookSearch();Next();TickRename();TickBookSearch();Next();TickRename();TickBookSearch();}
 public static string Run(){
  var first=new Keyboard();Keyboard.current=first;StartRename();Check(first.enabled&&Input.imeCompositionMode==IMECompositionMode.On&&first.listeners==1,"rename never enables IME");
  Next();first.Compose("cai liao");first.enterKey.wasPressedThisFrame=true;TickRename();Check(renaming&&Chests.saves==0,"candidate Enter saved chest");Check(ChestTitle().Contains("cai liao"),"rename composition invisible");
  Next();first.Type("材料箱");first.Compose("");Input.inputString="材料箱";first.enterKey.wasPressedThisFrame=true;TickRename();Check(renaming&&nameDraft=="材料箱","Chinese commit lost/duplicated or ended rename");Check(Chests.saves==0,"commit Enter leaked into save");
  Settle();first.enterKey.wasPressedThisFrame=true;TickRename();Check(!renaming&&Chests.OpenName=="材料箱"&&Chests.saves==1,"explicit Enter did not save Chinese name");Check(first.listeners==0&&!first.enabled&&Input.imeCompositionMode==IMECompositionMode.Auto,"rename leaked IME ownership");
  StartBookSearch();Next();first.Compose("qi qiu");first.backspaceKey.wasPressedThisFrame=true;TickBookSearch();Check(bookTyping&&bookSearch==""&&bookComposing=="qi qiu","composition backspace edited committed query");
  Next();first.Type("气球");first.Compose("");first.numpadEnterKey.wasPressedThisFrame=true;TickBookSearch();Check(bookTyping&&bookSearch=="气球"&&filters>0,"candidate numpad Enter ended search");
  Settle();first.backspaceKey.wasPressedThisFrame=true;TickBookSearch();Check(bookSearch=="气","Chinese backspace failed");
  var second=new Keyboard();Keyboard.current=second;Next();TickBookSearch();Check(first.listeners==0&&second.listeners==1&&second.enabled,"keyboard replacement leaves stale hook");second.Type("球");Check(bookSearch=="气球","new keyboard text ignored");
  second.enabled=false;Input.imeCompositionMode=IMECompositionMode.Off;Next();TickBookSearch();Check(second.enabled&&Input.imeCompositionMode==IMECompositionMode.On,"IME not restored after focus reset");
  bookInput.Anchor(new Vector2(80,120));Check(second.cursor.x==80&&Input.compositionCursorPos.y==120,"candidate cursor not anchored for both backends");
  StartRename();Check(!bookTyping&&renaming&&second.listeners==1,"switching editors leaves two text consumers");second.Type("改名");Check(nameDraft=="材料箱改名"&&bookSearch=="气球","text went to wrong editor");
  Next();Chests.IsOpen=false;TickRename();Check(!renaming&&second.listeners==0&&!second.enabled,"closed chest leaks keyboard hook");Check(Chests.saves==1,"closing chest saved unconfirmed name");Chests.IsOpen=true;
  StartBookSearch();Open=false;TickBookSearch();Check(!bookTyping&&second.listeners==0,"closed inventory leaks search IME");Open=true;
  Input.unavailable=true;StartBookSearch();Next();second.Compose("zhong");second.Type("中");second.Compose("");TickBookSearch();Check(bookTyping&&bookSearch=="气球中","Input System-only Chinese backend failed");EndBookSearch();Input.unavailable=false;
  Keyboard.current=null;StartBookSearch();Next();Input.inputString="文";TickBookSearch();TickBookSearch();Check(bookSearch=="气球中文","legacy fallback duplicated a frame");EndBookSearch();
  Check(UI.ImeTextEntry.Backspace("中文")=="中","Chinese text-element deletion");Check(UI.ImeTextEntry.Backspace("a\U0001F642")=="a","surrogate pair split");Check(UI.ImeTextEntry.Backspace("ae\u0301")=="a","combining mark split");
  Keyboard.current=second;StartBookSearch();bookSearch="";Next();second.Type(new string('中',40));TickBookSearch();Check(bookSearch.Length==32,"search length limit broken");EndBookSearch();
  Chests.OpenName="";StartRename();Next();second.Type(new string('箱',30));TickRename();Check(nameDraft.Length==20,"chest name length limit broken");EndRename(false);
  return "PASS "+checks+" actual IME and chest/search lifecycle checks; no native OS input or gameplay test";
 }
}
}
'@
$stub=$stub.Insert($stub.LastIndexOf('}', $stub.LastIndexOf('}')-1),($methods -join "`n")+"`n")
$temp=Join-Path ([IO.Path]::GetTempPath()) ('chinese-input-'+[guid]::NewGuid().ToString('N')+'.cs')
try {
 [IO.File]::WriteAllText($temp,$stub)
 Add-Type -Path @((Join-Path $root 'src/TerraKnight/UI/ImeTextEntry.cs'),$temp) -CompilerOptions '/warn:0'
 [TerraKnight.Items.ChineseInputScenarios]::Run()
} finally {Remove-Item -LiteralPath $temp -ErrorAction SilentlyContinue}
$chat=Get-Content (Join-Path $root 'src/TerraKnight/Net/NetChat.cs') -Raw
if(!$chat.Contains('if (Items.InventoryGui.TypingName) return;')){throw 'Chat can steal candidate numpad Enter'}
$inventory=Get-Content (Join-Path $root 'src/TerraKnight/Items/InventoryGui.cs') -Raw
if(!$inventory.Contains("EndRename(false);") -or !$inventory.Contains('EndBookSearch();')){throw 'Inventory close does not release editors'}
'PASS chat shortcut isolation and inventory close wiring'
