"""Builds the boss pet asset pack for an image-making agent: 空洞骑士首领宠物素材包/.

For each of the 48 boss fights (the relic manifest's order and references): its Hunter's Journal picture, the pet's
design (movement template, cell size, what each animation shows, its item), as spec.json and spec.md; the three
templates' frame tables as pictures; Terraria's own boss pets as reference sheets; and six upload batches as zips,
each one whole by itself (README, PROMPT, templates, references, the pixelize tool, its eight bosses).

    python tools/package_boss_pets.py
"""
import json
import shutil
import zipfile
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
PACK = ROOT / "空洞骑士首领宠物素材包"
RELICS = ROOT / "artifacts/boss-relics-v2/manifest.json"
ROSTER = ROOT / "artifacts/relic-references/boss-roster.json"
TERRARIA_IMAGES = Path(r"F:/SteamLibrary/steamapps/common/Terraria/Content/Images")
FONT = "C:/Windows/Fonts/msyh.ttc"
TERRARIA_EXPORT = Path(r"D:/ClaudeTemp/claude/e------------github-----projects---/618fcb2f-c0f1-4bfc-91ca-4305091bb4ad/scratchpad/terraria-pets")

SIZES = {"S": (36, 40), "M": (44, 48), "L": (54, 56)}
TEMPLATES = {
    "ground": {
        "zh": "地面跟随", "frames": 20,
        "anims": [("idle", 0, 3, 10, "待机"), ("walk", 4, 11, 5, "行走/奔跑"), ("jump", 12, 12, 8, "起跳上升"),
                  ("fall", 13, 13, 8, "下落"), ("fly", 14, 19, 4, "飞行追赶")],
        "baseline": list(range(0, 12)),
        "how": "在玩家身后的地面上走、跑、跳；玩家跑远、在空中或卡住时切换成“飞行追赶”飞过去，落地后又变回走路（Terraria 的 AI_026，和罐中脑、石巨人守卫、世纪之花幼苗一样）。",
        "analogs": ["Projectile_884", "Projectile_891", "Projectile_881"],
    },
    "flyer": {
        "zh": "飞行跟随", "frames": 8,
        "anims": [("fly", 0, 7, 5, "飞行")],
        "baseline": [],
        "how": "一直在空中，自由地飞着跟在玩家身边，按飞行速度微微倾斜（和宝贝蜜蜂、小骷髅王、小猪龙鱼一样）。",
        "analogs": ["Projectile_886", "Projectile_885", "Projectile_892"],
    },
    "anchor": {
        "zh": "肩旁悬浮", "frames": 12,
        "anims": [("float", 0, 7, 5, "漂浮"), ("action", 8, 11, 6, "小动作")],
        "baseline": [],
        "how": "悬在玩家肩膀旁边的固定位置，始终面朝玩家，轻轻上下漂浮；每隔几秒做一次“小动作”（和可疑眼睛、仙灵公主、杰克南瓜灯一样）。",
        "analogs": ["Projectile_882", "Projectile_895", "Projectile_896"],
    },
}

BATCHES = {1: "初入圣巢", 2: "泪城与王国", 3: "螳螂与深巢", 4: "战士之梦", 5: "斗兽场与骨钉大师", 6: "终章"}

# slug: (batch, template, size, light, variant_of, pet_zh, pet_en, item_zh, item_en, look, frame notes)
# Frame notes: ground -> idle, walk, jump, fall, fly; flyer -> fly; anchor -> float, action.
D = {}


def pet(slug, batch, template, size, pet_zh, pet_en, item_zh, item_en, look, notes, light=False, variant_of=None):
    D[slug] = dict(batch=batch, template=template, size=size, light=light, variant_of=variant_of, pet_zh=pet_zh, pet_en=pet_en,
                   item_zh=item_zh, item_en=item_en, look=look, notes=notes)


# ---- 1 初入圣巢
pet("falseknight", 1, "ground", "L", "小假骑士", "Tiny False Knight", "生锈的钉头锤", "Rusty Mace",
    "灰色厚重的层叠盔甲占了身体的一大半，弯角头盔、圆鼓鼓的护肩；头盔缝里露出藏在里面的小蛆虫的一双橙色眼睛；拖着一把比身子还大的圆头钉锤。",
    dict(idle="盔甲随呼吸一起一伏，头盔缝里的小眼睛眨一下。", walk="一步一顿的重踏，整副盔甲左右摇晃，钉锤拖在身后在地上弹一下。",
         jump="双手把钉锤举过头顶，膝盖一蹲一蹬。", fall="盔甲张开，小蛆虫慌张地从领口探出头。",
         fly="小蛆虫从盔甲背后钻出半个身子，盔甲的关节缝里冒出一串橙色感染气泡托着它往前飘（创意参考石巨人守卫脚底喷火的飞法）。"))
pet("falseknight-5", 1, "ground", "L", "小失败冠军", "Tiny Failed Champion", "褪色的冠军徽记", "Faded Champion's Crest",
    "和小假骑士同一个体型、同一套动作；盔甲换成梦境里那套更华丽的金白色铠甲，头盔的角更长，钉锤带金边，身上点缀几颗梦境白光点（用实心像素点表现）。",
    dict(idle="同小假骑士。", walk="同小假骑士。", jump="同小假骑士。", fall="同小假骑士。", fly="托着它的是一串白色梦境光球，而不是橙色感染气泡。"),
    variant_of="falseknight")
