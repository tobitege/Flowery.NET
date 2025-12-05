---
description: How to run terminal commands on Windows
---
# Terminal Commands on Windows

When running terminal commands on this Windows system, always prefix with `cmd /c` to ensure the command executes properly instead of just opening a command prompt.

// turbo-all

## Build Commands

1. Build the Gallery project:
```
cmd /c dotnet build Flowery.NET.Gallery\Flowery.NET.Gallery.csproj
```

2. Build the main library:
```
cmd /c dotnet build Flowery.NET\Flowery.NET.csproj
```

3. Build the entire solution:
```
cmd /c dotnet build
```

## Run Commands

4. Run the Gallery project:
```
cmd /c dotnet run --project Flowery.NET.Gallery\Flowery.NET.Gallery.csproj
```

## Other Commands

5. Create a directory (use `md` not `mkdir`):
```
cmd /c md FolderName
```

6. Copy files:
```
cmd /c copy "source\path" "destination\path"
```
