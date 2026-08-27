using TMPro;
using UnityEngine;

namespace AlipiriAR.Localization
{
    /// <summary>Attach to any label whose text must survive a live language switch — everything
    /// except one-shot dynamic values (distances, names) that are already re-set every frame.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedLabel : MonoBehaviour
    {
        private TMP_Text _text;
        private string _key;
        private object[] _args;

        public string Key
        {
            get => _key;
            set { _key = value; Refresh(); }
        }

        public static LocalizedLabel Bind(TMP_Text text, string key, params object[] args)
        {
            var label = text.GetComponent<LocalizedLabel>();
            if (label == null) label = text.gameObject.AddComponent<LocalizedLabel>();
            label._text = text;
            label._key = key;
            label._args = args;
            label.Refresh();
            return label;
        }

        private void Awake()
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
        }

        private void OnEnable() => Loc.OnLocaleChanged += Refresh;
        private void OnDisable() => Loc.OnLocaleChanged -= Refresh;

        public void SetArgs(params object[] args)
        {
            _args = args;
            Refresh();
        }

        private void Refresh()
        {
            if (_text == null || string.IsNullOrEmpty(_key)) return;
            _text.text = _args is { Length: > 0 } ? Loc.T(_key, _args) : Loc.T(_key);
        }
    }
}
