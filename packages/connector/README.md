# @mosaicxr-ai/connector

Links a local Unity Editor running Mosaic Bridge to a Mosaic Cloud service.

The connection is outbound only: this process dials the service, so nothing needs to
be opened on your network, and the Editor answers only calls arriving on that one
authenticated socket.

**macOS / Linux**
```
sh install.sh --url wss://cloud.example.com/tunnel --token <your-token> --project ~/Projects/Unity/my-course
```

**Windows** (PowerShell)
```
powershell -ExecutionPolicy Bypass -File .\install.ps1 -Url wss://cloud.example.com/tunnel -Token <your-token> -Project C:\Users\you\Projects\Unity\my-course
```

The installer checks the toolchain, adds the Mosaic Bridge package to the Unity
project, and starts the connector. Afterwards, starting it again is just:

```
npx @mosaicxr-ai/connector --url wss://cloud.example.com/tunnel --token <your-token>
```

Run it on the machine where Unity is open, once per session. It prints
`connector ready` when it finds the Editor, and reconnects by itself if the network
drops or the machine sleeps.

Options: `--discovery-file <path>` to point at a specific bridge-discovery.json when
several Editors are running, and `--verbose` to log each routed call.
Environment equivalents: `MOSAIC_CLOUD_URL`, `MOSAIC_CLOUD_TOKEN`.
