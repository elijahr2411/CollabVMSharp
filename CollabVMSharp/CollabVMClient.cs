#nullable enable
#pragma warning disable CS4014
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Timer = System.Timers.Timer;
// ReSharper disable FieldCanBeMadeReadOnly.Local
// ReSharper disable ArrangeObjectCreationWhenTypeNotEvident
// ReSharper disable InconsistentNaming
// ReSharper disable ArrangeObjectCreationWhenTypeEvident

namespace CollabVMSharp;
public class CollabVMClient {
    // Fields
    private Uri url;
    private string? username;
    private string? node;
    private Rank _rank;
    private Permissions _perms;
    private bool _connected;
    private bool _connectedToVM;
    private ClientWebSocket socket;
    private List<User> _users;
    private Timer NOPRecieve;
    private Image framebuffer;
    private TurnStatus _turnStatus;
    private Mouse mouse;
    // Tasks
    private TaskCompletionSource<Node[]> GotNodeList;
    private TaskCompletionSource<bool> GotConnectionToNode;
    private TaskCompletionSource<int> GotTurn;
    private TaskCompletionSource<Rank> GotStaff;
    private List<GetIPTask> GotIPTasks;
    // Properties
    public Rank Rank { get { return this._rank; } }
    public Permissions Permissions { get { return this._perms; } }
    public bool Connected { get { return this._connected; } }
    public bool ConnectedToVM { get { return this._connectedToVM; } }
    public User[] Users => _users.ToArray();
    public TurnStatus TurnStatus { get { return this._turnStatus; } }
    // Events
    public event EventHandler<ChatMessage> Chat;
    public event EventHandler<ChatMessage[]> ChatHistory;
    public event EventHandler ConnectedToNode;
    public event EventHandler NodeConnectFailed;
    public event EventHandler<RectEventArgs> Rect;
    public event EventHandler<string> Renamed;
    public event EventHandler<UserRenamedEventArgs> UserRenamed;
    public event EventHandler<User> UserJoined;
    public event EventHandler<User> UserLeft;
    public event EventHandler<VoteUpdateEventArgs> VoteUpdate;
    public event EventHandler VoteEnded;
    public event EventHandler<int> VoteCooldown;
    public event EventHandler<TurnUpdateEventArgs> TurnUpdate;
    public event EventHandler ConnectionClosed;
    /// <summary>
    /// Client for the CollabVM 1.x Server
    /// </summary>
    /// <param name="url">URL of the CollabVM Server to connect to (Should start with ws:// or wss://)</param>
    /// <param name="username">Username to join the VM as. If null, the server will assign a guest name.</param>
    /// <param name="node">Node to connect to. If null, a VM will not be automatically joined.</param>
    /// <param name="proxy">HTTP proxy to connect with. If null, a proxy will not be used.</param>
    public CollabVMClient(string url, string? username = null, string? node = null, string? proxy = null) {
        if (!Uri.TryCreate(url, UriKind.Absolute, out this.url)) {
            throw new UriFormatException("An invalid URI string was passed.");
        }
        if (this.url.Scheme != "ws" && this.url.Scheme != "wss") {
            throw new UriFormatException("The URL must have a valid websocket scheme (ws or wss)");
        }
        this.username = username;
        this.node = node;
        this._rank = Rank.Unregistered;
        this._perms = Permissions.None;
        this._connected = false;
        this._connectedToVM = false;
        this.framebuffer = new Image<Rgba32>(1, 1);
        this.socket = new();
        this.socket.Options.AddSubProtocol("guacamole");
        this.socket.Options.SetRequestHeader("Origin", "https://computernewb.com");
        if (proxy != null) {
            this.socket.Options.Proxy = new WebProxy(proxy);
        }
        this.NOPRecieve = new(10000);
        this.NOPRecieve.AutoReset = false;
        this.NOPRecieve.Elapsed += delegate { this.Disconnect(); };
        this._users = new();
        this.mouse = new();
        this.GotNodeList = new();
        this.GotConnectionToNode = new();
        this.GotTurn = new();
        this.GotStaff = new();
        this.GotIPTasks = new();
        // Assign empty handlers
        Chat += delegate { };
        ChatHistory += delegate { };
        ConnectedToNode += delegate { };
        NodeConnectFailed += delegate { };
        Rect += delegate { };
        Renamed += delegate { };
        UserRenamed += delegate { };
        UserJoined += delegate { };
        UserLeft += delegate { };
        VoteUpdate += delegate { };
        VoteEnded += delegate { };
        VoteCooldown += delegate { };
        TurnUpdate += delegate { };
        ConnectionClosed += delegate { };
    }
    /// <summary>
    /// Connect to the CollabVM Server
    /// </summary>
    public async Task Connect() {
        await this.socket.ConnectAsync(this.url, CancellationToken.None);
        this._connected = true;
        if (this.username != null)
            this.SendMsg(Guacutils.Encode("rename", this.username));
        else
            this.SendMsg(Guacutils.Encode("rename"));
        if (this.node != null)
            this.SendMsg(Guacutils.Encode("connect", this.node));
        this.NOPRecieve.Start();
        this.WebSocketLoop();
    }