pet("hornet", 1, "ground", "M", "小黄蜂", "Little Hornet", "丝线缠绕的小针", "Thread-Wound Needle",
    "白色面具、两根长长的分叉角（宠物化后依然要高高翘起）、深红色钟形褶皱斗篷、黑色细腿，背上扛着一根比身体还长的针。",
    dict(idle="斗篷轻轻摆动，针扛在肩上，第 3 帧歪一下头。", walk="小碎步快跑，斗篷向后飘起，针保持水平。", jump="收腿跃起，斗篷像钟一样鼓起。",
         fall="斗篷张开，像降落伞一样减速。", fly="把针朝前上方掷出，丝线拉着自己荡过去：掷出 → 丝线绷直 → 荡起 → 收针，6 帧一个循环。"))
pet("hornet-4", 1, "ground", "M", "小黄蜂·哨兵", "Little Hornet Sentinel", "带刺的丝线球", "Spiked Silk Ball",
    "和小黄蜂同一个体型、同一套动作；斗篷边缘破损，上面挂着几缕白色丝线，眼神更锐利（王国边缘的那一战）。",
    dict(idle="同小黄蜂。", walk="同小黄蜂。", jump="同小黄蜂。", fall="同小黄蜂。", fly="同小黄蜂，荡过去时身后带一颗小小的白色刺丝球。"),
    variant_of="hornet")
pet("mawlek", 1, "ground", "L", "小毛里克", "Mawlet", "躁郁的蛋", "Brooding Egg",
    "圆滚滚的深灰色分节甲壳，背上鼓着几颗橙色感染脓包，一对镰刀般的前爪，小小的头总是皱着眉。",
    dict(idle="整个身子鼓起又瘪下，像在生闷气，背上的脓包亮一下。", walk="用前爪撑地、整个身体一下一下跳着前进：8 帧正好是一次完整的跳跃（下蹲 → 腾空 → 落地压扁 → 回弹），参考史莱姆王子。",
         jump="前爪高高举起。", fall="缩成一团。", fly="背上的两颗脓包鼓成橙色气球把它吊起来，前爪在空中乱划。"))
pet("megamosscharger", 1, "ground", "M", "苔藓小冲锋者", "Mossy Charger Jr.", "一簇湿润的苔藓", "Damp Moss Tuft",
    "矮矮的圆丘，整个被一层层深绿色苔藓叶覆盖，底下露出一点黑色小身体和两只发光的小眼睛。",
    dict(idle="伪装成一小丛苔藓，眼睛只露出一半，叶子随风轻摆。", walk="低头猛冲，苔藓叶向后翻起，底下的小腿快速倒腾。", jump="整丛苔藓弹起。",
         fall="叶子向四周张开。", fly="背上的几片大叶子像螺旋桨一样转起来，把它带上天（参考世纪之花幼苗）。"))
pet("bigfly", 1, "flyer", "M", "迷你格鲁兹之母", "Gruzzling Mother", "一枚格鲁兹卵", "Gruz Egg",
    "胖胖的淡紫色肚子、深蓝色横纹、尖尖的尾巴，一对小得可怜的翅膀几乎带不动身子，眼睛总是眯着在打瞌睡。",
    dict(fly="小翅膀拼命扑扇，胖身子上下颠簸 1–2 个像素；第 4–5 帧张嘴打个哈欠。"))
pet("bigbuzzer", 1, "flyer", "M", "小反击蝇王", "Vengefly Prince", "蝇王的獠牙", "Vengefly Fang",
    "蓝色分节的腹部，四片长长的淡色翅膀，巨大的白色獠牙下颚（缩小后依然夸张），头顶一顶小小的王冠。",
    dict(fly="四片翅膀两两交替扑动，獠牙一张一合，身体微微前倾，好像随时要俯冲。"))

# ---- 2 泪城与王国
pet("mageknight", 2, "ground", "S", "小灵魂战士", "Soul Squire", "一缕灵魂火", "Soul Wisp",
    "圆圆的深石板蓝灵魂铠甲，头盔里亮着橙色的灵魂光眼，细细的小腿，握着一根细长的骨钉。",
    dict(idle="身体轻轻颤动，橙色眼光一明一暗。", walk="小步快走，骨钉横在身前。", jump="举钉跃起。", fall="下摆鼓起。",
         fly="化成一团橙色灵魂光，只剩头盔的轮廓在光里，像瞬移一样闪着飞过去。"))
pet("magelord", 2, "anchor", "M", "迷你灵魂大师", "Mini Soul Master", "灌满灵魂的水晶球", "Soul-Filled Orb",
    "浅灰色圆滚滚的身体，酒红色高领，深蓝色分叉长袍下摆，身边绕着一颗橙色灵魂球。",
    dict(float="身体缓缓上下浮动，长袍下摆飘动，灵魂球绕身体转一圈（转到身后时被身体挡住）。", action="双手举起施法，灵魂球亮一下再落回原位。"))
