# ScriptLoader for BepInEx 5.4

This is a BepInEx 5 plugin that allows you to run C# script files without compiling them to a DLL.

This plugin uses a modified version of the Mono Compiler Service (mcs) that allows to use most of C# 7 features.  
The compiler relies on `System.Reflection.Emit` being present! As such, Unity games using .NET Standard API (i.e. has `netstandard.dll` in the `Managed` folder) 
will not be able to run this plugin!

**Now scripts ignore visibility checks!** Thanks to [custom MCS](https://github.com/denikson/mcs-unity), you can now access private members (methods, fields) via scripts!

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
// #proc_filter Game.exe

using UnityEngine;
...
```

The `proc_filter` tag acts like BepinProcess attribute in BepInEx: it allows you to specify which processes to run the script on.

The `ref` tag is special: ScriptLoader will automatically load any assemblies specified with the tag.  
The path is relative to the `scripts` folder, but you can use `${Folder}` to reference some special folders.

Currently the following special folders are available:

* `Managed` -- path to the game's Managed folder with all the main DLLs
* `Scripts` -- path to the `scripts` folder
* `BepInExRoot` -- path to the `BepInEx` folder

**WIP**: At the moment, all tags but `ref` have no effect. A GUI is planned to allow to disable any scripts.

### Compilation errors

At this moment the compilation errors are simply written to the BepInEx console.

### Upgrading to 1.2.4.0

Starting 1.2.4.0, you might see the following error when loading a script:

```
Skipping loading `...` because it references outdated HarmonyWrapper and BepInEx.Harmony. To fix this, refer to github.com/denikson/BepInEx.ScriptLoader#upgrading-to-1240.
```

This error happens when using older ScriptLoader scripts with ScriptLoader 1.2.4.0 or newer.

In most cases, you can fix the script yourself. Open the script specified in the error into Notepad or some other text editor and do the following changes:

* Remove `using BepInEx.Harmony;` line
* Replace `HarmonyWrapper.PatchAll` with `Harmony.CreateAndPatchAll`

Then try to run the game again. If the error persists or you get some other error, the script is too complex to fix by this guide. In that case please conact the developer of the script and ask them to fix it.