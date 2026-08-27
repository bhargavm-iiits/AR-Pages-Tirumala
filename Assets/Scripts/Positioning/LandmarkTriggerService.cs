using System;
using System.Collections.Generic;
using AlipiriAR.Data;
using AlipiriAR.Utilities;

namespace AlipiriAR.Positioning
{
    /// <summary>Fires once when the live fix enters a landmark's trigger radius (PLAN.md §02/§06)
    /// — dedupes per session so re-entering the same radius doesn't refire, and auto-marks the
    /// landmark visited through the same VisitedStore the Landmarks tab's manual ring uses, so
    /// automatic and manual visits never disagree.</summary>
    public class LandmarkTriggerService
    {
        private readonly IReadOnlyList<LandmarkData> _landmarks;
        private readonly VisitedStore _visitedStore;
        private readonly HashSet<int> _firedThisSession = new();

        public event Action<LandmarkData> OnArrived;

        public LandmarkTriggerService(IReadOnlyList<LandmarkData> landmarks, VisitedStore visitedStore)
        {
            _landmarks = landmarks;
            _visitedStore = visitedStore;
        }

        public void Feed(double lat, double lon)
        {
            foreach (var landmark in _landmarks)
            {
                if (_firedThisSession.Contains(landmark.Id)) continue;

                double distance = GeoMath.HaversineMeters(lat, lon, landmark.Latitude, landmark.Longitude);
                if (distance > landmark.TriggerRadiusMeters) continue;

                _firedThisSession.Add(landmark.Id);
                _visitedStore.SetVisited(landmark.Id, true);
                OnArrived?.Invoke(landmark);
            }
        }

        public void ResetSession() => _firedThisSession.Clear();
    }
}
