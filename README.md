# URG-Unity

- [English](#english)
- [日本語](#日本語)

## English

Use a HOKUYO `UST-10LX` laser scanner as touchless input for Unity. This repository contains the installable UPM package, the packaged SCIP DLL source, and a minimal sample scene for checking the workflow quickly.

### What This Project Does

- Converts `UST-10LX` SCIP distance scans into a 2D scan plane in Unity
- Finds touch-like hit points from the scan and turns them into Unity input
- Supports both Unity UI pointer input and Input System touchscreen input
- Includes a ready-to-use `SCIPInputModules.prefab` for scan visualization and runtime calibration
- Saves calibration values and sensor address in Unity `PlayerPrefs`

### Repository At A Glance

- `Packages/com.mediafrontjapan.urg-unity`
  - Installable UPM package
- `Assets/Sample.unity`
  - Minimal sample scene with `SCIPInputModules`, a `Screen Space Overlay` canvas, an `EventSystem`, and a test button
- `src/MediaFrontJapan.SCIP`
  - Source project for `MediaFrontJapan.SCIP.dll`
- `Packages/com.mediafrontjapan.urg-unity/THIRD_PARTY_NOTICES.md`
  - License notes for bundled third-party DLLs

### Install With UPM

In Unity Package Manager, use `Add package from git URL...` and enter:

```text
https://github.com/MediaFrontJapan/URG-Unity.git?path=/Packages/com.mediafrontjapan.urg-unity
```

Unity resolves these package dependencies automatically:

- `com.unity.burst`
- `com.unity.collections`
- `com.unity.inputsystem`
- `com.unity.mathematics`
- `com.unity.ugui`

### Quick Start

1. Add the package to a Unity `6000.3.11f1` or newer project.
2. Open `Assets/Sample.unity`, or add `SCIPInputModules.prefab` to your own scene.
3. If you use your own scene, keep an `EventSystem` in the scene. The sample scene uses `InputSystemUIInputModule`.
4. Make sure the calibration canvas uses `Screen Space Overlay` if you want drag-based calibration.
5. Set `SCIPClient.playerPrefsKey` and `SCIPScanPlane.playerPrefsKey` to the same value.
6. Connect the PC and the HOKUYO `UST-10LX` to the same network.
7. Enter Play Mode, press `C`, calibrate, then press `C` again to close the UI and save.

### Runtime Calibration With `C`

Press `C` to open or close the calibration UI. Closing the UI saves the current scan-plane settings to `PlayerPrefs`.

| Control | What it changes |
| --- | --- |
| `C` | Toggles the calibration UI and saves when the UI closes |
| Left drag | Moves `Offset`, which shifts the scan plane on the canvas |
| `Scale` | Changes the scan-plane size on screen. Larger values make the same physical movement cover more UI space |
| `Angle` | Rotates the scan plane to match the real sensor mounting angle |
| `Address` | Saves the sensor IP address that `SCIPClient` reads on the next enable or next Play Mode run |

Notes for the calibration UI:

- Cyan markers: currently detected hit points
- Text fields are wired to `OnSubmit`, so type the value and confirm it with Enter before closing the UI

### Important Notes

- The current runtime supports `UST-10LX` only.
- `SCIPTouchscreenSupport` exposes the scan result as an Input System touchscreen device.
- `SCIPInputModule` is also included if you want direct Unity UI pointer routing instead of the Input System path.
- Drag calibration depends on `SCIPScanPlane.ScreenToLocalPoint`, which supports `Screen Space Overlay` only.
- Bundled third-party DLL license notes are listed in `Packages/com.mediafrontjapan.urg-unity/THIRD_PARTY_NOTICES.md`.

## 日本語

HOKUYO `UST-10LX` を Unity の非接触入力として使うためのリポジトリです。インストール用 UPM パッケージ、同梱 SCIP DLL のソース、そして動作確認用の最小サンプルシーンをまとめています。

### このプロジェクトでできること

- `UST-10LX` の SCIP 距離データを Unity 上の 2D スキャン平面に変換する
- スキャン結果からタッチ相当のヒット位置を抽出して Unity 入力へ渡す
- Unity UI のポインター入力と Input System のタッチ入力の両方に対応する
- `SCIPInputModules.prefab` を使ってスキャン可視化と実行時キャリブレーションをすぐ始められる
- キャリブレーション値とセンサー IP を Unity `PlayerPrefs` に保存できる

### リポジトリ概要

- `Packages/com.mediafrontjapan.urg-unity`
  - 配布用の UPM パッケージ
- `Assets/Sample.unity`
  - `SCIPInputModules`、`Screen Space Overlay` の Canvas、`EventSystem`、テスト用ボタンを置いた最小サンプルシーン
- `src/MediaFrontJapan.SCIP`
  - `MediaFrontJapan.SCIP.dll` のソースプロジェクト

### UPM での導入

Unity Package Manager の `Add package from git URL...` に次を入力してください。

```text
https://github.com/MediaFrontJapan/URG-Unity.git?path=/Packages/com.mediafrontjapan.urg-unity
```

依存パッケージは Unity が自動で解決します。

- `com.unity.burst`
- `com.unity.collections`
- `com.unity.inputsystem`
- `com.unity.mathematics`
- `com.unity.ugui`

### クイックスタート

1. Unity `6000.3.11f1` 以降のプロジェクトにパッケージを追加します。
2. `Assets/Sample.unity` を開くか、自分のシーンに `SCIPInputModules.prefab` を配置します。
3. 自前のシーンを使う場合は `EventSystem` を置いてください。サンプルシーンでは `InputSystemUIInputModule` を使っています。
4. ドラッグでキャリブレーションしたい場合は、対象 Canvas を `Screen Space Overlay` にします。
5. `SCIPClient.playerPrefsKey` と `SCIPScanPlane.playerPrefsKey` を同じ値にします。
6. PC と HOKUYO `UST-10LX` を同じネットワークに接続します。
7. Play Mode 中に `C` を押して調整し、終わったらもう一度 `C` を押して UI を閉じて保存します。

### `C` キーで開く設定 UI

`C` キーで設定 UI を開閉します。UI を閉じたタイミングで、その時点のスキャン平面設定が `PlayerPrefs` に保存されます。

| 操作 | 変わる内容 |
| --- | --- |
| `C` | 設定 UI の表示を切り替え、閉じると保存する |
| 左ドラッグ | `Offset` を変更し、スキャン平面の位置を Canvas 上で動かす |
| `Scale` | スキャン平面の画面上の大きさを変える。値を大きくすると同じ物理移動でも UI 上では大きく動く |
| `Angle` | スキャン平面を回転させ、実際のセンサー設置角度に合わせる |
| `Address` | `SCIPClient` が次回の有効化時または次の Play Mode 開始時に読むセンサー IP を保存する |

設定 UI まわりの補足:

- シアンのマーカー: 現在検出されているヒット点
- テキスト入力欄は `OnSubmit` で反映されるので、値を入力したら Enter で確定してから UI を閉じてください

### 注意点

- 現在のランタイムは `UST-10LX` のみ対応です。
- `SCIPTouchscreenSupport` を使うと、スキャン結果を Input System のタッチデバイスとして扱えます。
- Input System を経由せず、Unity UI へ直接ポインター入力として流したい場合は `SCIPInputModule` も使えます。
- ドラッグによるキャリブレーションは `SCIPScanPlane.ScreenToLocalPoint` の制約上、`Screen Space Overlay` のみ対応です。
