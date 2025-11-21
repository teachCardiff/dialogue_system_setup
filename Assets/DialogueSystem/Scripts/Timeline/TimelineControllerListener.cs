using UnityEngine;
using UnityEngine.Playables;

// Scene MonoBehaviour that listens to a TimelineEventChannel and controls a PlayableDirector.
// Attach to any GameObject in the scene, assign director & channel in Inspector.
public class TimelineControllerListener : MonoBehaviour
{
    [Header("References")]
    public PlayableDirector director;
    public TimelineEventChannel channel;

    [Header("Options")]
    public bool logEvents = false;

    private void OnEnable()
    {
        if (channel != null)
        {
            channel.PlayRequested += OnPlayRequested;
            channel.PauseRequested += OnPauseRequested;
            channel.StopRequested += OnStopRequested;
            channel.ResumeRequested += OnResumeRequested;
        }
    }

    private void OnDisable()
    {
        if (channel != null)
        {
            channel.PlayRequested -= OnPlayRequested;
            channel.PauseRequested -= OnPauseRequested;
            channel.StopRequested -= OnStopRequested;
            channel.ResumeRequested -= OnResumeRequested;
        }
    }

    private void OnPlayRequested()
    {
        if (logEvents) Debug.Log("TimelineControllerListener: PlayRequested");
        if (director == null) return;
        director.Play();
    }

    private void OnPauseRequested()
    {
        if (logEvents) Debug.Log("TimelineControllerListener: PauseRequested");
        if (director == null) return;
        director.Pause();
    }

    private void OnStopRequested()
    {
        if (logEvents) Debug.Log("TimelineControllerListener: StopRequested");
        if (director == null) return;
        director.Stop();
    }

    private void OnResumeRequested()
    {
        if (logEvents) Debug.Log("TimelineControllerListener: ResumeRequested");
        if (director == null) return;
        // Resume is Play when paused
        director.Play();
    }
}
