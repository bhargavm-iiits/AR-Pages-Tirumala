using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace AlipiriAR.Positioning
{
    [Serializable]
    internal class SessionJournalDto
    {
        public double s;
        public double sigma;
        public double baroBias;
        public string confidence;
        public string savedAtUtc;
    }

    /// <summary>
    /// Persists {s, baroBias, confidence, utc} to Application.persistentDataPath — Docs/
    /// update1.md §04 Phase 4 item 3. Writes go to a temp file and are swapped in, so a crash
    /// mid-write leaves the previous good save intact. This class only persists and loads the
    /// data; it deliberately does not build the "Resume from ~4.2 km?" confirmation UI itself —
    /// that belongs in whichever screen owns app-launch flow, which should call TryLoad and
    /// decide what to show. Wire NavigationSession's own periodic Save calls (every ~30 s and on
    /// Pause/End) are the write side; nothing calls TryLoad yet.
    /// </summary>
    public class SessionJournal
    {
        private const string FileName = "session_journal.json";
        private readonly string _path;

        public SessionJournal()
        {
            _path = Path.Combine(Application.persistentDataPath, FileName);
        }

        public void Save(double s, double sigmaMeters, double baroBiasMeters, NavigationConfidence confidence)
        {
            var dto = new SessionJournalDto
            {
                s = s,
                sigma = sigmaMeters,
                baroBias = baroBiasMeters,
                confidence = confidence.ToString(),
                savedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            };

            string json = JsonUtility.ToJson(dto);
            string tmpPath = _path + ".tmp";
            File.WriteAllText(tmpPath, json);

            if (File.Exists(_path)) File.Delete(_path);
            File.Move(tmpPath, _path);
        }

        public bool TryLoad(out double s, out double sigmaMeters, out double baroBiasMeters, out NavigationConfidence confidence, out TimeSpan elapsedSinceSave)
        {
            s = 0.0;
            sigmaMeters = 50.0;
            baroBiasMeters = 0.0;
            confidence = NavigationConfidence.Booting;
            elapsedSinceSave = TimeSpan.Zero;

            if (!File.Exists(_path)) return false;

            SessionJournalDto dto;
            try
            {
                dto = JsonUtility.FromJson<SessionJournalDto>(File.ReadAllText(_path));
            }
            catch (Exception)
            {
                return false;
            }

            if (dto == null) return false;
            if (!DateTime.TryParse(
                    dto.savedAtUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal, out var savedAtUtc))
            {
                return false;
            }

            s = dto.s;
            sigmaMeters = dto.sigma;
            baroBiasMeters = dto.baroBias;
            Enum.TryParse(dto.confidence, out confidence);
            elapsedSinceSave = DateTime.UtcNow - savedAtUtc;
            return true;
        }

        public void Clear()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }
    }
}
