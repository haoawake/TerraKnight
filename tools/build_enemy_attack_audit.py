"""Current-code audit plus individually curated attack/phase research and native scene evidence.

Does not execute build_enemy_ai_design.build(), copy its proposed upgrades, or claim runtime proof.
"""
from pathlib import Path
from collections import Counter
from urllib.parse import quote
import hashlib
import json
import re
import build_enemy_ai_design as research

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'artifacts/enemy-attack-audit'
REPORT = ROOT / 'docs/全怪物攻击方式与阶段彻查-2026-10-21.md'

# Every key is explicitly assigned. Absence is an error, not an inference of no projectile.
PROJECTILES = {}


def assign(keys, text):
    for key in keys.split():
        if key in PROJECTILES:
            raise ValueError('Duplicate projectile assignment: ' + key)
        PROJECTILES[key] = text


assign('Crawler Buzzer Bouncer Climber Hatchling ZombieRunner ZombieHornhead ZombieLeaper ZombieBarger ZombieShield Roller Mummy MossWalker MossmanRunner Mosquito MossCharger SnapperTrap AcidWalker AcidFlyer MossKnightFat MossFlyer JellyCrawler FungCrawler FungoonBaby MushroomBaby MantisFlyerChild Mantis Sentry SentryFat FlyingSentrySword RoyalDandy RoyalCoward RoyalPlumper GorgeousHusk GreatShieldZombie MageBlob MageBalloon FlipHopper Inflater Flukefly FatFluke MinesCrawler CrystalCrawler BabyCentipede SpiderCorpse MiniSpider SpiderFlyer SlashSpider AbyssCrawler OrangeBalloon BlowFly Hopper GiantHopper BeeStinger BigBee GrubMimic GrassHopper HeavyMantis ColRoller ColMosquito ColShield ColFlyingSentry ColWorm ColHopper Sibling PrayerSlug MenderBug HealthScuttler OrangeScuttler Pigeon LazyFlyer EggSac WhiteRoyal Worm BigCentipede ZapBug PalaceFly AbyssTendril BigBuzzer BigFly MegaMossCharger GhostMarmu NailBros Nailsage Zote ZotelingBuzzer ZotelingHopper FlukeLarva Revek ZotelingMighty LankyZoteling ZotesCurse', '研究资料未列独立飞行弹体；身体、武器、机关或特殊状态的攻击见行为栏。此结论尚未逐对象排除所有场景差异。')
assign('Spitter', '单发酸液朝玩家方向吐出；按发射方向飞行，区别于追踪球。精确角度与锁向帧待解码。')
assign('Hatcher', '产幼体，不是发射不可击杀弹丸；活体孵化与死亡产两只分开统计。')
assign('Blocker', '开壳向外吐液，或产小巴德尔；靠近闭壳。两种产物分别是弹体与可击杀敌人。')
assign('ZombieGuard', '棍砸/后跳砸地产生贴地向外传播的冲击波；不是朝玩家飞行的抛物弹。各动作是否出波待场景 FSM 细核。')
assign('MossmanShaker', '近身爆炸气云，属于局部范围伤害，不应写成远程追踪弹。')
assign('BlobFlyer Blobble', '固定斜四向喷液，受击也可触发；不是每颗都瞄玩家。')
assign('PlantShooter', '朝玩家方向吐刺球；可由骨钉反弹。近身缩回。')
assign('MossKnight', '投刺球配合盾斩；投掷的数量、仰角、反弹交互待逐动作核实，不从升级建议取值。')
assign('Jellyfish', '本体破裂后释放会追逐并爆炸的橙色核心；死亡衍生物，不能只统计活体吐弹。')
assign('FungusFlyer', '向身体下方喷气，持续范围危险；不是任意方向瞄准球。')
assign('MushroomTurret', '吐出会追踪、会爆炸的孢子，可诱导回撞；近身缩回。追踪角速度与寿命未核定。')
assign('FungifiedZombie', '活体近身放云，尸体再爆；区分两次范围伤害来源。')
assign('MushroomRoller', '滚球/跳扑沿路留毒雾；属于运动后的残留危险区。')
assign('MushroomBrawler', '向外吐黏液并短时留地；近身另有头砸。落点、数量待原生字段核实。')
assign('FlyingSentryJavelin', '向玩家投直线长矛；持矛俯冲是本体动作，不能合算为第二种飞弹。')
assign('Mage MageKnight', '生成会转向追逐玩家的灵魂球；传送、冲斩是另外的本体动作。灵魂战士召唤愚蠢只适用于特定战斗版本。')
assign('CeilingDropper', '俯冲后自爆，是携爆身体；碰地形也能引爆，不是另吐一枚炸弹。')
assign('Flukeman', '死后拆成会飞的上半与爬行下半，二者是敌人部件，不是弹幕。')
assign('ZombieMiner ZombieMiner@0 ColMiner', '远距投镐/武器、近距挥砍；投物可反弹。阶段键不证明弹道不同，具体抛射字段待核实。')
assign('CrystalFlyer', '朝玩家抛水晶；撞表面形成有伤害的晶簇，晶簇可击碎/借力。飞行段与留场段分列。')
assign('LaserBug', '背部沿表面朝外射激光；细预警线之后才是伤害束，不能按无前摇弹丸描述。')
assign('BeamMiner', '头灯瞄向玩家后发激光；预警与伤害分段，原版可被攻击打断。')
assign('AngryBuzzer', '死亡后的延迟爆炸；不列活体主动飞弹。')
assign('BurstingBouncer', '活体向下滴感染液，死亡另有延迟爆炸；滴液不等于瞄准射击。')
assign('BurstingZombie', '本体冲锋后碰撞/死亡爆炸；没有据此增加独立远程弹丸。')
assign('SpittingZombie', '朝玩家方向抛成簇腐蚀液，落地短暂残留；弹簇与液池要分别核验。')
assign('CentipedeHatcher', '孵出刻尘者，产满后改俯冲；是召唤物与行为切换，不是连续飞弹。')
assign('ShootSpider', '附壁/顶时朝玩家吐毒；眼光与叫声是出手提示，确切单轮弹数待核实。')
assign('LesserMawlek', '远距吐带下落轨迹的酸液、落地短留；近距爪扫。')
assign('MawlekTurret', '固定口部向外喷大片感染液；地面与天顶朝向不同，不主动移动追人。')
assign('SuperSpitter', '一轮三发扇形散射，中央弹朝玩家；两侧偏角未核定，发射后不应标成三枚追踪球。')
assign('BeeHatchling', '普通蜂接触追击；蜂巢骑士所产蜂是从上方下冲的召唤体，两者受伤规则不同。')
assign('ZombieHive', '活体周期产蜂，死亡产三蜂；蜂为独立敌人。')
assign('GardenZombie', '向上半圆喷五刺；死亡还留下可伤人的尖刺尸体，法术击杀差异待实战确认。')
assign('MantisHeavyFlyer', '抛镰刃沿先下降再上升的弯曲路线飞行；不能描述为直线矛。')
assign('ElectricMage', '在采样到的玩家位置生成连续电爆，属于位置指定范围技；没有必经的口部到落点飞行过程。')
assign('RoyalGuard', '投出会回返的爪刃，去程与回程都须避；另有近战连斩。')
assign('PaleLurker', '逃跑过程中留下不可直接击碎的短时尖刺；是路径残留物，另有近距挥臂。')
assign('FalseKnight FalseKnight@5', '棍砸地波沿地面传播；狂砸会导致上方落物。落物是垂直坠落危险，不能归为瞄准吐弹。')
assign('Mawlek', '平时向玩家侧抛感染液；大呕吐覆盖一侧区域，上方落下，和近距爪击可交织。')
assign('Hornet Hornet@4', '掷针沿前方飞出再沿丝线拉回，去/回程均危险；丝线圈属本体周围范围技。二战另布置悬空刺球，不是所有针都会追踪。')
assign('MantisLord SistersOfBattle', '附墙抛回旋刃，依起手高度出现不同曲线；多人阶段会组合投刃。不是自机狙击球。')
assign('MageLord', '追踪灵魂球；四球围绕本体横穿后回返；砸地产生向外地波。二阶段连续下砸与驻点放球交替。')
assign('MageLord@5', '同招式族但环绕球增为六颗，节奏与地波范围加强；二阶段仍有连续下砸/放球。')
assign('DungDefender', '抛粪球在场内弹跳；破土可同时喷出弹体。本体弹跳不要误算为另一发粪球。')
assign('FlukeMother', '从孔洞产飞吸虫，常成对；是可清理的召唤物，母体固定。活体上限待字段核定。')
assign('BlackKnight', '研究资料未列常规独立弹丸；地滚与弹跳滚是本体，滚动有骨钉防护。')
assign('JarCollector', '天顶落罐，罐破生成杂兵；罐是落物、杂兵是独立 AI，按神居档位区分怪池。')
assign('MegaBeamMiner EnragedGuardian', '手部激光瞄准玩家后锁束，吼叫触发天顶垂直束；细线预瞄与粗束判定分开。')
assign('MegaJellyfish', '全场分布电爆与按玩家位置逐次产生的电爆；神居欧玛核心是另一个敌人的死亡产物，借其破壳。')
assign('MimicSpider WingedNosk', '喷感染液与天顶滴液；翼之诺斯克另召感染气球并俯冲。两变体的地形与移动招式不能混写。')
assign('InfectedKnight', '跳砸向外喷四团感染液；另有感染瀑流，并可出现感染气球。')
assign('InfectedKnight@5', '跳砸改为六团感染液，气球更频繁；研究资料明确不使用残破容器的感染瀑流。')
assign('HiveKnight', '三颗蜂蜜刺球依次爆出尖刺；蜂群从上方下冲。刺球弹幕、可互动蜂群、本体斩击分别核验。')
assign('TraitorLord', '双飞镰/弧形刀波与贯场直立地波；后者应按穿越墙体危险处理，不能当普通低矮可跳地波。')
assign('Oblobble', '旋转多方向液弹，双体共同占场；一只死亡后幸存者加速狂暴，不能沿用双体时弹速结论。')
assign('LobsterLancer', '野兽吐三组落地感染液；滚动撞墙后弹跳向玩家的是本体。骑手跳劈独立执行。')
assign('HollowKnight', '感染散射按弧形覆盖，砸地产生竖柱，后段悬浮喷液；刀斩/地冲/弹跳属本体。')
assign('FinalBoss AbsoluteRadiance', '光束分轮扫向、放射剑、横向剑墙/纵向剑雨、扫场光墙、会追踪的光球；每种弹幕与各战斗阶段分开，不给整场统一弹道。无上辐光有双轮放射剑及终段双平台光球。')
assign('GhostXero', '飞钉朝玩家射出后回到本体，回程仍危险；双钉到四钉。')
assign('GhostAladar', '八向长矛环射，随血量降低增加连续轮数；近距离定向矛另列，确切触发规则待核。')
assign('GhostHu', '成列光环由上往下砸，另一种由两边向中央推进；找空隙，不是追踪飞弹。')
assign('GhostNoEyes', '场地幽灵横穿并上下波动，不是每只主动追踪；降血后更密。')
assign('GhostGalien', '主镰有惯性地追近并弹地；低血追加独立漂移小镰，二者路线不同。')
assign('GhostMarkoth', '飞钉先在场内生成、朝玩家锁向后射出；盾围身旋转并可扩张/回收，低血增加盾与钉压。')
assign('GreyPrince', '跳砸/落地地波；吐出有翼/跳跃幼体，后续战次增加爆炸幼体；幼体不是普通弹丸。')
assign('WhiteDefender', '白球抛出并弹跳；破土喷弹、拍地产生高刺、弹跳收尾有推进短刺。高刺和短刺不能混称同一种可跳地波。')
assign('Grimm', '火蝙蝠横向飞行、升空后落下火球、地刺；特定血量膨胀喷出分层火球（河豚）。')
assign('NightmareGrimm', '更强火蝙蝠/升空落火/地刺/河豚；新增逐次追身火柱，俯冲地冲留下火迹。')
assign('Paintmaster', '画笔颜色决定弹型：紫红大斩落漆、黄笔长刺、红笔跳落六漆弹、蓝笔地扫三弹；黄笔长刺本身属武器动作。')
assign('HollowKnightPrime', '扇形飞钉；下砸竖直剑柱；后段聚焦连锁爆破与水平虚空卷须，卷须不是可反弹小球。')
assign('ZotelingBalloon', '延时或死亡爆炸的悬空幼体；范围爆炸，非主动投掷。')
assign('FlameBearerSmall', '三向火球，另有冲锋；任务激活后才出现战斗。')
assign('FlameBearerMed', '五发分速火球及四球螺旋；精确速度/旋转字段待核，不套用梦魇版球数。')
assign('FlameBearerLarge', '五球喷射、五球螺旋及连续三火柱；另有高速冲锋。')
assign('Shade', '可使用玩家已取得的法术；法术来源和虚空之心状态影响实际威胁，不是一套固定完整技能。')
assign('HeavyZoteling', '跳落制造短地波；身体落点与波的横向传播分别躲。')
assign('TurretZoteling', '挂顶向玩家吐酸；自动离场和被杀分开计分。')
assign('HeadOfZote', '垂直砸落后大地波贯场；下落/升空还可能杀其他左特，属于场地危险型敌人。')
assign('FlukeZoteling', '从地面上升沿途留下滞留酸液；自动离场后仍需看残留寿命。')

