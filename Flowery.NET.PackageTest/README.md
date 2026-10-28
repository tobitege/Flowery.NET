# Flowery.NET.PackageTest

An Avalonia app that references Flowery.NET and Flowery.NET.Skia as NuGet packages, not as projects.
It is set up like the [README quick start](../README.md#quick-start), calls `UseFlowerySkia()`, and shows a glass with `BlurMode="SkiaSharp"` over black and white stripes.
With the live blur of Flowery.NET.Skia, the stripes behind the glass become an even gray.
A bar below the stripes names the effective blur mode of the glass and the version and commit of both packages.
The Avalonia packages of the app take the Avalonia version of `Flowery.NET.csproj`.

Run it with [`scripts/test_packages.ps1`](../scripts/README.md#test_packagesps1), with packages packed from this checkout, from a folder or from nuget.org.
The project is not part of `Flowery.NET.sln`, because nuget.org has the packages of the version in `Directory.Build.props` only after their release.

## The headless check

With `--check`, the app renders its window headless with SkiaSharp instead of opening it.
The check fails unless the glass uses the live SkiaSharp blur, the stripes beside the glass are sharp and the stripes behind it are blurred.
CI runs it with the packages of every commit, and the release workflow with the packages of the release before it publishes them.

## Package folders

The app restores its packages into `obj\Flowery.NET.PackageTest\packages`, one folder for packages from a local folder and one for nuget.org, not into the global package folder.
A package packed locally therefore never stands in for the package of the same version on nuget.org.
