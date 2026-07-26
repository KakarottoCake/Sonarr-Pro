# Contributing to Sonarr Pro

Contributions are welcome, including AI-assisted ones. There's no CLA and no
template to fill in.

## Pull requests

- **Explain the reasoning, not just the change.** A PR that says what broke and
  why this is the right fix is worth several that only say what changed.
- **Say what you actually verified.** "Tests pass, ran it against a real library"
  and "compiles, untested" are both fine answers. Claiming the first while
  meaning the second is not.
- **Prefer changes that stay mergeable with upstream.** This is a fork of an
  actively developed project, and every unnecessary divergence makes pulling in
  upstream fixes harder. If a bug turns out to be upstream's rather than this
  fork's, it's usually better to report it there than to patch around it here.

AI-assisted work doesn't need to be disguised. `Co-Authored-By` trailers are
accurate attribution and are welcome. See [AGENTS.md](AGENTS.md) if you're an
agent working in this repository.

## Before opening a PR

```bash
dotnet build src/Sonarr.sln -c Debug
yarn lint
```

CI runs both, plus the Core test suite and a Docker image build.

Build the **solution**, not individual project files — `Directory.Build.props`
loads `stylecop.json` through `$(SolutionDir)`, which is only defined for a
solution build. Building a bare `.csproj` produces hundreds of spurious StyleCop
errors on code that is perfectly fine.

The frontend needs `yarn build --env production`. Without that flag webpack emits
eval-source-map output and the UI loads as a blank page with no error in the
console.

## Bug reports and suggestions

[Open an issue](https://github.com/KakarottoCake/Sonarr-Pro/issues). Suggestions
are as welcome as bug reports.

Please don't raise Sonarr Pro problems on Sonarr's issue tracker, forums, or
Discord — they can't help with code they didn't write, and it costs their
volunteers time.

For bugs, the useful things to include are what you expected, what happened, and
the relevant lines from System → Logs. If it involves a specific series or
release, naming it helps more than anything else; most of the interesting bugs in
this fork have been specific to one title's metadata.
