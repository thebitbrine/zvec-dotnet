# Contributing

## Setup

1. Clone the repo
2. `python native/fetch-native.py` (downloads the prebuilt zvec library for your platform into `runtimes/{rid}/native/`)
3. `dotnet build`
4. `dotnet test --filter "FullyQualifiedName!~LargeScale"`

## Project layout

- `src/Zvec.Native/` -- P/Invoke declarations and enums. Maps directly to `c_api.h`. Packs the native libraries.
- `src/Zvec/` -- Public API. This is what users see.
- `tests/Zvec.Tests/` -- xUnit tests. Every public method should have test coverage.
- `samples/BasicSearch/` -- Runnable example, also run by CI.
- `native/fetch-native.py` -- Pins the upstream zvec release and the hashes of its SDK archives.

## Adding a new C API binding

1. Add the `[LibraryImport]` declaration in the appropriate `NativeMethods.*.cs` file
2. Add the public C# wrapper in `src/Zvec/`
3. Add tests
4. Build and run tests before submitting

Check the ownership rule in the header comment for every pointer you pass: some setters copy, some take ownership.

## Moving to a new zvec release

1. Diff `src/include/zvec/c_api.h` between the two upstream tags
2. Update `TAG` and the sha256 values in `native/fetch-native.py`
3. Update `Version` in `Directory.Build.props` to match
4. Fix what the diff broke, add bindings for what it added, run the tests

## Releasing

Push a `v*` tag. CI tests every platform, packs `Zvec.Native` and `Zvec`, pushes both to nuget.org and attaches them to a GitHub release. The tag sets the package version.

The push uses the `NUGET_API_KEY` repo secret (key "zvec-dotnet CI" on the TheBitBrine account, glob `Zvec*`). nuget.org caps new keys at 30 days, so expect to regenerate it before each release; a 403 "API key has expired" in the publish job means exactly that. Switching the workflow to nuget.org Trusted Publishing would remove the key entirely.

The package readme on nuget.org is this repo's README.md as it was when that version was packed. Editing the README changes nuget.org only with the next release.

## Code style

- No top-level statements in C# -- always use explicit `class` + `Main`
- No abstractions unless there are multiple concrete implementations
- Comments explain *why*, not *what*
- Keep unsafe code contained in the setter/query methods; public API should feel natural

## Commits

- Short imperative subject line
- Body explains why, not what
- No emoji, no AI attribution
