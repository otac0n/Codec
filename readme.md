Codec
=======

Asset browser for Metal Gear Solid games (and others).
This project is licensed under the [MIT](LICENSE.md) license.
Support for M2 files is provided as a separate project, licensed under the [GPL v3](Codec.M2/LICENSE.md) license.

Support for the following formats is implemented:

- Archives
  - Generic
    - .iso Images
    - .bin/cue Images
    - .zip Files [read & write]
    - .ciso GameCube images
  - Metal Gear Specific
    - M2 Archive (Master Collection v1)
      - .psb.m Packaging Format
    - .dar Archives (MGS1) [read & write]
    - .sdt Sound Packs (MGS2)
    - .sdx Sound Packs (MGS2)
    - STAGE.DIR Archive (MGS1 & MGSVR)
    - STAGE.DAT Archive (MGS2 & TTS)
    - FACE.DAT Archive (MGS1) [read & write]
    - DEMO.DAT Archive (MGS1)
    - .brf Briefing Files (MGS1 & MGSVR)
    - .slot Data Files (MGS4)
- Files
  - Generic
    - CD Audio Tracks
    - All Basic Image Formats (PNG, JPEG, BMP, etc.)
    - All Basic 3D Formats (FBX, OBJ, STL, STEP, etc.)
    - WAV & MP3 Audio Files
    - .pcx Image Files
  - Metal Gear Specific
    - .pll Image Files (MGS1)
    - .kmd Model Files (MGS1)
    - .kms Model Files (MGS2)
    - .tri Texture Files (MGS2 & MGS3)
    - .ctxr Texture Files (MGS2 & MGS3)
    - .txp Texture Files (MGS PW)
    - .mdn Model Files (MGS4)
    - .rpk Texture Files (MGS1) [read & write]

To view Master Collection resources, you will need to have a copy of the game on your system.  The tool will automatically detect the Steam location of the game, but you can browse to any location as desired.

|||
|-|-|
|![MGS1](assets/mgs1.png)|![MGS2](assets/mgs2.png)|
|![MGS3](assets/mgs3.png)|![MGS4](assets/mgs4.png)|
|![MG1](assets/mg1.png)|![MGSVR](assets/mgsvr.png)|

Running on Linux
----------------

```bash
sudo apt install -y libassimp-dev libmagickwand-dev mesa-utils
sudo apt-get install -y dotnet-runtime-9.0
chmod +x Codec.UI.Avalonia
```

Usage
-----

```csharp
// Setup
var services = Codec.UI.ServiceRegistration.RegisterHeadless();
var fsm = services.GetRequiredService<NestedFileSystemManager>(); // Grab the root filesystem.

// API
fsm.EnumerateEntries(@"G:\Rip\Exp\METAL GEAR SOLID DISC 1.CUE\MGS\FACE.DAT/0/f73b.face").Dump();

var path = "...";
using var readA = fsm.OpenRead(path);
using var readB = fsm.Open(path, new FileStreamOptions { Mode = FileMode.Open, Access = FileAccess.Read, Share = FileShare.Read });
using var readC = fsm.Open(path, new FileStreamOptions { Mode = FileMode.Open, Access = FileAccess.Read, Share = FileShare.None }); // Read/write file locks are supported hierarchically.
using var write = fsm.Open(path, new FileStreamOptions { Mode = FileMode.Open, Access = FileAccess.ReadWrite, Share = FileShare.ReadWrite }); // Read/write is supported on some filetypes.
// Some files are virtual and cannot be written. This will throw:
using var virtualA = fsm.Open(@"...\MGS\FACE.DAT/0/f73b.face/base.img", new FileStreamOptions { Mode = FileMode.Open, Access = FileAccess.ReadWrite, Share = FileShare.None });
// Some filesystems are read-only and cannot be written. This will throw:
using var virtualB = fsm.Open(@"...\METAL GEAR SOLID DISC 1.CUE\...", new FileStreamOptions { Mode = FileMode.Open, Access = FileAccess.ReadWrite, Share = FileShare.None });

// You can obtain bitmaps from known image types using:
var bitmap = fsm.Resolve<MagickImage>(path);
// Likewise with Auio streams:
var audio = fsm.Resolve<AudioStream>(path) ?? (AudioStream)fsm.OpenRead(path);
// And 3D Objects:
var scene = fsm.Resolve<RenderableScene>(path);
```