pet("magelord-5", 2, "anchor", "M", "迷你灵魂暴君", "Mini Soul Tyrant", "暗淡的灵魂球", "Dimmed Soul Orb",
    "和迷你灵魂大师同一个体型、同一套动作；长袍破烂、颜色更暗，眼神凶狠，身边的灵魂球变成两颗，带一点紫色。",
    dict(float="同迷你灵魂大师，两颗球交错着转。", action="同迷你灵魂大师。"), variant_of="magelord")
pet("dungdefender", 2, "ground", "M", "小粪虫防御者", "Little Dung Defender", "一颗温热的粪球", "Warm Dung Ball",
    "梅紫色分节甲壳带珊瑚粉镶边，一对小弯角，开朗的大眼睛，总是骄傲地挺着胸。",
    dict(idle="双手叉腰，胸口一挺一挺。", walk="缩成一颗粪球滚着走：8 帧正好滚一圈，球上的纹路跟着转，偶尔露出小角。",
         jump="从球里弹出来，四肢张开。", fall="张开双臂。", fly="抱着一颗大粪球，粪球冒出的气泡把他托着往前飘。"))
pet("whitedefender", 2, "ground", "M", "小白色防御者", "Little White Defender", "象牙色的粪球", "Ivory Dung Ball",
    "和小粪虫防御者同一个体型、同一套动作；甲壳换成象牙白，领口一圈深蓝色羽毛，身上几颗白色梦境光点。",
    dict(idle="同小粪虫防御者。", walk="同小粪虫防御者（滚动的是象牙色的球）。", jump="同小粪虫防御者。", fall="同小粪虫防御者。", fly="同小粪虫防御者。"),
    variant_of="dungdefender")
pet("blackknight", 2, "ground", "M", "小守望者骑士", "Watcher Squire", "守望塔的光蝇灯", "Watcher's Lumafly Lamp",
    "圆圆的深海军蓝甲虫铠甲，宽大的横向甲片，头上一只弯角，手持一把弯刀，甲片缝里露出发光的小眼睛。",
    dict(idle="立正站岗，第 2–3 帧打个瞌睡、头往下一点。", walk="蜷成球滚着前进：8 帧滚一圈（守望者骑士会滚着冲锋）。", jump="展开身体跳起。",
         fall="甲片张开。", fly="蜷成球，被三四只小光蝇（黄白色发光点）托着飞。"))
pet("jarcollector", 2, "ground", "M", "小收藏家", "Little Collector", "贴着标签的空罐子", "Labeled Empty Jar",
    "光亮的黑色无定形身体，四条细长手臂和蜘蛛般的手指，白色面具上一双大圆眼，怀里抱着一只装着小虫子的玻璃罐。",
    dict(idle="抱着罐子左右晃，罐子里的小虫跟着动。", walk="四条手臂一起爬，蹦蹦跳跳很兴奋。", jump="高高跳起，罐子举过头顶。", fall="四条手臂张开。",
         fly="倒挂着从上方荡过来，四条手臂乱舞（它在战斗里就是从天花板上落下来的）。"))
pet("flukemother", 2, "flyer", "M", "小吸虫母", "Little Flukemarm", "吸虫卵囊", "Fluke Egg Sac",
    "淡紫肉色的悬垂肉囊，身上一圈圈粉色管口，底部垂着两三只小吸虫幼虫。",
    dict(fly="肉囊像水母一样一收一放地飘，管口轮流张合，底下的小吸虫跟着摆尾巴。"))

# ---- 3 螳螂与深巢
pet("mantislord", 3, "ground", "M", "小螳螂领主", "Mantis Lordling", "领主的长矛尖", "Lord's Lance Tip",
    "很高的深靛色分叉冠饰（宠物化后依然醒目），褶皱斗篷，细长分节的下腹，手持细长的骨钉，姿态优雅。",
    dict(idle="持钉立正，冠饰微动，第 3 帧微微欠身行礼。", walk="优雅的长步，斗篷摆动。", jump="收身跃起。", fall="斗篷展开。",
         fly="背后展开一对薄薄的淡靛色螳螂翅，快速振动（两帧交替表现振翅）。"))
pet("sistersofbattle", 3, "ground", "L", "战斗小姐妹", "Battle Sisterlings", "三枚螳螂徽章", "Three Mantis Badges",
    "三只更小的螳螂领主排成一列站在一起：前面一只最高，后面两只稍矮、颜色稍深；冠饰、斗篷和骨钉都和小螳螂领主一致。",
    dict(idle="三只轮流呼吸、轮流歪头。", walk="三只步伐错开半拍一起走。", jump="一只接一只依次跳起，像一道波浪。", fall="三件斗篷一起张开。",
         fly="三只一起展开螳螂翅飞。"), variant_of="mantislord")
pet("traitorlord", 3, "ground", "M", "小叛徒领主", "Traitor Lordling", "叛徒的弯刃", "Traitor's Hooked Blade",
    "高大的青绿色层叠螳螂斗篷，宽大的钩状前臂刀，弯曲的大头冠，眼神凶狠。",
    dict(idle="两把弯刀前臂交叉在胸前，身体随呼吸起伏。", walk="弓着身子大步走，前臂一前一后摆。", jump="双刀高举。", fall="斗篷展开。",
         fly="背上展开青绿色的翅膀，前臂收在胸前。"))
