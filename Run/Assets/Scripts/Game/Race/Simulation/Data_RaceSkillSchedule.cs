using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectRun.Race
{
    // Pre-shuffled race-time slots. No frame-time random calls or presentation dependencies.
    public sealed class Data_RaceSkillSchedule
    {
        private sealed class Slot
        {
            public int Runner;
            public E_RaceSkillRarity Rarity;
            public float Due;
            public bool Used;
        }
        public float Spacing { get; private set; } = 1.6f;
        private readonly List<Slot> slots = new List<Slot>();

        public Data_RaceSkillSchedule(Data_RunnerSetup[] setups,float length)
        {
            if(setups.Length==0)return;
            var random=new System.Random(setups[0].SkillScheduleSeed ^ 0x51a7);
            var decks=new List<E_RaceSkillRarity>[setups.Length];
            int waves=0;
            float fastest=1;
            for(int id=0;id<setups.Length;id++)
            {
                var skills=setups[id].RaceSkills;
                fastest=Mathf.Max(fastest,setups[id].BaseSpeed);
                decks[id]=new List<E_RaceSkillRarity>();
                if(skills==null)continue;
                int rares=random.Next(skills.RareCountMin,skills.RareCountMax+1);
                for(int n=0;n<skills.NormalCount;n++)decks[id].Add(E_RaceSkillRarity.Normal);
                for(int n=0;n<rares;n++)decks[id].Add(E_RaceSkillRarity.Rare);
                Shuffle(decks[id],random);
                waves=Math.Max(waves,decks[id].Count);
            }
            for(int wave=0;wave<waves;wave++)
            {
                var round=new List<Slot>();
                for(int id=0;id<setups.Length;id++)
                    if(wave<decks[id].Count)round.Add(new Slot{Runner=id,Rarity=decks[id][wave]});
                Shuffle(round,random);
                slots.AddRange(round);
            }
            float estimate=length/fastest;
            for(int i=0;i<slots.Count;i++)
                slots[i].Due=estimate*Mathf.Lerp(.055f,.70f,(i+.15f+(float)random.NextDouble()*.7f)/Math.Max(1,slots.Count));
        }

        public bool TryTake(double time,Data_RunnerState[] runners,out int id,out E_RaceSkillRarity rarity)
        {
            foreach(var slot in slots)
            {
                if(slot.Used||slot.Due>time)continue;
                var runner=runners[slot.Runner];
                if(runner.Finished){slot.Used=true;continue;}
                if(time-Mathf.Max(runner.NormalSkillStarted,runner.RareSkillStarted)<4)continue;
                slot.Used=true;id=slot.Runner;rarity=slot.Rarity;return true;
            }
            id=-1;rarity=E_RaceSkillRarity.Normal;return false;
        }

        private static void Shuffle<T>(List<T> list,System.Random random)
        {
            for(int i=list.Count-1;i>0;i--)
            {int j=random.Next(i+1);T value=list[i];list[i]=list[j];list[j]=value;}
        }
    }
}
