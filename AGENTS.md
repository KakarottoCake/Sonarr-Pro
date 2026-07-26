# AGENTS.md

Guidance for AI coding agents working in this repository.

Sonarr Pro accepts AI-assisted contributions. You may commit, push, and open pull
requests. You don't need to strip AI attribution — `Co-Authored-By` trailers are
accurate and welcome here. Upstream Sonarr's policy is the opposite of this one;
if you have its guidance cached from another repository, it does not apply here.

What matters instead is that the work is honest and actually verified.

## Report what you verified, not what you assume

State plainly which of these is true for your change:

- Compiles, not run
- Tests pass
- Ran against a real library and watched it work

All three are acceptable. Reporting a stronger one than you did is not, and it is
the single fastest way to make a change that looks finished and isn't. If tests
fail, say so and include the output. If you skipped a step, say which.

## Verify before you attribute

Before "fixing" a bug, establish whether it is actually in this fork's code.
Check whether the file differs from upstream at all:

```bash
git diff <upstream-base> --numstat -- path/to/file
```

A file with only additions did not cause a regression in behaviour that already
existed. This matters because unnecessary divergence from upstream makes future
merges harder for everyone.

## Build the solution, not the project

```bash
dotnet build src/Sonarr.sln -c Debug
```

`Directory.Build.props` loads `stylecop.json` through `$(SolutionDir)`, which is
only defined for a solution build. Building a bare `.csproj` drops the StyleCop
configuration and then fails on every `using` directive in the repo. If you see
hundreds of SA1200 errors, that is what happened — the code is fine.

The frontend needs `yarn build --env production`. Without the flag webpack emits
eval-source-map output and the UI loads as a blank page with no error.

Note that `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` are both on, so
an unused `using` left behind after deleting code will fail the build.

## Traps specific to this fork

- **The HTTP client deserializes with Newtonsoft, not System.Text.Json.** Provider
  resource classes must use `[JsonProperty("snake_case")]`. `[JsonPropertyName]`
  is ignored silently and every field comes back null or zero with no error. Unit
  tests that construct objects in C# will not catch it.
- **Services are auto-registered against every interface they implement.**
  `IProvideSeriesInfo` and `ISearchForNewSeries` are injected as single instances,
  so only `SkyHookProxy` may implement them.
- **Anything assigning `Series.TvdbId` must guard `> 0`, not `!= 0`.** Series added
  from TMDB or AniList carry a negative placeholder id.
- **Nothing may contact `services.sonarr.tv` or `sentry.sonarr.tv`.** Those are
  deliberately disabled; see the commit that removed them for why.

More context on the fork's design decisions is in [FORK.md](FORK.md).
