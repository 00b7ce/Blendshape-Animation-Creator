# Blendshape Animation Creator

Unity Editor tool for creating and editing static facial-expression AnimationClips from a `SkinnedMeshRenderer` without changing the source avatar or prefab.

## Requirements

- Unity 2022.3
- No VRChat SDK dependency

## Open the tool

Open `Mekabu > Blendshape Animator`.

## Basic workflow

1. Drag an avatar root into **Avatar Root**.
2. The tool selects a `SkinnedMeshRenderer` named `Body` automatically. You can replace it using **Face Renderer**.
3. Create a new `.anim` asset or drag an existing standalone AnimationClip into **Editing Clip**.
4. Adjust blendshape values while checking the isolated preview.
5. Enable the blendshapes that should be written and press **Apply Blendshapes to Clip**.

Only blendshape curves for the selected renderer are replaced. Other curves in the AnimationClip are preserved.

## Preview controls

- Left drag: rotate
- Right drag: pan
- Mouse wheel: zoom
- Face / Full: switch framing
- Reset View: reset the preview camera

## Package development

The package ID is `com.mekabu.blendshape-animation-creator`. The distributable package root is this directory, not the surrounding Unity project.

## License

This package is licensed under the MIT License. See `LICENSE` for details.

## Contact

For questions and bug reports, contact `contact@mekabu.io`.
