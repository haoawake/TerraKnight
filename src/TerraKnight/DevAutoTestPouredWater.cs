using System.Collections;
using System.Linq;
using TerraKnight.Player;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator PouredWaterTest()
        {
            int checks=0, passed=0;
            void Check(string name,bool ok,string detail="")
            {
                checks++; if(ok)passed++;
                Plugin.Log.LogInfo($"[AutoTest] 倒水回归 {name} {(ok?"通过":"不通过")} {detail}");
            }
            Player.TerraStats.DevImmortal=true; mod.DevNoFallDamage=true;
            yield return GoTo("Town","left1");
            var r=TerrainSystem.Current.Region;
            var gates=Object.FindObjectsByType<TransitionPoint>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(t=>t.gameObject.scene.name=="Town").Select(t=>t.name).Distinct().ToArray();
            void ClearLiquid(TerrainRegion region)
            {
                for(int y=0;y<region.Grid.Height;y++)for(int x=0;x<region.Grid.Width;x++)
                    if(region.Grid.GetLiquid(x,y)>0)region.Liquid.Set(x,y,0,LiquidKind.Water);
            }
            int filled=0;
            for(int y=0;y<r.Grid.Height;y++)for(int x=0;x<r.Grid.Width;x++)
                if(r.Grid.InClip(x,y)&&!Cells.Blocks(r.Grid.Get(x,y))){r.Liquid.Set(x,y,255,LiquidKind.Water);filled++;}
            Check("实际灌满房间",filled>1000,$"液体格 {filled}");
            foreach(var gate in new[]{"right1","left1","bot1"})
            {
                Check("原生入口存在 "+gate,gates.Contains(gate));
                if(!gates.Contains(gate))continue;
                yield return GoTo("Crossroads_01",EntryGateOf("Crossroads_01"));
                yield return GoTo("Town",gate);
                var hero=HeroController.instance; var rb=hero.GetComponent<Rigidbody2D>();
                Check("满水换入口完成 "+gate,GameHooks.PlayerHasControl()&&!hero.cState.transitioning&&!GameManager.instance.IsInSceneTransition,
                    $"位置 {hero.transform.position}, 重力 {rb.gravityScale}");
                bool hasWater=PouredSwimming.Surface(hero,out var top,out _);
                Check("入口实际仍在液体中 "+gate,hasWater,$"水面 {top:F2}, 脚 {hero.GetComponent<BoxCollider2D>().bounds.min.y:F2}");
                bool controlled=true; float last=hero.transform.position.y,maxStep=0f;
                for(float until=Time.realtimeSinceStartup+1.5f;Time.realtimeSinceStartup<until;)
                {
                    controlled&=GameHooks.PlayerHasControl();maxStep=Mathf.Max(maxStep,Mathf.Abs(hero.transform.position.y-last));last=hero.transform.position.y;
                    yield return null;
                }
                Check("水中保持控制且没有瞬移 "+gate,controlled&&maxStep<0.8f,$"最大逐帧位移 {maxStep:F3}");
            }
            r=TerrainSystem.Current.Region; ClearLiquid(r);
            yield return new WaitForSeconds(1f);
            // Sealed reservoir above a two-cell slab; dry playable chamber below.
            const int cx=200, floor=40, lower=28, waterTop=54;
            void Cell(int x,int y,CellKind kind)=>TerraWorld.Set(x+r.CellOffset.x,y+r.CellOffset.y,kind);
            Vector3 At(int x,float y)=>r.Root.position+new Vector3((x+0.5f)*CellGrid.CellSize,y*CellGrid.CellSize,0f);
            for(int y=lower;y<=waterTop+5;y++)for(int x=cx-15;x<=cx+15;x++)
                Cell(x,y,x==cx-15||x==cx+15||y==lower||y==floor||y==floor-1?CellKind.Stone:CellKind.Empty);
            void FillPool()
            {
                for(int y=floor+1;y<waterTop;y++)for(int x=cx-14;x<=cx+14;x++)r.Liquid.Set(x,y,255,LiquidKind.Water);
            }
            FillPool();yield return new WaitForSeconds(0.8f);
            var h=HeroController.instance;var body=h.GetComponent<Rigidbody2D>();
            h.transform.position=At(cx,lower+1)+Vector3.up*1.4f;body.linearVelocity=Vector2.zero;
            yield return new WaitForSeconds(2f);
            Check("隔着方块的下层没有被吸到水面",h.GetComponent<BoxCollider2D>().bounds.max.y<At(cx,floor-1).y&&!PouredSwimming.Active&&GameHooks.PlayerHasControl(),
                $"角色 {h.transform.position}, 水面 {At(cx,waterTop).y:F1}, 重力 {body.gravityScale}");
            Check("固体阻挡回浮路径",!PouredSwimming.ClearRise(h,At(cx,waterTop).y));
            h.transform.position=At(cx,waterTop+4)+Vector3.up*1.4f;body.linearVelocity=Vector2.zero;
            yield return new WaitForSeconds(4f);
            float surface=At(cx,waterTop).y,feet=h.GetComponent<BoxCollider2D>().bounds.min.y;
            Check("自然落水并浮在真实水面",PouredSwimming.Active&&GameHooks.PlayerHasControl()&&Mathf.Abs(surface-feet-0.55f)<0.55f,$"脚 {feet:F2}, 水面 {surface:F2}");
            var inventoryKey=TerraKeys.Get(TerraKeys.Action.Inventory);
            yield return Tap(inventoryKey);
            Check("水中真实按键可以打开背包",Items.InventoryGui.Open);
            yield return Tap(inventoryKey);
            Check("水中真实按键可以关闭背包",!Items.InventoryGui.Open);
            var useKey=TerraKeys.Get(TerraKeys.Action.UseItem);
            mod.Inventory.Slots[0]=Items.ItemStack.Of(Items.ItemType.CopperBroadsword);
            mod.Inventory.Selected=0; mod.Inventory.Override=-1;
            TerraKeys.Set(TerraKeys.Action.UseItem,Key.K); TerraMod.DevNoRealMouse=false;
            mod.DevAim=(Vector2)h.transform.position+Vector2.right*4f;
            int serial=mod.UseSerial;
            yield return Tap(Key.K,0.35f);
            Check("水中真实输入可以使用武器",mod.UseSerial>serial,$"攻击序号 {serial}→{mod.UseSerial}");
            TerraMod.DevNoRealMouse=true;mod.DevAim=null;TerraKeys.Set(TerraKeys.Action.UseItem,useKey);
            float x0=h.transform.position.x;
            yield return Tap(Key.D,0.4f);
            Check("水中实际方向输入可以移动",Mathf.Abs(h.transform.position.x-x0)>0.3f,$"位移 {h.transform.position.x-x0:F2}");
            float before=h.GetComponent<BoxCollider2D>().bounds.min.y,peak=before;
            KeysDown(Key.Space);
            for(float until=Time.realtimeSinceStartup+0.35f;Time.realtimeSinceStartup<until;){peak=Mathf.Max(peak,h.GetComponent<BoxCollider2D>().bounds.min.y);yield return null;}
            KeysDown();
            for(float until=Time.realtimeSinceStartup+0.6f;Time.realtimeSinceStartup<until;){peak=Mathf.Max(peak,h.GetComponent<BoxCollider2D>().bounds.min.y);yield return null;}
            Check("水中实际跳跃可以出水",peak>surface+0.3f,$"脚 {before:F2}→{peak:F2}, 水面 {surface:F2}");
            yield return new WaitForSeconds(2f);
            ClearLiquid(r);yield return new WaitForSeconds(3f);
            var native=PlayMakerFSM.FindFsmOnGameObject(h.gameObject,"Surface Water");
            Check("抽干水后恢复下落而非浮空行走",!PouredSwimming.Active&&body.gravityScale>0.01f&&h.transform.position.y<surface-2f&&GameHooks.PlayerHasControl(),
                $"位置 {h.transform.position}, 重力 {body.gravityScale}, 原生状态 {native?.ActiveStateName}");
            // Submerged solid roof: physically stop at it, retaining controls.
            FillPool();for(int x=cx-14;x<=cx+14;x++)Cell(x,waterTop,CellKind.Stone);
            h.transform.position=At(cx,floor+2)+Vector3.up*1.4f;body.linearVelocity=Vector2.zero;
            yield return new WaitForSeconds(3f);
            Check("水下顶板阻挡上浮且不锁控制",h.GetComponent<BoxCollider2D>().bounds.max.y<=At(cx,waterTop).y+0.15f&&GameHooks.PlayerHasControl(),$"头 {h.GetComponent<BoxCollider2D>().bounds.max.y:F2}, 顶板 {At(cx,waterTop).y:F2}");
            for(int x=cx-14;x<=cx+14;x++)Cell(x,waterTop,CellKind.Empty);
            bool tear=PlayerData.instance.hasAcidArmour;
            foreach(var kind in new[]{LiquidKind.Acid,LiquidKind.Shimmer})
            {
                ClearLiquid(r); PlayerData.instance.hasAcidArmour=true;
                for(int y=floor+1;y<waterTop;y++)for(int x=cx-14;x<=cx+14;x++)r.Liquid.Set(x,y,255,kind);
                h.transform.position=At(cx,waterTop+4)+Vector3.up*1.4f;body.linearVelocity=Vector2.zero;
                yield return new WaitForSeconds(4f);
                Check(kind+" 倒出液体浮力与控制正常",PouredSwimming.Active&&GameHooks.PlayerHasControl()&&body.gravityScale>0.01f,
                    $"脚 {h.GetComponent<BoxCollider2D>().bounds.min.y:F2}, 原生状态 {native?.ActiveStateName}");
            }
            PlayerData.instance.hasAcidArmour=tear;ClearLiquid(r);FillPool();
            PotionBuffs.Give(15,3600);
            h.transform.position=At(cx,waterTop+4)+Vector3.up*1.4f;body.linearVelocity=Vector2.zero;
            yield return new WaitForSeconds(4f);
            Check("水上行走停在真实水面",Mathf.Abs(h.GetComponent<BoxCollider2D>().bounds.min.y-surface)<0.3f&&GameHooks.PlayerHasControl());
            yield return Tap(TerraKeys.Get(TerraKeys.Action.Down),0.6f);
            Check("按下可以穿过水面下潜",h.GetComponent<BoxCollider2D>().bounds.min.y<surface-0.4f&&GameHooks.PlayerHasControl());
            PlayerBuffs.Remove(15);
            Check("倒出液体没有原生伤敌组件",!Object.FindObjectsByType<PouredLiquidRegion>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Any(p=>p.GetComponentsInChildren<DamageEnemies>(true).Length>0||p.GetComponentsInChildren<PlayMakerFSM>(true).Any(f=>f.FsmName=="damages_enemy")));
            Plugin.Log.LogInfo($"[AutoTest] 倒水回归结果 {passed}/{checks}");
        }
    }
}
