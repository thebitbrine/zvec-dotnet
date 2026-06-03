# Contributing

## Setup

1. Clone the repo
2. Get native binaries -- either extract from the [zvec Python wheel](https://pypi.org/project/zvec/) or build from source:
   ```
   cd native
   powershell -File build-native.ps1 -Tag main   # Windows
   bash build-native.sh main                      # Linux/macOS
   ```
3. Place DLLs in `runtimes/{rid}/native/` (e.g. `runtimes/win-x64/native/zvec_c_api.dll`)
4. `dotnet build`
5. `dotnet test`

## Project layout

- `src/Zvec.Native/` -- P/Invoke declarations, SafeHandles, enums. Maps directly to `c_api.h`.
- `src/Zvec/` -- Public API. This is what users see.
- `tests/Zvec.Tests/` -- xUnit tests. Every public method should have test coverage.

## Adding a new C API binding

1. Add the `[LibraryImport]` declaration in the appropriate `NativeMethods.*.cs` file
2. If it returns/takes an opaque handle, add a SafeHandle subclass in `Handles/`
3. Add the public C# wrapper in `src/Zvec/`
4. Add tests
5. Build and run tests before submitting

## Code style

- No top-level statements in C# -- always use explicit `class` + `Main`
- No abstractions unless there are multiple concrete implementations
- Comments explain *why*, not *what*
- Keep unsafe code contained in the setter/query methods; public API should feel natural

## Tests

Run all tests except the long-running large-scale benchmarks:

```
dotnet test --filter "FullyQualifiedName!~LargeScale"
```

Run everything:

```
dotnet test
```

## Commits

- Short imperative subject line
- Body explains why, not what
- No emoji, no AI attribution
