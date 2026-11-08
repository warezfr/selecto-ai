# Contributing to Selecto AI

Thanks for helping. Bug reports, translations, extensions and code are all welcome.

## Prerequisites

- Windows 10 or 11 (the app is WPF).
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
- Optional, for installers: [Inno Setup 6](https://jrsoftware.org/isinfo.php). For the Store package: the Windows SDK (`makeappx`).

```powershell
git clone https://github.com/warezfr/selecto-ai.git
cd selecto-ai
dotnet build SelectBar.sln
dotnet test
```

## Workflow

1. Open an issue first for anything bigger than a small fix, so we can agree on the approach.
2. Fork, then create a branch from `main` (`fix/short-name` or `feature/short-name`).
3. Keep each pull request focused on one change.
4. Make sure `dotnet build SelectBar.sln` and `dotnet test` pass.
5. Open the pull request and fill in the template.

## C# style

- Target `net8.0-windows`, nullable reference types on (`<Nullable>enable</Nullable>`), implicit usings off: write your `using` lines.
- Follow the style of the surrounding code: 4-space indent, file-scoped or block namespaces as in the file you edit, `PascalCase` for types and members, `_camelCase` for private fields.
- Keep logic that does not need WPF or Win32 in plain classes that the test project can link, and keep UI code thin.
- Never put secrets, personal data or real API keys in the code, tests or logs.
- Do not add network calls without documenting them in the Privacy section of `README.md`.

## Tests are required

- Every bug fix comes with a test that fails before the fix.
- Every new behavior comes with xUnit tests in `SelectBar.Tests/`. Pure logic files are linked into the test project from `SelectBar.Tests/SelectBar.Tests.csproj`: add your file there if it must be tested.
- Run `dotnet test` before you push. CI runs it on every pull request.

## Translations (resx, four languages)

User-visible text lives in `SelectBar/Localization/`:

- `Strings.resx` (neutral, French), `Strings.en.resx`, `Strings.es.resx`, `Strings.ar.resx`.
- Add every new string to all four files with the same key. A missing key is a bug.
- Arabic is right-to-left: check your screen in Arabic. Keep wording short; the bar and the settings window have little room.
- Do not hard-code user-visible strings in C# or XAML.

## Extensions

Extensions in `extensions-library/<name>/` have a `<name>.js` file with a `==SelectoExtension==` header (`@identifier`, `@name`, `@description:fr|en|es|ar`, ...) and a `meta.json` with names and descriptions in the four languages. Copy an existing one such as `extensions-library/slug/`. Files must stay under 256 KB.

## Commits

- Short imperative subject (about 70 characters), for example `Fix flyout placement on a left taskbar`.
- Optional body that explains why, not what.
- One logical change per commit. Reference an issue with `Fixes #123`.

## Reporting issues

Use the issue templates. Include your Selecto AI version (Settings > About), edition (installer, portable or Store), Windows version and architecture, and the steps to reproduce. The log is in `%APPDATA%\SelectoAI\selecto.log`; remove anything private before posting it. For security problems, do not open a public issue: see [SECURITY.md](SECURITY.md).

## License

By contributing, you agree that your contribution is licensed under the [MIT License](LICENSE).
