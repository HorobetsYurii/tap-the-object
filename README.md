# Tap the Object

One object on screen. Each round loads an image through Addressables and applies it to the object. A hit
scores a point and starts the next round. A miss flashes the object red and leaves the image alone.

Unity 6000.4.7f1, Addressables 4.0.0.

Playable build: https://horobetsyurii.github.io/tap-the-object/

## Running it

Open the project and press Play on `Assets/Scenes/Game.unity`.

On first open the project builds the Addressables content and sets play mode to *Use Existing Build*, so
round images come over HTTPS rather than from the asset database. This happens once per clone
(`Scripts/Editor/FirstRunSetup.cs`). With no network the game shows the fallback texture and stays
playable. *Use Asset Database* in the Addressables Groups window switches back to local content.

## Layout

`Core/GameFlowController.cs` runs the round loop and cancellation.
`Content/AddressablesAssetProvider.cs` does the loading and owns the handles.
`Interaction/` hit-tests taps, `Presentation/` is the object and the HUD.

## Handles and cancellation

`IAssetProvider.LoadAsync` states the ownership rule: a successful load transfers the reference to the
caller, which disposes the handle; a call that throws leaves nothing to release.

This matters because `AsyncOperationHandle.ToAwaitable(token)` releases the handle when its token is
cancelled, and that registration outlives the await. Passing a long-lived token would let a later
cancellation release a texture that is still on screen. The provider scopes each await to a linked source
and disposes it as soon as the load settles.

Each round has a token source linked to the session's, and starting a round cancels the previous one. A
cancelled round returns without touching shared state, and releases its image if it arrived first.

No `async void` except one logged `Forget()` helper. No `.Result`, `.Wait()` or `WaitForCompletion()`.

## Content delivery

Two groups. Everything in both is loaded at runtime through `Addressables.LoadAssetAsync`, with the same
handles and the same release; the groups differ only in where the bundle comes from.

`Local_Core` holds the prefab and the fallback texture. Its bundle is built into the player and read from
disk. The fallback has to be local, or it would fail in the case it exists for, which is usually no
network. So does the prefab: without it there is nothing to play with, and it has no fallback of its own.

`Remote_RoundImages` holds the round images and a remote catalog, downloaded over HTTPS.

Round images are packed one bundle per image. Packed together, the first round would download all eight and
later rounds would read from memory.

Images are found by label rather than by address, so adding one is a content change, not a code change.

### Play mode scripts

Addressables 4 removed *Simulate Groups*. Two are left:

* *Use Existing Build*, selected on first open. Loads built bundles; remote ones over HTTPS from
  `Remote.LoadPath`, currently `https://horobetsyurii.github.io/tap-the-object/ServerData/[BuildTarget]`.
* *Use Asset Database*. Local emulation, nothing to build.

The play mode tests pass in both. Under the first one the round images can only have come over the network.

The selection is stored in `Library/AddressablesConfig.dat`, which is generated, not committed. That is why
a script sets it.

### Rebuilding and publishing

```bash
Unity -batchmode -quit -projectPath . -executeMethod TapTheObject.Editor.AddressablesBuildCommand.Build
pwsh Tools/publish-content.ps1 -Push
```

`Build` accepts an optional `-remoteLoadPath <url>`. The publish script copies `ServerData/` and, if
present, `Builds/WebGL` to the `gh-pages` branch, which GitHub Pages serves. That branch holds generated
output only.

Publish after every content build. At runtime the remote catalog overrides the local one, so an old catalog
points at bundle names that no longer exist, including local ones.

## Tests

```bash
Unity -runTests -batchmode -projectPath . -testPlatform EditMode -testResults editmode.xml
Unity -runTests -batchmode -projectPath . -testPlatform PlayMode -testResults playmode.xml
```

Edit mode runs the round loop against a fake provider whose loads the test completes by hand: a round
superseded mid-download, a failed download, teardown while a load is in flight, and no handle outliving
teardown.

Play mode covers the real provider, the selection raycast, and the scene reaching a playable first round.

## While an image is loading

The object keeps the previous image, dimmed, until the new one arrives.

It does not switch to the fallback texture. The fallback means the download failed, and using it for
loading as well would make a slow network look like a broken one. Fast tapping would also flicker the
object between the fallback and real images.

Taps still count during a load, so the score can go up several times before a new image appears. A hit
always scores and a new round always cancels the pending download, so this follows from the two rules
together. Blocking taps during a load would remove the cancellation case from the game entirely.

## Notes

* Built-in render pipeline. URP would add a pipeline asset and its settings for one lit sphere.
* `SimulatedNetworkAssetProvider` adds latency and failures on demand. It affects round images only;
  failing the prefab or the fallback texture would just end the session. Off by default, see *Diagnostics*
  on `GameBootstrap`.