OpenSource Info
---------------

| Project | License | Details |
|---------|---------|---------|
| [Silk.NET](https://github.com/dotnet/Silk.NET) | [MIT](https://github.com/dotnet/Silk.NET/blob/main/LICENSE.md) | 3D rendering and windowing |
| [Assimp](https://github.com/Saalvage/AssimpNetter) | [MIT + BSD-3-Clause](https://github.com/Saalvage/AssimpNetter/blob/master/License.txt) | 3D object loading and saving |
| [Magick.NET](https://github.com/dlemstra/Magick.NET) | [Apache 2.0](https://github.com/dlemstra/Magick.NET/blob/main/License.txt) | Image loading and saving |
| [NAudio](https://github.com/naudio/naudio) | [MIT](https://github.com/naudio/naudio/blob/main/LICENSE) | Audio loading and playing |
| [System.IO.Abstractions](https://github.com/TestableIO/System.IO.Abstractions) | [MIT](https://github.com/TestableIO/System.IO.Abstractions/blob/main/LICENSE) | Nested filesystems |
| [CueSharp](https://www.nuget.org/packages/CueSharp) | [BSD-2-Clause](https://www.nuget.org/packages/CueSharp/1.0.1/License) | CUE format |
| [DiscUtils](https://github.com/DiscUtils/DiscUtils) | [MIT](https://github.com/DiscUtils/DiscUtils/blob/develop/LICENSE.txt) | ISO format |
| [GMWare.M2](https://gitlab.com/modmyclassic/sega-mega-drive-mini/marchive-batch-tool) | [GPL 3.0](https://gitlab.com/modmyclassic/sega-mega-drive-mini/marchive-batch-tool/-/blob/master/COPYING) | M2 Archive format |
| [metalgeardev/MGS1](https://github.com/metalgeardev/MGS1) | | Reference code |
| [mgs_reversing](https://github.com/FoxdieTeam/mgs_reversing) | | Reference code |
| [tools-mgs](https://github.com/Joy-Division/tools-mgs/) | [zlib](https://github.com/Joy-Division/tools-mgs/blob/master/LICENSE) | Reference code |
| [CtxrTool](https://github.com/Jayveer/CtxrTool) | [MIT](https://github.com/Jayveer/CtxrTool/blob/master/README.md) | Reference code |
| [MGS-Master-Collection-Noesis](https://github.com/Jayveer/MGS-Master-Collection-Noesis) | | Reference code |
| [kmy2ob](https://github.com/gdkchan/kmy2ob) | | Reference code |
| [Arsenal](https://github.com/Jayveer/Arsenal/tree/master) | MIT | Reference code |
| [Solideye](https://github.com/Jayveer/Solideye/tree/master) | MIT | Reference code |
| [MGS-MDP-Noesis](https://github.com/Jayveer/MGS-MDP-Noesis) | | Reference code |
| [MGS2-Sound-Tools](https://github.com/Gaming-With-Portals/MGS2-Sound-Tools) | [MIT](https://github.com/Gaming-With-Portals/MGS2-Sound-Tools/blob/main/LICENSE) | Reference code |
| [Metal Gear Master Collection](https://store.steampowered.com/app/2131630/METAL_GEAR_SOLID__Master_Collection_Version/) | Non-transferrable | You need your own license to this software, and your license may not cover this usage. |
| [Digital-7 Font](http://style7.website/font.php?font=digital-7) | Freeware for home use | Frequency display (coming soon) |
| [Font Awesome Free Icons](https://fontawesome.com/icons) | [CC BY 4.0](https://fontawesome.com/license/free) | Used for UI icons |
