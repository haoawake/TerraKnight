using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_17housequery"): the housing query the way a player uses it, standing and sitting on Dirtmouth's
    /// bench. The inventory open, the housing page, the "?" taken; then a real left click (the mouse device's own state)
    /// on the street: Terraria's verdict in the chat at the bottom left.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static void RealMouse(Vector2 screen, bool down)
        {
            var m = Mouse.current;
            if (m == null) return;
            var st = new MouseState { position = screen };
            if (down) st = st.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(m, st);
        }

        IEnumerator HouseQuery17()
        {
            yield return GoTo("Town", "left1");
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(2f);
            yield return QueryClick("站着");

            // On the bench.
            var hero = HeroController.instance;
            PlayMakerFSM bench = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (f.FsmName == "Bench Control" && f.gameObject.scene.name == "Town") { bench = f; break; }
            if (bench == null) { FxCheck("坐在长椅上用问号", false, "德特茅斯没有长椅"); yield break; }
            hero.transform.position = Clear(hero, (Vector2)bench.transform.position + new Vector2(0.3f, 0.6f));
            yield return new WaitForSeconds(1f);
            for (int k = 0; k < 3 && !PlayerData.instance.atBench; k++)
            {
                yield return Tap(Key.W, 0.3f);
                yield return Tap(Key.UpArrow, 0.3f);
                yield return new WaitForSeconds(2.5f);
            }
            yield return QueryClick($"坐在长椅上（坐下 {PlayerData.instance.atBench}）");
        }

        IEnumerator QueryClick(string how)
        {
            var hero = HeroController.instance;
            var cam = GameCameras.instance.mainCamera;
            Items.InventoryGui.Open = true;
            yield return new WaitForSeconds(0.4f);
            // The housing page, then the "?".
            UI.TerraCursor.DevDrives = true;
            if (!Town.TownNpcs.PanelOpen)
            {
                var page = Items.InventoryGui.DevPageIconRect(1);
                if (page != null)
                {
                    UI.TerraCursor.DevAt = page.Value.center;
                    yield return null;
                    UI.TerraCursor.DevClick();
                    yield return new WaitForSeconds(0.4f);
                }
            }
            var q = Items.InventoryGui.DevRectOf("NpcHead", -1);
            if (q != null && !Town.TownNpcs.Querying)
            {
                UI.TerraCursor.DevAt = q.Value.center;
                yield return null;
                UI.TerraCursor.DevClick();
                yield return new WaitForSeconds(0.3f);
            }
            UI.TerraCursor.DevDrives = false;
            UI.TerraCursor.DevAt = null;
            bool querying = Town.TownNpcs.Querying;
            // A real click on the street, a few steps from the Knight.
            Vector2 at = (Vector2)hero.transform.position + new Vector2(2.5f, 0.5f);
            var sp = cam.WorldToScreenPoint(new Vector3(at.x, at.y, 0f));
            var screen = new Vector2(sp.x, sp.y);
            int said0 = Net.NetChat.DevLines;
            Town.TownNpcs.DevSaid.Clear();
            TerraMod.DevNoRealMouse = false;
            RealMouse(screen, false);
            yield return null;
            yield return null;
            bool overUi = Items.InventoryGui.MouseOverUi, control = GameHooks.PlayerHasControl();
            RealMouse(screen, true);
            yield return null;
            yield return null;
            RealMouse(screen, false);
            yield return new WaitForSeconds(0.4f);
            TerraMod.DevNoRealMouse = true;
            Shot("Town", "housequery_" + (control ? "standing" : "bench"));
            yield return new WaitForSeconds(0.2f);
            string said = string.Join(" / ", Town.TownNpcs.DevSaid);
            FxCheck($"{how}拿着问号点房间，左下角说这里能不能住", querying && Net.NetChat.DevLines > said0 && said.Length > 0,
                $"问号在光标上 {querying}，点的时候在界面上 {overUi}，能操作 {control}，说了「{said}」，聊天 {said0}→{Net.NetChat.DevLines} 行");
            Items.InventoryGui.Open = false;
            yield return new WaitForSeconds(0.3f);
        }
    }
}
