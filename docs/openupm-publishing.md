# Publishing the Unity packages to OpenUPM

The three UPM packages (`unity/LiteRT` → `com.koki-ibukuro.litert`, `unity/LiteRT.Unity` →
`com.koki-ibukuro.litert.unity`, `unity/LiteRT.LM` → `com.koki-ibukuro.litert.lm`) are
published as **signed** packages: the
[release-unity workflow](../.github/workflows/release-unity.yml) packs and signs them with the
Unity UPM CLI and attaches the `.tgz` archives to a GitHub Release. OpenUPM then consumes the
release assets directly (`trackingMode: githubRelease`), so the gitignored native binaries never
need to be committed.

References:

- <https://openupm.com/docs/signing-upm-packages.html#github-actions-flow>
- <https://github.com/openupm/com.example.signed-upm>
- <https://zenn.dev/asus4/articles/89c8ae3a1bb891>

## One-time setup

### 1. Unity service account (signing credentials)

1. In the [Unity Cloud Dashboard](https://cloud.unity.com/), open your organization and create a
   **Service Account**.
2. Assign it the **Package Manager Package Signer** role.
3. Create a key and record the **Key ID** and **Secret**.
4. Record the **Organization ID** (visible in the organization settings).

### 2. GitHub repository secrets

Add these three secrets under *Settings → Secrets and variables → Actions*:

| Secret | Value |
| --- | --- |
| `UPM_SERVICE_ACCOUNT_KEY_ID` | Service account key ID |
| `UPM_SERVICE_ACCOUNT_KEY_SECRET` | Service account key secret |
| `UPM_ORG_ID` | Unity organization ID |

### 3. OpenUPM submission (after the first tagged release exists)

Submit **all three** packages at <https://openupm.com/packages/add/>. In each package's metadata
(`data/packages/<name>.yml` in the [openupm/openupm](https://github.com/openupm/openupm) repo),
set the GitHub-release tracking mode and the asset-name prefix that identifies each package's
`.tgz` in a release that carries all of them:

```yaml
# com.koki-ibukuro.litert.yml
trackingMode: githubRelease
githubReleaseAssetName: 'com.koki-ibukuro.litert-'
```

```yaml
# com.koki-ibukuro.litert.unity.yml
trackingMode: githubRelease
githubReleaseAssetName: 'com.koki-ibukuro.litert.unity-'
```

```yaml
# com.koki-ibukuro.litert.lm.yml
trackingMode: githubRelease
githubReleaseAssetName: 'com.koki-ibukuro.litert.lm-'
```

The prefix must exclude the version number; the trailing `-` keeps the core package's prefix from
matching the other two packages' archives.

> Package names must not use well-known scopes like `com.github`
> ([criteria](https://openupm.com/docs/adding-upm-package.html#upm-package-criteria)) — these
> packages use the `com.koki-ibukuro` scope (reverse domain of <https://koki-ibukuro.com/>).

## Releasing a new version

1. Bump `version` in all three `unity/*/package.json` files, and the `com.koki-ibukuro.litert`
   dependency version inside `unity/LiteRT.Unity/package.json` and `unity/LiteRT.LM/package.json`
   — all must match (the release workflow enforces this). Update the `CHANGELOG.md` files.
2. Merge to `main`, then tag and push:

   ```sh
   git tag v0.1.0   # tag = "v" + package.json version
   git push origin main v0.1.0
   ```

3. The `release-unity` workflow verifies the tag matches the package versions, populates the
   native libraries, packs + signs both packages, and creates a GitHub Release with the two
   signed `.tgz` assets (each containing `package/.attestation.p7m`).
4. OpenUPM detects the new release asset automatically (allow some time for its pipeline).

### Testing without a release

Run the `release-unity` workflow manually (*Actions → release-unity → Run workflow*). Dispatch
runs perform the same pack + sign steps but upload the `.tgz` archives as a workflow artifact
(`upm-packages`) instead of creating a GitHub Release. The signing secrets must already be
configured.

### LiteRT-LM version pinning

All workflows source the prebuilt natives from the **latest LiteRT-LM release tag** by default
(resolved at run time), not `main` — upstream `main` can change the C ABI ahead of the committed
bindings (e.g. the July 2026 opaque-struct refactor of the C API). Pass an explicit
`litert_lm_ref` to `workflow_dispatch` runs to override. Before bumping past a LiteRT-LM release
that changes the C API, regenerate the bindings first.
