# Blendshape Animation Creator

A Unity Editor tool for previewing avatar blendshapes and creating or editing facial-expression AnimationClips without modifying the source avatar or prefab.

## Requirements

- Unity 2022.3
- No VRChat SDK dependency

## Installation

Add the following community repository to VRChat Creator Companion, then install **Blendshape Animation Creator** from Manage Project.

```text
https://vpm-repo.mekabu.io/index.json
```

The repository endpoint will become available with the first public release.

## Usage

Open `Mekabu > Blendshape Animator` in Unity.

1. Drag an avatar root into **Avatar Root**.
2. Confirm or replace the automatically selected **Face Renderer**.
3. Create a new AnimationClip or select an existing standalone clip.
4. Adjust blendshape values in the isolated preview.
5. Enable the blendshapes to write and apply them to the clip.

## Development

The VPM package source is located at:

```text
Packages/com.mekabu.blendshape-animation-creator
```

## Repository deployment

`Deployment/sync_vpm_release.py` mirrors the latest public GitHub Release into
`/mnt/data/projects/vpm-repository`. It preserves existing package versions,
verifies immutable release files by SHA-256, and updates `index.json`
atomically. Example systemd service and timer units are included in the same
directory.

The OCI host is expected to keep a public HTTPS clone at
`/mnt/data/projects/Blendshape-Animation-Creator`. The service pulls that clone
before checking the latest release.

## License

Licensed under the MIT License. See [LICENSE](LICENSE).

## Contact

For questions and bug reports, contact `contact@mekabu.io`.
