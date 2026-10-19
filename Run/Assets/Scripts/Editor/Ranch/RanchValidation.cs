using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using ProjectRun.Ranch;
using ProjectRun.Race;

namespace ProjectRun.Editor
{
    public static class RanchValidation
    {
        private static readonly List<string> checks = new List<string>();
        private const string reportFolder = "DataBundles/아트/목장_시설물_20261019/검증";
        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("Ranch verification failed: " + description);
            checks.Add("PASS " + description);
        }

        [MenuItem("Tools/Project Run/Ranch/Validate Grid and Assets")]
        public static string ValidateEditMode()
        {
            checks.Clear();
            var catalog=Resources.Load<Data_RanchCatalog>("Prefabs/Ranch/Data_RanchCatalog");
            var system=UnityEngine.Object.FindFirstObjectByType<Sys_RanchPlacement>();
            Check(system!=null && system.gameObject.scene.path==RanchSceneBuilder.ScenePath,"Correct scene");
            Check(catalog.Facilities.Length==11,"Eleven distinct facilities");
            Check(catalog.Facilities.Select(f=>f.Id).Distinct().Count()==11,"Unique catalog IDs");
            Check(catalog.Facilities.All(f=>f.Prefab!=null && f.Portrait!=null),"All model and portrait references assigned");
            Check(RanchGrid.Validate(catalog,system.InitialLayout,system.GridSize),"Default layout has no overlaps or out-of-bounds placements");
            var placed=new List<Data_RanchPlacement>{new Data_RanchPlacement{InstanceId="a",FacilityId="shelter",X=0,Y=0}};
            var candidate=new Data_RanchPlacement{InstanceId="b",FacilityId="feed_store",X=3,Y=0};
            Check(!RanchGrid.CanPlace(catalog,placed,candidate,system.GridSize,out _),"Overlapping footprint rejected");
            candidate.X=4;Check(RanchGrid.CanPlace(catalog,placed,candidate,system.GridSize,out _),"Touching edges allowed");
            candidate.X=-1;Check(!RanchGrid.CanPlace(catalog,placed,candidate,system.GridSize,out _),"Negative locked expansion rejected");
            candidate.X=system.GridSize.x;Check(!RanchGrid.CanPlace(catalog,placed,candidate,system.GridSize,out _),"Positive locked expansion rejected");
            candidate.X=18;candidate.FacilityId="shelter";candidate.Rotation=1;Check(!RanchGrid.CanPlace(catalog,placed,candidate,system.GridSize,out _),"Rotated footprint checks boundary");
            Check(catalog.Find("shelter").Size(1)==new Vector2Int(3,4),"Non-square footprint rotates correctly");
            for(int rotation=0;rotation<4;rotation++)foreach(var definition in catalog.Facilities)
            {
                candidate.FacilityId=definition.Id;candidate.Rotation=rotation;candidate.X=8;candidate.Y=6;
                Check(RanchGrid.CanPlace(catalog,placed,candidate,system.GridSize,out _),definition.Id+" valid at rotation "+rotation);
            }
            var save=JsonUtility.FromJson<Data_RanchSave>(JsonUtility.ToJson(system.InitialLayout));
            Check(RanchGrid.Validate(catalog,save,system.GridSize),"JSON layout round trip");
            save.Version=900;Check(!RanchGrid.Validate(catalog,save,system.GridSize),"Unsupported save version rejected");save.Version=1;
            save.Facilities.Add(save.Facilities[0].Copy());Check(!RanchGrid.Validate(catalog,save,system.GridSize),"Duplicate instance IDs rejected");
            var panel=UnityEngine.Object.FindFirstObjectByType<UIPanel_Ranch>();var scaler=panel.GetComponentInParent<UnityEngine.UI.CanvasScaler>();
            Check(scaler.referenceResolution==new Vector2(1280,720) && scaler.screenMatchMode==UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand,"1280x720 Expand canvas");
            Check(panel.SafeArea!=null,"Safe Area assigned");
            Check(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length==1,"Single event system");
            Check(panel.GetComponentsInChildren<UnityEngine.UI.Text>(true).All(t=>t.font!=null),"All labels have a Korean font");
            Check(catalog.Facilities.All(f=>f.Prefab.GetComponent<Collider>()!=null),"Placeable selection colliders assigned");
            Directory.CreateDirectory(reportFolder);File.WriteAllLines(reportFolder+"/그리드와_에셋_검증.txt",checks);
            return checks.Count+" checks passed";
        }

