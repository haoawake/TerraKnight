using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Net;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        static string SlideMessage(string prefix)
        {
            foreach (string line in NetChat.DevTexts())
            {
                int p = line.IndexOf(prefix, StringComparison.Ordinal);
                if (p >= 0) return line.Substring(p + prefix.Length).Split('|')[0];
            }
            return "";
        }

        static bool SlideDrawn(HealthManager hm) => hm != null && hm.gameObject.activeInHierarchy && hm.GetComponentsInChildren<Renderer>().Any(r => r.enabled && !(r is ParticleSystemRenderer));

        void SlideRender(HealthManager hm, string label)
        {
            // Hidden game windows return a black screen capture. Render the actual live
            // enemy and both player sprite trees without the window's final framebuffer.
            var renderers = hm.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToList();
            var players = UnityEngine.Object.FindObjectsByType<Player.TerraPlayerSprite>(FindObjectsSortMode.None);
            foreach (var p in players) renderers.AddRange(p.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)));
            var layers = renderers.Select(r => r.gameObject.layer).ToArray();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var go = new GameObject("TK Slide Verification Camera");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = Mathf.Max(3f, bounds.extents.y + 1f, bounds.extents.x * 540f / 960f + 1f);
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.16f, 0.18f, 0.22f);
            cam.cullingMask = 1 << 31; cam.nearClipPlane = 0.01f; cam.farClipPlane = 100f;
            cam.transform.position = new Vector3(bounds.center.x, bounds.center.y, -40f);
            var rt = new RenderTexture(960, 540, 24);
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            var active = RenderTexture.active;
            try
            {
                foreach (var r in renderers) r.gameObject.layer = 31;
                cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(outDir, label + "_sprites.png"), tex.EncodeToPNG());
                NetCheck(players.Length >= 2, $"Slide live render includes two player sprite trees {label} count={players.Length}");
                Plugin.Log.LogInfo($"[AutoTest] Slide render opacity {label} peer={Other()?.Latest.Opacity:F2} trees=" + string.Join(",", players.Select(p => p.transform.parent?.name + ":" + p.GetComponentsInChildren<SpriteRenderer>().Count(r => r.enabled && r.color.a > 0.05f))));
            }
            finally
            {
                for (int n = 0; n < renderers.Count; n++) if (renderers[n] != null) renderers[n].gameObject.layer = layers[n];
                cam.targetTexture = null; RenderTexture.active = active;
                rt.Release(); UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(tex); UnityEngine.Object.Destroy(go);
            }
        }

        IEnumerator NetSlideTest()
        {
            NbPrepare();
            PlayerData.instance.isInvincible = true;
            World.EnemyAiMode.Current = World.EnemyAiKind.Vanilla;
            bool host = suite.StartsWith("netslide_host");
            bool clientOwns = suite.EndsWith("_late");
            if (host)
            {
                for (float end = Time.realtimeSinceStartup + 100f; Time.realtimeSinceStartup < end && Other()?.Visible != true;) yield return null;
                NetCheck(Other()?.Visible == true, "Slide host sees client before tests");
                NetChat.Say("ns:mode:" + (clientOwns ? "client|" : "host|"));
            }
            else
            {
                yield return WaitChat("ns:mode:", 120f);
                clientOwns = SlideMessage("ns:mode:") == "client";
            }
            bool owns = host != clientOwns;
            string[] rooms = { "Crossroads_15", "Deepnest_East_06" };
            for (int i = 0; i < rooms.Length; i++)
            {
                string room = rooms[i];
                if (owns)
                {
                    yield return SealTravel(room, EntryGateOf(room));
                    NetChat.Say($"ns:entered:{i}|");
                    yield return WaitChat($"ns:joined:{i}|", 80f);
                }
                else
                {
                    yield return WaitChat($"ns:entered:{i}|", 80f);
                    yield return SealTravel(room, EntryGateOf(room));
                    NetChat.Say($"ns:joined:{i}|");
                }
                yield return new WaitForSecondsRealtime(1f);
                NetCheck(Enemies.Owns(room) == owns && Enemies.RemoteOwned(room) != owns, $"Slide {room} authority owns={owns}");
                NetCheck(Other()?.Visible == true, $"Slide {room} both players visible on {(host ? "host" : "client")}");
                if (owns) yield return NetSlideOwner(room, i);
                else yield return NetSlideFollower(room, i);
            }
            NetChat.Say(host ? "ns:host:done|" : "ns:client:done|");
            yield return WaitChat(host ? "ns:client:done|" : "ns:host:done|", 60f);
            NetCheck(Other()?.Visible == true, $"Slide final remote player still visible on {(host ? "host" : "client")}");
            yield return new WaitForSecondsRealtime(1f);
        }

        IEnumerator NetSlideOwner(string room, int i)
        {
            var targets = UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                .Where(h => h != null && !h.isDead && h.gameObject.scene.name == room && h.gameObject.activeInHierarchy && !Enemies.IsPuppet(h) && h.GetComponent<Rigidbody2D>()?.bodyType == RigidbodyType2D.Dynamic && !TerraKnockback.Immune(h) && !EnemyStats.NoKnockback(EnemyStats.Of(h)))
                .GroupBy(h => UI.EnemyHover.RecordOf(h)).Select(g => g.First()).Take(3).ToArray();
            NetCheck(targets.Length > 0, $"Slide {room} actual native enemies found n={targets.Length}");
            NetChat.Say($"ns:count:{i}:{targets.Length}|");
            for (int j = 0; j < targets.Length; j++)
            {
                var hm = targets[j];
                var rb = hm.GetComponent<Rigidbody2D>();
                var scripts = SlideHold(hm);
                var floor = SlideFloor(hm);
                float gravity = rb.gravityScale;
                Vector2 origin = rb.position;
                var heroRb = HeroController.instance.GetComponent<Rigidbody2D>();
                var constraints = heroRb.constraints;
                heroRb.constraints = RigidbodyConstraints2D.FreezeAll;
                hm.hp = 1000;
                rb.linearVelocity = Vector2.zero;
                HeroController.instance.transform.position = hm.transform.position + new Vector3(3f, 2f, 0f);
                try
                {
                    for (float end = Time.time + 5f; Time.time < end && Enemies.DevIdOf(hm) < 0;) yield return null;
                    int id = Enemies.DevIdOf(hm);
                    yield return new WaitForSecondsRealtime(0.6f);
                    NetChat.Say($"ns:target:{i}:{j}:{id}|");
                    yield return WaitChat($"ns:ready:{i}:{j}|", 20f);
                    rb.linearVelocity = new Vector2(4f, 0f);
                    int applied = Enemies.DevApplied;
                    NetChat.Say($"ns:hitnow:{i}:{j}|");
                    for (float end = Time.time + 8f; Time.time < end && hm.hp >= 1000;) yield return null;
                    var flight = hm.GetComponent<KnockFlight>();
                    NetCheck(hm.hp < 1000 && Enemies.DevApplied > applied && flight?.Active == true, $"Slide remote hit {room}/{UI.EnemyHover.RecordOf(hm)} hp={hm.hp} active={flight?.Active}");
                    SlideCommand(hm, 0f, gravity <= 0.01f ? (float?)0f : null);
                    for (float end = Time.time + 3f; Time.time < end && flight?.Active == true;) yield return null;
                    Plugin.Log.LogInfo($"[AutoTest] Slide handoff {hm.name} velocity={rb.linearVelocity} native={HarmonyLib.AccessTools.Field(typeof(KnockFlight), "nativeVelocity").GetValue(flight)} enabled=" + string.Join(",", hm.GetComponentsInChildren<MonoBehaviour>().Where(b => b.enabled).Select(b => b.GetType().Name)));
                    float x0 = rb.position.x;
                    yield return new WaitForSecondsRealtime(0.5f);
                    NetCheck(flight?.Active == false && Mathf.Abs(rb.position.x - x0) < 0.04f && Mathf.Abs(rb.linearVelocity.x) < 0.04f, $"Slide owner stopped {room}/{UI.EnemyHover.RecordOf(hm)} drift={rb.position.x - x0:F3} vx={rb.linearVelocity.x:F3}");
                    NetCheck(Mathf.Abs(rb.gravityScale - gravity) < 0.001f && SlideDrawn(hm), $"Slide owner physics and drawn {hm.name}");
                    Shot(room, "netslide_owner_stopped_" + j);
                    SlideRender(hm, $"{room}_owner_{j}");
                    yield return new WaitForSecondsRealtime(0.2f);
                    SlideRender(hm, $"{room}_owner_{j}_next");
                    var p = NetScenes.ToLocal(room, hm.transform.position);
                    NetChat.Say(FormattableString.Invariant($"ns:end:{i}:{j}:{p.x:F4}:{p.y:F4}:{hm.hp}|"));
                    yield return WaitChat($"ns:ack:{i}:{j}|", 20f);
                }
                finally
                {
                    if (floor != null) UnityEngine.Object.Destroy(floor);
                    heroRb.constraints = constraints;
                    if (hm != null) { rb.position = origin; rb.linearVelocity = Vector2.zero; SlideRelease(scripts); }
                }
            }
        }

        IEnumerator NetSlideFollower(string room, int i)
        {
            yield return WaitChat($"ns:count:{i}:", 30f);
            int.TryParse(SlideMessage($"ns:count:{i}:"), out int count);
            NetCheck(count > 0, $"Slide follower target count {room}={count}");
            for (int j = 0; j < count; j++)
            {
                yield return WaitChat($"ns:target:{i}:{j}:", 30f);
                int.TryParse(SlideMessage($"ns:target:{i}:{j}:"), out int id);
                HealthManager hm = null;
                for (float end = Time.time + 10f; Time.time < end;)
                {
                    hm = UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).FirstOrDefault(h => h != null && h.gameObject.scene.name == room && Enemies.DevPuppetId(h) == id && Enemies.IsPuppet(h));
                    if (hm != null && SlideDrawn(hm) && hm.hp == 1000) break;
                    yield return null;
                }
                NetCheck(hm != null && SlideDrawn(hm) && hm.hp == 1000, $"Slide follower sees native enemy id={id} {hm?.name} hp={hm?.hp}");
                if (hm != null) HeroController.instance.transform.position = hm.transform.position + new Vector3(-3f, 2f, 0f);
                var heroRb = HeroController.instance.GetComponent<Rigidbody2D>();
                var constraints = heroRb.constraints;
                heroRb.constraints = RigidbodyConstraints2D.FreezeAll;
                NetChat.Say($"ns:ready:{i}:{j}|");
                yield return WaitChat($"ns:hitnow:{i}:{j}|", 20f);
                if (hm != null)
                {
                    var col = hm.GetComponents<Collider2D>().FirstOrDefault(c => c.enabled);
                    int forwarded = Enemies.DevForwarded;
                    if (col != null) mod.Melee.HitWith(col, 100, 2f, 0f, -1f, hm.transform.position + Vector3.right);
                    NetCheck(Enemies.DevForwarded > forwarded, $"Slide follower forwards actual melee {hm.name}");
                }
                yield return WaitChat($"ns:end:{i}:{j}:", 20f);
                var fields = SlideMessage($"ns:end:{i}:{j}:").Split(':');
                yield return new WaitForSecondsRealtime(0.4f);
                if (hm != null && fields.Length == 3)
                {
                    var want = new Vector2(float.Parse(fields[0], CultureInfo.InvariantCulture), float.Parse(fields[1], CultureInfo.InvariantCulture));
                    var got = NetScenes.ToLocal(room, hm.transform.position);
                    int hp = int.Parse(fields[2], CultureInfo.InvariantCulture);
                    NetCheck(Vector2.Distance(want, got) < 0.3f && SlideDrawn(hm) && hm.hp == hp && hp < 1000, $"Slide follower matches stopped pose and damage {hm.name} error={Vector2.Distance(want, got):F3} hp={hm.hp}/{hp}");
                    var before = got;
                    yield return new WaitForSecondsRealtime(0.3f);
                    got = NetScenes.ToLocal(room, hm.transform.position);
                    NetCheck(Mathf.Abs(got.x - before.x) < 0.04f && hm.GetComponent<KnockFlight>()?.Active != true, $"Slide follower no independent drift {hm.name} dx={got.x - before.x:F3}");
                    Shot(room, "netslide_follower_stopped_" + j);
                    SlideRender(hm, $"{room}_follower_{j}");
                    yield return new WaitForSecondsRealtime(0.2f);
                    SlideRender(hm, $"{room}_follower_{j}_next");
                }
                else NetCheck(false, $"Slide follower missing result {i}/{j}");
                heroRb.constraints = constraints;
                NetChat.Say($"ns:ack:{i}:{j}|");
            }
        }
    }
}
