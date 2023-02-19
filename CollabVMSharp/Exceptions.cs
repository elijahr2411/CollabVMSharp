using System;

namespace CollabVMSharp;

public class NoPermissionException : Exception {
    public NoPermissionException(string action) : base($"You do not have permission to {action} on this VM.") {}
}

public class NotConnectedToNodeException : Exception {
    public NotConnectedToNodeException(string action) : base($"You must be connected to a node to {action}") {
    }
}