# Contributing to Mosaic Bridge

Thanks for considering a contribution.

Mosaic Bridge is released under the [Apache License 2.0](LICENSE).

## Developer Certificate of Origin (DCO)

This project uses the [Developer Certificate of Origin (DCO)](https://developercertificate.org/)
to confirm contributors have the right to submit their code under the
project's license.

**Every commit must be signed off** with the `-s` flag:

```bash
git commit -s -m "your commit message"
```

This adds a `Signed-off-by: Your Name <your@email>` trailer to the commit
message, which asserts that:

- You wrote the code, or
- You have the right to submit it under the Apache License 2.0,
- And you agree to the terms of the DCO (linked above).

PRs whose commits are not signed off will be blocked by the DCO check.

## Workflow

1. Fork the repo
2. Create a topic branch: `git checkout -b your-change`
3. Commit with sign-off: `git commit -s -m "describe your change"`
4. Push your branch: `git push origin your-change`
5. Open a pull request against `main`

## What to expect

- Automated checks (DCO sign-off, CI) run on every PR
- A maintainer will review your PR
- Larger changes benefit from a preceding issue discussion

## Code of Conduct

Participation in this project is governed by the
[Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md).
