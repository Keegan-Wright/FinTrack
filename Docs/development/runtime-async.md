# Runtime async

The server and the tests are built with .NET 11's runtime async. The runtime runs async methods itself, where before
the compiler turned each one into a state machine. That means fewer allocations, and fewer frames in the debugger's
call stack. Exception stack traces read the same as before, and nothing changes in how async code is written.

It's turned on for every `net11.0` project in `Directory.Build.targets`, with `runtime-async=on` in `Features`.

## Not in the browser

The browser runs the client on Mono, which doesn't support runtime async
([dotnet/runtime#109632](https://github.com/dotnet/runtime/issues/109632)). With it on, the client never starts. So
the projects the browser loads keep state machines:

- `FinanceTracker.Client`
- `FinanceTracker.Contracts` and `FinanceTracker.Design`, which the client references

Each sets `<FinTrackRunsInBrowser>true</FinTrackRunsInBrowser>`. A new project the client references needs it too.
The source generators target netstandard2.0, so it never applies to them.

To check what a project gets:

```sh
dotnet msbuild Src/FinanceTracker.Client -getProperty:Features
```
