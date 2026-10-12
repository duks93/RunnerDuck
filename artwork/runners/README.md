# White runner artwork

Ten white runners share the duck's ink style: Penguin, Chicken, Dinosaur, Owl, Cat, Dog, Pig, Sheep, Fox, and Turtle. Each `<id>/base.png` is one generated drawing of the body and head without legs. It was generated from the user's drawing in `../duck/style-reference.png`. `generation-report.json` records every prompt, the provider, and the raw and published hashes.

## Rig

`runners.json` is the motion recipe. `scripts/artwork/build_runner_rig.py` cuts the head, and for the dinosaur, cat, dog and fox a tail, from each drawing. Each cut part keeps a feathered overlap with the body, so its turn never opens a seam. The legs reuse the duck's generated stick and foot cutouts (`../duck/parts`) on fixed-length two-bone legs. Each hip sits a fixed distance above the drawing's lower edge. The motion is authored rather than generated, and sprite-gen's affine curation and `compose-layers` bake each pose.

- Two-legged runners alternate their feet by half a cycle, like the duck. The penguin and owl rock once per cycle.
- Four-legged runners use a lateral-sequence walk (hind, front, other hind, other front, a quarter cycle apart) with at least two feet down. Hind joints bend backward and front joints forward.
- Every loop has 8 to 12 poses and lasts one second at the base rate: Owl, Pig and Turtle 8; Penguin, Chicken and Sheep 10; Dinosaur, Cat, Dog and Fox 12.

## Reproduce

Use the sprite-gen dedicated virtualenv (**2.44.0**). Rebuilding from the committed drawings and duck parts requires no provider login:

```sh
python scripts/artwork/build_runner_rig.py --out-dir .sprite-work/runners/rig
python scripts/artwork/package_runners.py --run-dir .sprite-work/runners/rig
python scripts/verify_assets.py
```

Use an empty output directory. The packager downsamples the 3x bakes to **192x128** cells and keeps the generated white fill and black ink, like the filled duck. It writes `LocalPackage/Sources/UserInterface/Resources/Runners/<id>.png` and `<id>.json`, `<id>/rig-poses.json`, and the `<id>/walk.gif` preview. `scripts/verify_assets.py` checks the atlas size, transparency, white body, distinct poses, fixed leg lengths, grounded feet that never slide forward, supporting feet, and time-normalized speeds across the loop seam.
