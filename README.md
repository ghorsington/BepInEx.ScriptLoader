# ScriptLoader for BepInEx 5

This is a BepInEx 5 plugin that allows you to run C# script files without compiling them to a DLL.

This plugin uses a modified version of the Mono Compiler Service (mcs) that allows to use most of C# 7 features.  
The compiler relies on `System.Reflection.Emit` being present! As such, Unity games using .NET Standard API (i.e. has `netstandard.dll` in the `Managed` folder) 
will not be able to run this plugin!

## Why

Because sometimes I'm lazy to open Visual Studio and write proper plugins.

## Installation

Download the latest plugin version from releases and place it into `BepInEx/plugin` folder.

## Writing and installing scripts

**To install scripts**, create a `scripts` folder in the game's root folder and place raw `.cs` files (C# source code) into it.  
**To remove scripts**, remove them from the `scripts` folder (or change file extension to `.cs.off`).

ScriptLoader will automatically load and compile all C# source code files it finds in the folder.  
ScriptLoader will also automatically run any `static void Main()` methods it finds.

Example script:

```csharp
using UnityEngine;

public static class MyScript {
    public static void Main() {
        Debug.Log("Hello, world!");
    }
}
```

### Compilation errors

At this moment the compilation errors are simply written to the BepInEx console.

## TODO

* [ ] Script reloading
* [ ] Specifying script metadata (name, description, DLL dependencies)
* [ ] Maybe a UI?
* [ ] Optionally an ability to locate and use `csc` to compile scripts when mcs cannot be used