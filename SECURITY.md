# Security Policy

## Reporting a Vulnerability

If you discover a security vulnerability in Mosaic Bridge, please report it
privately — **do not open a public GitHub issue**.

Email: **security@mosaicxr.ai** (pending — until then, use m.soutari@gmail.com)

Please include:

- A description of the vulnerability
- Steps to reproduce
- Affected version(s)
- Potential impact
- Any suggested mitigation

We aim to acknowledge receipt within 72 hours and provide an initial
assessment within 7 days.

## Supported Versions

During the pre-1.0 period, only the `main` branch receives security updates.

## Threat Model (overview)

Mosaic Bridge runs in-process inside the Unity Editor. It exposes tool calls
via an HTTP listener that binds only to the loopback interface (`127.0.0.1`)
and authenticates requests using HMAC-SHA256 challenge-response. The listener
does not accept remote connections by default.

The bridge does not transmit telemetry by default. No project data, tool
calls, or code is sent to any external service without explicit user action.

Full threat model documentation will follow in the 1.0 release.