PHASES = {
    'Hatcher': '有限累计产仔，死亡还会产仔；属于次数/死亡行为，不是已证实的低血二阶段。',
    'MossCharger': '披叶冲锋→掉叶露虫逃跑→钻地重新披叶；可重复的护甲/行为状态切换。',
    'MossKnightFat': '三次预跳后追扑是招式序列；取得帝王之翼/完成守梦者条件引发死亡属于世界状态变化。',
    'Jellyfish': '本体死亡→核心延迟追踪→爆炸；死亡派生流程，非活体低血阶段。',
    'FlipHopper': '受击翻背后改跳跃追击，再次受击恢复；可逆受击状态。',
    'Inflater': '受击/碰撞触发膨胀反弹→回缩；周期形态而非低血二阶段。',
    'Flukeman': '本体被杀后上、下半分别激活；应按死亡分裂部件覆盖，不能只查本体一条命。',
    'CentipedeHatcher': '累计孵出五只后停止孵化并改俯冲咬击；次数触发的行为阶段，非血量阈值。',
    'SpiderCorpse': '特定躯壳死亡→尸体抽动→尸变爬行；条件复活，不可推广至全体躯壳。',
    'GrubMimic': '瓶中伪装→释放变形→追撞；登场形态切换。',
    'GardenZombie': '活体喷刺→死亡刺尸；尸体危险延续，法术击杀差异待核。',
    'Mantis': '螳螂领主战后友好，受击可反击；属于阵营/世界条件变化。',
    'MantisFlyerChild': '同螳螂部族友好条件；不是原生血量阶段。',
    'Sibling': '虚空之心改变敌意/出现状态；不是低血转阶段。',
    'PalaceFly': '受击分裂短暂失去接触危险→重组恢复；平台支点循环，不是普通击杀。',
    'FalseKnight': '盔甲打空→露头→头部受击计数→起身狂砸，重复三轮；野外/梦境末尾破地板后还有头部收尾，神居不同。当前 BossPhases 以 Stunned Amount 与身体/Head 区分。',
    'FalseKnight@5': '失败冠军沿用盔甲/露头循环；强化的是动作、地波与落物，不是新加另一套 Boss。代码以基础 Record+场景识别。',
    'BigFly': '死亡后野外产幼体，其他场景可能不同；不是半血二阶段。',
    'MantisLord': '一名领主退场后两名共同作战；本项目按 Mantis Lord 与 S1/S2 身体识别两阶段。',
    'SistersOfBattle': '单人开场→三人共同战斗；是场景变体，不是另一个掉落键。',
    'MageLord': '第一身体倒下→假死/破地板→Mage Lord Phase2，第二身体连续下砸与驻点施法；不是回血 bug。',
    'MageLord@5': '灵魂暴君同为两身体顺序阶段，第二阶段节奏与范围不同；按梦境/神居场景分支。',
    'BlackKnight': '连续唤醒多名骑士，通常最多两名同时活动，吊灯可减少总数；BossPhases 的 0/1/2/3 是血量分账批次，不等于每批都新增招式。',
    'NailBros': '先奥罗单人；倒下再起，马托加入双人战。代码监测奥罗 hp 归零后再有血并识别 Mato。',
    'Oblobble': '一只死亡→幸存者狂暴，更快移动/喷弹；典型同伴死亡触发，不是固定半血。',
    'LobsterLancer': '骑手+野兽双体，野兽被杀会结束主要战斗；不能认定必须依次杀两条完整血池。',
    'Hornet@4': '血量推进后开始布置刺球，另有招架反击；精确低血阈值待当前 FSM 解码。',
    'HollowKnight': '血量推进逐步加入感染相关技→自刺/悬浮喷液/弹跳→衰弱收尾；梦钉、大黄蜂介入与结局分支是事件链，不能套统一百分比。',
    'FinalBoss': '地面弹幕→地刺/剑雨等阶段→平台战→爬升→终结演出；每段进入依赖原生血量/事件，精确阈值待核。',
    'AbsoluteRadiance': '强化地面/平台阶段→爬升→双平台终段光球战；不能照搬普通辐光最终演出。',
    'GhostXero': '降至约半血后由双钉增为四钉；具体本机比较阈值待字段核实。',
    'GhostAladar': '降血后八向矛环从单轮变双轮/三轮；阈值待核。',
    'GhostNoEyes': '降血增加幽灵密度/节奏；场地版本不同，不是 BossPhases 中另建身体的阶段。',
    'GhostGalien': '随血量下降先后追加两把小镰；精确阈值待核。',
    'GhostMarkoth': '约半血后增加第二盾并提升飞钉压力；无地板是神居档位的场地差异，不是半血撤地板。',
    'DungDefender': '中途一次短时连续破土强化；随后恢复常规动作，属于临时强化而非永久新增第二身体。',
    'WhiteDefender': '有一次连续破土强化；重复梦战的难度成长是跨战次变化，不是本场低血转阶段。',
    'GreyPrince': '后续挑战轮次增加爆炸幼体等压力；同一战的跳砸/摔倒/召唤属选招，不把战次当血量阶段。',
    'Grimm': '血量节点强制进入河豚弹幕，之后恢复决斗；开场鞠躬被打也可触发惩罚河豚。节点和惩罚触发必须分别验证。',
    'NightmareGrimm': '血量节点河豚，平时已有火柱等招；火柱不是仅因二阶段才存在。精确阈值待核。',
    'Nailsage': '常规多段剑技→末段高速绕场后近身旋斩；低血招式模式切换。',
    'HollowKnightPrime': '本机 GG_Hollow_Knight / HK Prime 的 Set Phase HP：Quarter HP=floor(登场HP/3)，Half HP=Quarter HP×2；Phase? 按这两值分 PHASE1/2/3。Half/Quarter 是变量名，不是 1/2、1/4。原生阶段与聚焦/卷须选招还需按 Choice P1/P2/P3 的路径对应，不能只看命名。',
    'EnragedGuardian': '普通守卫之后的独立强化重战；不是普通守卫同一条血打半才变成暴怒。',
    'Shade': '技能取决于玩家已解锁法术，虚空之心改变敌意；死亡回收与领地返回是任务状态。',
    'Revek': '梦钉吸收墓地灵魂后出场追斩，最终剩余条件可梦钉；剧情守护者流程，非一般血条阶段。',
    'FlameBearerSmall': '任务进度决定出现/战斗，传送→攻击→传送循环，不等于三阶亲族会在单场升级。',
    'FlameBearerMed': '独立的亲族等级，由任务进度决定；单场仍为传送攻击循环。',
    'FlameBearerLarge': '独立梦魇亲族等级；火柱/螺旋是已有选招，不宣称有额外低血阶段。',
    'HeadOfZote': '出现→落下→停留→升空离场；计时生命周期，不是低血阶段。',
    'FlukeZoteling': '出土→上升留液→自动离场；生命周期，不是低血二阶段。',
    'TurretZoteling': '挂顶攻击→计时自动离场；离场不等于玩家击杀。',
}

