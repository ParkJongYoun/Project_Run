using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using ProjectRun.Race.CameraRig;

namespace ProjectRun.Race.EditorTools
{
    [CustomEditor(typeof(RaceFinishOutro))]
    public sealed class RaceFinishOutroEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var outro = (RaceFinishOutro)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Shots 편집 후 'Build Timeline From Shots'를 누르면 타임라인 클립/바인딩이 다시 생성됩니다.\n" +
                                    "카메라를 RaceCameraZone에 연결하지 마세요.", MessageType.Info);
            if (GUILayout.Button("Add Follow Camera")) AddCamera(outro);
            if (GUILayout.Button("Build Timeline From Shots")) BuildTimeline(outro);
        }

        /// <summary>Duplicates the last shot camera (or creates a default one) and appends it to Shots.</summary>
        public static Unity.Cinemachine.CinemachineCamera AddCamera(RaceFinishOutro outro)
        {
            Undo.RecordObject(outro, "Add Finish Camera");
            Unity.Cinemachine.CinemachineCamera source = null;
            for (int i = outro.Shots.Count - 1; i >= 0 && source == null; i--)
                if (outro.Shots[i] != null) source = outro.Shots[i].Camera;

            GameObject go;
            if (source != null)
            {
                go = Object.Instantiate(source.gameObject, source.transform.parent);
                go.transform.position = source.transform.position;
            }
            else
            {
                go = new GameObject();
                go.transform.SetParent(outro.transform.parent, false);
                var cam = go.AddComponent<Unity.Cinemachine.CinemachineCamera>();
                cam.Lens.FieldOfView = 48f; cam.Lens.NearClipPlane = 0.1f; cam.Lens.FarClipPlane = 1500f;
                var follow = go.AddComponent<Unity.Cinemachine.CinemachineFollow>();
                follow.FollowOffset = new Vector3(0f, 3f, -8f);
                follow.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.LockToTargetWithWorldUp;
                follow.TrackerSettings.PositionDamping = new Vector3(0.25f, 0.25f, 0.25f);
                go.AddComponent<Unity.Cinemachine.CinemachineRotationComposer>();
            }
            go.name = "CM_Finish_" + (outro.Shots.Count + 1).ToString("00");
            var newCam = go.GetComponent<Unity.Cinemachine.CinemachineCamera>();
            newCam.Priority = 0;
            // Default edit-time target (avoids "missing tracking target" warnings; replaced by the protagonist at runtime).
            if (newCam.Follow == null && outro.Rig != null && outro.Rig.FollowCamera != null)
            {
                newCam.Follow = outro.Rig.FollowCamera.Follow;
                newCam.LookAt = outro.Rig.FollowCamera.LookAt;
            }
            Undo.RegisterCreatedObjectUndo(go, "Add Finish Camera");
            outro.Shots.Add(new RaceFinishOutro.Shot { Camera = newCam });
            EditorUtility.SetDirty(outro);
            EditorSceneManager.MarkSceneDirty(outro.gameObject.scene);
            return newCam;
        }

        /// <summary>Rebuilds the Director's timeline (one Cinemachine track) from Shots and rebinds everything.</summary>
        public static void BuildTimeline(RaceFinishOutro outro)
        {
            var director = outro.Director;
            if (director == null) { Debug.LogError("RaceFinishOutro: Director is not assigned.", outro); return; }
            var timeline = director.playableAsset as TimelineAsset;
            if (timeline == null || !AssetDatabase.Contains(timeline))
            {
                Debug.LogError("RaceFinishOutro: Director needs a saved Timeline asset.", outro);
                return;
            }
            var brain = Object.FindFirstObjectByType<Unity.Cinemachine.CinemachineBrain>();

            Undo.RecordObject(director, "Build Finish Timeline");
            // Remove existing Cinemachine tracks (their clips/sub-assets go with them).
            var old = new List<TrackAsset>();
            foreach (var t in timeline.GetOutputTracks()) if (t is Unity.Cinemachine.CinemachineTrack) old.Add(t);
            foreach (var t in old) { director.ClearGenericBinding(t); timeline.DeleteTrack(t); }

            var track = timeline.CreateTrack<Unity.Cinemachine.CinemachineTrack>(null, "Finish Cameras");
            director.SetGenericBinding(track, brain);

            double time = 0;
            TimelineClip prev = null;
            foreach (var shot in outro.Shots)
            {
                if (shot == null || shot.Camera == null) continue;
                double blend = prev == null ? 0 : Mathf.Min(shot.BlendIn, (float)prev.duration);
                var clip = track.CreateClip<Unity.Cinemachine.CinemachineShot>();
                clip.displayName = shot.Camera.name;
                clip.start = time - blend;
                clip.duration = shot.Duration + blend;
                if (prev != null && blend > 0) { clip.blendInDuration = blend; prev.blendOutDuration = blend; }

                var asset = (Unity.Cinemachine.CinemachineShot)clip.asset;
                asset.VirtualCamera.exposedName = GUID.Generate().ToString();
                asset.VirtualCamera.defaultValue = null;
                director.SetReferenceValue(asset.VirtualCamera.exposedName, shot.Camera);
                EditorUtility.SetDirty(asset);

                time = clip.end;
                prev = clip;
            }

            EditorUtility.SetDirty(track);
            EditorUtility.SetDirty(timeline);
            EditorUtility.SetDirty(director);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(outro.gameObject.scene);
            UnityEditor.Timeline.TimelineEditor.Refresh(UnityEditor.Timeline.RefreshReason.ContentsAddedOrRemoved);
            Debug.Log("RaceFinishOutro: timeline rebuilt, duration " + timeline.duration.ToString("0.00") + "s.", outro);
        }
    }
}