    private async void WebSocketLoop() {
        ArraySegment<byte> receivebuffer = new ArraySegment<byte>(new byte[8192]);
        do {
            MemoryStream ms = new();
            WebSocketReceiveResult res;
            do {
                res = await socket.ReceiveAsync(receivebuffer, CancellationToken.None);
                if (res.MessageType == WebSocketMessageType.Close) {
                    this.Disconnect();
                    return;
                }
                await ms.WriteAsync(receivebuffer.Array, 0, res.Count);
            } while (!res.EndOfMessage);
            string msg;
            try {
                msg = Encoding.UTF8.GetString(ms.ToArray());
            } catch (Exception e) {
                #if DEBUG
                await Console.Error.WriteLineAsync($"Failed to read message from socket: {e.Message}");
                #endif
                continue;
            } finally {ms.Dispose();}
            this.ProcessMessage(msg);
        } while (socket.State == WebSocketState.Open);
        this.Cleanup();
    }

    private async void ProcessMessage(string msg) {
        string[] msgArr;
        try {
            msgArr = Guacutils.Decode(msg);
        } catch (Exception e) {
            #if DEBUG
            await Console.Error.WriteLineAsync($"Failed to decode incoming message: {e.Message}");
            #endif
            return;
        }
        if (msgArr.Length < 1) return;
        this.NOPRecieve.Stop();
        this.NOPRecieve.Interval = 10000;
        this.NOPRecieve.Start();
        switch (msgArr[0]) {
            case "nop": {
                this.SendMsg("3.nop;");
                break;
            }
            case "connect": {
                switch (msgArr[1]) {
                    case "0":
                        this.NodeConnectFailed.Invoke(this, EventArgs.Empty);
                        this.GotConnectionToNode.TrySetResult(false);
                        break;
                    case "1":
                        this._connectedToVM = true;
                        this.ConnectedToNode.Invoke(this, EventArgs.Empty);
                        this.GotConnectionToNode.TrySetResult(true);
                        break;
                }
                break;
            }
            case "chat": {
                if (msgArr.Length > 3) {
                    List<ChatMessage> msgs = new();
                    for (int i = 1; i < msgArr.Length; i += 2) {
                        msgs.Add(new ChatMessage {
                            Username = msgArr[i],
                            Message = WebUtility.HtmlDecode(msgArr[i+1])
                        });
                    }
                    ChatHistory.Invoke(this, msgs.ToArray());
                    // I should probably add a config option for whether or not the message should be HTML encoded
                } else Chat.Invoke(this, new ChatMessage {Username = msgArr[1], Message = WebUtility.HtmlDecode(msgArr[2])});
                break;
            }
            case "captcha": {
                throw new Exception("This VM requires a captcha to connect to. Please do not attempt to bypass this. Contact the CollabVM admins (or the owner of the server you're connecting to) to request to be added to the bot whitelist.");
                break;
            }
            case "list": {
                List<Node> nodes = new();
                for (var i = 1; i < msgArr.Length; i += 3) {
                    nodes.Add(new Node {
                        ID = msgArr[i],
                        Name = msgArr[i+1],
                        Thumbnail = Convert.FromBase64String(msgArr[i+2])
                    });
                    this.GotNodeList.TrySetResult(nodes.ToArray());
                }
                break;
            }
            case "size": {
                if (msgArr[1] != "0") return;
                this.framebuffer = new Image<Rgba32>(int.Parse(msgArr[2]), int.Parse(msgArr[3]));
                break;
            }
            case "png": {
                if (msgArr[2] != "0") return;
                Image rect = Image.Load(Convert.FromBase64String(msgArr[5]));
                framebuffer.Mutate(f => f.DrawImage(rect, new Point(int.Parse(msgArr[3]), int.Parse(msgArr[4])), 1));
                this.Rect.Invoke(this, new RectEventArgs {
                    X = int.Parse(msgArr[3]),
                    Y = int.Parse(msgArr[4]),
                    Data = rect,
                });
                break;
            }
            case "rename": {
                switch (msgArr[1]) {
                    case "0": {
                        var user = _users.Find(u => u.Username == username);
                        if (user != null) {
                            user.Username = msgArr[3];
                        }
                        this.username = msgArr[3];
                        this.Renamed.Invoke(this, msgArr[3]);
                        break;
                    }
                    default: {
                        var user = _users.Find(u => u.Username == msgArr[2]);
                        user.Username = msgArr[3];
                        this.UserRenamed.Invoke(this, new UserRenamedEventArgs {
                            OldName = msgArr[2],
                            NewName = msgArr[3],
                            User = user
                        });
                        break;
                    }
                }
                break;
            }
            case "adduser": {
                for (int i = 2; i < msgArr.Length; i += 2) {
                    // This can happen when a user logs in
                    _users.RemoveAll(u => u.Username == msgArr[i]);
                    var user = new User {
                        Username = msgArr[i],
                        Rank = msgArr[i + 1] switch {
                            "0" => Rank.Unregistered,
                            "2" => Rank.Admin,
                            "3" => Rank.Moderator,
                            _ => Rank.Unregistered
                        },
                        Turn = TurnStatus.None
                    };
                    this.UserJoined.Invoke(this, user);
                    this._users.Add(user);
                }
                break;
            }
            case "remuser": {
                for (int i = 2; i < msgArr.Length; i++) {
                    var user = this._users.Find(u=>u.Username==msgArr[i]);
                    this._users.Remove(user);
                    UserLeft.Invoke(this, user);
                }
                break;
            }
            case "vote": {
                switch (msgArr[1]) {
                    case "0":
                    case "1":
                        this.VoteUpdate.Invoke(this, new VoteUpdateEventArgs {
                            No = int.Parse(msgArr[4]),
                            Yes = int.Parse(msgArr[3]),
                            Status = msgArr[1] switch {"0" => VoteStatus.Started, "1" => VoteStatus.Update},
                            TimeToVoteEnd = int.Parse(msgArr[2])
                        });
                        break;
                    case "2":
                        this.VoteEnded.Invoke(this, EventArgs.Empty);
                        break;
                    case "3":
                        this.VoteCooldown.Invoke(this, int.Parse(msgArr[2]));
                        break;
                }
                break;
            }
            case "turn": {
                List<User> queue = new();
                // Reset turn data
                this._users.ForEach(u => u.Turn = TurnStatus.None);
                this._turnStatus = TurnStatus.None;
                int queuedUsers = int.Parse(msgArr[2]);
                if (queuedUsers == 0) {
                    this.TurnUpdate.Invoke(this, new TurnUpdateEventArgs {
                        Queue = Array.Empty<User>(),
                        TurnTimer = null,
                        QueueTimer = null
                    });
                    return;
                }
                var currentTurnUser = _users.First(u => u.Username == msgArr[3]);
                if (msgArr[3] == this.username) {
                    this._turnStatus = TurnStatus.HasTurn;
                    this.GotTurn.TrySetResult(int.Parse(msgArr[1]));
                }
                currentTurnUser.Turn = TurnStatus.HasTurn;
                queue.Add(currentTurnUser);
                if (queuedUsers > 1) {
                    for (int i = 1; i < queuedUsers; i++) {
                        var user = _users.Find(u => u.Username == msgArr[i + 3]);
                        user.Turn = TurnStatus.Waiting;
                        if (msgArr[i + 3] == this.username)
                            this._turnStatus = TurnStatus.Waiting;
                        queue.Add(user);
                    }
                }
                this.TurnUpdate.Invoke(this, new TurnUpdateEventArgs {
                    Queue = queue.ToArray(),
                    TurnTimer = (this._turnStatus == TurnStatus.HasTurn) ? int.Parse(msgArr[1]) : null,
                    QueueTimer = (this._turnStatus == TurnStatus.Waiting) ? int.Parse(msgArr[msgArr.Length-1]) : null,
                });
                break;
            }
            case "admin": {
                switch (msgArr[1]) {
                    case "0": {
                        switch (msgArr[2]) {
                            case "0":
                                throw new InvalidCredentialException("The provided password was incorrect.");
                                break;
                            case "1":
                                this._rank = Rank.Admin;
                                this._perms = Permissions.All;
                                this.GotStaff.TrySetResult(Rank.Admin);
                                break;
                            case "3":
                                this._rank = Rank.Moderator;
                                this._perms = new Permissions(int.Parse(msgArr[3]));
                                this.GotStaff.TrySetResult(Rank.Moderator);
                                break;
                        }
                        break;
                    }
                    case "19":
                        var tsk = this.GotIPTasks.Find(x => x.username == msgArr[2]);
                        if (tsk == null) return;
                        tsk.IPTask.TrySetResult(msgArr[3]);
                        break;
                }
                break;
            }
        }
    }
    /// <summary>
    /// Close the connection to the server
    /// </summary>
    public async Task Disconnect() {
        this._connected = false;
        if (this.socket.State == WebSocketState.Open)
            await this.SendMsg("10.disconnect;");
        await this.socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        this.Cleanup();
        return;
    }

