using System.Collections.Generic;
using AlipiriAR.Core;
using UnityEngine;

namespace AlipiriAR.Data
{
    /// <summary>Manual visited/unvisited state for landmarks (PLAN.md §06 — "useful when a
    /// trigger is missed under canopy"). Separate from the automatic GPS-trigger marking
    /// LandmarkTriggerService will do once Scene 5 exists; both write to the same store.</summary>
    public class VisitedStore
    {
        private const string PrefsKey = "visited.landmark_ids";

        private readonly HashSet<int> _visited = new();
        private bool _loaded;

        public bool IsVisited(int landmarkId)
        {
            EnsureLoaded();
            return _visited.Contains(landmarkId);
        }

        public void SetVisited(int landmarkId, bool visited)
        {
            EnsureLoaded();
            if (visited) _visited.Add(landmarkId);
            else _visited.Remove(landmarkId);
            Persist();
        }

        public int VisitedCount
        {
            get { EnsureLoaded(); return _visited.Count; }
        }

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            string raw = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrEmpty(raw)) return;

            foreach (var part in raw.Split(','))
            {
                if (int.TryParse(part, out int id)) _visited.Add(id);
            }
        }

        private void Persist()
        {
            PlayerPrefs.SetString(PrefsKey, string.Join(",", _visited));
            PlayerPrefs.Save();
        }

        public static VisitedStore Resolve()
        {
            if (!ServiceLocator.TryGet<VisitedStore>(out var store))
            {
                store = new VisitedStore();
                ServiceLocator.Register(store);
            }
            return store;
        }
    }
}
