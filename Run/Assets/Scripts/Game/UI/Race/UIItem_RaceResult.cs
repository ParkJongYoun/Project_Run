using UnityEngine;

namespace ProjectRun.Race
{
    public struct Data_RaceResultRow
    {
        public Data_RunnerSetup Runner;
        public int Place;
        public double Time, Gap;
        public bool Selected;
    }

    public sealed class UIItem_RaceResult : UIItem_Base<Data_RaceResultRow>
    {
        public UnityEngine.UI.Image Portrait, Background;
        public UnityEngine.UI.Text RankLabel, NameLabel, MountLabel, TimeLabel, GapLabel;
        public GameObject MyBadge;

        public override void Bind(Data_RaceResultRow data)
        {
            gameObject.SetActive(true);
            Portrait.sprite = data.Runner.FacePortrait != null ? data.Runner.FacePortrait : data.Runner.Portrait;
            RankLabel.text = data.Place.ToString();
            NameLabel.text = data.Runner.Name;
            MountLabel.text = data.Runner.Mount;
            TimeLabel.text = data.Time.ToString("0.000") + " s";
            GapLabel.text = data.Place == 1 ? "WINNER" : "+" + data.Gap.ToString("0.000") + " s";
            Background.color = data.Selected ? new Color(1, .91f, .68f) : data.Place == 1 ? new Color(1, .95f, .8f) : new Color(.97f, .97f, .94f);
            MyBadge.SetActive(data.Selected);
        }
    }
}
