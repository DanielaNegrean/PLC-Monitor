# Python client (PySide6 + asyncua)

The original implementation of the heating-system monitor, written in Python before the project was ported to .NET MAUI. It connects to the same Siemens PLC over OPC UA and displays the run-time counters in a Qt desktop window.

It is kept in this repository as the starting point of the project — the data mapping, the polling approach and the counter layout used by the MAUI client all come from here.

> For the current, maintained client see the [root README](../README.md). This folder is not under active development.

---

## What it does

- Connects to an OPC UA server at an address and port entered in the window
- Reads one array node once per second and writes the values into the interface
- Shows run-time counters for recirculation pump, gas boiler, puffer pump and heat pump
- Shows run-time counters per heating zone: basement, ground floor, first floor, attic
- Shows the "previous day" total next to every current value
- Shows the countdown to the next recirculation cycle
- Start / Stop control over the polling loop, with connection errors surfaced in a status label

## How it compares to the MAUI client

| | Python client | MAUI client |
|---|---|---|
| Platforms | Windows (any Qt desktop) | Windows, Android |
| Counters tab | yes | yes |
| Digital status signals | no | yes |
| Write-back commands | no | yes |
| Automatic reconnection | no | yes |
| Theme, unit and settings persistence | no | yes |
| Architecture | single window class | MVVM, services, simulated data source |

## Tech stack

| Component | Purpose |
|---|---|
| [PySide6](https://doc.qt.io/qtforpython/) | Qt 6 interface, loaded at runtime from a `.ui` file |
| [asyncua](https://github.com/FreeOpcUa/opcua-asyncio) | Asynchronous OPC UA client |
| [qasync](https://github.com/CabbageDevelopment/qasync) | Bridges the Qt event loop with `asyncio` |

The interface is built in Qt Designer and loaded with `QUiLoader` at startup rather than compiled, so `interfata.ui` can be edited without touching the Python code. Every widget the application uses is resolved by `objectName` on launch and the app fails immediately with a clear message if one is missing.

## Running it

Requires Python 3.9 or newer.

```bash
cd python-client

python -m venv .venv
# Windows
.venv\Scripts\activate
# Linux / macOS
source .venv/bin/activate

pip install -r requirements.txt
python UI_app.py
```

1. Enter the **PLC address** and **port** in the fields at the top.
2. Press **Connection** to start reading.
3. Press **Stop Connection** to stop the polling loop.

## Configuration

### PLC address

The address and port fields default to the placeholder values stored in `interfata.ui`. They hold no real address — enter your own before connecting.

### OPC UA node

The counters are read from a single array node, set in `UI_app.py`:

```python
nod_variabila = client.get_node("ns=4;i=68")
```

Change it if your PLC exposes the data elsewhere; the NodeId can be found with a client such as [UaExpert](https://www.unified-automation.com/products/development-tools/uaexpert.html). Note that the MAUI client reads its counters from a different node, so the two values are not interchangeable.

### Array layout

| Index | Meaning | Index | Same, previous day |
|---|---|---|---|
| 0 | Recirculation pump | 4 | — |
| 1 | Gas boiler | 5 | — |
| 2 | Puffer pump | 6 | — |
| 3 | Heat pump | 7 | — |
| 8 | Heating, basement | 12 | — |
| 9 | Heating, ground floor | 13 | — |
| 10 | Heating, first floor | 14 | — |
| 11 | Heating, attic | 15 | — |
| 16 | Time until next recirculation | | |

The indexes are read in the polling loop inside `porneste_citirea()`.

## Files

```
python-client/
├─ UI_app.py          entry point, window class and OPC UA polling loop
├─ interfata.ui       Qt Designer layout
├─ requirements.txt
└─ README.md
```

## Known limitations

Carried over honestly from the original build, and most of them are what motivated the port:

- **No OPC UA security.** The client connects anonymously, with no certificate validation.
- **No reconnection.** A dropped connection ends the read loop; the user has to press Connection again.
- **Raw values.** Counters are shown as plain integers, with no unit conversion or formatting.
- **No persistence.** Address, port and readings are not saved between runs, and no history is kept.
- **Fixed interval.** The one-second polling interval is hard-coded.
- **Polling, not subscriptions.** OPC UA subscriptions would be more efficient; polling was chosen for simplicity.