OVERRIDES = {
    'Hornet': ('大黄蜂·守护者', 'Hornet Protector'), 'Hornet@4': ('大黄蜂·哨兵', 'Hornet Sentinel'),
    'FalseKnight@5': ('失败冠军', 'Failed Champion'), 'MageLord@5': ('灵魂暴君', 'Soul Tyrant'),
    'InfectedKnight@5': ('失落近亲', 'Lost Kin'), 'ZombieMiner@0': ('躯壳矿工·低阶段映射', 'Husk Miner'),
    'Grimm': ('剧团团长格林', 'Troupe Master Grimm'), 'NightmareGrimm': ('梦魇之王格林', 'Nightmare King Grimm'),
    'FinalBoss': ('辐光', 'The Radiance'), 'HollowKnight': ('空洞骑士', 'The Hollow Knight'),
    'NailBros': ('奥罗与马托', 'Brothers Oro & Mato'), 'Oblobble': ('奥波路波', 'Oblobbles'),
    'AbyssCrawler': ('阴影爬虫', 'Shadow Creeper (Hollow Knight)'),
    'AbyssTendril': ('虚空卷须', 'Void Tendrils (Hollow Knight)'), 'PalaceFly': ('翅膀傀儡', 'Wingmould (Hollow Knight)'),
}


