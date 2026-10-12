# Duck artwork

The user's drawing in `style-reference.png` defines the long bill, small head, bean-shaped body and rounded black feet. The yellow app icon keeps its existing generated source (`icon-source.png`).

## Eight-frame walking loop

Both platforms share one walking loop: **8 poses at 8 FPS**, one second per cycle at the base rate. CPU load changes playback from 0.8x to 2x while preserving the current pose. No idle, run, or transition clips are shipped. Thumbnails use the first walking pose.

The accepted generated cutouts in `parts/` are reused unchanged. Their prompts, extraction request, hashes and generation report are retained. `scripts/artwork/build_duck_rig.py` authors the joint motion and uses sprite-gen affine curation and `compose-layers` to bake the poses. The eight poses match every fifth frame of the former 40-frame walk, including the wraparound; no new artwork generation is needed.

The feet alternate by half a cycle and always have a supporting foot. Legs keep their fixed segment lengths. The head rotates around its neck with a small delay behind the rigid body motion.

## Reproduce

Use the sprite-gen dedicated virtualenv with **2.35.0, commit `47e985b6320eb000e587632c2cd62a98b1bc5c99`**. Rebuilding from the committed parts requires no provider login:

```sh
python scripts/artwork/build_duck_rig.py --parts artwork/duck/parts --out-dir .sprite-work/duck-walk-v3
python scripts/artwork/package_duck.py --run-dir .sprite-work/duck-walk-v3
python scripts/verify_assets.py
```

Here `python` is the sprite-gen virtualenv executable, for example `..\sprite-gen\.venv\Scripts\python.exe` on Windows. The importer's "stale rows kept as-is (raw missing)" note is expected for imported cutouts with no raw generation rows.

The packager downsamples the 3x layer bakes, converts dark ink into template alpha, and expands the outline by 2/3 of a source pixel for small-size visibility. Each atlas is **1536x128**, with eight **192x128** cells and no unused cells. The filled variant preserves the white body. `source-manifest.json` records the export settings, frame rectangles and timing.

`walk.gif` previews the runtime poses. GIF timing uses centiseconds, so the preview samples at 50 FPS to preserve the one-second cycle; the app uses the exact manifest timing.

## Verification

`rig-poses.json` records the eight joint poses. The packager writes source fingerprints and clipping checks to `artifacts/asset-check/layer-validation.json`. `scripts/verify_assets.py` checks all frame references, the compact atlas size, transparency, tintability, distinct poses, fixed leg lengths, supporting feet, grounded foot direction and the loop seam. Motion bounds use elapsed time rather than assuming a dense frame sequence. Verification reports and small-size sheets are written under ignored `artifacts/asset-check/`.

Native rendering tests cover the outline and filled variants, theme tinting, horizontal flip and display sizes. Their reports remain under ignored `artifacts/` directories or CI artifacts. Old previews and verification records for the removed clips are recoverable in Git history.
