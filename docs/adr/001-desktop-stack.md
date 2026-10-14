# ADR-001: WPF with self-contained .NET 10
Date: 2026-10-09. Status: accepted as reversible engineering default.

## Decision
Use a native WPF Windows x64 application and a framework-independent core library. Keep dependencies minimal. Ship an unpack-and-run self-contained directory. Core tests are an ordinary console executable so the baseline has no test framework bootstrap dependency.

## Comparison
| Stack | Fit | Cost |
|---|---|---|
| WPF / .NET | Mature file dialogs, keyboard/UI Automation, image virtualization, process APIs, XInput interop; single Hub process; no browser runtime | Windows only; custom visual polish required; self-contained runtime size |
| WinUI 3 | Modern native controls | Windows App SDK deployment/bootstrap complicates strict portable distribution |
| Avalonia | Portable desktop UI, similar C# core | Cross-platform surface unused; additional rendering/runtime packages |
| Tauri | Small shell, strong HTML design tooling | WebView2 deployment and Rust/JS split; browser child processes |
| Electron | Excellent web tooling | Large runtime and multi-process overhead without a product requirement |

The user explicitly delegated stack selection and asked not to choose by AI convenience. Windows portability and filesystem/process lifecycle dominate this product. WPF design can follow Impeccable's shaping, review and token discipline; browser-only CSS detectors cannot certify a native UI. Native render captures and interaction smoke tests supply visual evidence.

## Consequences
No web server or browser process in production. Model services remain UI-independent. Application data is rooted beside the executable; external media paths remain explicit. Startup initializes only manifests and local state, never network downloads. Package runtime inclusion must be measured. Controller discovery uses Windows XInput; gameplay input is configured by emulator adapters. No global dependency-injection container.
