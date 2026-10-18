using UnityEngine;

namespace ProjectRun.Race
{
    public static class Mgr_Table
    {
        private static Table_Demo demo;
        public static Table_Demo Demo
        {
            get
            {
                if (demo == null)
                {
                    var source = Mgr_Bundle.Load<TextAsset>("Tables/Table_Demo");
                    if (source == null) throw new System.InvalidOperationException("Table_Demo is required.");
                    demo = JsonUtility.FromJson<Table_Demo>(source.text);
                    if (demo.Monsters == null || demo.Monsters.Length != 3)
                        throw new System.InvalidOperationException("Demo requires three monsters.");
                }
                return demo;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => demo = null;
    }
}
