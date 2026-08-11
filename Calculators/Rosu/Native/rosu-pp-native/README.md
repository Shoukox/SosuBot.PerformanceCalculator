# rosu-pp native integration

This directory contains an isolated stateless Rust `cdylib` kept for
experiments and comparison. It is not registered or used by the bot runtime.

```text
optional RosuPerformanceCalculator
  -> LibraryImport / stable C ABI
  -> rosu_pp_native
  -> rosu-pp
```

No child process is started per calculation. A call sends beatmap bytes and a
UTF-8 JSON request and receives one owned UTF-8 JSON envelope.

## Versioning

- Native wrapper: `0.1.0`
- `rosu-pp`: `4.0.1`
- `rosu-pp` commit: `27a67242cad155a2767f5a25a423ca4ec3e3c274`
- Rust toolchain: `1.94.0`
- C# target: `.NET 10` (`net10.0`)

The API was checked against the local checkout at
`/home/shoukko/Desktop/rosu-pp`, which is tag `v4.0.1` at the commit above.
`Cargo.toml` uses the same exact Git commit instead of an absolute path so
clean CI and Docker builds remain reproducible. For local rosu-pp development,
the dependency can temporarily be changed to:

```toml
rosu-pp = { path = "../../../../../../rosu-pp", version = "=4.0.1" }
```

Do not commit that machine-specific path.

## Repository fit

The existing solution was analysed before adding the wrapper:

- all projects target `net10.0`;
- the bot uses the official `PPCalculator` through the public
  `IPerformanceCalculator` contract;
- beatmaps are downloaded/cached by `BeatmapsService` and exposed as streams;
- live scores use `OsuApi.BanchoV2.Models.Score`, `ScoreStatistics`, and API mod
  models;
- `PerformanceRequestMapper` reuses the existing API score/statistics DTOs;
- the Generic Host, `ILogger`, xUnit, Docker, and prometheus-net infrastructure
  are reused;
- the repository previously had no Runtime Identifiers on its projects, so
  native assets are resolved from NuGet-style `runtimes/<rid>/native` folders.

`GameMode` is intentionally calculator-local: the only other four-mode enum is
in the database project, and referencing it would reverse the existing project
dependency. `Mods` is a legacy `u32`-compatible bitmask because this is the
stable numeric format accepted by the selected rosu-pp API. Custom rate settings
are supplied through `ClockRate`.

## Supported platforms

The C# project packages:

```text
runtimes/linux-x64/native/librosu_pp_native.so
runtimes/linux-arm64/native/librosu_pp_native.so
runtimes/win-x64/native/rosu_pp_native.dll
```

Linux artifacts use glibc. The production Docker build compiles the x64 `.so`
on Debian Bookworm and copies only the resulting library into the final .NET
runtime image. macOS produces `librosu_pp_native.dylib` when Cargo is built on
macOS, but a macOS asset is not currently packaged.

## Build locally

Prerequisites are the .NET 10 SDK and Rust 1.94.0. From the repository root:

```bash
cd SosuBot.PerformanceCalculator/Calculators/Rosu/native/rosu-pp-native
cargo test --locked
cargo build --release --locked
install -D -m 0755 target/release/librosu_pp_native.so \
  ../../runtimes/linux-x64/native/librosu_pp_native.so

cd ../../../../..
dotnet build SosuBot.sln --configuration Release
dotnet test SosuBot.sln --configuration Release --no-build
```

On Windows, build `--target x86_64-pc-windows-msvc` and copy
`target/x86_64-pc-windows-msvc/release/rosu_pp_native.dll` into
`runtimes/win-x64/native/`. Linux ARM64 uses target
`aarch64-unknown-linux-gnu` and an `aarch64-linux-gnu-gcc` linker. CI builds and
uploads all three targets with unambiguous artifact names.

To inspect publish and package layout:

```bash
dotnet publish \
  tests/SosuBot.PerformanceCalculator.Smoke/SosuBot.PerformanceCalculator.Smoke.csproj \
  --configuration Release --output artifacts/smoke
dotnet pack SosuBot.PerformanceCalculator/SosuBot.PerformanceCalculator.csproj \
  --configuration Release --output artifacts/packages
```

## Docker

The regular bot image builds Rust and .NET in separate SDK stages. Neither SDK
is present in the final image. The dedicated target executes a real P/Invoke
calculation on the same .NET runtime base as production:

```bash
docker build -f SosuBot/Dockerfile --target calculator-smoke \
  -t sosubot/rosu-pp-smoke .
docker run --rm sosubot/rosu-pp-smoke

docker build -f SosuBot/Dockerfile -t sosubot/bot .
```

## C ABI and memory ownership

The only exports are:

```c
char *rosu_calculate(
    const uint8_t *beatmap, size_t beatmap_len,
    const uint8_t *request_json, size_t request_json_len);
void rosu_free_string(char *value);
```