    private void Cleanup(bool fireDisconnect = true) {
        this._users.Clear();
        this._connected = false;
        this._connectedToVM = false;
        this._rank = Rank.Unregistered;
        this._perms = Permissions.None;
        this.NOPRecieve.Stop();
        this.NOPRecieve.Interval = 10000;
        if (fireDisconnect)
            this.ConnectionClosed.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Send a raw string message over the socket
    /// </summary>
    public Task SendMsg(string msg) {
        if (!this._connected) throw new WebSocketException("Cannot send a message while the socket is closed");
        return this.socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text,
            true, CancellationToken.None);
    }
    /// <summary>
    /// Request a list of VMs from the server.
    /// </summary>
    /// <returns>A list of VMs</returns>
    public async Task<Node[]> ListVMs() {
        GotNodeList = new();
        SendMsg(Guacutils.Encode("list"));
        return await GotNodeList.Task;
    }
    /// <summary>
    /// Attempt to connect to a VM
    /// </summary>
    /// <param name="node">ID of the VM to connect to</param>
    /// <returns>True if successful, false otherwise</returns>
    public async Task<bool> ConnectToVM(string node) {
        if (this._connectedToVM)
            throw new Exception("Already connected to a node. You must disconnect and reconnect to switch nodes.");
        this.GotConnectionToNode = new();
        this.SendMsg(Guacutils.Encode("connect", node));
        return await this.GotConnectionToNode.Task;
    }

