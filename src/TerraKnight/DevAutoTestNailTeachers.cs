using System;
using System.Collections;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using TerraKnight.Items;
using UnityEngine.InputSystem;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator StartTeacherTalk(PlayMakerFSM teacher)
        {
            var hero=HeroController.instance;
            hero.RegainControl();hero.StartAnimationControl();hero.FaceRight();
            Put(hero,(Vector2)teacher.transform.position+new Vector2(-2,1));
            yield return new WaitForSecondsRealtime(1f);
            yield return Tap(Player.TerraKeys.Get(Player.TerraKeys.Action.Up),.15f);
            yield return new WaitForSecondsRealtime(.2f);
        }

        IEnumerator SelectTeacherChoice(PlayMakerFSM choice,int wanted,bool unavailableAllowed=false)
        {
            var menuObject=choice.FsmVariables.FindFsmGameObject("UI List")?.Value;
            var menu=menuObject!=null?menuObject.GetComponents<PlayMakerFSM>().FirstOrDefault(f=>f.FsmName=="ui_list"):null;
            if(menu==null)throw new InvalidOperationException("Native yes/no list not found");
            float readyUntil=Time.realtimeSinceStartup+2f;
            while((!menu.gameObject.activeInHierarchy || string.IsNullOrEmpty(menu.ActiveStateName)) && Time.realtimeSinceStartup<readyUntil)yield return null;
            yield return new WaitForSecondsRealtime(.15f);
            // Drive the list's own navigation events, independent of user menu bindings.
            // Confirmation still goes through the real input and the native payment FSM.
            var navigation=menu.FsmStates.SelectMany(s=>s.Transitions).Select(t=>t.EventName).Distinct()
                .Where(e=>e.Contains("RIGHT")||e.Contains("LEFT")||e.Contains("DOWN")||e.Contains("UP")).ToArray();
            foreach(var ev in navigation){
                if(menu.FsmVariables.FindFsmInt("Current Item")?.Value==wanted)break;
                menu.SendEvent(ev);yield return new WaitForSecondsRealtime(.15f);
            }
            int selected=menu.FsmVariables.FindFsmInt("Current Item")?.Value??-1;
            Plugin.Log.LogInfo($"[AutoTest] TEACH_CHOICE wanted={wanted} selected={selected} state={menu.ActiveStateName}");
            if(selected!=wanted)Plugin.Log.LogInfo("[AutoTest] TEACH_NAV "+string.Join(",",navigation));
            if(!unavailableAllowed && selected!=wanted)Plugin.Log.LogError("[AutoTest] NAIL_TEACHERS native-choice 不通过");
            yield return Tap(Key.Space,.1f);
        }

        IEnumerator GuardedNailTeachers()
        {
            var work=NailTeachersTest();
            try { while(true) { bool more;
                try { more=work.MoveNext(); } catch(Exception e) {Plugin.Log.LogError("[AutoTest] NAIL_TEACHERS 不通过 "+e);break;}
                if(!more)break;yield return work.Current;
            }} finally {(work as IDisposable)?.Dispose();KeysDown();}
        }

        IEnumerator NailTeachersTest()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated save required");
            mod.ApplyCreative(false);TerraMod.DevAllInfinite=false;
            var pd=PlayerData.instance;var hero=HeroController.instance;
            mod.DevNoFallDamage=true;pd.isInvincible=true;
            foreach(var flag in new[]{"hasNailArt","hasCyclone","hasDashSlash","hasUpwardSlash","hasAllNailArts",
                "metNailmasterMato","metNailmasterSheo","metNailmasterOro"}) pd.SetBool(flag,false);
            var evidence=new List<object>();int checks=0,failed=0;
            void Check(string label,bool pass){checks++;if(!pass)failed++;Plugin.Log.LogInfo($"[AutoTest] NAIL_TEACHERS {label} {(pass?"通过":"不通过")}");}
            foreach(var entry in new[]{("Room_nailmaster","hasCyclone"),("Room_nailmaster_02","hasDashSlash"),("Room_nailmaster_03","hasUpwardSlash")})
            {
                string room=entry.Item1,flag=entry.Item2;
                yield return GoTo(room,"left1");
                var teacher=Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                    .FirstOrDefault(f=>f.gameObject.scene.name==room && f.FsmName=="Conversation Control" && f.name.StartsWith("NM ") && !f.name.Contains("Modeller"));
                Check(room+"/teacher",teacher!=null);if(teacher==null)continue;
                Check(room+"/first-offer",Player.NailServices.DevTarget(teacher,"Meet","CONVO_FINISH")=="Box Down2");
                Check(room+"/repeat-offer",Player.NailServices.DevTarget(teacher,"Reoffer","CONVO_FINISH")=="Box Down2"
                    && Player.NailServices.DevTarget(teacher,"Convo Choice","REOFFER")=="Reoffer");
                Shop.Spend(Shop.Purse);Shop.Earn(1000000);
                Put(hero,(Vector2)teacher.transform.position+new Vector2(-3,1));
                var trail=new List<string>();bool declined=false,sawOffer=false,sawReoffer=false,dumped=false,poorStarted=false,poorChecked=false;int offers=0;
                long before=Shop.Purse;int toll=-1,confirmAtOffer=2;float nextInput=0;
                yield return StartTeacherTalk(teacher);
                float until=Time.realtimeSinceStartup+85f;
                while(Time.realtimeSinceStartup<until)
                {
                    string state=teacher.ActiveStateName;
                    if(trail.Count==0 || trail[trail.Count-1]!=state){trail.Add(state);if(state=="Send Text"){offers++;sawReoffer=offers>=2;}Plugin.Log.LogInfo("[AutoTest] TEACH_STATE "+room+" "+state);}
                    if(state=="Send Text")
                    {
                        sawOffer=true;
                        // After confirming a choice, the native UI closes before its requester
                        // receives NO/YES. Do not act on that same offer a second time.
                        if(declined && offers<confirmAtOffer){yield return null;continue;}
                        var choice=Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                            .FirstOrDefault(f=>f.name=="Text YN" && f.FsmName=="Dialogue Page Control");
                        if(choice!=null){toll=choice.FsmVariables.FindFsmInt("Toll Cost")?.Value??-1;
                            if(!dumped){DumpFsm(choice);dumped=true;}}
                        // Let the actual yes/no UI finish typing and activate its list before choosing.
                        if(choice!=null && choice.ActiveStateName!="Ready for Input"){
                            if(Time.realtimeSinceStartup>=nextInput){nextInput=Time.realtimeSinceStartup+1f;yield return Tap(Key.Space,.08f);}
                            yield return null;continue;
                        }
                        if(!declined){Shot(room,"offer");yield return SelectTeacherChoice(choice,2);declined=true;Check(room+"/decline-does-not-learn",!pd.GetBool(flag)&&Shop.Purse==before);}
                        else if(poorStarted && !poorChecked){
                            yield return SelectTeacherChoice(choice,1,true);yield return new WaitForSecondsRealtime(1.2f);
                            Check(room+"/insufficient-funds-blocked",!pd.GetBool(flag)&&Shop.Purse==0);poorChecked=true;
                            if(teacher.ActiveStateName=="Send Text"){yield return SelectTeacherChoice(choice,2);}
                            confirmAtOffer=offers+1;
                            Shop.Earn(1000000);before=Shop.Purse;
                        }
                        else if(Time.realtimeSinceStartup>=nextInput)
                        {nextInput=Time.realtimeSinceStartup+1f;yield return SelectTeacherChoice(choice,1);confirmAtOffer=offers+1;}
                    }
                    if(state=="Idle" && declined && !pd.GetBool(flag)){
                        if(flag=="hasUpwardSlash" && !poorStarted){Shop.Spend(Shop.Purse);poorStarted=true;}
                        yield return StartTeacherTalk(teacher);
                    }
                    if(pd.GetBool(flag) && state=="Idle")break;
                    yield return new WaitForSecondsRealtime(.05f);
                }
                Check(room+"/native-learning",pd.GetBool(flag)&&pd.hasNailArt);
                Check(room+"/decline-can-reoffer",declined&&sawOffer&&sawReoffer);
                Check(room+"/payment",toll>=0 && before-Shop.Purse==toll*100L);
                Check(room+"/lesson-returns-idle",teacher.ActiveStateName=="Idle");
                if(flag=="hasUpwardSlash")Check(room+"/insufficient-funds-tested",poorChecked);
                Shot(room,"learned");
                long afterLearn=Shop.Purse;bool repeatedOffer=false;
                yield return StartTeacherTalk(teacher);float again=Time.realtimeSinceStartup+3f;
                while(Time.realtimeSinceStartup<again){repeatedOffer|=teacher.ActiveStateName=="Send Text";yield return null;}
                Check(room+"/learned-no-repeat-payment",!repeatedOffer&&Shop.Purse==afterLearn&&pd.GetBool(flag));
                evidence.Add(new{Room=room,Flag=flag,Learned=pd.GetBool(flag),Toll=toll,Paid=before-Shop.Purse,Offers=offers,InsufficientFundsChecked=poorChecked,States=trail});
                hero.RegainControl();hero.StartAnimationControl();
            }
            yield return GoTo("Town","left1");
            Check("all-three-persist-across-scenes",pd.hasCyclone&&pd.hasDashSlash&&pd.hasUpwardSlash&&pd.hasNailArt);
            mod.Inventory.Slots[0]=ItemStack.Of(ItemType.KnightNail);mod.Inventory.Selected=0;mod.Inventory.Override=-1;
            var oldKey=Player.TerraKeys.Get(Player.TerraKeys.Action.UseItem);
            Player.TerraKeys.Set(Player.TerraKeys.Action.UseItem,Key.K);
            try {
                yield return null;Check("learned-nail-ready",NailArts.Ready);
                var arts=hero.GetComponents<PlayMakerFSM>().First(f=>f.FsmName=="Nail Arts");
                foreach(bool spin in new[]{false,true}){
                    float hold=Time.realtimeSinceStartup+2f;
                    while(Time.realtimeSinceStartup<hold){KeysDown(spin?new[]{Key.K,Key.W}:new[]{Key.K});yield return null;}
                    KeysDown(spin?new[]{Key.W}:new Key[0]);
                    var states=new HashSet<string>();float until=Time.realtimeSinceStartup+2f;
                    while(Time.realtimeSinceStartup<until){states.Add(arts.ActiveStateName);yield return null;}
                    KeysDown();Check(spin?"learned-cyclone-releases":"learned-great-slash-releases",states.Any(s=>s.Contains(spin?"Cyclone":"G Slash")));
                    Plugin.Log.LogInfo("[AutoTest] LEARNED_ART "+string.Join(",",states));
                }
                float dashHold=Time.realtimeSinceStartup+2f;
                while(Time.realtimeSinceStartup<dashHold){KeysDown(Key.K);yield return null;}
                Player.TerraMovement.HkShadowDash(hero,1);KeysDown();
                var dashStates=new HashSet<string>();float dashUntil=Time.realtimeSinceStartup+2f;
                while(Time.realtimeSinceStartup<dashUntil){dashStates.Add(arts.ActiveStateName);yield return null;}
                Check("learned-dash-slash-releases",dashStates.Any(s=>s.Contains("Dash Slash")));
                Plugin.Log.LogInfo("[AutoTest] LEARNED_DASH_ART "+string.Join(",",dashStates));
            } finally {Player.TerraKeys.Set(Player.TerraKeys.Action.UseItem,oldKey);KeysDown();}
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,"nail-teachers-check.json"),JsonConvert.SerializeObject(new{Checks=checks,Failures=failed,Teachers=evidence},Formatting.Indented));
        }

        IEnumerator NailTeachersScan()
        {
            foreach (var room in new[] { "Room_nailmaster", "Room_nailmaster_02", "Room_nailmaster_03" })
            {
                yield return GoTo(room, "left1");
                yield return new WaitForSeconds(.5f);
                var teacher=Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                    .FirstOrDefault(f=>f.gameObject.scene.name==room && f.FsmName=="Conversation Control" && f.name.StartsWith("NM ") && !f.name.Contains("Modeller"));
                Plugin.Log.LogInfo("[AutoTest] NAIL_TEACHER "+room+" "+teacher?.name);
                if(teacher!=null) DumpFsm(teacher);
                Shot(room,"teacher_scan");
            }
        }
    }
}
