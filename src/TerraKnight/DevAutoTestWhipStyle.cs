using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "whipstyle", Items.WhipStyles, the user's 10-26 "给他们不同的鞭子加入不同的甩鞭动画", "完全不符合物理", "一把鞭子
    /// 不只有一个挥舞动画"): in Dirtmouth by its Target Dummy, every whip lashed at it three times running from three quarters of its
    /// reach: each lash strikes the dummy, the three go round the figure eight, forehand, backhand, forehand (all but the Moon
    /// Lord whip's, Terraria's), its lowest link
    /// under his feet noted (it goes through the ground as through walls); the first lash shot at four moments (Town_whipstyle_&lt;whip&gt;_1..4), the others at their crack
    /// (_l2, _l3); then walls before the dummy, the grid's (wall) and Hollow Knight's own colliders (hkwall): the rope goes
    /// through them and strikes only what the player sees (the user's 10-26 "恢复成原来无视墙体的样子…改回看不见就打不中"); and a
    /// block in the air the rope goes through, never caught on it (wrap). TK_WHIP_ONLY narrows it to some whips and walls.
    /// Lines "[AutoTest] 鞭子动作 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator WhipStyleTest()
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 鞭子动作 {what} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            BladeArts.Set(true);
            Whips.DevCount = true;
            for (float t = Time.realtimeSinceStartup + 10f; TargetDummy.DevAt == null && Time.realtimeSinceStartup < t;) yield return null;
            var at = TargetDummy.DevAt;
            if (!at.HasValue) { Check(false, "德特茅斯的训练假人"); yield break; }
            float bottom = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
            var aim = at.Value + new Vector2(0f, 0.8f);
            var only = new List<string>((System.Environment.GetEnvironmentVariable("TK_WHIP_ONLY") ?? "").Split(','));
            var whips = new[]
            {
                ItemType.LeatherWhip, ItemType.Snapthorn, ItemType.SpinalTap, ItemType.Firecracker, ItemType.CoolWhip, ItemType.Durendal, ItemType.MorningStar,
                ItemType.DarkHarvest, ItemType.Kaleidoscope, ItemType.SlimeWhip, ItemType.CobWhip, ItemType.CorruptWhip, ItemType.CrimsonWhip,
                ItemType.MeteorWhip, ItemType.FlowerWhip, ItemType.EelWhip, ItemType.ConstellationWhip, ItemType.MoonLordWhip,
            };
            foreach (var type in whips)
            {
                if (only.Count > 0 && only[0].Length > 0 && !only.Contains(type.ToString())) continue;
                var def = ItemDb.Get(type);
                float reach = Whips.DevReachPx(def) * Effects.TerraParticles.Px;
                float dx = -Mathf.Min(reach * 0.75f, 9f);
                hero.transform.position = Clear(hero, new Vector2(at.Value.x + dx, at.Value.y - bottom + 0.05f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                hero.FaceRight();
                yield return new WaitForSeconds(0.5f);
                bool shouldStyle = type != ItemType.MoonLordWhip;
                float seconds = Mathf.Max(1f, Mathf.Round(def.UseAnimation * 60f) * Equipment.WhipScale()) / 60f;
                var moves = new List<string>();
                int struck = 0;
                float lowest = -999f;
                Whips.DevStyled.Remove(def.Whip);
                int carried0 = Whips.DevCarried;
                Whips.DevFrames = Whips.DevLinks = Whips.DevKinks = Whips.DevBehind = 0;
                for (int lash = 1; lash <= 3; lash++)
                {
                    while (mod.IsUsingItem || !mod.DevReady) yield return null;
                    int hits0 = TargetDummy.DevHits;
                    Whips.DevLowest = -999f;
                    mod.DevAim = aim;
                    mod.DevUse(type, aim);
                    if (lash == 1)
                        for (int k = 1; k <= 4; k++)
                        {
                            yield return new WaitForSeconds(seconds * 0.2f);
                            Shot("Town", $"whipstyle_{type}_{k}");
                        }
                    else
                    {
                        yield return new WaitForSeconds(seconds * 0.6f);
                        Shot("Town", $"whipstyle_{type}_l{lash}");
                    }
                    for (float t = Time.time + 2f; Time.time < t && mod.IsUsingItem;) yield return null;
                    if (TargetDummy.DevHits > hits0) struck++;
                    moves.Add(Whips.DevLastMove);
                    lowest = Mathf.Max(lowest, Whips.DevLowest);
                    Whips.DevLastMove = "";
                }
                mod.DevAim = null;
                bool styled = Whips.DevStyled.Contains(def.Whip);
                // The figure eight: forehand, backhand, forehand (the user's 10-26 "画一个八字顺手又…抽，形成一个循环").
                bool eight = moves.Count == 3 && moves[0] == "正手抽" && moves[1] == "反手抽" && moves[2] == "正手抽";
                // Smooth (the user's 10-26 "上下疯狂抖动"、"往后抽"、"切换都应该是丝滑的"): few sharp folds a frame, hardly any of the
                // rope behind him, the second and third lashes carried on from the one before.
                float kinks = Whips.DevKinks / (float)Mathf.Max(1, Whips.DevFrames);
                float behind = Whips.DevBehind * 100f / Mathf.Max(1, Whips.DevLinks);
                int carried = Whips.DevCarried - carried0;
                bool smooth = !shouldStyle || kinks <= 1f && behind <= 8f && carried >= 2;
                // The rope may go under the floor (through the ground as through walls, the user's 10-27 "应该站着也能穿墙"): its lowest noted only.
                Check(struck == 3 && styled == shouldStyle && (!shouldStyle || eight) && smooth,
                    $"{def.Name}：离假人 {-dx:F1}（够到 {reach:F1}），三下打中 {struck}，甩法 {string.Join(" → ", moves)}，最低在脚下 {lowest:F0} px，折角 {kinks:F2}/帧，身后 {behind:F0}%，接续 {carried}");
            }
            // Walls of stone between him and the dummy. The rope goes through them as Terraria's does; behind a high one (6 blocks)
            // the dummy is out of sight and never struck; behind a low one (3 blocks) it is struck as Terraria's sight lines over a
            // low ledge let it be.
            if (only.Count == 0 || only[0].Length == 0 || only.Contains("wall"))
            {
                var wallBase = CellGrid.WorldToCell(new Vector2(at.Value.x - 2f, at.Value.y + 0.1f));
                foreach (int height in new[] { 3, 6 })
                {
                    for (int y = 0; y < height; y++) for (int x = 0; x < 2; x++) TerraWorld.Set(wallBase.x + x, wallBase.y + y, CellKind.Stone);
                    TerraWorld.Live?.Tick();
                    Physics2D.SyncTransforms();
                    foreach (var type in new[] { ItemType.LeatherWhip, ItemType.MorningStar, ItemType.EelWhip })
                    {
                        var def = ItemDb.Get(type);
                        hero.transform.position = Clear(hero, new Vector2(at.Value.x - 4f, at.Value.y - bottom + 0.05f));
                        hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                        hero.FaceRight();
                        // A pause: the whip's lashes start over, its first (the Leather Whip's from over the top).
                        yield return new WaitForSeconds(1.5f);
                        int inside0 = Whips.DevInside, hits0 = Whips.DevStrikes;
                        float seconds = Mathf.Max(1f, Mathf.Round(def.UseAnimation * 60f) * Equipment.WhipScale()) / 60f;
                        int lashes = 0;
                        for (int lash = 1; lash <= 2; lash++)
                        {
                            while (mod.IsUsingItem || !mod.DevReady) yield return null;
                            mod.DevAim = aim;
                            mod.DevUse(type, aim);
                            yield return new WaitForSeconds(seconds * 0.62f);
                            if (lash == 1) Shot("Town", $"whipstyle_wall{height}_{type}");
                            for (float t = Time.time + 2f; Time.time < t && mod.IsUsingItem;) yield return null;
                            lashes++;
                        }
                        mod.DevAim = null;
                        int inside = Whips.DevInside - inside0, struck = Whips.DevStrikes - hits0;
                        Whips.DevInsideWhy = "";
                        bool through = inside > 0, unseen = height < 6 || struck == 0;
                        Check(through && unseen,
                            $"{height} 格高的墙后 {def.Name}：穿过墙画的链节 {inside}，墙后的假人挨了 {struck} 下{(height >= 6 ? "（看不见，应为 0）" : "（矮墙，按视线）")}");
                    }
                    for (int y = 0; y < height; y++) for (int x = 0; x < 2; x++) TerraWorld.Set(wallBase.x + x, wallBase.y + y, CellKind.Empty);
                    TerraWorld.Live?.Tick();
                    Physics2D.SyncTransforms();
                }
            }
            // Walls of Hollow Knight's own kind, colliders the grid never took in (a solid box, then a hollow loop of edges as his
            // rooms' are), too tall to see over: the rope through it, the dummy behind it never struck.
            if (only.Count == 0 || only[0].Length == 0 || only.Contains("hkwall"))
                foreach (bool hollow in new[] { false, true })
                {
                    var rect = new Rect(at.Value.x - 2.5f, at.Value.y - 0.5f, 1f, 20f);
                    var wall = new GameObject("Dev whip wall") { layer = ChunkColliders.TerrainLayer };
                    wall.transform.position = rect.center;
                    if (hollow)
                    {
                        float hx = rect.width / 2f, hy = rect.height / 2f;
                        wall.AddComponent<EdgeCollider2D>().points = new[] { new Vector2(-hx, -hy), new Vector2(-hx, hy), new Vector2(hx, hy), new Vector2(hx, -hy), new Vector2(-hx, -hy) };
                    }
                    else wall.AddComponent<BoxCollider2D>().size = rect.size;
                    Physics2D.SyncTransforms();
                    Whips.DevBox = rect;
                    foreach (var type in new[] { ItemType.LeatherWhip, ItemType.MorningStar, ItemType.EelWhip })
                    {
                        var def = ItemDb.Get(type);
                        hero.transform.position = Clear(hero, new Vector2(at.Value.x - 4f, at.Value.y - bottom + 0.05f));
                        hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                        hero.FaceRight();
                        yield return new WaitForSeconds(1.5f);
                        // The whip's own strikes (the dummy's count takes in what an earlier whip left on it burning).
                        int inBox0 = Whips.DevInBox, hits0 = Whips.DevStrikes;
                        float seconds = Mathf.Max(1f, Mathf.Round(def.UseAnimation * 60f) * Equipment.WhipScale()) / 60f;
                        for (int lash = 1; lash <= 2; lash++)
                        {
                            while (mod.IsUsingItem || !mod.DevReady) yield return null;
                            mod.DevAim = aim;
                            mod.DevUse(type, aim);
                            yield return new WaitForSeconds(seconds * 0.62f);
                            if (lash == 1) Shot("Town", $"whipstyle_hk{(hollow ? "edge" : "box")}_{type}");
                            for (float t = Time.time + 2f; Time.time < t && mod.IsUsingItem;) yield return null;
                        }
                        mod.DevAim = null;
                        int inBox = Whips.DevInBox - inBox0, struck = Whips.DevStrikes - hits0;
                        Check(inBox > 0 && struck == 0, $"{(hollow ? "空心边" : "实心")}的空洞骑士墙后 {def.Name}：穿过墙画的链节 {inBox}，墙后的假人挨了 {struck} 下（看不见，应为 0）");
                    }
                    Object.Destroy(wall);
                    Whips.DevBox = default;
                    yield return null;
                    Physics2D.SyncTransforms();
                }
            // A 3x3 block of stone in the air at half the reach, a little over the aim, under the forehand's strike: the rope goes
            // through it, never caught or reeled in, the lashes going round the eight as in the open.
            if (only.Count == 0 || only[0].Length == 0 || only.Contains("wrap"))
                foreach (var type in new[] { ItemType.MorningStar, ItemType.LeatherWhip })
                {
                    var def = ItemDb.Get(type);
                    float reach = Whips.DevReachPx(def) * Effects.TerraParticles.Px;
                    var stand = Clear(hero, new Vector2(at.Value.x - 12f, at.Value.y - bottom + 0.05f));
                    hero.transform.position = stand;
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    hero.FaceRight();
                    yield return new WaitForSeconds(1.5f);
                    var shoulder = (Vector2)hero.transform.position + new Vector2(0f, 0.3f);
                    var mid = CellGrid.WorldToCell(shoulder + new Vector2(reach * 0.45f, reach * 0.18f));
                    for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++) TerraWorld.Set(mid.x + x, mid.y + y, CellKind.Stone);
                    TerraWorld.Live?.Tick();
                    Physics2D.SyncTransforms();
                    var lo = CellGrid.CellCenter(mid.x - 1, mid.y - 1) - Vector2.one * (CellGrid.CellSize / 2f);
                    Whips.DevBox = new Rect(lo, Vector2.one * (CellGrid.CellSize * 3f));
                    Whips.DevSides = 0;
                    int inside0 = Whips.DevInside, inBox0 = Whips.DevInBox, snags0 = Whips.DevSnags, reels0 = Whips.DevReels;
                    var wrapAim = shoulder + new Vector2(reach * 0.9f, 0f);
                    float seconds = Mathf.Max(1f, Mathf.Round(def.UseAnimation * 60f) * Equipment.WhipScale()) / 60f;
                    var names = new List<string>();
                    int sidesAfterFirst = 0;
                    for (int lash = 1; lash <= 3; lash++)
                    {
                        while (mod.IsUsingItem || !mod.DevReady) yield return null;
                        mod.DevAim = wrapAim;
                        mod.DevUse(type, wrapAim);
                        for (int k = 1; k <= 4; k++)
                        {
                            yield return new WaitForSeconds(seconds * 0.2f);
                            if (lash <= 2) Shot("Town", $"whipstyle_wrap_{type}_{lash}_{k}");
                        }
                        for (float t = Time.time + 2f; Time.time < t && mod.IsUsingItem;) yield return null;
                        names.Add(Whips.DevLastMove);
                        if (lash == 1) sidesAfterFirst = Whips.DevSides;
                    }
                    mod.DevAim = null;
                    int snags = Whips.DevSnags - snags0, reels = Whips.DevReels - reels0, inBox = Whips.DevInBox - inBox0;
                    Check(snags == 0 && reels == 0 && names.Count == 3 && !names.Contains("收鞭") && inBox > 0,
                        $"空中 3x3 石块 {def.Name}：够到 {reach:F1}，挂住 {snags} 次，收鞭 {reels} 次，甩法 {string.Join(" → ", names)}，穿过石块画的链节 {inBox}");
                    for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++) TerraWorld.Set(mid.x + x, mid.y + y, CellKind.Empty);
                    TerraWorld.Live?.Tick();
                    Physics2D.SyncTransforms();
                    Whips.DevBox = default;
                    yield return new WaitForSeconds(1.5f);
                }
            Whips.DevCount = false;
            Plugin.Log.LogInfo($"[AutoTest] 鞭子动作 合计 {ok}/{all}");
        }
    }
}
