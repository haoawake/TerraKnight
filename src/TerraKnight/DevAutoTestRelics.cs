using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "relics"): Master Mode relics. A boss's drops paid on Expert give no relic, on Master its own (the False
    /// Knight's); a fight the tables key as another its variant's (the Radiance in GG_Radiance: the Absolute Radiance's);
    /// every boss's table ends with one; a relic sells for a gold's worth; three of them set down in Dirtmouth, each as
    /// 2 cells wide and 3 cells high, shot. All 48 textures are loaded and checked against the common front base.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator RelicsTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 圣物 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", "left1");
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(1f);
            var hero = HeroController.instance;
            var difficulty = GameDifficulty.Current;
            var got = new List<ItemType>();
            EnemyDrops.DevSink = (t, n) => got.Add(t);
            GameDifficulty.Current = DifficultyLevel.Expert;
            EnemyDrops.Roll("FalseKnight", hero.transform.position);
            bool expertNone = !got.Exists(Relics.Is);
            got.Clear();
            GameDifficulty.Current = DifficultyLevel.Master;
            EnemyDrops.Roll("FalseKnight", hero.transform.position);
            bool masterOwn = got.Contains(ItemType.RelicFalseKnight);
            got.Clear();
            Relics.ContextScene = "GG_Radiance";
            EnemyDrops.Roll("FinalBoss", hero.transform.position);
            Relics.ContextScene = null;
            bool variant = got.Contains(ItemType.RelicAbsoluteRadiance);
            EnemyDrops.DevSink = null;
            GameDifficulty.Current = difficulty;
            Check("专家不掉", expertNone, "");
            Check("大师掉自己的", masterOwn, "");
            Check("神居的无上辐光掉它自己的", variant, "");
            Check("每个首领的掉落表都有", EnemyDrops.DevRelicRows >= 40, $"{EnemyDrops.DevRelicRows} 张表");
            var def = ItemDb.Get(ItemType.RelicFalseKnight);
            long sell = Shop.SellValue(ItemStack.Of(ItemType.RelicFalseKnight));
            Check("能卖钱", sell > 0 && def.Rare == -13 && def.PlacesStation == StationKind.RelicFalseKnight, $"卖 {sell}，稀有度 {def.Rare}，名字 {def.Name}");

            var wrongSize = new List<string>();
            var wrongTexture = new List<string>();
            var wrongBase = new List<string>();
            Color32[] commonBase = null;
            foreach (var kv in Relics.Looks)
            {
                if (kv.Value.W != 2 || kv.Value.H != 3) wrongSize.Add(kv.Key.ToString());
                var tex = Relics.Texture(kv.Value.Texture);
                if (tex.width != 64 || tex.height != 96 || tex.filterMode != FilterMode.Point)
                {
                    wrongTexture.Add($"{kv.Key}:{tex.width}x{tex.height}/{tex.filterMode}");
                    continue;
                }
                var pixels = tex.GetPixels32();
                if (commonBase == null)
                {
                    commonBase = new Color32[64 * 16];
                    System.Array.Copy(pixels, commonBase, commonBase.Length);
                }
                bool sameBase = true;
                for (int i = 0; i < commonBase.Length; i++)
                    if (!pixels[i].Equals(commonBase[i])) { sameBase = false; break; }
                if (!sameBase) wrongBase.Add(kv.Key.ToString());
            }
            Check("48款统一两格宽三格高", Relics.Looks.Count == 48 && wrongSize.Count == 0, string.Join("、", wrongSize));
            Check("48款贴图64乘96且像素采样", Relics.Looks.Count == 48 && wrongTexture.Count == 0, string.Join("、", wrongTexture));
            Check("48款共用同一正视底座", commonBase != null && wrongTexture.Count == 0 && wrongBase.Count == 0, string.Join("、", wrongBase));

            // Three set down side by side, out on the street.
            hero.transform.position = new Vector2(110f, 12f);
            yield return new WaitForSeconds(1f);
            var feet = hero.GetComponent<BoxCollider2D>().bounds;
            int placed = 0;
            float x = feet.center.x + 2f;
            foreach (var kind in new[] { StationKind.RelicFalseKnight, StationKind.RelicMantisLord, StationKind.RelicHollowKnight })
            {
                // Within reach of each: the player stands beside where it goes.
                hero.transform.position = new Vector3(x - 1.5f, hero.transform.position.y, hero.transform.position.z);
                for (int k = 0; k < 6 && !Stations.TryPlace(kind, hero, new Vector2(x, feet.min.y + 0.3f)); k++) x += 0.5f;
                placed++;
                x += Relics.Looks[kind].W * 0.5f + 0.5f;
            }
            yield return new WaitForSeconds(0.5f);
            Check("放下三个", Stations.DevBoxOf(StationKind.RelicFalseKnight) != null && Stations.DevBoxOf(StationKind.RelicMantisLord) != null && Stations.DevBoxOf(StationKind.RelicHollowKnight) != null, Stations.DevDescribe());
            foreach (var kind in new[] { StationKind.RelicFalseKnight, StationKind.RelicMantisLord, StationKind.RelicHollowKnight })
            {
                var box = Stations.DevBoxOf(kind);
                var go = GameObject.Find("Terra Station " + kind);
                var renderer = go != null ? go.GetComponent<SpriteRenderer>() : null;
                var size = renderer != null && renderer.sprite != null ? renderer.sprite.bounds.size : Vector3.zero;
                bool geometry = box.HasValue && Mathf.Abs(box.Value.width - 2 * CellGrid.CellSize) < 0.001f
                    && Mathf.Abs(box.Value.height - 3 * CellGrid.CellSize) < 0.001f;
                bool picture = Mathf.Abs(size.x - 2 * CellGrid.CellSize) < 0.001f
                    && Mathf.Abs(size.y - 3 * CellGrid.CellSize) < 0.001f;
                Check(kind + "放置及显示占格", geometry && picture, $"占格 {box}，显示 {size}");
            }
            Shot("Town", "relics_placed");
            Check("场景内圣物验证画面", CaptureRelicsCamera(), "独立验证相机渲染已放置对象，排除近相机淡出遮罩");
            Plugin.Log.LogInfo($"[AutoTest] 圣物 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }

        bool CaptureRelicsCamera()
        {
            var firstBox = Stations.DevBoxOf(StationKind.RelicFalseKnight);
            var lastBox = Stations.DevBoxOf(StationKind.RelicHollowKnight);
            if (!firstBox.HasValue || !lastBox.HasValue) return false;
            var centre = (firstBox.Value.center + lastBox.Value.center) * 0.5f;
            var cameraObject = new GameObject("Relic validation camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.CopyFrom(GameCameras.instance.mainCamera);
            camera.enabled = false;
            // Render the actual placed scene objects on their Z plane, excluding near-camera fade/UI quads.
            camera.transform.position = new Vector3(centre.x, centre.y + 0.3f, -30f);
            camera.transform.rotation = Quaternion.identity;
            camera.orthographic = true;
            camera.orthographicSize = 2.5f;
            camera.aspect = 1280f / 720f;
            camera.nearClipPlane = 29.5f;
            camera.farClipPlane = 30.5f;
            camera.cullingMask = 1; // Stations are ordinary Default-layer SpriteRenderers.
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.12f, 0.16f, 1f);
            camera.ResetProjectionMatrix();
            camera.ResetWorldToCameraMatrix();
            var previousActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                string path = System.IO.Path.Combine(outDir, "Town_relics_camera.png");
                System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
                var pixels = image.GetPixels32();
                var first = pixels[0];
                int different = 0, gold = 0;
                for (int i = 0; i < pixels.Length; i += 17)
                {
                    if (!pixels[i].Equals(first)) different++;
                    if (pixels[i].r > 100 && pixels[i].g > 60 && pixels[i].b < pixels[i].g * 0.8f) gold++;
                }
                Plugin.Log.LogInfo($"[AutoTest] 圣物场景验证相机 {path}，中心 {centre}，非均匀采样 {different}，金色采样 {gold}");
                return different > 100 && gold > 20;
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(image);
                Object.Destroy(cameraObject);
            }
        }
    }
}