pet("mimicspider", 3, "ground", "M", "小诺斯克", "Little Nosk", "拙劣的骑士面具", "Crude Knight Mask",
    "黑色蜘蛛般的身体、细长折角的腿、橙色条纹的肚子；脸上戴着一张模仿小骑士的白色面具（仿得很拙劣，一边大一边小）。",
    dict(idle="缩成一团假装成小骑士的样子，面具朝前，第 3 帧偷偷伸出一条蜘蛛腿又缩回去。", walk="八条长腿飞快爬行，身体压得很低。", jump="腿全部收起弹跳。",
         fall="长腿张开。", fly="长腿在空中乱舞着扑过来（参考罐中脑飞行时悬空摆动的腿）。"))
pet("wingednosk", 3, "flyer", "M", "小有翼诺斯克", "Little Winged Nosk", "黑色革翅碎片", "Leathery Wing Scrap",
    "小诺斯克的身体，橙色肋状的肚子，背上一对巨大的黑色革质翅膀，脸上戴着模仿大黄蜂的面具（神居里那只）。",
    dict(fly="一对大翅膀用力扑动，下垂的长腿随着身体的起伏摆动。"), variant_of="mimicspider")
pet("infectedknight", 3, "ground", "M", "小残破容器", "Little Broken Vessel", "橙色的感染气球", "Infection Balloon",
    "破损不对称的白色外壳角，破烂的海军蓝斗篷，头顶飘着一个大大的橙色感染气球（用一根感染丝连着），手里握着骨钉。",
    dict(idle="身子有点歪地站着，头顶的气球一晃一晃。", walk="一瘸一拐、但很快的步伐。", jump="用力高跳（残破容器很能跳）。", fall="斗篷张开。",
         fly="双手抓住头顶的感染气球飘起来，气球鼓得更大。"))
pet("infectedknight-5", 3, "ground", "M", "小失落近亲", "Little Lost Kin", "漆黑的感染气球", "Void-Stained Balloon",
    "和小残破容器同一个体型、同一套动作；整体被虚空染黑：外壳灰黑，斗篷更破，气球变成暗紫色、往下滴黑色。",
    dict(idle="同小残破容器。", walk="同小残破容器。", jump="同小残破容器。", fall="同小残破容器。", fly="同小残破容器（抓的是暗紫色气球）。"),
    variant_of="infectedknight")
pet("hiveknight", 3, "ground", "M", "小蜂巢骑士", "Hive Squire", "金黄的蜂蜡", "Golden Beeswax",
    "金黄条纹的蜜蜂腹部，厚重的石板蓝分节护手，透明的翅膀，长长的骨钉，身边绕着一两只小蜜蜂。",
    dict(idle="挺立不动，翅膀偶尔振一下，小蜜蜂绕着头转。", walk="稳重的大步。", jump="翅膀一振跃起。", fall="翅膀展开减速。",
         fly="翅膀高速振动（两帧交替表现模糊），小蜜蜂跟在后面。"))

# ---- 4 战士之梦
pet("ghostxero", 4, "anchor", "M", "小泽若", "Little Xero", "泽若的墓碑碎片", "Xero's Grave Shard",
    "鲑鱼灰色层叠胸甲，飘逸的深色缎带状翅膀和触须，身后悬着四把小骨钉；梦境亡灵的通透感用浅色描边和亮点表现，不用真正的半透明。",
    dict(float="缎带翅膀像波浪一样飘，四把小骨钉在身后缓缓上下错动。", action="四把小骨钉同时指向前方，闪一下。"))
pet("ghostaladar", 4, "anchor", "S", "小戈布", "Little Gorb", "一枚金色梦之针", "Golden Dream Needle",
    "奶油色扇形带脊的外壳，细长分节的尾巴，周围一圈辐射状的小梦之针。",
    dict(float="上下漂浮，扇壳微微开合，针圈慢慢旋转（每帧针的位置挪一格）。", action="仰头张开（喊“升天！”的样子），针圈向外扩一下再收回。"))
pet("ghostmarmu", 4, "flyer", "S", "小马尔穆", "Little Marmu", "一朵王后花园的花", "Queen's Garden Flower",
    "浅鼠尾草绿的毛毛虫，宽大的橙褐色圆形口环，身上软软的小疙瘩，两根长长的触角。",
    dict(fly="一边飘一边打滚：前 4 帧伸展身体漂浮，后 4 帧卷成球转一圈（马尔穆在战斗里会卷成球弹来弹去）。"))
pet("ghostnoeyes", 4, "anchor", "M", "小无眼", "Little No Eyes", "破旧的蒙眼布", "Tattered Blindfold",
    "长长飘逸的象牙色头发，深色缠布的身体，破碎的浅色斗篷，身边飘着一两个白色小梦境灵魂。",
    dict(float="头发和斗篷像在水里一样慢慢飘，小灵魂绕着她转。", action="双手捂住脸（她在哭泣），肩膀一抖。"))
pet("ghostmarkoth", 4, "anchor", "M", "小马科斯", "Little Markoth", "冥想者的念珠", "Meditator's Beads",
    "巨大的灰紫色蛾翼斗篷，白色念珠项圈，身旁一把小骨钉和一面旋转的梦之盾。",
    dict(float="蛾翼斗篷缓缓开合，梦之盾绕身体转一圈（转到身后时被挡住）。", action="闭目冥想，梦之盾停在身前发亮。"))