        public static string ValidatePlayMode()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Play mode is required");
            checks.Clear();
            var system=UnityEngine.Object.FindFirstObjectByType<Sys_RanchPlacement>();
            var panel=UnityEngine.Object.FindFirstObjectByType<UIPanel_Ranch>();
            string before=JsonUtility.ToJson(system.Layout);bool hadSave=PlayerPrefs.HasKey(Sys_RanchPlacement.SaveKey);
            string stored=PlayerPrefs.GetString(Sys_RanchPlacement.SaveKey,"");
            Action<string> click=name=>panel.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b=>b.name==name).onClick.Invoke();
            try
            {
                click("btn_build");Check(system.IsBuilding && panel.Drawer.gameObject.activeSelf,"Build menu callback opens grid and catalog");
                click("btn_facility_feed_store");Check(system.Preview!=null,"Facility card starts preview");
                system.MovePreview(12,3);Check(!system.CanConfirm && !panel.ConfirmButton.interactable,"Overlap disables confirm UI");
                int count=system.Layout.Facilities.Count;Check(!system.Confirm() && system.Layout.Facilities.Count==count,"Invalid confirm has no mutation");
                system.MovePreview(12,6);Check(system.CanConfirm,"Empty valid cells accepted");
                string instanceId=system.Preview.InstanceId;click("btn_confirm");Check(system.Layout.Facilities.Count==count+1,"Confirm button places one instance");
                Check(PlayerPrefs.GetString(Sys_RanchPlacement.SaveKey).Contains(instanceId),"Confirmed layout persisted");
                system.BeginMove(instanceId);system.MovePreview(0,0);click("btn_cancel");
                Check(system.Layout.Facilities.First(f=>f.InstanceId==instanceId).X==12,"Move cancel restores original position");
                system.BeginMove(instanceId);click("btn_remove");Check(system.Layout.Facilities.Count==count,"Remove callback restores count");
                click("btn_next");Check(panel.GetComponentsInChildren<UnityEngine.UI.Button>(true).Any(b=>b.name=="btn_facility_gate" && b.gameObject.activeInHierarchy),"Catalog second page usable");
                click("btn_monsters");Check(panel.Modal.gameObject.activeSelf && system.InputBlocked,"Monster panel blocks world input");
                Check(!panel.Navigation.gameObject.activeSelf,"Modal hides bottom navigation");
                click("btn_close");Check(!system.InputBlocked,"Closing modal restores input");
                click("btn_training");click("btn_place_training");Check(system.Preview.FacilityId=="training","Training menu places training ground");
                click("btn_rotate");Check(system.Preview.Rotation==1,"Rotate callback works");click("btn_cancel");
                click("btn_settings");click("btn_reset_layout");Check(panel.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name=="btn_keep"),"Reset requires explicit confirmation");
                click("btn_keep");click("btn_close");click("btn_home");
                Check(!system.IsBuilding && !panel.Modal.gameObject.activeSelf,"Home returns to uncluttered ranch");
                Check(JsonUtility.ToJson(system.Layout)==before,"Tests restored scene layout");
            }
            finally
            {
                system.Cancel();system.SetBuildMode(false);
                if(hadSave)PlayerPrefs.SetString(Sys_RanchPlacement.SaveKey,stored);else PlayerPrefs.DeleteKey(Sys_RanchPlacement.SaveKey);
                PlayerPrefs.Save();
            }
            Directory.CreateDirectory(reportFolder);File.WriteAllLines(reportFolder+"/플레이_기능검증.txt",checks);
            return checks.Count+" Play checks passed";
        }
    }
}
