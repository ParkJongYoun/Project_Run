using UnityEngine;
using UnityEngine.UI;

namespace ProjectRun.Race
{
    public sealed class UIItem_RaceSkillRibbon : MonoBehaviour
    {
        public Image Face;
        public Text Skill;
        public Text Runner;
        public Image Frame;
        public Image UniquePoint;
        public CanvasGroup Opacity;
        public float Age { get; private set; }
        public bool Active { get; private set; }
        public float Duration { get; private set; }
        public float MinimumHold { get; private set; }
        public const float RowHeight = 36;
        public const float RowSpacing = 40;
        private RectTransform rect;
        private float vertical;
        public void Show(Data_RunnerSetup entry,int id,E_RaceSkillRarity rarity,bool hero,int slot)
        {
            rect=(RectTransform)transform;Age=0;Active=true;
            Duration=rarity==E_RaceSkillRarity.Unique?4.3f:3.2f;
            MinimumHold=rarity==E_RaceSkillRarity.Unique?1.8f:1.2f;
            Color tint=rarity==E_RaceSkillRarity.Normal?new Color(.05f,.85f,1):
                rarity==E_RaceSkillRarity.Rare?new Color(.75f,.28f,1):new Color(1,.73f,.08f);
            rect.sizeDelta=new Vector2(WidthFor(rarity),RowHeight);
            Face.sprite=entry.FacePortrait??entry.Portrait;
            Face.preserveAspect=true;
            Skill.text=entry.RaceSkills.SkillName(rarity);
            Runner.text=(id+1)+"  "+entry.Name;
            Frame.color=tint;
            UniquePoint.color=tint;
            UniquePoint.gameObject.SetActive(rarity==E_RaceSkillRarity.Unique);
            Skill.color=rarity==E_RaceSkillRarity.Unique?new Color(1,.92f,.66f):Color.white;
            Runner.color=hero?new Color(.3f,.95f,1):new Color(.72f,.8f,.9f);
            vertical=-RowHeight*.5f-slot*RowSpacing;Opacity.alpha=0;gameObject.SetActive(true);
        }
        public static float WidthFor(E_RaceSkillRarity rarity)=>
            rarity==E_RaceSkillRarity.Unique?260:rarity==E_RaceSkillRarity.Rare?232:204;
        public void Tick(float delta,int depth)
        {
            if(!Active)return;
            Age+=delta;
            if(Age>=Duration){Clear();return;}
            float enter=Mathf.SmoothStep(0,1,Age/.24f);
            float leave=Mathf.SmoothStep(0,1,(Age-Duration+.3f)/.3f);
            vertical=Mathf.Lerp(vertical,-RowHeight*.5f-depth*RowSpacing,1-Mathf.Exp(-delta*14));
            rect.anchoredPosition=new Vector2((1-enter)*110+leave*28,vertical+leave*14);
            Opacity.alpha=enter*(1-leave);
        }
        public void RetireSoon(){if(Age>=MinimumHold)Duration=Mathf.Min(Duration,Age+.3f);}
        public void Clear(){Active=false;Age=0;if(Opacity!=null)Opacity.alpha=0;gameObject.SetActive(false);}
    }
}
