namespace ErenshorCraftingExpanded
{
    public enum ForageInteractionCapturePhase
    {
        Idle = 0,
        HoverCandidate = 1,
        Selected = 2,
        Gathering = 3,
        Completing = 4,
        Cancelled = 5
    }

    // Pure transaction-local selection/channel state. Hover may nominate a candidate, but a
    // right-click selection captures one stable node/token. Camera/hover changes cannot replace
    // that identity while the channel is active.
    public sealed class ForageInteractionCaptureState
    {
        private ForageInteractionCapturePhase _phase;
        private long _token;
        private string _nodeId;
        private string _lastTerminalReason;

        public ForageInteractionCaptureState()
        {
            _phase = ForageInteractionCapturePhase.Idle;
            _token = 0;
            _nodeId = string.Empty;
            _lastTerminalReason = string.Empty;
        }

        public ForageInteractionCapturePhase Phase { get { return _phase; } }
        public long Token { get { return _token; } }
        public string NodeId { get { return _nodeId ?? string.Empty; } }
        public string LastTerminalReason { get { return _lastTerminalReason ?? string.Empty; } }

        public void SetHoverCandidate(string nodeId)
        {
            if (_phase == ForageInteractionCapturePhase.Selected ||
                _phase == ForageInteractionCapturePhase.Gathering ||
                _phase == ForageInteractionCapturePhase.Completing)
                return;

            _token = 0;
            _nodeId = nodeId ?? string.Empty;
            _phase = string.IsNullOrEmpty(_nodeId)
                ? ForageInteractionCapturePhase.Idle
                : ForageInteractionCapturePhase.HoverCandidate;
        }

        public bool TrySelect(string nodeId, long token)
        {
            if (string.IsNullOrEmpty(nodeId) || token <= 0) return false;
            if (_phase == ForageInteractionCapturePhase.Selected ||
                _phase == ForageInteractionCapturePhase.Gathering ||
                _phase == ForageInteractionCapturePhase.Completing)
                return false;

            _nodeId = nodeId;
            _token = token;
            _lastTerminalReason = string.Empty;
            _phase = ForageInteractionCapturePhase.Selected;
            return true;
        }

        public bool TryBeginGathering(long token)
        {
            if (_phase != ForageInteractionCapturePhase.Selected || token <= 0 || token != _token)
                return false;
            _phase = ForageInteractionCapturePhase.Gathering;
            return true;
        }

        // Compatibility helper retained for deterministic tests/older call sites. New production
        // code intentionally performs Select -> Gathering as two explicit transitions.
        public bool TryCapture(string nodeId, long token)
        {
            return TrySelect(nodeId, token) && TryBeginGathering(token);
        }

        public bool IsCaptured(long token)
        {
            return token > 0 && token == _token &&
                (_phase == ForageInteractionCapturePhase.Selected ||
                 _phase == ForageInteractionCapturePhase.Gathering ||
                 _phase == ForageInteractionCapturePhase.Completing);
        }

        public bool IsGathering(long token)
        {
            return token > 0 && token == _token &&
                (_phase == ForageInteractionCapturePhase.Gathering ||
                 _phase == ForageInteractionCapturePhase.Completing);
        }

        public bool TryBeginCompleting(long token)
        {
            if (_phase != ForageInteractionCapturePhase.Gathering || token <= 0 || token != _token)
                return false;
            _phase = ForageInteractionCapturePhase.Completing;
            return true;
        }

        public void MarkCancelled(long token, string reason)
        {
            if (token <= 0 || token != _token) return;
            if (_phase != ForageInteractionCapturePhase.Selected &&
                _phase != ForageInteractionCapturePhase.Gathering &&
                _phase != ForageInteractionCapturePhase.Completing)
                return;
            _lastTerminalReason = reason ?? string.Empty;
            _phase = ForageInteractionCapturePhase.Cancelled;
        }

        public void Release(string terminalReason)
        {
            if (!string.IsNullOrEmpty(terminalReason)) _lastTerminalReason = terminalReason;
            _phase = ForageInteractionCapturePhase.Idle;
            _token = 0;
            _nodeId = string.Empty;
        }

        internal static string RunSelfTests()
        {
            ForageInteractionCaptureState state = new ForageInteractionCaptureState();
            if (state.Phase != ForageInteractionCapturePhase.Idle) return "FAIL capture initial state";
            state.SetHoverCandidate("herb-a");
            if (state.Phase != ForageInteractionCapturePhase.HoverCandidate || state.NodeId != "herb-a") return "FAIL hover candidate";
            if (!state.TrySelect("herb-a", 7) || state.Phase != ForageInteractionCapturePhase.Selected) return "FAIL selected transition";
            state.SetHoverCandidate("herb-b");
            if (!state.IsCaptured(7) || state.NodeId != "herb-a") return "FAIL selected identity replaced by hover";
            if (state.TrySelect("herb-b", 8)) return "FAIL overlapping selection admitted";
            if (!state.TryBeginGathering(7) || state.Phase != ForageInteractionCapturePhase.Gathering) return "FAIL gathering transition";
            if (!state.TryBeginCompleting(7) || state.Phase != ForageInteractionCapturePhase.Completing) return "FAIL completion transition";
            state.MarkCancelled(7, "out-of-range");
            if (state.Phase != ForageInteractionCapturePhase.Cancelled || state.LastTerminalReason != "out-of-range") return "FAIL cancel transition";
            state.Release("released");
            if (state.Phase != ForageInteractionCapturePhase.Idle || state.Token != 0 || state.NodeId.Length != 0) return "FAIL release reset";
            if (state.LastTerminalReason != "released") return "FAIL terminal reason";
            if (!state.TryCapture("herb-c", 9) || !state.IsGathering(9)) return "FAIL compatibility capture";
            state.Release("complete");
            return "PASS forage interaction select/channel state";
        }
    }
}
