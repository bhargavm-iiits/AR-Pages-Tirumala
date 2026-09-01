using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace AlipiriAR.Core
{
    public enum ConnectivityStatus { Unknown, Online, Offline }

    /// <summary>
    /// Real online/offline detection, not just Application.internetReachability — that property
    /// only reports whether a radio is active, not whether it can actually reach anything, and
    /// reads "reachable" on plenty of networks with no real internet path (captive portal, cell
    /// data with no signal deep in a canopy). Runs a periodic HEAD probe with a 3 s timeout and
    /// applies hysteresis (2 consecutive successes to go Online, 3 consecutive failures to go
    /// Offline) so one flaky probe doesn't flap IsOnline back and forth (Docs/update1.md §02
    /// F-06, Phase 2 items 4-5). GoogleTileSession routes its session-creation attempts through
    /// this instead of always trying blind and eating a timeout on every dead-zone tile miss.
    /// </summary>
    public class ConnectivityService : MonoBehaviour
    {
        private const string ProbeUrl = "https://www.gstatic.com/generate_204";
        private const float ProbeIntervalSecondsOnline = 30f;
        private const float ProbeIntervalSecondsOffline = 10f;
        private const int ProbeTimeoutSeconds = 3;
        private const int SuccessesToGoOnline = 2;
        private const int FailuresToGoOffline = 3;

        private int _consecutiveSuccesses;
        private int _consecutiveFailures;

        public ConnectivityStatus Status { get; private set; } = ConnectivityStatus.Unknown;
        public bool IsOnline => Status == ConnectivityStatus.Online;

        public event Action<ConnectivityStatus> OnStatusChanged;

        public static ConnectivityService Create()
        {
            var go = new GameObject("~ConnectivityService");
            DontDestroyOnLoad(go);
            var service = go.AddComponent<ConnectivityService>();
            service.StartCoroutine(service.ProbeLoop());
            return service;
        }

        /// <summary>Cheap gate ahead of a real request, per Phase 2 item 4 — checks the radio
        /// state and the hysteresis-smoothed status without launching an extra probe. Callers
        /// with no ConnectivityService registered yet (e.g. very early in a cold start) should
        /// treat that as "attempt it" — see GoogleTileSession.EnsureSession.</summary>
        public bool ShouldAttemptNetwork() =>
            Application.internetReachability != NetworkReachability.NotReachable && Status != ConnectivityStatus.Offline;

        private IEnumerator ProbeLoop()
        {
            while (true)
            {
                if (Application.internetReachability == NetworkReachability.NotReachable)
                {
                    RecordFailure();
                }
                else
                {
                    yield return Probe();
                }

                float interval = Status == ConnectivityStatus.Online ? ProbeIntervalSecondsOnline : ProbeIntervalSecondsOffline;
                yield return new WaitForSecondsRealtime(interval);
            }
        }

        private IEnumerator Probe()
        {
            using var request = UnityWebRequest.Head(ProbeUrl);
            request.timeout = ProbeTimeoutSeconds;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success) RecordSuccess();
            else RecordFailure();
        }

        private void RecordSuccess()
        {
            _consecutiveFailures = 0;
            _consecutiveSuccesses++;
            if (Status != ConnectivityStatus.Online && _consecutiveSuccesses >= SuccessesToGoOnline)
                SetStatus(ConnectivityStatus.Online);
        }

        private void RecordFailure()
        {
            _consecutiveSuccesses = 0;
            _consecutiveFailures++;
            if (Status != ConnectivityStatus.Offline && _consecutiveFailures >= FailuresToGoOffline)
                SetStatus(ConnectivityStatus.Offline);
        }

        private void SetStatus(ConnectivityStatus status)
        {
            Status = status;
            OnStatusChanged?.Invoke(status);
        }
    }
}
