using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectRun.Race.Editor
{
    // Exclude only reviewed legacy roots for the three-scene game build.
    // References from retained Resources, scenes and preloaded assets always win.
    // The original paths/GUIDs are restored even if building fails.
    [InitializeOnLoad]
    internal sealed class DemoWebBuildResources : IDisposable
    {
        private const string resourceRoot = "Assets/Resources/";
        private const string stagingRoot = "Assets/WebBuildExcluded";
        private const string journalPath = DemoWebBuild.Records + "/리소스_복구목록.json";

        // Do not exclude new Resources by default. Extend this list after auditing runtime loads.
        private static readonly string[] legacyPaths = {
            "FoxImpostor/FoxDirectionalImpostor.asset",
            "FoxImpostor/FoxSpriteSet.asset",
            "FoxImpostor/Variants/FoxImpostor_12.asset",
            "FoxImpostor/Variants/FoxImpostor_16.asset",
            "FoxImpostor/Variants/FoxImpostor_24.asset",
            "FoxImpostor/Variants/FoxImpostor_40.asset",
            "FoxImpostor/Variants/FoxImpostor_72.asset",
            "Prefabs/Lobby/P_Lobby_Matyr.prefab",
            "Prefabs/Lobby/P_Lobby_Minorous.prefab",
            "Prefabs/Lobby/P_Lobby_WildRose.prefab",
            "Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_Alpaca.prefab",
            "Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_Baphomet.prefab",
            "Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_BaphometJr.prefab",
            "Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_BlueDragon.prefab",
            "Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_ButlerWhisper.prefab",
            "Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_Deviruchi.prefab",
            "Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_Edgar.prefab",
            "Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_FlameBird.prefab",
            "Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_Fox.prefab",
            "Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_GiantPoring.prefab",
            "Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_Horse.prefab",
            "Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_Lunatic.prefab",
            "Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_Matyr.prefab",
            "Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_MatyrIndependent.prefab",
            "Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_Minorous.prefab",
            "Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_Orc.prefab",
            "Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_Pecopeco.prefab",
            "Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_Poring.prefab",
            "Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_PorterPuppy.prefab",
            "Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_Savage.prefab",
            "Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_Smokie.prefab",
            "Prefabs/Race/CartoonRaceLineup/P_Runner_CartoonRace_FlameBird.prefab",
            "Prefabs/Race/CartoonRaceLineup/P_Runner_CartoonRace_Fox.prefab",
            "Prefabs/Race/CartoonRaceLineup/P_Runner_CartoonRace_Horse.prefab",
            "Prefabs/Race/CartoonRaceLineup/P_Runner_CartoonRace_Savage.prefab",
            "Prefabs/Race/CartoonRaceLineup/P_Runner_CartoonRace_Smokie.prefab",
            "Prefabs/Race/IndependentMonsters/P_Runner_MatyrIndependent.prefab",
            "Prefabs/Race/P_Runner_01.prefab",
            "Prefabs/Race/P_Runner_02.prefab",
            "Prefabs/Race/P_Runner_03.prefab",
            "Prefabs/Race/P_Runner_04.prefab",
            "Prefabs/Race/P_Runner_05.prefab",
            "Prefabs/Race/P_Runner_06.prefab",
            "Prefabs/Race/Sprites2D/P_Runner_2D_Edgar.prefab",
            "Prefabs/Race/Sprites2D/P_Runner_2D_GiantPoring.prefab",
            "Prefabs/Race/Sprites2D/P_Runner_2D_Lunatic.prefab",
            "Prefabs/Race/Sprites2D/P_Runner_2D_Minorous.prefab",
            "Prefabs/Race/Sprites2D/P_Runner_2D_Savage.prefab",
            "Prefabs/Race/Sprites2D/P_Runner_2D_Smokie.prefab",
            "Prefabs/Ranch/P_Ranch_race_track.prefab",
            "Prefabs/Ranch/P_Ranch_waterfall.prefab",
            "Prefabs/Showcase/V_Cartoon_NewMinotaurus.prefab",
            "Prefabs/Showcase/V_NewMinotaurus.prefab",
            "Prefabs/UI/Panel/UIPanel_DemoLobby.prefab",
            "Prefabs/UI/Panel/UIPanel_Lobby.prefab",
            "Prefabs/UI/Panel/UIPanel_PlayMode.prefab",
            "Prefabs/UI/Panel/UIPanel_PlayModeReturn.prefab",
            "Prefabs/UI/Panel/UIPanel_SpriteRaceHUD.prefab",
            "Prefabs/UI/Panel/UIPanel_Test.prefab",
            "Prefabs/UI/Slot/UIItem_RaceResult.prefab",
            "Race2D/Course_Meadow.asset",
            "Race2D/Sprites/Run_Edgar.asset",
            "Race2D/Sprites/Run_GiantPoring.asset",
            "Race2D/Sprites/Run_Lunatic.asset",
            "Race2D/Sprites/Run_Minorous.asset",
            "Race2D/Sprites/Run_Savage.asset",
            "Race2D/Sprites/Run_Smokie.asset",
            "UI/Demo/Training/Eddga.asset",
            "UI/Demo/Training/Minorous.asset",
            "UI/Demo/Training/Poring.asset",
            "UI/Lobby/WildRose_Idle.asset",
            "UI/Lobby/WildRose_Idle_Contour_v03.asset",
        };

        // These are loaded by strings rather than serialized scene references.
        private static readonly string[] runtimePaths = {
            "Tables/Table_Demo.json", "Tables/Table_RaceSkills.json", "Tables/Data_CartoonRaceRoster.asset",
            "UI/Demo/Data_DemoArt.asset", "UI/Demo/Mat_DemoSkill.mat",
            "UI/Ranch/Data_RanchArt.asset", "UI/RanchCollection/Data_RanchCollectionArt.asset",
            "Prefabs/UI/Panel/UIPanel_DemoRace.prefab",
            "Prefabs/UI/Slot/UIItem_DemoRaceResult.prefab", "Prefabs/UI/Slot/UIItem_RaceProgress.prefab",
            "RaceSkillEffects/Data_RaceSkillEffects.asset",
            "Race2D/AutoSprite/Run_Minorous.asset", "Race2D/AutoSprite/Run_GiantPoring.asset",
            "Race2D/AutoSprite/Run_Smokie.asset", "Race2D/AutoSprite/Run_Savage.asset",
            "Race2D/AutoSprite/Run_Lunatic.asset", "Race2D/AutoSprite/Run_Edgar.asset"
        };

        [Serializable] private sealed class MoveRecord
        {
            public string Source;
            public string Destination;
            public string Guid;
            public string Original;
            public string OriginalMeta;
        }
        [Serializable] private sealed class MoveJournal
        {
            public MoveRecord[] Moves = Array.Empty<MoveRecord>();
        }

        static DemoWebBuildResources()
        {
            EditorApplication.delayCall += () => {
                if (!BuildPipeline.isBuildingPlayer && !EditorApplication.isCompiling && File.Exists(journalPath))
                {
                    try { Restore(); }
                    catch (Exception exception) { Debug.LogException(exception); }
                }
            };
        }

        public static string[] Preview(string[] scenes)
        {
            foreach (string path in runtimePaths)
                if (AssetDatabase.LoadMainAssetAtPath(resourceRoot + path) == null)
                    throw new InvalidOperationException("Required runtime resource is missing: " + path);

            var candidates = new HashSet<string>(legacyPaths.Select(p => resourceRoot + p)
                .Where(p => AssetDatabase.LoadMainAssetAtPath(p) != null), StringComparer.Ordinal);
            var retained = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.Contains("/Resources/") && !AssetDatabase.IsValidFolder(p) && !candidates.Contains(p));
            var preloaded = PlayerSettings.GetPreloadedAssets().Where(a => a != null).Select(AssetDatabase.GetAssetPath);
            var roots = retained.Concat(scenes).Concat(preloaded).Distinct().ToArray();
            var required = new HashSet<string>(AssetDatabase.GetDependencies(roots, true), StringComparer.Ordinal);
            return candidates.Where(p => !required.Contains(p)).OrderBy(p => p).ToArray();
        }

        public static DemoWebBuildResources Begin(string[] scenes)
        {
            Restore();
            string[] excluded = Preview(scenes);
            Directory.CreateDirectory(DemoWebBuild.Records);
            File.WriteAllLines(DemoWebBuild.Records + "/빌드제외_리소스.txt", excluded);
            File.WriteAllLines(DemoWebBuild.Records + "/동적로드_필수리소스.txt", runtimePaths);
            if (!AssetDatabase.IsValidFolder(stagingRoot))
                AssetDatabase.CreateFolder("Assets", "WebBuildExcluded");
            var journal = new MoveJournal {
                Moves = excluded.Select(path => new MoveRecord {
                    Source = path,
                    Guid = AssetDatabase.AssetPathToGUID(path),
                    Original = Convert.ToBase64String(File.ReadAllBytes(path)),
                    OriginalMeta = Convert.ToBase64String(File.ReadAllBytes(path + ".meta")),
                    Destination = stagingRoot + "/" + AssetDatabase.AssetPathToGUID(path) + "_" + Path.GetFileName(path)
                }).ToArray()
            };
            // Persist the full plan before the first move so interrupted builds are recoverable.
            File.WriteAllText(journalPath, JsonUtility.ToJson(journal, true));
            try
            {
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (var move in journal.Moves)
                    {
                        string error = AssetDatabase.MoveAsset(move.Source, move.Destination);
                        if (!string.IsNullOrEmpty(error)) throw new IOException(error);
                    }
                }
                finally { AssetDatabase.StopAssetEditing(); }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("WEB_BUILD_RESOURCES_EXCLUDED " + excluded.Length);
                return new DemoWebBuildResources();
            }
            catch { Restore(); throw; }
        }

        public void Dispose() => Restore();

        [MenuItem("Tools/Project Run/Demo/Restore build resources")]
        public static void Restore()
        {
            if (!File.Exists(journalPath)) return;
            var journal = JsonUtility.FromJson<MoveJournal>(File.ReadAllText(journalPath));
            if (journal == null || journal.Moves == null) throw new IOException("Invalid resource recovery journal.");
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var move in journal.Moves)
                {
                    // The journal never grants permission to move arbitrary project files.
                    if (string.IsNullOrEmpty(move.Source) ||
                        !move.Source.StartsWith(resourceRoot, StringComparison.Ordinal) ||
                        !legacyPaths.Contains(move.Source.Substring(resourceRoot.Length)) ||
                        move.Destination != stagingRoot + "/" + move.Guid + "_" + Path.GetFileName(move.Source))
                        throw new IOException("Unexpected resource recovery path.");
                    string current = AssetDatabase.GUIDToAssetPath(move.Guid);
                    if (current == move.Source) continue;
                    if (current != move.Destination) throw new IOException("Resource recovery GUID mismatch: " + move.Source);
                    string error = AssetDatabase.MoveAsset(move.Destination, move.Source);
                    if (!string.IsNullOrEmpty(error)) throw new IOException(error);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            // Unity may reserialize moved assets (including line endings). Restore the exact
            // bytes after the moves have been imported, preserving custom names and metadata.
            foreach (var move in journal.Moves)
            {
                if (!string.IsNullOrEmpty(move.Original))
                    File.WriteAllBytes(move.Source, Convert.FromBase64String(move.Original));
                if (!string.IsNullOrEmpty(move.OriginalMeta))
                    File.WriteAllBytes(move.Source + ".meta", Convert.FromBase64String(move.OriginalMeta));
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var move in journal.Moves)
            {
                if (AssetDatabase.GUIDToAssetPath(move.Guid) != move.Source)
                    throw new IOException("Resource was not restored: " + move.Source);
                if (!string.IsNullOrEmpty(move.Original) &&
                    Convert.ToBase64String(File.ReadAllBytes(move.Source)) != move.Original)
                    throw new IOException("Resource content was not restored: " + move.Source);
                if (!string.IsNullOrEmpty(move.OriginalMeta) &&
                    Convert.ToBase64String(File.ReadAllBytes(move.Source + ".meta")) != move.OriginalMeta)
                    throw new IOException("Resource metadata was not restored: " + move.Source);
            }
            File.Delete(journalPath);
            if (Directory.Exists(stagingRoot) && Directory.GetFileSystemEntries(stagingRoot).Length == 0)
                AssetDatabase.DeleteAsset(stagingRoot);
            Debug.Log("WEB_BUILD_RESOURCES_RESTORED " + journal.Moves.Length);
        }
    }
}