pet("ghosthu", 4, "anchor", "S", "小胡长老", "Little Elder Hu", "金色的祈祷圆环", "Golden Prayer Ring",
    "浅灰色的破旧长袍，大大的金色圆环和念珠，圆脸小眼。",
    dict(float="盘腿悬浮、上下浮动，身边一枚金环慢慢自转。", action="双手合十，头顶落下一个小金环又消失。"))
pet("ghostgalien", 4, "anchor", "M", "小加利安", "Little Galien", "迷你镰刀", "Miniature Scythe",
    "深灰色圆顶盔甲，露出弯曲笼状的肋骨，扛着一把巨大的月牙镰刀，身旁飘着一把旋转的小镰刀。",
    dict(float="上下漂浮，小镰刀在身旁旋转（每帧转 45°）。", action="挥一下大镰刀。"))
pet("megajellyfish", 4, "flyer", "M", "小乌姆", "Little Uumuu", "装着电流的泡泡", "Charged Bubble",
    "圆形的果冻膜（通透感用浅色外圈和内部高光表现），里面一团橙黄色核心泡泡，下面垂着白色分叉的电触须，身边跟着两只小乌玛（白色小水母）。",
    dict(fly="伞盖一收一放地推进（水母游动的节奏），触须随之摆动，核心里的光点挪动；第 6 帧闪一下白色电光。"))

# ---- 5 斗兽场与骨钉大师
pet("lobsterlancer", 5, "ground", "L", "小神之驯服者", "Little God Tamer", "驯兽师的长矛", "Tamer's Lance",
    "一只青绿色分节盔甲的小野兽，背上棕褐色带角的脊甲；野兽背上骑着更小的酒红色螳螂般的驯兽师，举着长矛。",
    dict(idle="野兽趴着喘气，驯兽师举矛摆姿势。", walk="野兽小跑，驯兽师随着颠簸上下。", jump="野兽跳起，驯兽师抓紧。", fall="两个一起张开四肢。",
         fly="野兽蜷成一个球，驯兽师站在球上，像踩着气球一样被吹着飘。"))
pet("oblobble", 5, "flyer", "S", "小奥波路波", "Little Oblobble", "奥波路波的软刺", "Oblobble Spike",
    "圆滚滚的青蓝色同心圆外壳，背上一排小三角刺，两对浅色的昆虫翅膀，一张总在嘟囔的嘴。",
    dict(fly="两对翅膀交替扑动，胖身子上下颠，第 5 帧嘴里吐出一个小泡泡。"))
pet("zote", 5, "ground", "S", "小左特", "Little Zote", "钝掉的“终结者”", "Blunt Life Ender",
    "破旧的深灰色小斗篷，一长一短、歪歪扭扭的象牙色角，握着一根钝钝的木头骨钉（他管它叫“终结者”），表情自信过头。",
    dict(idle="叉腰挺胸摆姿势（自以为很帅），第 3 帧挥一下钉。", walk="大摇大摆地走，第 9 帧（行走第 6 帧）被自己绊一下、马上站稳。", jump="笨拙地起跳，四肢乱挥。",
         fall="惊慌地张开双手。", fly="被一只会飞的小左特头（左特林）叼着斗篷拎着飞，自己还在乱踢腿。"))
pet("greyprince", 5, "ground", "L", "小灰色王子左特", "Little Grey Prince Zote", "灰色王子的皇冠", "Grey Prince's Crown",
    "巨大的深石板色斗篷，圆滚滚的梦境身体，短短歪歪的象牙色角，苍白的脸，头顶一顶小王冠；比小左特更胖更大，气势十足。",
    dict(idle="胸口起伏，骄傲地抬着头。", walk="沉重地跺步，每一步身子都往下一沉。", jump="高高跳起，准备砸地。", fall="身体压扁，准备落地。",
         fly="被三只小左特头一起抬着飞。"))
pet("nailbros", 5, "ground", "L", "小奥罗和小马托", "Oro & Mato Jr.", "两把交叉的骨钉", "Crossed Nails",
    "两个敦实的灰靛色斗篷小身影，酒红色围脖，各扛一把超大的宽骨钉，一高一矮并排站。",
    dict(idle="两人背靠背，轮流呼吸。", walk="一起小跑，步伐错开半拍。", jump="一起跳起。", fall="一起张开斗篷。",
         fly="两人背靠背旋转着飞过来，两把骨钉像螺旋桨一样转。"))
pet("paintmaster", 5, "ground", "M", "小绘画大师席奥", "Little Sheo", "沾满颜料的画笔", "Paint-Soaked Brush",
    "大大的棕褐色围裙，深靛色斗篷，紫色头带，扛着一支比身体还大的画笔，笔尖带着粉紫色颜料。",
    dict(idle="拿画笔在空中比划，第 3 帧笔尖滴下一滴颜料。", walk="画笔扛在肩上，蹦跳着走。", jump="举笔跃起。", fall="围裙张开。",
         fly="骑在画笔上，像骑扫帚一样飞，笔尾甩出彩色颜料点。"))
