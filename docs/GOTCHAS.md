# Gotchas

## Avalonia 12: `BringIntoView` does not exist in the current context

**Cause:** in Avalonia 12 it is an extension method (`Avalonia.Controls.ControlExtensions`), no longer a
member of `Control`. **Fix:** call `this.BringIntoView()`.

## AVLN2000: Unable to resolve property FontFamily on type Control

**Cause:** one style selector listing several control types (`TextBox.mono, TextBlock.mono`) is typed as
`Control`, which has no `FontFamily`. **Fix:** one `<Style>` per control type, or `TextElement.FontFamily`.

## NullReferenceException in MainViewModel constructor

**Cause:** setting `SectionViewModel.Type` raises the change callback, which rebuilds the YAML before every
section exists. **Fix:** keep `loading = true` until all sections are built.

## Studio CI fails restoring Taskus.Core

**Cause:** `Directory.Packages.props` pins a Taskus.Core version that is not on nuget.org yet (it exists only
in the local feed). **Fix:** release that Taskus version first (its release pushes the package through
Trusted Publishing), then push Studio.
