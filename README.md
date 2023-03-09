# CollabVMSharp

CollabVM client library in C#.

## Usage
The API is well documented with XML documentation, meaning hovering over a method or property in Visual Studio or another IDE should give a pretty good idea of how to do things. For a basic usage example, see below

### Example

```cs
using System.Console;
using CollabVMSharp;
// Instantiate the client
var cvm = new CollabVMClient("wss://computernewb.com/collab-vm/vm0", "cvmsharptest", "vm0b0t");
// Connect to the VM
await cvm.Connect();
// Send a chat
await cvm.SendChat("What hath god wrought?");
// Queue a turn, wait until we get the turn
await cvm.GetTurn();
// Type a string into the VM
await cvm.TypeString("hey sexies");
// Login as an admin or mod
await cvm.Login("hunter2");
// Run a command in the QEMU monitor and get a response
Console.WriteLine(await cvm.QEMUMonitor("info block"));
// Send a message when someone takes a turn
cvm.TurnUpdate += async (_, e) => {
    await cvm.SendChat($"You have the turn, {e.Queue[0].Username}!");
};
```
