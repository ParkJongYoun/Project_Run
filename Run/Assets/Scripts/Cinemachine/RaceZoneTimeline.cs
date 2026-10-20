using UnityEngine;
using UnityEngine.Playables;

namespace ProjectRun.Race.CameraRig
{
    /// <summary>
    /// Plays <see cref="Director"/> from 0 when <see cref="Zone"/> goes live, and stops it when the zone
    /// turns off or the race resets. Use for zone cameras animated on a Timeline (e.g. CM_Arc_Orbit).
    /// </summary>
    [DefaultExecutionOrder(120)]
    public sealed class RaceZoneTimeline : MonoBehaviour
    {
        [Tooltip("Zone whose live state drives the timeline.")]
        public RaceCameraZone Zone;
        [Tooltip("Director holding the camera animation. Play On Awake should be off.")]
        public PlayableDirector Director;

        private bool wasLive;

        private void OnEnable() { Mgr_Event.Reset += Stop; }

        private void OnDisable()
        {
            Mgr_Event.Reset -= Stop;
            Stop();
            wasLive = false;
        }

        private void LateUpdate()
        {
            bool live = Zone != null && Zone.IsLive;
            if (live && !wasLive && Director != null)
            {
                Director.time = 0;
                Director.Play();
                Director.Evaluate();
            }
            else if (!live && wasLive) Stop();
            wasLive = live;
        }

        private void Stop()
        {
            if (Director != null && Director.state == PlayState.Playing) Director.Stop();
        }
    }
}
