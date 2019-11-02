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

    public static void Unload() {
        // Unload and unpatch everything before reloading the script
    }
}
```

### Reloading scripts

ScriptLoader automatically detects changes in the scripts and reloads them.  

In order to make your script reloadable, **you must implement `static void Unload()`** method that cleans up any used resources.  
This is done because of Mono's limitation: you cannot actually unload any already loaded assemblies in Mono. Because of that, you should 
clean up your script so that the newly compiled script will not interfere with your game!

### Specifying metadata

You can specify metadata *at the very start of your script* by using the following format:

```csharp
// #name Short name of the script
// #author ghorsington
// #desc A longer description of the script. This still should be a one-liner.
// #ref ${Managed}/UnityEngine.UI.dll
// #ref ${BepInExRoot}/core/MyDependency.dll

using UnityEngine;
...
```

The `ref` tag is special: ScriptLoader will automatically load any assemblies specified with the tag.  
The path is relative to the `scripts` folder, but you can use `${Folder}` to reference some special folders.

Currently the following special folders are available:

* `Managed` -- path to the game's Managed folder with all the main DLLs
* `Scripts` -- path to the `scripts` folder
* `BepInExRoot` -- path to the `BepInEx` folder

**WIP**: At the moment, all tags but `ref` have no effect. A GUI is planned to allow to disable any scripts.

### Compilation errors

At this moment the compilation errors are simply written to the BepInEx console.

## TODO

* [x] Script reloading
* [x] Specifying script metadata (name, description, DLL dependencies)
* [ ] Maybe a UI?
* [ ] Optionally an ability to locate and use `csc` to compile scripts when mcs cannot be used