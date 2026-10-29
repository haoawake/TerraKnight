using System.Collections;
using System.Linq;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "pinsprites", for the Collector's Map's pins on the world map): the game's loaded sprites whose names speak of
    /// pins, grubs or benches, with their sizes and textures; and the player's pin flags. Lines "[AutoTest] 图钉 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator PinSpritesTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var pd = PlayerData.instance;
            Plugin.Log.LogInfo($"[AutoTest] 图钉 标记：幼虫 {pd.hasPinGrub}，长椅 {pd.hasPinBench}，已救幼虫场景 {pd.scenesGrubRescued?.Count ?? 0}");
            var sprites = Resources.FindObjectsOfTypeAll<Sprite>()
                .Where(s => s != null && (s.name.ToLowerInvariant().Contains("pin") || s.name.ToLowerInvariant().Contains("grub") || s.name.ToLowerInvariant().Contains("bench")))
                .Select(s => $"{s.name}[{s.rect.width:0}x{s.rect.height:0} {s.texture?.name}]").Distinct().OrderBy(x => x).ToList();
            foreach (var chunk in sprites.Select((s, i) => (s, i)).GroupBy(x => x.i / 12))
                Plugin.Log.LogInfo("[AutoTest] 图钉 " + string.Join("，", chunk.Select(x => x.s)));
            Plugin.Log.LogInfo($"[AutoTest] 图钉 共 {sprites.Count}");
        }
    }
}
