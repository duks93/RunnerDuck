# Duck artwork

The user's drawing in `style-reference.png` defines the long bill, small head, bean-shaped body and rounded black feet. The yellow app icon keeps its existing generated source (`icon-source.png`).

## Motion v2: new generated cutouts, authored motion

The four images in `parts/` were newly generated through **sprite-gen `gen-set`, provider `codex`, model `gpt-6.1-sol`**, then extracted by sprite-gen. They are the torso, head, leg segment and foot. The exact prompts, extraction request, hashes and generation report summary are committed alongside them.

The animation uses these same cutouts in every frame. **The joint trajectories and transitions are authored in `scripts/artwork/build_duck_rig.py`; they are not GPT-generated full-body pose rows.** sprite-gen's affine curation and `compose-layers` bake the articulated character. This replaces the earlier independently generated pose rows, whose feet changed shape and jumped between locations. The earlier workflow remains recoverable in Git history.

| Clip | Frames | FPS | Duration |
| --- | ---: | ---: | ---: |
| Idle | 48 | 24 | 2 seconds |
| Walk | 40 | 40 | 1 second |
| Run | 36 | 60 | 0.6 seconds |
| Each of the four adjacent-state transitions | 18 | 60 | 0.3 seconds |

Idle feet stay fixed. Each moving leg alternates between a horizontal stance track and a lifted recovery arc; the horizontal velocity matches at touchdown and lift-off. The legs retain fixed segment lengths. Walking always has a supporting foot; running includes short flight phases. The head rotates separately around its neck, with a small phase delay behind the body rhythm. Body motion is rigid rotation/translation, without per-frame scaling or breathing distortion.

Transitions interpolate joint targets with a smooth easing curve and a small foot lift. Their endpoints reuse the exact phase-zero cached loop images. The runtime still waits for that connection pose, retains the latest target during transitions and routes idle/run changes through walking. Intermediates are baked; the app only selects cached frames.

## Reproduce the animation

Use the sprite-gen dedicated virtualenv with **2.35.0, commit `47e985b6320eb000e587632c2cd62a98b1bc5c99`**. No provider login is needed to rebuild from the committed extracted parts. From this repository:

```sh
python scripts/artwork/build_duck_rig.py --parts artwork/duck/parts --out-dir .sprite-work/duck-rig-v2
python scripts/artwork/package_duck.py --run-dir .sprite-work/duck-rig-v2
python scripts/verify_assets.py
```

Here `python` means the Python executable inside the sprite-gen virtualenv. For example, on Windows use `..\sprite-gen\.venv\Scripts\python.exe`.

The rig adapter normalizes the generated parts, masks the hidden neck-tab cap, imports repeated cutout instances through sprite-gen's PNG importer, writes affine curation and rig declarations, and calls the engine layer composer. The importer intentionally has no raw pose rows, so the engine's “stale rows kept as-is (raw missing)” note describes imported source pools; it is not a missing generation fallback. No hand-drawn replacement sprite is used.

`package_duck.py` downsamples the 3× layer bakes, turns dark ink into template alpha, and expands the stroke by 2/3 of a source-cell pixel for small-size visibility. This export adjustment is recorded in `source-manifest.json`. The shared 192×128 atlas contains 196 cells, with 188 referenced cells after transition endpoint aliasing. Both platforms load this same atlas and `animation.json`; their built-in duck timers now tick at approximately 60 Hz. Custom runners keep their existing speed mechanism.

To regenerate the component artwork, prepare a fresh run using `parts/request.json` and `style-reference.png`, place each exact entry from `parts/prompts.json` in the corresponding prepared prompt file, then run `sprite-gen gen-set --provider codex --model gpt-6.1-sol` and `sprite-gen extract`. Generated artwork is nondeterministic. Review and approve its proportions and attachment points before replacing the committed parts; the rig's pivots and neck mask are calibrated for the accepted four images.

## Verification and retained files

`rig-poses.json` records every foot, knee, hip and head target. `layer-validation.json` records source revision fingerprints and confirms that no layer clips. `scripts/verify_assets.py` verifies atlas bounds, transparency, tintability, all references, exact transition endpoints, fixed leg lengths, grounded foot direction, running flight, loop seams, actual rendered eye movement and byte-identical idle sole pixels. Its small-size sheets contain every frame at 16/24/32 pixels.

Windows and macOS native rendering checks additionally exercise their actual image caches, theme tinting, horizontal flip and display sizes. `validation.json` records the completed checks for this asset hash. `qa-notes.md` records the visual review; numerical checks alone do not establish natural motion.

`idle.gif`, `walk.gif`, `run.gif` and `motion-demo.gif` are exported through sprite-gen's GIF encoder from the runtime frames. GIF previews sample at 50 FPS because GIF timing has centisecond resolution; the runtime manifest retains the exact clip timing. The interactive review can step through every original frame.

Only selected generated parts, reference art, final assets, reproducible code, prompts, versions and verification records are tracked. Raw generation caches, provider session identifiers, authentication, temporary layer pools and builds stay outside Git under ignored work directories.
