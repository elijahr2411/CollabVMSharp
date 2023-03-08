namespace CollabVMSharp; 

public class Permissions {
    private bool restore;
    private bool reboot;
    private bool ban;
    private bool kick;
    private bool mute;
    private bool forcevote;
    private bool bypassendturn;
    private bool rename;
    private bool getip;
    private bool xss;
    
    public bool Restore { get { return restore; } }
    public bool Reboot { get { return reboot; } }
    public bool Ban { get { return ban; } }
    public bool Kick { get { return kick; } }
    public bool Mute { get { return mute; } }
    public bool ForceVote { get {  return forcevote; } }
    public bool BypassAndEndTurns { get { return bypassendturn; } }
    public bool Rename { get { return rename; } }
    public bool GetIP { get { return getip; } }
    public bool XSS { get { return xss; } }

    public Permissions(int permissionvalue) {
        if ((permissionvalue & 1) != 0) restore = true;
        if ((permissionvalue & 2) != 0) reboot = true;
        if ((permissionvalue & 4) != 0) ban = true;
        if ((permissionvalue & 8) != 0) forcevote = true;
        if ((permissionvalue & 16) != 0) mute = true;
        if ((permissionvalue & 32) != 0) kick = true;
        if ((permissionvalue & 64) != 0) bypassendturn = true;
        if ((permissionvalue & 128) != 0) rename = true;
        if ((permissionvalue & 256) != 0) getip = true;
        if ((permissionvalue & 512) != 0) xss = true;
    }

    public static Permissions All => new Permissions(65535);
    public static Permissions None => new Permissions(0);
}

public enum Rank {
    Unregistered = 0,
    Moderator = 3,
    Admin = 2
}