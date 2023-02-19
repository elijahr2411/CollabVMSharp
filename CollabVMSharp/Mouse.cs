namespace CollabVMSharp; 

public class Mouse {
    public bool Left { get; set; }
    public bool Middle { get; set; }
    public bool Right { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public Mouse() {
        Left = false;
        Middle = false;
        Right = false;
        X = 0;
        Y = 0;
    }
    public int MakeMask() {
        int mask = 0;
        if (this.Left) mask |= 1;
        if (this.Middle) mask |= 2;
        if (this.Right) mask |= 4;
        return mask;
    }

    public void LoadMask(int mask) {
        this.Left = false;
        this.Middle = false;
        this.Right = false;
        if ((mask & 1) != 0) this.Left = true;
        if ((mask & 2) != 0) this.Middle = true;
        if ((mask & 4) != 0) this.Right = true;
    }
}

public enum MouseButton {
    Left,
    Middle,
    Right
}