"""Generate the runtime catalog from the reviewed quest-fish design, retaining its Chinese text."""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TIPS = [
    'The orange light in its belly feels warm.', 'Poke it and it spits out a swarm of smaller fish.', 'Its hollow shell rattles when it swims.',
    'Lose it in the grass and you will never find it.', 'It smells like a lake after rain.', 'It swims fast, then pulls itself back with a silk thread.',
    'Do not breathe deeply.', 'Keep it away from your fishing line.', 'All it says is "Wuhb".',
    'Whatever you do, do not squeeze.', 'It feels tingly.', 'It has read more books than you.',
    'It has never stopped crying.', 'When it dances, the whole pond shakes.', 'You can see it shining through the fish basket.',
    'Its smell has... character.', 'Once it latches on, it never lets go.', 'Holding it makes acid seem a little less painful.',
    'It flies away just as you fall asleep.', 'The inscription reads: "It was a good fish."', 'Hit it with the Dream Nail and it says "Go away".',
    'It is fishing too.', 'It looks like someone you know.', 'More legs than fins.',
    'Watch the thorns.', 'Flowers bloom wherever it swims.', 'It is always looking for someone.',
    'It is snowing. No, that is ash.', 'Holding it makes your heartbeat stronger.', 'It believes it is the strongest fish alive.',
    'While you watch it, it watches you.', 'Lost fish follow its light.', 'It seems to be looking for its siblings.',
    'It never stops praying.', 'It stays so still, it could be carved from stone.', 'Looking straight at it makes you dizzy.',
]
CHAT = [
    "The water in the Crossroads is acting funny! There is a fish covered in orange bubbles, grinning at me! I am not scared... I just want it in a jar beside my bed. Catch it! And do not touch the bubbles. I have no idea what they do!",
    "I saw a round fish with wings buzzing out of the lake and falling back in! I bet it has lots of little fish inside. Bring it here! I want to see what happens when I poke it!",
    "There is a fish wearing an empty bug shell, wobbling around just like those husks! I want to pull the shell off and see what is inside... Probably nothing. Go!",
    "A fish in that green place disguised itself as moss! I watched it all afternoon and it never moved... Then I fell asleep and it stole my bait! Catch it! I want revenge!",
    "Deep in Greenpath there is a lake with a huge sleeping slug god! Its children look like soft little blue fish... I want one! Will the god get angry? That is your problem!",
    "Someone in a red cloak keeps jumping around Greenpath and stabbing my bobber! There is a fish shaped just like her needle, all long and pointy. Catch it so I can poke back!",
    "The fish in the Wastes grow mushrooms on their heads! Touch one and it blasts out spores. I sneezed all day! Catch one! I have someone very annoying to fumigate!",
    "Those mantises are fierce! The acid below their village has a fish with scythe fins. It snipped my line in one bite! I want it to cut my hair... Just kidding. Probably.",
    "A fish with a mushroom on its head keeps going 'Wuhb, wuhb' and running off to another pond! I chased it through three ponds... Your turn!",
    "There is a see-through fish in the fog with an orange core! I poked it with a stick and BOOM! My eyebrows still have not grown back. You catch it. I will watch from far away.",
    "I saw an electric eel in the canyon zap a glowing bug! I want to wire it onto my roof as a lamp... You will catch it for me, right?",
    "The canyon has a big building full of books, and people say its fish can read! I do not believe it... But if they can, one can do my homework. Go fishing!",
    "It always rains in that city, and a little fish there looks just like a tear! I wonder what a crying fish looks like... I already know you look awful when you cry. Go!",
    "Those fat city nobles dance all day, and the fish in their ponds are just as fat! Bring me the fattest one! I want to watch it spin around my fish tank!",
    "The Soul Sanctum's scholars put souls inside fish for experiments! One glows and vanishes in a flash. Catch it! I want to see if it can flash straight into my pocket.",
    "There is a knight rolling dung balls in the Waterways, and all his fish have rolled into balls too! They STINK! I want one for somebody's pillow. Do not ask. Just go!",
    "There is a flat thing in the pipes that sticks to your face! I want to stick one on somebody's door... Your door, actually. Just kidding! Go catch it!",
    "Deep in the Waterways is a grove with sparkling green acid! They say a fish there came from a lady's tears... How romantic. Yuck. Bring it here!",
    "An old lady with wings in the graveyard says fish can fly in dreams! I do not want a dream. I want a real one! Catch me a flying fish!",
    "I once caught a swimming tombstone in the Resting Grounds! I was so scared I threw away my rod! Now I think it was just a fish... Catch it again for me. I will keep the rod this time.",
    "They say the Dream Nail lets you hear a fish's thoughts. I tried it... It said 'Go away.' I want a more polite one! Off you go!",
    "Deepnest is pitch black, but a fish there carries a little lamp! It is fishing too! I want a contest... Actually, you compete with it, then bring it back to me.",
    "I saw another you by the water in Deepnest! Terrifying... Then it opened its mouth and showed rows of teeth! A fish that changes shape! Catch it, and do not get fooled!",
    "There is an eel like a centipede down there, with more legs than I have hooks! I lost count after thirty... Count them for me, and bring it back while you are there.",
    "The Gardens have thorny vines everywhere, even on the fish! I got pricked so much my hands are still swollen. You catch one. Wear gloves... Or do not, if you have none.",
    "A white lady lives deep in the Gardens, and her fish look like flower petals! I want to give one to... Nobody! I just want it! Hurry up!",
    "A little round thing rolls around the Gardens muttering 'Where are you...' A fish in the water looks just like it! Catch it! I want it rolling across my floor!",
    "Ash keeps falling at Kingdom's Edge, all over my head! Even the fish are dusty grey! Bring one here so I can wash it and see its real color.",
    "There is a hive near the Edge with blue blood that supposedly helps you last longer! A fish has been soaking in it... Catch it! I want to make juice from its blood!",
    "A fish kept shouting 'I am the strongest!' and then a tiny shrimp knocked it out! I think we will be great friends... Bring it here!",
    "At the very bottom of the world is a black lake with black fish and just two eyes! They give me nightmares... So I want one under my bed. Then I will not be scared. Makes sense, right?",
    "There is a lighthouse beside the Abyss lake, and a fish below it makes its own light! Is it guiding other fish? I want it to guide me... Where? No idea!",
    "The Abyss has tiny black shadows, and a fish trails black smoke just like them! You are from Terraria. Surely you have seen stranger fish? Bring this one here anyway. I want to name it!",
    "Above the clouds there is a place full of people in gold masks, praying all day! Even their pond fish have halos! Catch one! I want to see if it still shines at night!",
    "A fish in Godhome's pond sat so still I thought it was a statue! Then it swam away when I turned around! I want it outside my house beside the real statues!",
    "A fish in Godhome shines so brightly it dazzles my eyes! Someone said it is a child of light... Whatever! Catch it for my nightlight!",
]


