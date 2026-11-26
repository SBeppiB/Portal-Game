using System;

namespace Portals.Runtime.Mechanics
{
    public class PortalFormationResult
    {
        public bool Success;
        public PortalFormationFailureReason FailureReason = PortalFormationFailureReason.None;
        public string Message = string.Empty;
        public double RequiredEnergy;
        public PortalDiagnosticData Diagnostics;
    }
}
