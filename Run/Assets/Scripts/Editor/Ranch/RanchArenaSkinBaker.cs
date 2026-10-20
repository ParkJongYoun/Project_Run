using UnityEditor;
using UnityEngine;
using ProjectRun.Race;

namespace ProjectRun.Editor
{
    // 경기장 패널 프리팹(UIPanel_RanchArena)에 새 UI(경기장 수첩 / 출전할 친구)를 직접 구워 넣는다.
    // 실행(Play) 때 코드가 입히던 것과 같은 코드를 에디터에서 한 번 실행하고 프리팹으로 저장한다.
    public static class RanchArenaSkinBaker
    {
        private const string prefabPath = "Assets/Resources/Prefabs/UI/Panel/UIPanel_RanchArena.prefab";
        private const string ranchArtPath = "Assets/Resources/UI/Ranch/Data_RanchArt.asset";
        private const string collectionArtPath = "Assets/Resources/UI/RanchCollection/Data_RanchCollectionArt.asset";

        [MenuItem("Tools/Project Run/Ranch/Bake Arena Skin Into Prefab")]
        public static void Bake()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("경기장 스킨 굽기", "Play 모드를 끄고 다시 실행하세요.", "확인");
                return;
            }
            // 새 이미지가 임포트되어 art에 등록되어 있는지 확인한다
            var ranchArt = AssetDatabase.LoadAssetAtPath<Data_DemoArt>(ranchArtPath);
            var collectionArt = AssetDatabase.LoadAssetAtPath<Data_DemoArt>(collectionArtPath);
            if (ranchArt == null || ranchArt.Get("arena_notebook_frame") == null || ranchArt.Get("arena_entry_frame") == null)
            {
                EditorUtility.DisplayDialog("경기장 스킨 굽기", "새 이미지가 아직 등록되지 않았습니다.\nAssets > Reimport 후 다시 실행해 보세요.", "확인");
                return;
            }

            // 프리팹을 열어서(편집용 복사본) 수첩 / 출전할 친구 팝업을 새 모양으로 바꾸고 다시 저장한다
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var panel = root.GetComponent<UIPanel_RanchArena>();
                bool notebook = panel.ApplyNotebookSkin(ranchArt);
                bool entrants = panel.ApplyEntrantsSkin(ranchArt, collectionArt);
                if (notebook || entrants)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    AssetDatabase.SaveAssets();
                }
                Debug.Log("경기장 스킨 굽기 완료 - 경기장 수첩: " + (notebook ? "적용" : "이미 적용됨/건너뜀")
                          + ", 출전할 친구: " + (entrants ? "적용" : "이미 적용됨/건너뜀"));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
