namespace CollabVMSharp; 

/// <summary>
/// A VM recieved from the list opcode
/// </summary>
public class Node {
    /// <summary>
    /// ID of the VM
    /// </summary>
    public string ID { get; set; }
    /// <summary>
    /// Display name of the VM. May contain HTML.
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// JPEG thumbnail of the VM, usually in 400x300 resolution
    /// </summary>
    public byte[] Thumbnail { get; set; }
}