# Releasing

Steps for publishing a new version of the plugin on GitHub and the Unity Asset Store.

## 1. Prepare the release

- [ ] Merge every pull request that should ship in this version.
- [ ] Pick the version number following [Semantic Versioning](https://semver.org/): major for breaking changes, minor for new features or deprecations, patch for fixes only.
- [ ] Open a pull request titled `chore: release vX.Y.Z` that renames `## Unreleased` in `Changelog.md` to `## X.Y.Z`, and merge it into `main`.

## 2. Publish the GitHub release

- [ ] Create a release on [GitHub](https://github.com/arcweave/arcweave-unity-plugin/releases/new) with a new tag `vX.Y.Z` on `main`, titled `vX.Y.Z`, and click **Generate release notes**. Or run:

  ```sh
  gh release create vX.Y.Z --target main --title vX.Y.Z --generate-notes
  ```

- [ ] Leave **Set as the latest release** ticked, unless the release is a fix for an older major version. The website and the README link to the files of the latest release, so they would otherwise point to the older version.

## 3. Check the downloads

Publishing the release runs the [Release packages](.github/workflows/release-packages.yml) workflow, which attaches `ArcweaveForUnity.unitypackage` and `ArcweaveForUnity.zip` to it.

- [ ] Check that the [workflow run](https://github.com/arcweave/arcweave-unity-plugin/actions/workflows/release-packages.yml) succeeded and both files are on the release page.

  If the run failed because of a temporary error, click **Re-run jobs**. A re-run uses the workflow and build script from the release tag, so if they needed a fix, merge it into `main` and attach the files from your machine instead:

  ```sh
  bash .github/scripts/build-release-packages.sh vX.Y.Z dist
  gh release upload vX.Y.Z dist/ArcweaveForUnity.unitypackage dist/ArcweaveForUnity.zip --clobber
  ```

- [ ] Check that these links download the new files:
  - https://github.com/arcweave/arcweave-unity-plugin/releases/latest/download/ArcweaveForUnity.unitypackage
  - https://github.com/arcweave/arcweave-unity-plugin/releases/latest/download/ArcweaveForUnity.zip

## 4. Update the Asset Store package

Unity has no official way to upload to the Asset Store from CI, so this step is manual. Asset Store users don't get the new version until Unity approves it.

- [ ] Open the [Publisher Portal](https://publisher.unity.com), select **Arcweave for Unity** and select **Create a new draft to edit**.
- [ ] Open this repository's Unity project at the release tag (`git checkout vX.Y.Z`), with the [Asset Store Publishing Tools](https://docs.unity.com/en-us/asset-store/publishing/asset-packages/upload) installed.
- [ ] Select **Tools > Asset Store > Validator**, validate the `Assets/Arcweave` folder and fix any reported issues.
- [ ] Select **Tools > Asset Store > Uploader**, log in, select the new draft and upload the `Assets/Arcweave` folder with **Export and Upload**. Only `Assets/Arcweave` should be exported, not `Assets/ArcweaveTests`.
- [ ] In the Publisher Portal, set the version to `X.Y.Z`, paste the version's `Changelog.md` section as the release notes, check the supported Unity versions, and select **Submit**.
- [ ] Once Unity approves it, check that the [Asset Store page](https://assetstore.unity.com/packages/tools/integration/arcweave-for-unity-148326) shows the new version.