def main():
    native = json.loads((DEST / 'native-index.json').read_text(encoding='utf-8'))
    decoded = json.loads((DEST / 'decoded-index.json').read_text(encoding='utf-8'))
    action_index = json.loads((DEST / 'action-index.json').read_text(encoding='utf-8'))
    ownership = json.loads((DEST/'owner-links.json').read_text(encoding='utf-8')) if (DEST/'owner-links.json').exists() else None
    shared = json.loads((DEST/'shared-index.json').read_text(encoding='utf-8')) if (DEST/'shared-index.json').exists() else None
    source = ROOT / 'src/TerraKnight/Items/EnemyDropData.cs'
    roster = {m[0]: dict(en=m[1], zh=m[2], stage=int(m[3]), boss=m[4] == 'true', kind=m[5], hp=int(m[6]), scene=m[7]) for m in re.findall(r'\["([^"]+)"\] = new Entry \{ En = "([^"]+)", Zh = "([^"]+)", Stage = (\d+), Boss = (true|false), Kind = "([^"]+)", Hp = (\d+).*?Scene = "([^"]+)"', source.read_text(encoding='utf-8-sig'))}
    assert len(roster) == len(re.findall(r'= new Entry \{', source.read_text(encoding='utf-8-sig'))), 'Roster parser lost entries'
    original = [r.split('|') for r in research.DATA.strip().splitlines()]
    extras = [r.split('|') for r in research.EXTRA.strip().splitlines()]
    assert {r[1] for r in original} == set(roster), 'Research roster drift: review new/removed keys'
    allkeys = set(roster) | {r[1] for r in extras}
    assert allkeys == set(PROJECTILES), {'missing': sorted(allkeys - set(PROJECTILES)), 'unknown': sorted(set(PROJECTILES) - allkeys)}
    scenes = {r['scene']: r for r in native['scenes']}
    stats = (ROOT / 'src/TerraKnight/Items/EnemyStats.cs').read_text(encoding='utf-8-sig')
    relics = set(re.findall(r'"([^"]+)"', re.search(r'RelicBosses = new HashSet<string> \{([^}]+)', stats)[1]))
    entries = []
    for group, key, base, _proposal, _visual in original:
        e = dict(roster[key], key=key, group=group, behavior=base, extra=False)
        e['classification'] = ('Boss' if key in relics or (e['kind'] != 'miniboss' and (e['boss'] or e['kind'] in ('boss', 'arena_boss', 'dream_boss', 'warrior_dream', 'godhome_boss') or e['hp'] >= 300)) else '精英' if e['kind'] != 'passive' and (e['kind'] == 'miniboss' or e['hp'] >= 70) else '无敌/平台障碍' if e['kind'] in ('invulnerable', 'pogo') else '生态/特殊' if e['kind'] in ('passive', 'unique') else '小怪')
        entries.append(e)
    extra_scenes = dict(EnragedGuardian='Mines_32', SistersOfBattle='GG_Mantis_Lords_V', WingedNosk='GG_Nosk_Hornet', AbsoluteRadiance='GG_Radiance', Shade='动态死亡场景', FlukeLarva='Waterways_08', Revek='RestingGrounds_08')
    for group, key, zh, en, base, _proposal, _visual in extras:
        entries.append(dict(key=key, group=group, zh=zh, en=en, behavior=base, extra=True, classification='Boss变体' if key in ('EnragedGuardian', 'SistersOfBattle', 'WingedNosk', 'AbsoluteRadiance') else '特殊/挑战单位', scene=extra_scenes.get(key, 'GG_Mighty_Zote')))
    missing_scene = []
    for e in entries:
        key = e['key']
        if key in OVERRIDES:
            e['zh'], e['en'] = OVERRIDES[key]
        if key == 'ZombieMiner@0':
            e['behavior'] = '矿工招式族：近身挥镐，远处投镐，可反弹；@0 是项目阶段/场景映射，不代表新物种或已证实不同弹道。'
        e['projectiles'] = PROJECTILES[key]
        e['phases'] = PHASES.get(key, '现有研究未列独立血量转阶段；待机/警觉/攻击/恢复不单独算二阶段。未用已解码的全部 FSM 证明不存在隐藏阈值。')
        e['source'] = 'https://hollowknight.wiki/w/' + quote(e['en'].replace(' ', '_'), safe="_()&'")
        s = scenes.get(e['scene'])
        e['nativeSceneFile'] = s['file'] if s else None
        e['nativeActionFile'] = next((r['file'] for r in action_index['scenes'] if r['scene']==e['scene']),None)
        e['nativeEvidence'] = '代表场景已读取并解码；对象归属另见 owner-links.json，不把全场动作归给本敌人' if s else '此场景名未在原生列表匹配；需检查动态实例/别名'
        e['runtimeVerifiedThisRun'] = False
        if s is None:
            missing_scene.append(dict(key=key, scene=e['scene']))
    hashes = {str(p.relative_to(ROOT)).replace('\\', '/'): hashlib.sha256(p.read_bytes()).hexdigest() for p in [source, ROOT / 'tools/build_enemy_ai_design.py', ROOT / 'src/TerraKnight/Items/EnemyStats.cs', ROOT / 'src/TerraKnight/Items/BossPhases.cs', ROOT / 'src/TerraKnight/Items/BossBalance.cs', ROOT / 'src/TerraKnight/World/EnemyAggro.cs', ROOT / 'src/TerraKnight/Net/EnemyTargets.cs']}
    result = dict(rosterCount=len(roster), extraCount=len(extras), entryCount=len(entries), missingRosterKeys=[], individuallyAssignedProjectileKeys=len(PROJECTILES), sceneRead=native['scenesRead'], sceneExpected=native['scenesExpected'], nativeErrors=native['errors'], decodedFsmCount=decoded['decoded'],decodedFsmErrors=decoded['errors'],actionCounts=action_index['counts'], unmatchedRepresentativeScenes=missing_scene, classifications=dict(Counter(e['classification'] for e in entries)), behaviorEvidence='Reviewed pre-existing research, with explicit supplemental attack/phase annotations; no fresh online verification. Per-object native evidence is a separate layer.', currentCodeEvidence='Current workspace source, may differ from installed plugin.', nativeEvidence='Current installed original assets: exact-length validated FSM decoding, state transitions, common action fields, variables and component ownership; not runtime/reachability proof.', runtimeVerifiedThisRun=False, sourceSha256=hashes, entries=entries)
    (DEST / 'coverage.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    if ownership:
        result['representativeFsmMatchedEntries'] = len(entries)-len(ownership['unmatchedRepresentativeEntries'])
        result['representativeFsmUnmatchedEntries'] = ownership['unmatchedRepresentativeEntries']
        result['componentDecodeErrors'] = ownership['componentDecodeErrors']
        result['nonFsmEntries'] = ownership.get('nonFsmEntries',[])
        result['unresolvedEntryIdentities'] = ownership.get('unresolvedEntryIdentities',[])
        result['nativeIdentityCoveredEntries'] = len(entries)-len(result['unresolvedEntryIdentities'])
        if shared:
            result['sharedAssetsRead'] = len(shared['assets'])
            result['sharedFsmsDecoded'] = shared['fsms']
            result['sharedDecodeErrors'] = shared['errors']
        (DEST / 'coverage.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    text = [f'''# 全怪物攻击方式与阶段彻查

日期：2026-10-21（用户时区）。对象：本项目当前工作区与本机 Hollow Knight 原生资源。

## 结论与证据边界

已逐项整理 **{len(roster)} 个当前名册键 + {len(extras)} 个补充单位/独立变体 = {len(entries)} 项**，每项单列攻击、弹幕/产物、阶段/形态和代表场景。键数不等于物种数。已只读扫描本机原生资源 **{native['scenesRead']}/{native['scenesExpected']} 场景**，异常 {len(native['errors'])}；这是资源覆盖率，不是实战通过率。

这份文档不把 AI 升级设计里的建议当成已实现行为。行为概括来自仓库已有研究，本次增加逐键弹道和阶段注释；本次没有重新在线核实每页社区资料。当前代码的机制结论另列。原生资源已进一步解码 **{decoded['decoded']:,} 个 FSM**（对象长度校验失败 {decoded['errors']}）：**{action_index['counts']['states']:,} 状态、{action_index['counts']['transitions']:,} 条转移、{action_index['counts']['actions']:,} 动作**，其中 {action_index['counts']['disabledActions']:,} 个动作标记为禁用，不能直接列为实际攻击。动作参数共 {action_index['counts']['parameters']:,} 项，{action_index['counts']['unresolvedParameters']:,} 项保留为未展开。完整逐对象字段与本页的行为研究是两层证据，不能相互冒充。

已解析常见速度、角度、事件、对象引用与比较参数；数值变量区分序列化值、变量名和 FSM 初值。**变量初值不等于运行时值；完整对象解码不等于所有路径已人工复核可达。** 子物体、共享 prefab、全局池、跨 FSM 事件与 C# 组件仍需接着追踪。没有证据的概率、锁向帧、冷却或最终有效百分比不填猜测值。详见 [逐怪物原生 FSM 证据](全怪物原生FSM逐项证据-2026-10-21.md)。

**本轮未启动游戏、未操作存档、未逐场实战、未证明已安装 Mod 与工作区一致。** 下文是可追溯的静态审计与行为研究清单；不宣称“全部怪物实机彻查通过”。

已补扫 **{result.get('sharedAssetsRead',0)} 个共享资源文件**，额外解码 **{result.get('sharedFsmsDecoded',0):,} 个 FSM**，共享资源解码异常 {result.get('sharedDecodeErrors',0)}。场景与共享资源合计 **{decoded['decoded']+result.get('sharedFsmsDecoded',0):,} 个 FSM**。逐项原生身份入口覆盖 **{result.get('nativeIdentityCoveredEntries',0)}/182**：{result.get('representativeFsmMatchedEntries',0)} 项连到同对象 FSM（部分是其他实际出现场景/共享 prefab），另 {len(result.get('nonFsmEntries',[]))} 项用组件与脚本入口覆盖：{', '.join(result.get('nonFsmEntries',[]))}。身份入口覆盖不等于所有生成链、子节点、路径和实战结果已经逐项验收。

## 当前 Mod 对攻击链的实际影响

|入口|当前代码可确认的行为|审计含义|
|---|---|---|
|EnemyStats.cs / BossStats.cs|保留原生 hp 为真实存储，以比例换算泰拉伤害/生命，原生脚本仍读原血量|显示血量变化不代表原生转阶段阈值被删除；不能直接套泰拉替身 Boss 的 AI|
|EnemyStats.EliteKind / IsBossKind|反击蝇王 BigBuzzer、灵魂战士 MageKnight 已在 RelicBosses 中按 Boss；其余按种类和原生名册生命分类，精英阈值 70、Boss 兜底 300，miniboss 有专门例外|旧设计把这两个放在精英栏已不适合当前项目分类；运行时还有 HasBossScale/场景识别|
|World/EnemyAggro.cs|非 Boss 被玩家打中后 10 秒维持唤醒范围/视线；攻击用范围不会一概强开|远程先手可以使怪接敌；不能说所有普通怪仍完全只靠原始距离唤醒|
|BossWake.cs|通过原生事件叫醒并保护出场；登场 FSM 会被跟踪|睡眠/出场/无敌段与“不会攻击”分开|
|BossBalance.StunCounts|Boss 硬直命中计数最短间隔 0.25 秒；空洞难度禁止所有 Boss 的此类硬直计数，精英不走 Boss 硬直计数|高攻速武器不代表每次命中都能积一次硬直；具体硬直仍由原 FSM 决定|
|BossPhases.cs|识别假骑士、灵魂大师、螳螂领主、守望者、奥罗马托的多身体/循环，分配生命并在适当阶段结算治疗|仅服务血量/阶段奖励识别，绝不是全 Boss 选招和低血阶段总表|
|World/EnemyBlockers.cs|内部敌人专用障碍以及无缝相邻已连接门的相关障碍可关闭|原版的房间内活动范围可能改变，不能只凭原版路径描述推断现版不会追过通道|
|World/EnemyUnstick.cs|普通移动怪卡实心地形后移到附近可站位置；Boss、钻地/附壁等有例外|可见位移可能是脱卡，并非怪物新增传送招式|
|Net/EnemyTargets.cs|权威端按距离和仇恨选择玩家，Boss 通常至少保持目标 1.5 秒（目标消失例外），FSM 使用代理目标|联机弹幕可能指向另一名玩家；切目标间隔不等于每招固定锁向时刻|
|Net/EnemyAttacks.cs / EnemyParts.cs|同步敌人攻击判定和部件，远端使用权威状态|有命中判定/字符串不证明双方视觉、弹幕寿命、阶段交接均正确|

注：以上读取的是当前工作区；不包含安装或发布声明。工作区已有其他任务的改动，本轮只新增审计脚本、数据和文档。

## 本轮直接解码的具体发现

|对象/路径|原生字段与转移|能够确认的含义|
|---|---|---|
|一战大黄蜂 / Throw Antic→Throw|GetAngleToTarget2D 在前摇采样 Hero Obj，everyFrame=false；SetVelocityAsAngle 给 Needle 写 Angle 与 Throw Speed（资源初值 38），everyFrame=false|此释放动作是一回写入，不能描述成每帧持续瞄准。38 是原生单位的变量初值，未声明实战始终等于38|
|同场 Needle / Out→Decel→Return→Notify|Out 等待约0.3；Decel 减速并检查 Speed；Return 的 iTweenMoveTo 指向 Return Vector；Notify 发 NEEDLE RETURN|去程、减速、返回和通知构成独立弹体 FSM；不只是本体播放一次投掷动画。Return Vector 来源与运行时变化要继续沿写入追踪|
|二战大黄蜂 / Escalation|读取 HP 后与常量480比较，greaterThan=FINISHED；其余分支和状态结束需结合后续转移|存在原生低血增强检查，不能将其刺球安排说成任意时刻完全等概率；480不能直接冒充泰拉显示血量或统一百分比|
|纯粹容器 / Set Phase HP→Phase?|启用动作以 HP/3 写 Quarter HP，再×2写 Half HP；另一个旧算式动作已禁用。IntOperator 的 operation=3/2 分别为 Divide/Multiply，已核对本机 Assembly-CSharp|实际比较点是约2/3与1/3，不是变量字面上的1/2和1/4；整数除法有取整。原生字段名不能当机制结论|
|泽若 / Sword Summon / Init→Check|Half HP 由 HP/2 算出；Check 的 equal/lessThan 事件均为 SUMMON|是实际半血比较，和纯粹容器那个名为 Half HP 的变量不是同一规则|
|马科斯 / Rage Check / Check|HP 与 Rage HP 比较，equal/lessThan 发 RAGE|存在独立狂暴检查 FSM；只查本体 Control 或 BossPhases 会漏掉此触发链|

这些是特定本机场景的静态字段/动作结论，不替代所有世界/神居档位及当前 Mod 实战。动作的 enabled 标记也不代表所属 FSM/物体当时已启用或该状态一定可达。完整字段入口：[大黄蜂一战](../artifacts/enemy-attack-audit/actions/448-GG_Hornet_1.json)、其他场景可按 [动作索引](../artifacts/enemy-attack-audit/action-index.json) 查找。\n
## 最容易误判的阶段和弹幕

|情况|应该如何记录|
|---|---|
|守望者骑士分批起身|多体接替；代码里的四批血量账不等于四次新增招式|
|奥波路波少一只|同伴死亡触发幸存者狂暴，不是固定半血|
|刻尘孵化者产满五只|次数触发改俯冲，不是 hp 阈值|
|吸虫怪死亡后上下半|死亡分裂后还有两种攻击体，不能只记“死了”|
|大水母核心、尸爆、刺尸、晶簇、火迹|均需记录击杀/发射之后的危险，不只统计怪物活体动作|
|气球、蜂群、罐中怪、飞吸虫|独立敌人/召唤物，与普通弹丸区分；受伤规则与清理条件单查|
|灵魂大师假死|第二身体战斗是真阶段，不是一次硬直|
|暴怒守卫、梦境三重战|独立重战变体，不是基础 Boss 打到一半变身|
|马科斯无地板、收藏家高阶怪池、乌姆破壳|场景/神居档位差异，不能泛化到所有版本|
|失落近亲|不能把残破容器的感染瀑流当成其既有攻击|
|空洞骑士衰弱与大黄蜂介入|剧情/结局事件链，不是简单统一百分比的阶段表|
|格林河豚|血量节点与鞠躬受击惩罚两类触发分开|
|斯莱末段、纯粹容器低血增招、辐光多段|即使没出现在 BossPhases.Of，也不代表没有招式阶段|

## 全量目录

每项“行为/弹幕/阶段”属于研究层；“原生场景”提供原始线索，“已解码动作”提供场景内全部状态/字段，具体同对象归属见另一份逐项证据。默认未发现阶段不等于已证明没有阶段。社区链接为复核入口，并非本轮已逐页重新访问。

''']
    group = None
    for i, e in enumerate(entries, 1):
        if group != e['group']:
            group = e['group']
            text.append(f"### {group}\n\n")
        text.append(f"#### {i:03d} {e['zh']} · {e['key']}\n\n分类：{e['classification']}；代表场景：`{e['scene']}`" + ('；补充项，不代表已有独立掉落键。' if e['extra'] else f"；名册种类：`{e['kind']}`。") + '\n\n')
        text.append(f"- **攻击与移动**：{e['behavior']}\n- **弹幕、投掷与产物**：{e['projectiles']}\n- **阶段、形态与死亡后**：{e['phases']}\n")
        evidence = f"[场景资源证据](../artifacts/enemy-attack-audit/{e['nativeSceneFile']})" if e['nativeSceneFile'] else e['nativeEvidence']
        if e['nativeActionFile']:
            evidence += f"；[已解码动作](../artifacts/enemy-attack-audit/{e['nativeActionFile']})"
        text.append(f"- **证据**：行为研究待原生逐动作复核；[社区资料]({e['source']})；{evidence}；本轮无实战通过记录。\n\n")
    text.append('''## 场景变体必须分开复核

|记录族|独立检查路径|
|---|---|
|BigBuzzer|苍绿野外、斗兽场、神居单/双王；分别查召唤是否启用与波数|
|MageKnight|圣所首战、其他带愚蠢版本、斗兽场/神居；不可全局开启同一召唤描述|
|FalseKnight|野外假骑士、梦境失败冠军、神居对应重战；查最后露头与地板流程|
|MageLord|普通灵魂大师、灵魂暴君、神居；核两身体衔接及四/六球|
|MantisLord|普通单→双、战斗姐妹单→三；按场景分支统计身体|
|MegaBeamMiner|一战与上层暴怒守卫；代表名册不应掩盖重战|
|MegaJellyfish|教师档案馆奎若破壳、神居欧玛核心破壳|
|MimicSpider|地面诺斯克与翼之诺斯克，两套移动/召唤路径|
|InfectedKnight|残破容器与失落近亲；四/六液以及感染瀑流差异|
|BeeHatchling|地图小蜜蜂与蜂巢骑士召唤蜂；骨钉/法术可杀性不同|
|JarCollector|世界与神居高阶罐内怪池；不只比较 Boss 本体|
|GhostNoEyes / GhostMarkoth|普通与高阶神居的地形/平台差异|
|FinalBoss|普通辐光与无上辐光；终段流程必须独立|
|Zote / 无尽折磨个体|剧情零伤害与挑战接触伤害不能混用|
|Shade / Sibling|虚空之心、玩家已学法术、回收与领地条件|
|ZombieMiner@0|场景/阶段映射不等于新增招式；对照实际实例和基础记录|

## 尚未取得的证据及后续验收入口

1. **逐招式有效参数与可达路径复核**：状态、转移与常见动作参数已经解码；还需沿变量写入、对象层级、prefab/全局池引用和跨 FSM 事件，确定每招最终生效的生成位置、弹数、速度/角度、追踪、碰撞、寿命、延时和进入条件。未展开字段保留原始 ActionData，不能默认零值。
2. **覆盖组件脚本和运行时生成物**：场景组件统计不等于已追完全部 C# 行为脚本、共享 prefab 和运行时实例；字符串相似不能当成同一个怪物的招式。
3. **单机验证**：每个攻击至少观察完整预警→释放→碰撞/消退，每个阶段触发一次；记录角色能力、难度、神居档位、战次、场景、对象、原生 hp、显示 hp、事件序列与视频。未观察到的技能继续记未覆盖，不能记不存在。
4. **干预路径**：近身、远距、高处、背后、挡墙、受击、反弹、法术击杀、瞬杀越阶段、硬直、出场、死亡后危险、离开重进都要检查。用独立存档；不要以固定靶 DPS 或 build 成功代替 Boss AI 验证。
5. **联机验证**：两名玩家分别成为目标、两端都能看见弹幕并正确受伤，覆盖同时转阶段、掉线/重连和危险清理；需两端完整证据。本轮无此验收结果。

本轮发现的是旧设计与当前分类不一致、文档阶段表容易被误读、已有测试不能证明敌人全招式覆盖；没有凭静态猜测修改战斗 AI。

## 机器可核对的文件

- [覆盖、逐项注释与当前源文件哈希](../artifacts/enemy-attack-audit/coverage.json)
- [全部场景索引、资源哈希和扫描错误](../artifacts/enemy-attack-audit/native-index.json)
- [完整 FSM 解码索引与错误数](../artifacts/enemy-attack-audit/decoded-index.json)
- [动作参数索引和未展开计数](../artifacts/enemy-attack-audit/action-index.json)
- [共享预制体/资源解码索引](../artifacts/enemy-attack-audit/shared-index.json)、[本机原生程序集哈希](../artifacts/enemy-attack-audit/assembly-hashes.json)
- [逐怪物原生 FSM 证据](全怪物原生FSM逐项证据-2026-10-21.md)
- [同对象组件归属与未匹配清单](../artifacts/enemy-attack-audit/owner-links.json)
- [只读资源扫描器](../tools/audit_enemy_attack_assets.py)
- [报告生成器](../tools/build_enemy_attack_audit.py)
- [序列化字段布局读取器](../tools/EnemyAttackSchema.cs)、[FSM 解码器](../tools/decode_enemy_fsm.py)、[动作参数解码器](../tools/summarize_enemy_fsm.py)、[原生组件映射](../tools/map_enemy_attack_owners.py)、[逐项证据连接器](../tools/link_enemy_attack_evidence.py)
- [共享资源扫描器](../tools/audit_enemy_shared_assets.py)
- [原有设计稿](../空洞骑士全敌人AI升级设计.md)：仅复用原版研究栏，没有复用新版建议。

资源扫描只读游戏安装目录，不加载游戏进程，也不读写存档。重新运行扫描后再运行报告生成器，可检查名册/注释漂移；新增键未分配弹幕注释时会失败，避免默认为“无弹幕”。
''')
    REPORT.write_text(''.join(text), encoding='utf-8')
    print(json.dumps({k: v for k, v in result.items() if k not in ('entries', 'sourceSha256')}, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
