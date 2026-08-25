using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityTime = UnityEngine.Time;

namespace emiteat.NexUI.Integrations.UGUI
{
    /// <summary>
    /// Shared plumbing for the text effects: they drive whatever text component is on the object,
    /// TextMeshPro or legacy Text, so a project does not have to pick one to use them.
    /// </summary>
    public abstract class NXTextBehaviour : MonoBehaviour
    {
        private TMP_Text _tmp;
        private Text _legacy;

        protected string TextValue
        {
            get => _tmp != null ? _tmp.text : _legacy != null ? _legacy.text : string.Empty;
            set
            {
                if (_tmp != null) _tmp.text = value;
                else if (_legacy != null) _legacy.text = value;
            }
        }

        protected virtual void Awake() => Resolve();

        protected void Resolve()
        {
            _tmp = GetComponent<TMP_Text>();
            if (_tmp == null) _legacy = GetComponent<Text>();
        }

        protected bool HasText => _tmp != null || _legacy != null;
    }
}
