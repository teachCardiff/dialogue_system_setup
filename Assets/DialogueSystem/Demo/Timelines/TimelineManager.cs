using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class TimelineManager : MonoBehaviour
{
    public void PlayTimeline(PlayableDirector timeline)
    {
        timeline.Play();
    }
}
