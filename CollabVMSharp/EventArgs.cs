using System.Threading.Tasks;
using SixLabors.ImageSharp;

namespace CollabVMSharp;

public class UserRenamedEventArgs {
    public string OldName { get; set; }
    public string NewName { get; set; }
    public User User;
}

public class VoteUpdateEventArgs {
    public int Yes { get; set; }
    public int No { get; set; }
    public VoteStatus Status { get; set; }
    /// <summary>
    /// Amount of time until the vote ends, in milliseconds
    /// </summary>
    public int TimeToVoteEnd { get; set; }
}

public class SendVoteResult {
    /// <summary>
    /// True if your vote was sent successfully, false if there's a cooldown.
    /// </summary>
    public bool Success { get; set; }
    public int? CooldownTime { get; set; }
}

public class TurnUpdateEventArgs {
    /// <summary>
    /// The amount of time left on your turn in milliseconds. Null if you don't have the turn.
    /// </summary>
    public int? TurnTimer;
    /// <summary>
    /// The amount of time left before you get your turn in milliseconds. Null if you aren't waiting.
    /// </summary>
    public int? QueueTimer;
    /// <summary>
    /// The turn queue. The first element (index 0) has the turn, all following elements are the waiting users in order
    /// </summary>
    public User[] Queue;
}

public class RectEventArgs {
    public int X { get; set; }
    public int Y { get; set; }
    public Image Data { get; set; }
}

// this might not be the best place for this IDK
public enum VoteStatus {
    Started,
    Update,
}

public class GetIPTask {
    public string username { get; set; }
    public TaskCompletionSource<string> IPTask;
}