pet("nailsage", 5, "ground", "S", "小斯莱", "Little Sly", "伟大骨钉贤者的钱袋", "Sly's Coin Purse",
    "小小的灰色苍蝇般身体，配一把巨大的宽三角骨钉，薄薄的小翅膀，紫色细节，一双狡黠的眼睛。",
    dict(idle="拄着大骨钉站着，第 2–3 帧掏出一枚吉欧看一眼又收起来。", walk="小碎步快走，大骨钉扛在肩上。", jump="原地转一圈跃起。", fall="小翅膀张开。",
         fly="抱着骨钉高速旋转成一团小旋风飞过来（大回旋斩）。"))
pet("palelurker", 5, "ground", "S", "小苍白潜伏者", "Little Pale Lurker", "被藏起来的简单钥匙", "Hidden Simple Key",
    "破旧的橄榄绿小斗篷，大大的带裂纹的象牙色外壳（边缘参差像皇冠），一根细长的针，眼睛从外壳下面偷看。",
    dict(idle="缩在壳下只露出眼睛，偷偷左右看。", walk="弓着身子鬼鬼祟祟地小跑。", jump="弹起。", fall="斗篷张开。",
         fly="缩成一颗带刺的球，翻滚着弹过来（像她扔出的刺球）。"))

# ---- 6 终章
pet("megabeamminer", 6, "ground", "L", "小水晶守卫", "Little Crystal Guardian", "一块粉色水晶", "Pink Crystal Chunk",
    "高高的三角形粉色水晶头，巨大的粉色棱角肩部水晶，炭灰色分节身体，一双橙色的大眼睛。",
    dict(idle="坐在一张小长椅上打瞌睡（水晶守卫最出名的样子），第 3 帧睁一下眼；长椅只画在待机帧里。", walk="站起来沉重地跺步走。",
         jump="肩上的水晶一闪，跳起。", fall="身体缩起。", fly="肩上的水晶喷出粉色水晶光推着它飞（参考石巨人守卫脚底喷火）。"))
pet("enragedguardian", 6, "ground", "L", "小暴怒守卫", "Little Enraged Guardian", "过度生长的水晶", "Overgrown Crystal",
    "和小水晶守卫同一个体型、同一套动作；水晶长得更大更杂乱、颜色更深（紫红色），眼睛变成愤怒的红橙色；它不坐长椅了。",
    dict(idle="站着跺脚生气，眼光一闪一闪。", walk="同小水晶守卫。", jump="同小水晶守卫。", fall="同小水晶守卫。", fly="同小水晶守卫（喷出紫红色的水晶光）。"),
    variant_of="megabeamminer")
pet("grimm", 6, "ground", "M", "小格林团长", "Little Troupe Master", "梦魇之灯的火苗", "Nightmare Lantern Flame",
    "黑色蝙蝠般的斗篷带猩红色内衬，高高弯曲的领尖，细长尖锐的脸，红色的瞳孔，姿态优雅（注意和已有的“格林之子”区分：这是团长本人的迷你版）。",
    dict(idle="双手背在身后，微微前倾像在鞠躬，斗篷尾摆动。", walk="踮着脚尖优雅地走，像在跳舞。", jump="斗篷张开像蝙蝠翼一样跃起。", fall="斗篷收拢。",
         fly="斗篷化作一对蝙蝠翼扇动着飞，翼尖带红黑色火苗。"))
pet("nightmaregrimm", 6, "ground", "M", "小梦魇之王", "Little Nightmare King", "梦魇之心", "Nightmare Heart",
    "和小格林团长同一个体型、同一套动作；斗篷变成鲜艳的猩红色，头顶高耸火焰状的王冠领，身上带猩红色火苗像素点。",
    dict(idle="同小格林团长。", walk="同小格林团长。", jump="同小格林团长。", fall="同小格林团长。", fly="同小格林团长，翼尖的火焰更大、是猩红色。"),
    variant_of="grimm")
pet("hollowknight", 6, "ground", "M", "小空洞骑士", "Little Hollow Knight", "锁链的碎片", "Broken Chain Link",
    "破旧的暗橄榄色长斗篷，巨大不对称的破损容器角，只有一只手臂，拿着一把磨损的长骨钉，胸口透出一点橙色感染光。",
    dict(idle="低着头站着，斗篷下摆飘，胸口的感染光一闪一闪。", walk="拖着骨钉缓慢有力地走。", jump="骨钉举起跃起。", fall="斗篷张开。",
         fly="身后伸出几条黑色的虚空触手，把自己托起来飘过去。"))
pet("hollowknightprime", 6, "ground", "M", "小纯粹容器", "Little Pure Vessel", "无瑕的骨钉", "Flawless Nail",
    "和小空洞骑士同一个体型、同一套动作；完整对称的象牙白容器角，浅灰色分叉披风，黑色身体，干净的长骨钉，姿态端正，没有感染光。",
    dict(idle="端正地站着，披风轻摆。", walk="同小空洞骑士，但步伐稳健。", jump="同小空洞骑士。", fall="同小空洞骑士。", fly="同小空洞骑士。"),
    variant_of="hollowknight")
pet("finalboss", 6, "anchor", "L", "小辐光", "Little Radiance", "一片发光的羽毛", "Radiant Feather",
    "白色羽毛般的蛾子，三根针状的辐射王冠，展开的分层羽翼，身体中间一道长长的光之剑形，橙白色的眼睛。它是照明宠物，会照亮周围。",
    dict(float="羽翼缓缓扇动，王冠的光芒一长一短（用像素线条表现光线），身体上下浮动。", action="双翼完全展开，光芒最亮的一帧，然后收回。"), light=True)