def main():
    text = (ROOT / 'docs/任务鱼列表-2026-10-24.md').read_text(encoding='utf-8')
    sections = re.split(r'^### (.+?)（阶段 (\d)，.*?）\s*$', text.split('## 鱼表')[1], flags=re.M)
    entries = []
    for i in range(1, len(sections), 3):
        area, stage, body = sections[i:i+3]
        for n, name, en, tip in re.findall(r'^\| Q(\d+) \| (.*?) \| (.*?) \| (.*?) \|$', body, re.M):
            chat = re.search(r'^- \*\*Q'+n+r'\*\*：(.*)$', body, re.M).group(1)
            entries.append((int(n), area, int(stage), name, en, tip, chat))
    assert len(entries) == len(TIPS) == len(CHAT) == 36
    q = lambda s: json.dumps(s, ensure_ascii=False)
    lines = []
    for n, area, stage, name, en, tip, chat in entries:
        lines.append(f'            new Entry(ItemType.HkQuestFish{n:02}, {stage}, {q(area)}, {q(name)}, {q(en)}, {q(tip)}, {q(TIPS[n-1])}, {q(chat)}, {q(CHAT[n-1])}),')
    code = '''// Generated by tools/build_questfish_catalog.py from docs/任务鱼列表-2026-10-24.md.
using System.Collections.Generic;

namespace TerraKnight.Items
{
    internal static class QuestFishCatalog
    {
        internal sealed class Entry
        {
            public readonly ItemType Fish;
            public readonly int Stage;
            public readonly string Area, Name, EnglishName, Tip, EnglishTip, Chat, EnglishChat;
            public Entry(ItemType fish, int stage, string area, string name, string englishName, string tip, string englishTip, string chat, string englishChat)
            { Fish = fish; Stage = stage; Area = area; Name = name; EnglishName = englishName; Tip = tip; EnglishTip = englishTip; Chat = chat; EnglishChat = englishChat; }
        }

        internal static readonly Entry[] All =
        {
ROWS
        };

        internal static Entry Find(ItemType fish)
        {
            foreach (var entry in All) if (entry.Fish == fish) return entry;
            return null;
        }

        static string Text(string zh, string en) => Lang.Culture.StartsWith("zh") ? zh : (Lang.Lookup(zh, Lang.Culture) ?? en);

        internal static bool Localize(ItemDef item)
        {
            var entry = Find(item.Type);
            if (entry == null) return false;
            item.Name = Text(entry.Name, entry.EnglishName);
            item.Tooltip = Text(entry.Tip, entry.EnglishTip) + "\\n" + Fishing.QuestTooltip(item.Type);
            return true;
        }

        internal static string Dialogue(ItemType fish)
        {
            var entry = Find(fish);
            return entry == null ? null : Text(entry.Chat, entry.EnglishChat);
        }

        internal static void AddItems(List<ItemDef> list)
        {
            for (int i = 0; i < All.Length; i++)
            {
                var entry = All[i];
                list.Add(new ItemDef { Type = entry.Fish, Name = entry.Name, Tooltip = entry.Tip,
                    Texture = "QuestFish_Q" + (i + 1).ToString("D2"), QuestFish = true, MaxStack = 1,
                    Rare = -11, Stage = entry.Stage, Source = ItemSource.Drop, Value = 0, Price = 0, UseSound = null });
            }
        }
    }
}
'''.replace('ROWS', '\n'.join(lines))
    (ROOT / 'src/TerraKnight/Items/QuestFishCatalog.cs').write_text(code, encoding='utf-8')
    print(f'Generated {len(entries)} quest fish with Chinese and English dialogue')


if __name__ == '__main__':
    main()
