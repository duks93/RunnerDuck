# Custom runners

The built-in runners are the duck variants and the white animal runners listed in the README. You can still import your own PNG keyframes in **Settings → Runner**. Use transparent PNGs with a consistent canvas, body size, and ground line; provide frames in playback order. Template runners use their alpha mask and the current theme color. Color runners retain their colors.

The custom runner record is unchanged:

```json
{"id":"my-runner","name":"My Runner","isTemplate":true,"frameOrder":[0,1,2,1]}
```

Each unique index references `frame-0.png`, `frame-1.png`, and so on in that runner's data directory. The settings UI handles creation and ordering. Keep IDs distinct from the reserved built-in runner IDs (`duck`, `duck-filled`, `penguin`, `chicken`, `dinosaur`, `owl`, `cat`, `dog`, `pig`, `sheep`, `fox`, `turtle`).

Custom runners keep their existing frame-order and CPU-speed behavior; they do not need motion-state clips. Missing selections or unreadable custom frames recover to Duck. RunnerDuck uses its own data directory and does not automatically import another app's settings or custom runners.
