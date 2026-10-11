# Custom runners

Duck is the only built-in runner. You can still import your own PNG keyframes in **Settings → Runner**. Use transparent PNGs with a consistent canvas, body size, and ground line; provide frames in playback order. Template runners use their alpha mask and the current theme color. Color runners retain their colors.

The custom runner record is unchanged:

```json
{"id":"my-runner","name":"My Runner","isTemplate":true,"frameOrder":[0,1,2,1]}
```

Each unique index references `frame-0.png`, `frame-1.png`, and so on in that runner's data directory. The settings UI handles creation and ordering. Keep IDs distinct from the reserved built-in `duck` ID.

Custom runners keep their existing frame-order and CPU-speed behavior; they do not need motion-state clips. Missing selections or unreadable custom frames recover to Duck. RunnerDuck uses its own data directory and does not automatically import another app's settings or custom runners.
