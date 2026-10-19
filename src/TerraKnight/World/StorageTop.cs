using System.Collections.Generic;
using UnityEngine;
namespace TerraKnight.World
{
    /// <summary>The solid, one-way top of a 3x2 dresser. Its contents use the existing chest save and host lock.</summary>
    internal sealed class StorageTop : MonoBehaviour
    {
        static readonly List<StorageTop> all = new List<StorageTop>();
        BoxCollider2D body;
        float offUntil;
        Vector2 size;
        Rect Box => new Rect(transform.position.x-size.x/2f,transform.position.y,size.x,size.y);
        public void Init(Rect box, PhysicsMaterial2D material)
        {
            size=box.size;
            var go = new GameObject("Terra Dresser Top") { layer = ChunkColliders.TerrainLayer };
            go.transform.SetParent(transform,false);
            go.transform.localPosition = new Vector3(0,box.height/2f,0);
            go.AddComponent<TerraOwned>();
            go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            body = go.AddComponent<BoxCollider2D>(); body.size = box.size; body.sharedMaterial = material; body.usedByEffector = true;
            var e=go.AddComponent<PlatformEffector2D>();e.useOneWay=true;e.surfaceArc=160;e.useSideFriction=false;e.useSideBounce=false;
            all.Add(this);
        }
        void OnDestroy() => all.Remove(this);
        void Update()
        {
            if(body==null)return;
            var hero=HeroController.SilentInstance;var b=Box;
            bool inside=false;
            if(hero!=null&&!body.enabled){var h=hero.GetComponent<BoxCollider2D>().bounds;inside=h.max.x>b.xMin&&h.min.x<b.xMax&&h.max.y>b.yMin&&h.min.y<b.yMax-.02f;}
            body.enabled=Time.time>=offUntil&&!inside;
        }
        public static bool TopAt(float x,float y)
        {
            foreach(var t in all){if(t==null||t.body==null||!t.gameObject.activeInHierarchy)continue;var b=t.Box;if(x>b.xMin&&x<b.xMax&&Mathf.Abs(y-b.yMax)<.06f)return true;}return false;
        }
        public static bool DropThrough(Bounds feet,float seconds)
        {
            bool any=false;
            foreach(var t in all){if(t==null||t.body==null||!t.gameObject.activeInHierarchy)continue;var b=t.Box;if(feet.max.x<=b.xMin||feet.min.x>=b.xMax||Mathf.Abs(feet.min.y-b.yMax)>.15f)continue;t.offUntil=Time.time+seconds;t.body.enabled=false;any=true;}return any;
        }
    }
    internal sealed partial class Chests
    {
        public static string DefaultTitle => IsOpen && RegionalFurniture.IsDresser(open.Style) ? Items.ItemDb.Get(RegionalChests.ItemOf(open.Style)).Name : Lang.T("箱子");
        internal static IEnumerable<(Rect Box,int Type)> HousingBoxes()
        {
            foreach(var c in chests)if(c.Region!=null)yield return(c.Box,RegionalFurniture.IsDresser(c.Style)?88:21);
        }
    }
}