    /// <summary>
    /// Send a key to the VM. If you don't have the turn, nothing will happen
    /// </summary>
    /// <param name="keysym">X11 Keysym of the key to send.</param>
    /// <param name="down">Whether or not the key is pressed</param>
    public Task SendKey(int keysym, bool down) => this.SendMsg(Guacutils.Encode("key", keysym.ToString(), down ? "1" : "0"));

    /// <summary>
    /// Move the mouse
    /// </summary>
    /// <param name="x">Horizontal position or offset of the mouse</param>
    /// <param name="y">Vertical position or offset of the mouse</param>
    /// <param name="relative">If true, mouse is moved relative to it's current position. If false, the mouse will be moved to the exact coordinates given</param>
    public async Task MoveMouse(int x, int y, bool relative = false) {
        if (relative) {
            this.mouse.X += x;
            this.mouse.Y += y;
        }
        else {
            this.mouse.X = x;
            this.mouse.Y = y;
        }
        this.sendMouse();
    }
    /// <summary>
    /// Set the pressed mouse buttons to the given mask
    /// </summary>
    /// <param name="mask">The button mask, representing the pressed or released status of each mouse button.</param>
    public async Task MouseBtn(int mask) {
        this.mouse.LoadMask(mask);
        await this.sendMouse();
    }

