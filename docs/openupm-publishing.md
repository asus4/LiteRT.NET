# Publishing the Unity packages to OpenUPM

The three UPM packages (`com.koki-ibukuro.litert`, `com.koki-ibukuro.litert.unity`,
`com.koki-ibukuro.litert.lm`) are packed and **signed** by the
[release-unity workflow](../.github/workflows/release-unity.yml) and attached as `.tgz`
assets to a GitHub Release; OpenUPM consumes the release assets
(`trackingMode: githubRelease`), so the gitignored natives are never committed.

References:

- <https://openupm.com/docs/signing-upm-packages.html#github-actions-flow>
- <https://github.com/openupm/com.example.signed-upm>
- <https://zenn.dev/asus4/articles/89c8ae3a1bb891>

## One-time setup

1. [Unity Cloud Dashboard](https://cloud.unity.com/): create a **Service Account** with
   the **Package Manager Package Signer** role; record its Key ID, Secret, and the
   Organization ID.
2. GitHub secrets (*Settings → Secrets and variables → Actions*):

   | Secret | Value |
   | --- | --- |
   | `UPM_SERVICE_ACCOUNT_KEY_ID` | Service account key ID |
   | `UPM_SERVICE_ACCOUNT_KEY_SECRET` | Service account key secret |
   | `UPM_ORG_ID` | Unity organization ID |

3. After the first tagged release, submit **all three** packages at
   <https://openupm.com/packages/add/> with:

   ```yaml
   trackingMode: githubRelease
   githubReleaseAssetName: '<package name>-'   # e.g. 'com.koki-ibukuro.litert-'
   ```

   The prefix must exclude the version; the trailing `-` keeps the core prefix from
   matching `com.koki-ibukuro.litert.unity-…` / `com.koki-ibukuro.litert.lm-…`. Package
   names must not use well-known scopes like `com.github` — these use `com.koki-ibukuro`.

## Releasing a new version

1. Bump `version` in all three `unity/*/package.json` files and the
   `com.koki-ibukuro.litert` dependency version in `unity/LiteRT.Unity/package.json` and
   `unity/LiteRT.LM/package.json` — all must match (the workflow enforces this). Update
   the `CHANGELOG.md` files.
2. Merge to `main`, then tag and push (tag = `"v"` + package.json version):

   ```sh
   git tag v0.1.0
   git push origin main v0.1.0
   ```

3. The `release-unity` workflow verifies versions, populates natives, packs + signs, and
   creates the GitHub Release with signed `.tgz` assets (each containing
   `package/.attestation.p7m`).
4. OpenUPM picks up the new release asset automatically.

Dry run: dispatch `release-unity` manually — same pack + sign, but the `.tgz` archives
upload as a workflow artifact (`upm-packages`) instead of a Release. Signing secrets must
be configured.

### LiteRT-LM version pinning

Workflows source the prebuilt LM natives from the **latest LiteRT-LM release tag**
(resolved at run time), not `main` — upstream `main` can change the C ABI ahead of the
committed bindings. Override with `litert_lm_ref` on `workflow_dispatch`. Regenerate the
bindings before bumping past a LiteRT-LM release that changes the C API.