pet("absoluteradiance", 6, "anchor", "L", "小无上辐光", "Little Absolute Radiance", "燃烧的辐光之羽", "Blazing Radiant Feather",
    "和小辐光同一个体型、同一套动作；羽翼更大更亮，王冠多出几根光刺，周围飘着金色光点。同样是照明宠物。",
    dict(float="同小辐光。", action="同小辐光，光刺一起伸长。"), light=True, variant_of="finalboss")


def font(size):
    try:
        return ImageFont.truetype(FONT, size)
    except OSError:
        return ImageFont.load_default()


def template_picture(key, t, cell=(44, 48)):
    """A template's frame table: a numbered slot per frame, the animations as coloured bands with their names."""
    k = 3
    cw, ch = cell
    n = t["frames"]
    gap = 6
    w = n * (cw * k + gap) + 40
    h = ch * k + 170
    im = Image.new("RGB", (w, h), (34, 38, 54))
    d = ImageDraw.Draw(im)
    d.text((20, 12), f"{t['zh']}模板（{key}）：{n} 帧。这里横着排开方便看；交付的 pet.png 由 pixelize.py 从上到下竖着拼，每格 cell_w × cell_h",
           font=font(26), fill=(255, 255, 255))
    colors = [(230, 120, 90), (110, 190, 120), (120, 160, 240), (220, 190, 90), (190, 120, 220)]
    top = 60
    for i in range(n):
        x = 20 + i * (cw * k + gap)
        d.rectangle([x, top, x + cw * k, top + ch * k], outline=(160, 170, 200), width=2)
        d.rectangle([x, top + (ch - 2) * k, x + cw * k, top + ch * k], fill=(70, 50, 60))
        d.text((x + 6, top + 4), str(i), font=font(22), fill=(255, 230, 90))
        if key == "ground":
            yb = top + (ch - 2) * k - 2
            d.line([x, yb, x + cw * k, yb], fill=(255, 120, 120), width=2)
    for j, (aid, a, b, ticks, zh) in enumerate(t["anims"]):
        x0 = 20 + a * (cw * k + gap)
        x1 = 20 + b * (cw * k + gap) + cw * k
        y = top + ch * k + 14
        d.rectangle([x0, y, x1, y + 30], fill=colors[j % len(colors)])
        d.text((x0 + 6, y + 2), f"{zh} {a}–{b}（每帧 {ticks} tick）" if a != b else f"{zh} {a}", font=font(20), fill=(20, 20, 30))
    note = "红线 = 脚底基线（地面帧的脚踩在这里）；每格底部 2 像素（暗红）留空。" if key == "ground" else "每格底部 2 像素（暗红）留空；角色画在格子中间。"
    d.text((20, h - 46), note, font=font(20), fill=(220, 220, 235))
    return im