    /// <summary>
    /// Press or release a mouse button
    /// </summary>
    /// <param name="btn">The button to change the state of</param>
    /// <param name="down">True if the button is down, false if it's up</param>
    public async Task MouseBtn(MouseButton btn, bool down) {
        switch (btn) {
            case MouseButton.Left:
                this.mouse.Left = down;
                break;
            case MouseButton.Middle:
                this.mouse.Middle = down;
                break;
            case MouseButton.Right:
                this.mouse.Right = down;
                break;
        }
        await this.sendMouse();
    }

    /// <summary>
    /// Type a specified character to the VM
    /// </summary>
    /// <param name="c">The character to type</param>
    /// <param name="down">Whether the key is pressed or released, or null to press and then release</param>
    public async Task SendChar(char c, bool? down = null) {
        int keysym = Keyboard.KeyMap[c];
        if (down != null)
            await this.SendKey(keysym, (bool)down);
        else {
            await this.SendKey(keysym, true);
            await Task.Delay(20);
            await this.SendKey(keysym, false);
        }
    }
    /// <summary>
    /// Press a special key on the VM
    /// </summary>
    /// <param name="key">Key to send</param>
    /// <param name="down">Whether the key is pressed or released, or null to press and then release</param>
    public async Task SendSpecialKey(SpecialKey key, bool? down = null) {
        int keysym = (int)key;
        if (down != null)
            await this.SendKey(keysym, (bool)down);
        else {
            await this.SendKey(keysym, true);
            await Task.Delay(20);
            await this.SendKey(keysym, false);
        }
    }

    /// <summary>
    /// Type a string into the VM
    /// </summary>
    /// <param name="str">String to type</param>
    public async Task TypeString(string str) {
        foreach (char c in str) {
            await SendChar(c);
            await Task.Delay(20);
        }
    }
    /// <summary>
    /// Request or cancel a turn
    /// </summary>
    /// <param name="take">True to request a turn, false to cancel</param>
    public Task Turn(bool take) => this.SendMsg(Guacutils.Encode("turn", take ? "1" : "0"));

    /// <summary>
    /// Request a turn and returns once the turn is received.
    /// </summary>
    /// <returns>How long you have the turn, in milliseconds</returns>
    public async Task<int> GetTurn() {
        if (this._turnStatus == TurnStatus.HasTurn)
            throw new Exception("You already have the turn");
        this.GotTurn = new();
        this.Turn(true);
        return await this.GotTurn.Task;
    }
    
    /// <summary>
    /// Log in as an Admin or Moderator
    /// </summary>
    /// <param name="password">Password to log in with</param>
    /// <returns>The rank received</returns>
    public async Task<Rank> Login(string password) {
        this.GotStaff = new();
        this.SendMsg(Guacutils.Encode("admin", "2", password));
        return await this.GotStaff.Task;
    }

