using System.Collections;
using System.Collections.Generic;
using System.IO;
using AlipiriAR.Utilities;
using Newtonsoft.Json;
using UnityEngine;

namespace AlipiriAR.Profile
{
    /// <summary>
    /// Appends a login to logins.json (the authoritative, append-only record — a ZIP can't be
    /// patched row-by-row, so the .xlsx is always fully regenerated from this) and then rebuilds
    /// login.xlsx from the shipped template. Editor Play mode writes to the exact path requested
    /// (Assets/login.xlsx) so it can be watched live in Excel; on-device there is no such path —
    /// the APK is read-only at runtime — so it writes to persistentDataPath and Settings exposes
    /// an export via the Android share sheet. PLAN.md §12.
    /// </summary>
    public class ExcelLoginStore
    {
        private const string TemplateRelativePath = "Templates/login_template.xlsx";
        private const string LogFileName = "logins.json";
        private const string XlsxFileName = "login.xlsx";

        private readonly List<LoginEntry> _entries = new();
        private byte[] _templateBytes;

        public string XlsxPath { get; }
        public string LogPath { get; }

        public ExcelLoginStore()
        {
            LogPath = Path.Combine(Application.persistentDataPath, LogFileName);
#if UNITY_EDITOR
            XlsxPath = Path.Combine(Application.dataPath, XlsxFileName); // Assets/login.xlsx
#else
            XlsxPath = Path.Combine(Application.persistentDataPath, XlsxFileName);
#endif
        }

        public IEnumerator LoadExistingLog()
        {
            if (File.Exists(LogPath))
            {
                try
                {
                    string json = File.ReadAllText(LogPath);
                    var loaded = JsonConvert.DeserializeObject<List<LoginEntry>>(json);
                    if (loaded != null) _entries.AddRange(loaded);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[ExcelLoginStore] Could not read existing {LogPath}: {e.Message}");
                }
            }

            yield return StreamingAssetsLoader.LoadBytes(TemplateRelativePath, bytes => _templateBytes = bytes);
        }

        /// <summary>Appends one entry and regenerates login.xlsx. Never throws — a full disk,
        /// a locked file (Excel has it open) or a read-only folder must never block the user
        /// from continuing into the app (PLAN.md §12).</summary>
        public bool AppendAndExport(string name, int age, string languageLabel)
        {
            _entries.Add(new LoginEntry(name, age, languageLabel));

            bool logSaved = TrySaveLog();
            bool xlsxSaved = TryExportXlsx();
            return logSaved && xlsxSaved;
        }

        /// <summary>Settings screen's "Export Login Sheet" — rewrites login.xlsx from the
        /// existing log without adding a new entry. Call after LoadExistingLog() completes.</summary>
        public bool RegenerateExport() => TryExportXlsx();

        private bool TrySaveLog()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.WriteAllText(LogPath, JsonConvert.SerializeObject(_entries, Formatting.Indented));
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ExcelLoginStore] Failed to write {LogPath}: {e.Message}");
                return false;
            }
        }

        private bool TryExportXlsx()
        {
            if (_templateBytes == null)
            {
                Debug.LogWarning("[ExcelLoginStore] Template not loaded — skipping .xlsx export this run.");
                return false;
            }

            try
            {
                byte[] workbook = XlsxWriter.BuildWorkbook(_templateBytes, _entries);
                WriteWithFallback(XlsxPath, workbook);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ExcelLoginStore] Failed to export {XlsxPath}: {e.Message}");
                return false;
            }
        }

        /// <summary>If login.xlsx is open and locked in Excel, fall back to "login (1).xlsx"
        /// rather than losing the write entirely.</summary>
        private static void WriteWithFallback(string path, byte[] bytes)
        {
            try
            {
                File.WriteAllBytes(path, bytes);
                return;
            }
            catch (IOException)
            {
                string dir = Path.GetDirectoryName(path)!;
                string name = Path.GetFileNameWithoutExtension(path);
                string ext = Path.GetExtension(path);
                for (int i = 1; i <= 5; i++)
                {
                    string candidate = Path.Combine(dir, $"{name} ({i}){ext}");
                    try
                    {
                        File.WriteAllBytes(candidate, bytes);
                        Debug.LogWarning($"[ExcelLoginStore] {path} was locked — wrote {candidate} instead.");
                        return;
                    }
                    catch (IOException) { /* try the next candidate */ }
                }
                throw;
            }
        }
    }
}