Inputs have explicit lengths and are never interpreted as NUL-terminated
strings. The returned pointer comes from `CString::into_raw`; C# converts it in
a `try` block and always calls `rosu_free_string` in `finally`. Null is accepted
by the free function. Unsafe pointer conversion is confined to `lib.rs` and
`RosuNativeApi`.

The Rust export wraps pointer conversion, parsing, calculation, and response
serialization in `catch_unwind`. Panics become `INTERNAL_PANIC`; raw panic
payloads and backtraces never cross the ABI. Stable native codes include
`INVALID_ARGUMENT`, `INVALID_JSON`, `BEATMAP_PARSE_ERROR`,
`INVALID_SCORE_STATE`, `UNSUPPORTED_MODE`, `DIFFICULTY_CALCULATION_ERROR`,
`PERFORMANCE_CALCULATION_ERROR`, `SERIALIZATION_ERROR`, and `INTERNAL_PANIC`.
C# maps error envelopes and loader failures to `RosuNativeException` while
retaining loader exceptions as `InnerException`.

## Score input rules

Exactly one input form is accepted:

1. `Accuracy` with optional combo and misses; rosu-pp generates a valid hit
   distribution.
2. Complete mode-specific hit counts, misses, and combo where relevant.

Supplying accuracy and exact counts together is rejected. Exact counts must add
up to the difficulty attributes' object count (including partial plays), combo
cannot exceed calculated max combo, and `PassedObjects` cannot exceed the map.
Difficulty attributes are calculated once and passed into `Performance::new`;
they are not recalculated in the same request.

The mapper identifies score origin using the existing osu! API field:

```text
LegacyScoreId present -> IsLazer = false
LegacyScoreId absent  -> IsLazer = true
```

`IsLazer` is serialized even when false; the wrapper never relies on the
library default. Existing osu! API `LargeTickHit`, `SmallTickHit`, and
`SliderTailHit` values map to the optional lazer fields. Callers without these
statistics may leave them null; an exact score then defaults missing fields to
zero and can be less accurate than an API result with complete slider data.
Legacy total score is accepted only for stable osu!standard scores.

Native maps may be used directly in their own mode. rosu-pp-supported conversion
from osu!standard to taiko, catch, or mania is allowed. Reverse and cross-mode
conversions are rejected with `UNSUPPORTED_MODE`.

## Tests and golden values

Rust tests cover null pointers, invalid UTF-8/JSON, damaged beatmaps, score-state
validation, and the successful pipeline. C# tests cover request validation,
native envelopes, memory release on all response paths, seven standard mod
goldens, four modes, lazer versus legacy, 10,000 sequential calls, and parallel
calls.

Golden values use tolerances, never exact floating-point equality. Update them
only together with an intentional algorithm/version change, and record the new
rosu-pp version and commit in this document.

## Logging and metrics

Calculations log mode, calculation type, status/error code, and duration. They
never log beatmap content or the request JSON. The host's existing Prometheus
registry exposes:

- `sosubot_pp_calculations_total`
- `sosubot_pp_calculation_errors_total`
- `sosubot_pp_calculation_duration_seconds`
- `sosubot_pp_native_loaded`

Labels are limited to mode, status, calculation type, and stable error code.

## Updating rosu-pp

1. Inspect the target tag's actual `Beatmap`, `Difficulty`, `Performance`,
   `ScoreState`, conversion, and checked-calculation APIs.
2. Pin both the exact version and full commit in `Cargo.toml`.
3. Run `cargo update -p rosu-pp` and commit `Cargo.lock`.
4. Run Rust tests, Clippy, .NET tests, all golden cases, publish checks, and the
   Docker smoke target.
5. Review every changed PP/star golden instead of accepting bulk updates.

## Known limitations

- The bot does not use this native calculator. All score and map calculations
  go through the DI-registered official `PPCalculator` implementation of
  `IPerformanceCalculator`.
- Structured lazer mod settings are not encoded by the legacy bitmask; only
  explicit `ClockRate` is exposed in this first contract.
- API score statistics cannot distinguish an absent non-nullable integer field
  from an actual zero; callers should verify slider statistics availability.
- macOS is buildable but not shipped as a runtime asset.
- There are no native handles or difficulty cache. The production API remains
  deliberately stateless until parsing, difficulty, performance, JSON, and
  P/Invoke costs are benchmarked separately.

## Native loading troubleshooting

- `NATIVE_LIBRARY_LOAD_ERROR`: confirm the RID folder is present in output and
  publish directories and that the Linux runtime provides glibc.
- `NATIVE_LIBRARY_ARCHITECTURE_ERROR`: inspect the asset with `file`; process
  and library architectures must match.
- `NATIVE_ENTRY_POINT_ERROR`: rebuild wrapper and verify `rosu_calculate` and
  `rosu_free_string` with `nm -D` (Linux) or `dumpbin /exports` (Windows).
- On Linux, use `ldd librosu_pp_native.so` to identify a missing system library.
