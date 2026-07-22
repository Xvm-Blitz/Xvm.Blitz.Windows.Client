# Code signing policy

[Free code signing provided by SignPath.io](https://signpath.io/), [certificate by SignPath Foundation](https://signpath.org/).

## What is signed

- Windows single-file release EXE published on [GitHub Releases](https://github.com/Xvm-Blitz/Xvm.Blitz.Windows.Client/releases) (`XVMBlitz-*-win-x64.exe`).

## Build and signing process

- Artifacts are built from this repository by GitHub Actions (`.github/workflows/build.yml`).
- Only CI-built artifacts from GitHub-hosted runners are submitted to SignPath for signing.
- The private key is held by SignPath (HSM-backed). This project does not store the private key.
- Each signing request requires explicit approval by an Approver.

## Team roles

### Authors (commit access; may modify the repository without additional reviews)

- [Organization owners](https://github.com/orgs/Xvm-Blitz/people?query=role%3Aowner)

### Reviewers (review required for changes proposed by non-committers)

- [Organization owners](https://github.com/orgs/Xvm-Blitz/people?query=role%3Aowner)
- Policy: all external pull requests are reviewed by a maintainer before merge

### Approvers (approve each signing request)

- [Organization owners](https://github.com/orgs/Xvm-Blitz/people?query=role%3Aowner)
- Policy: each signing request requires explicit approval by a maintainer

## Distribution

- https://github.com/Xvm-Blitz/Xvm.Blitz.Windows.Client/releases

## Privacy policy

This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it.

Network requests made at the user’s request (for example battle statistics via the XVM Blitz API, when an API key is configured) are described in the application UI.