    /// <summary>
    /// Send a message to the VM chat
    /// </summary>
    /// <param name="msg">Message to send</param>`
    public Task SendChat(string msg) => this.SendMsg(Guacutils.Encode("chat", msg));

    /// <summary>
    /// Restore the VM
    /// </summary>
    public async Task Restore() {
        if (!this._perms.Restore)
            throw new NoPermissionException("Restore VM");
        if (!this._connectedToVM)
            throw new NotConnectedToNodeException("Restore VM");
        await this.SendMsg(Guacutils.Encode("admin", "8", this.node));
    }
    
    public async Task Reboot() {
        if (!this._perms.Reboot)
            throw new NoPermissionException("Reboot VM");
        if (!this._connectedToVM)
            throw new NotConnectedToNodeException("Reboot VM");
        await this.SendMsg(Guacutils.Encode("admin", "10", this.node));
    }
    
    public async Task ClearTurnQueue() {
        if (!this._perms.BypassAndEndTurns)
            throw new NoPermissionException("Clear the turn queue");
        if (!this._connectedToVM)
            throw new NotConnectedToNodeException("Clear the turn queue");
        await this.SendMsg(Guacutils.Encode("admin", "17", this.node));
    }
    
    public async Task BypassTurn() {
        if (!this._perms.BypassAndEndTurns)
            throw new NoPermissionException("Bypass Turn");
        if (!this._connectedToVM)
            throw new NotConnectedToNodeException("Bypass Turn");
        await this.SendMsg(Guacutils.Encode("admin", "20"));
    }
    
    public async Task EndTurn(string user) {
        if (!this._perms.BypassAndEndTurns)
            throw new NoPermissionException("End Turn");
        if (!this._connectedToVM)
            throw new NotConnectedToNodeException("End Turn");
        await this.SendMsg(Guacutils.Encode("admin", "16", user));
    }
    
    public async Task Ban(string user) {
        if (!this._perms.Ban)
            throw new NoPermissionException("Ban");
        if (!this._connectedToVM)
            throw new NotConnectedToNodeException("Ban");
        await this.SendMsg(Guacutils.Encode("admin", "12", user));
    }
    
    public async Task Kick(string user) {
        if (!this._perms.Kick)
            throw new NoPermissionException("Kick");
        if (!this._connectedToVM)
            throw new NotConnectedToNodeException("Kick");
        await this.SendMsg(Guacutils.Encode("admin", "15", user));
    }
    
    public async Task RenameUser(string user, string newname) {
        if (!this._perms.Rename)
            throw new NoPermissionException("Rename user");
        if (!this._connectedToVM)
            throw new NotConnectedToNodeException("Rename user");
        await this.SendMsg(Guacutils.Encode("admin", "18", user, newname));
    }
    
    public async Task MuteUser(string user, bool permanent) {
        if (!this._perms.Mute)
            throw new NoPermissionException("Mute user");
        if (!this._connectedToVM)
            throw new NotConnectedToNodeException("Mute user");
        await this.SendMsg(Guacutils.Encode("admin", "14", user, permanent ? "1" : "0"));
    }
    
    public async Task<string> GetIP(string user) {
        if (!this._perms.GetIP)
            throw new NoPermissionException("Get IP");
        if (!this._connectedToVM)
            throw new NotConnectedToNodeException("Get IP");
        var tsk = new GetIPTask {
            IPTask = new(),
            username = user
        };
        this.GotIPTasks.Add(tsk);
        this.SendMsg(Guacutils.Encode("admin", "19", user));
        return await tsk.IPTask.Task;
    }
    
    private Task sendMouse() => this.SendMsg(Guacutils.Encode("mouse", mouse.X.ToString(), mouse.Y.ToString(), mouse.MakeMask().ToString()));
} 