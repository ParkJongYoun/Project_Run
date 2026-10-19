// 폰트 파일(TTF/OTF)을 TextMeshPro SDF 폰트 에셋으로 만들어 주는 에디터 스크립트
// - 메뉴: Tools > Fonts > Build SDF Font Assets  (수동 실행)
// - 폰트가 Assets/Fonts 에 있고 SDF 에셋이 아직 없으면 컴파일 직후 한 번 자동 생성
#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ProjectRun.EditorTools
{
    [InitializeOnLoad]
    public static class FontSDFBuilder
    {
        // 폰트가 들어있는 폴더와 SDF 에셋을 저장할 폴더
        private const string FontFolder = "Assets/Fonts";
        private const string SdfFolder = "Assets/Fonts/SDF";

        // 만들 폰트 목록: 원본 파일명 (확장자 포함)
        private static readonly string[] FontFiles =
        {
            "BlackHanSans-Regular.ttf",
            "Pretendard-Medium.otf",
        };

        // 에디터가 로드될 때 한 번 실행 (이미 있으면 아무것도 안 함)
        static FontSDFBuilder()
        {
            EditorApplication.delayCall += () => Build(false);
        }

        [MenuItem("Tools/Fonts/Build SDF Font Assets")]
        public static void BuildFromMenu() => Build(true);

        private static void Build(bool force)
        {
            if (!AssetDatabase.IsValidFolder(FontFolder)) return;
            if (!AssetDatabase.IsValidFolder(SdfFolder)) AssetDatabase.CreateFolder(FontFolder, "SDF");

            foreach (var file in FontFiles)
            {
                var fontPath = FontFolder + "/" + file;
                var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
                if (font == null) continue; // 폰트가 아직 임포트되지 않았으면 건너뜀

                var name = Path.GetFileNameWithoutExtension(file);
                var assetPath = SdfFolder + "/" + name + " SDF.asset";
                if (!force && AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null) continue;
                if (force && AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null)
                    AssetDatabase.DeleteAsset(assetPath); // 수동 실행 시에는 새로 다시 만듦

                // 폰트 에셋 생성
                // - 샘플링 크기 90, 패딩 9, SDFAA 방식
                // - Dynamic: 글자를 쓰는 순간 아틀라스에 추가 (한글 11,172자를 미리 굽지 않아도 됨)
                // - 아틀라스 1024x1024, 가득 차면 새 아틀라스를 자동으로 추가(멀티 아틀라스)
                var asset = TMP_FontAsset.CreateFontAsset(
                    font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                    AtlasPopulationMode.Dynamic, true);
                if (asset == null)
                {
                    Debug.LogError("SDF 폰트 에셋 생성 실패: " + fontPath);
                    continue;
                }

                asset.name = name + " SDF";
                AssetDatabase.CreateAsset(asset, assetPath);

                // 아틀라스 텍스처와 머티리얼을 에셋 안에 서브 에셋으로 저장 (Font Asset Creator와 동일한 구조)
                if (asset.material != null)
                {
                    asset.material.name = name + " SDF Material";
                    AssetDatabase.AddObjectToAsset(asset.material, asset);
                }
                if (asset.atlasTextures != null)
                {
                    for (int i = 0; i < asset.atlasTextures.Length; i++)
                    {
                        asset.atlasTextures[i].name = name + " SDF Atlas " + i;
                        AssetDatabase.AddObjectToAsset(asset.atlasTextures[i], asset);
                    }
                }

                EditorUtility.SetDirty(asset);
                Debug.Log("SDF 폰트 에셋 생성: " + assetPath);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
#endif
