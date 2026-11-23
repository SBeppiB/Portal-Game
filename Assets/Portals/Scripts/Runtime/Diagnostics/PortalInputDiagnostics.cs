using UnityEngine;

namespace Portals.Diagnostics
{
    public class PortalInputDiagnostics : MonoBehaviour
    {
        private float _nextSummaryTime;
        private float _lastHorizontal;
        private float _lastVertical;
        private bool _wasShift;
        private bool _wasCtrl;

        private void Update()
        {
            if (PortalDiagnostics.Service == null) return;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            float mx = Input.GetAxisRaw("Mouse X");
            float my = Input.GetAxisRaw("Mouse Y");
            bool shift = Input.GetKey(KeyCode.LeftShift);
            bool ctrl = Input.GetKey(KeyCode.LeftControl);
            bool e = Input.GetKeyDown(KeyCode.E);
            bool t = Input.GetKeyDown(KeyCode.T);

            if (Mathf.Abs(h - _lastHorizontal) > 0.01f || Mathf.Abs(v - _lastVertical) > 0.01f)
            {
                if (PortalDiagnostics.Service.Throttle("input.move", 0.5f))
                    PortalDiagnostics.Service.Debug("Input", "Movement input changed", $"H={h:0.00} V={v:0.00}");
            }
            if (Mathf.Abs(mx) > 0.01f || Mathf.Abs(my) > 0.01f)
            {
                if (PortalDiagnostics.Service.Throttle("input.mouse", 0.5f))
                    PortalDiagnostics.Service.Debug("Input", "Mouse input changed", $"X={mx:0.00} Y={my:0.00}");
            }
            if (shift != _wasShift)
            {
                PortalDiagnostics.Service.Info("Input", "Shift " + (shift ? "pressed" : "released"), null);
                _wasShift = shift;
            }
            if (ctrl != _wasCtrl)
            {
                PortalDiagnostics.Service.Info("Input", "Ctrl " + (ctrl ? "pressed" : "released"), null);
                _wasCtrl = ctrl;
            }
            if (e)
                PortalDiagnostics.Service.Info("Input", "E pressed (pickup)", null);
            if (t)
                PortalDiagnostics.Service.Info("Input", "T pressed (portal gun)", null);

            _lastHorizontal = h;
            _lastVertical = v;

            if (Time.time >= _nextSummaryTime)
            {
                _nextSummaryTime = Time.time + 5f;
                if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f || Mathf.Abs(mx) > 0.01f || Mathf.Abs(my) > 0.01f)
                {
                    PortalDiagnostics.Service.Info("Input", "Input summary", $"H={h:0.00} V={v:0.00} MX={mx:0.00} MY={my:0.00} shift={shift} ctrl={ctrl}");
                }
            }

            if (PortalDiagnostics.Service.Throttle("input.playerstatus", 10f))
            {
                bool active = gameObject.activeInHierarchy;
                PortalDiagnostics.Service.Info("Player", "Player status", $"active={active} enabled={enabled}");
            }
        }
    }
}