def main():
    rows = json.loads(RELICS.read_text(encoding="utf-8"))
    roster = {r["key"]: r for r in json.loads(ROSTER.read_text(encoding="utf-8"))}
    order = [s for b in sorted(BATCHES) for s in D if D[s]["batch"] == b]
    assert len(order) == 48 and len({r["slug"] for r in rows} - set(D)) == 0, set(r["slug"] for r in rows) - set(D)
    by_slug = {r["slug"]: r for r in rows}

    if PACK.exists():
        for sub in ("bosses", "templates", "上传用"):
            shutil.rmtree(PACK / sub, ignore_errors=True)
    PACK.mkdir(exist_ok=True)
    (PACK / "templates").mkdir()
    for key, t in TEMPLATES.items():
        template_picture(key, t).save(PACK / "templates" / f"{key}.png")

    # Terraria's own pets: the sheets as they are and at 3x numbered.
    # Exported once with tools/XnbDump (Projectile_881-896, Item_4797-4811) into TERRARIA_EXPORT; kept as they are when it is gone.
    ref = PACK / "terraria-reference"
    ref.mkdir(exist_ok=True)
    src = TERRARIA_EXPORT
    frames_of = {"Projectile_881": 12, "Projectile_882": 20, "Projectile_884": 14, "Projectile_885": 10, "Projectile_886": 8, "Projectile_890": 12,
                 "Projectile_891": 15, "Projectile_892": 6, "Projectile_894": 8, "Projectile_895": 6, "Projectile_896": 16}
    for p in (src.glob("*.png") if src.exists() else []):
        if p.stem in frames_of or p.stem.startswith("Item_"):
            shutil.copy(p, ref / p.name)
    for stem, n in frames_of.items():
        if not (ref / f"{stem}.png").exists():
            continue
        im = Image.open(ref / f"{stem}.png").convert("RGBA")
        fh = im.height // n
        k = 3
        out = Image.new("RGBA", (n * (im.width + 6) * k, (fh + 12) * k), (34, 38, 54, 255))
        d = ImageDraw.Draw(out)
        for i in range(n):
            f = im.crop((0, i * fh, im.width, (i + 1) * fh)).resize((im.width * k, fh * k), Image.NEAREST)
            x = i * (im.width + 6) * k
            out.alpha_composite(f, (x, 12 * k))
            d.text((x + 2, 2), str(i), font=font(20), fill=(255, 230, 90, 255))
        out.save(ref / f"{stem}_frames_x3.png")

    manifest = []
    for idx, slug in enumerate(order, 1):
        p = D[slug]
        r = by_slug[slug]
        t = TEMPLATES[p["template"]]
        cw, ch = SIZES[p["size"]]
        ro = roster.get(r["key"]) or roster.get(r["key"].split("@")[0]) or {}
        folder = f"{idx:02d}-{slug}"
        spec = {
            "index": idx, "folder": folder, "slug": slug, "key": r["key"], "boss_zh": r["name"], "boss_en": r["en"],
            "stage": ro.get("stage"), "batch": p["batch"], "batch_zh": BATCHES[p["batch"]],
            "pet_zh": p["pet_zh"], "pet_en": p["pet_en"], "item_zh": p["item_zh"], "item_en": p["item_en"],
            "template": p["template"], "template_zh": t["zh"], "light_pet": p["light"], "variant_of": p["variant_of"],
            "size": p["size"], "cell_w": cw, "cell_h": ch, "art_w": cw // 2, "art_h": (ch - 2) // 2,
            "frame_count": t["frames"],
            "animations": [{"id": a, "from": f, "to": to, "ticks": tk, "zh": zh, "notes": p["notes"].get(a, "")} for a, f, to, tk, zh in t["anims"]],
            "baseline_frames": t["baseline"], "faces": "left",
            "look": p["look"], "boss_traits": r.get("traits", ""),
            "terraria_analogs": t["analogs"], "item_icon_art": [16, 16],
        }
        bdir = PACK / "bosses" / folder
        bdir.mkdir(parents=True)
        shutil.copy(r["reference"], bdir / "reference.png")
        if p["variant_of"]:
            base = by_slug[p["variant_of"]]
            shutil.copy(base["reference"], bdir / "reference_base.png")
        (bdir / "spec.json").write_text(json.dumps(spec, ensure_ascii=False, indent=2), encoding="utf-8")
        lines = [
            f"# {idx:02d} {p['pet_zh']}（{p['pet_en']}）",
            "",
            f"- 来自首领：**{r['name']}**（{r['en']}），第 {spec['batch']} 批「{BATCHES[p['batch']]}」",
            f"- 物品（召唤它的宠物物品）：**{p['item_zh']}**（{p['item_en']}），图标 16×16 美术像素",
            f"- 模板：**{t['zh']}**（`{p['template']}`），{t['frames']} 帧 —— {t['how']}",
            f"- 尺寸：{p['size']} 号，每格 {cw}×{ch} 贴图像素 = {cw // 2}×{(ch - 2) // 2} 美术像素（每个美术像素放大成 2×2，格子底部 2 行留空）",
            f"- 朝向：**朝左**画（游戏里向右走时自动翻转）",
        ]
        if p["light"]:
            lines.append("- **照明宠物**：游戏会给它加光源，贴图本身不用画发光的半透明光晕，用亮色像素表现发光即可")
        if p["variant_of"]:
            base = D[p["variant_of"]]
            lines.append(f"- **变体**：基于「{base['pet_zh']}」（同一批里先画它），体型、轮廓、每一帧的动作都和它一致，只改下面写的外观差异；`reference_base.png` 是本体的原版立绘")
        lines += ["", "## 外观", "", p["look"], "", f"原版首领特征（英文）：{r.get('traits', '')}", "", "## 每组动作", ""]
        for a in spec["animations"]:
            span = f"帧 {a['from']}" if a["from"] == a["to"] else f"帧 {a['from']}–{a['to']}"
            lines.append(f"- **{a['zh']}**（{span}，每帧 {a['ticks']} tick ≈ {a['ticks'] * 1000 // 60} ms）：{a['notes']}")
        lines += ["", "## 参考", "", "- `reference.png`：原版猎人日志立绘（造型以它为准，不要发明原版没有的部件）",
                  f"- 同类的泰拉宠物：{'、'.join(f'`terraria-reference/{a}_frames_x3.png`' for a in t['analogs'])}（看帧的分配、动作幅度和像素画法）",
                  f"- 模板帧表：`templates/{p['template']}.png`"]
        (bdir / "spec.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
        manifest.append(spec)
    (PACK / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")

    # Upload batches: each zip whole by itself.
    up = PACK / "上传用"
    up.mkdir()
    common = ["README.md", "PROMPT.md", "tools/pixelize.py"] + [f"templates/{k}.png" for k in TEMPLATES] + \
             [f"terraria-reference/{p.name}" for p in sorted(ref.iterdir())]
    for b, name in BATCHES.items():
        z = up / f"批次{b}-{name}.zip"
        specs = [s for s in manifest if s["batch"] == b]
        with zipfile.ZipFile(z, "w", zipfile.ZIP_DEFLATED) as zf:
            for rel in common:
                if (PACK / rel).exists():
                    zf.write(PACK / rel, rel)
            for s in specs:
                for f in sorted((PACK / "bosses" / s["folder"]).iterdir()):
                    zf.write(f, f"bosses/{s['folder']}/{f.name}")
            zf.writestr("batch.json", json.dumps(specs, ensure_ascii=False, indent=2))
        print(z.name, len(specs), "只", f"{z.stat().st_size // 1024} KB")
    print("bosses:", len(manifest))


if __name__ == "__main__":
    main()
