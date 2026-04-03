# URG-Unity

- [English](#english)
- [日本語](#日本語)

## English

This package lets you use the HOKUYO `UST-10LX` laser scanner as Unity UI input and Input System touchscreen input. It receives SCIP distance data, converts it to a 2D scan plane, and forwards the result to `EventSystem` or Input System touch devices.

### Installation

In Unity Package Manager, use `Add package from git URL...` and enter:

```text
https://github.com/MediaFrontJapan/URG-Unity.git?path=/Packages/com.mediafrontjapan.urg-unity
```

The package requires these UPM dependencies, which Unity resolves automatically:

- `com.unity.burst`
- `com.unity.collections`
- `com.unity.inputsystem`
- `com.unity.mathematics`
- `com.unity.ugui`

### Included Content

- `Runtime/MediaFrontJapan/SCIP/Scripts`
  - Sensor connection, scan processing, and input conversion
- `Runtime/MediaFrontJapan/SCIP/SCIPInputModules.prefab`
  - Ready-to-use prefab with the scan plane, config UI, and input modules
- `Runtime/Plugins`
  - SCIP communication DLLs
- `THIRD_PARTY_NOTICES.md`
  - License notes for bundled third-party DLLs

### Setup

1. Add `SCIPInputModules.prefab` to your scene.
2. Make sure the connected sensor is a HOKUYO `UST-10LX`.
3. Make sure `SCIPClient.playerPrefsKey` and `SCIPScanPlane.playerPrefsKey` use the same value.
4. Enter Play Mode and press `C` to open the config UI.
5. Adjust IP address, position, angle, and scale.
6. Press `C` again to close the config UI and save the current settings.

### Saved Settings

The package saves calibration data to Unity `PlayerPrefs` when the config UI closes. It does not create a dedicated config file in the package directory.

The stored JSON payload includes:

- `Offset`
- `Scale`
- `Angle`
- `Address`

The `PlayerPrefs` entry name is the shared `playerPrefsKey` value.

Storage uses the host Unity project's `companyName` and `productName`, so the exact location depends on the project that installs this package.

Typical Unity `PlayerPrefs` storage locations are:

- Windows Editor Play Mode:
  - `HKCU\Software\Unity\UnityEditor\<CompanyName>\<ProductName>`
- Windows standalone player:
  - `HKCU\Software\<CompanyName>\<ProductName>`
- macOS Editor Play Mode:
  - `~/Library/Preferences/com.<CompanyName>.<ProductName>.plist`
- macOS standalone player:
  - `~/Library/Preferences/<BundleIdentifier>.plist`
- Linux standalone player:
  - `~/.config/unity3d/<CompanyName>/<ProductName>`

### Important Notes

- This package supports only `UST-10LX`. If the connected device reports a different scan length, the connection is rejected.
- The drag-based calibration workflow assumes a `Screen Space Overlay` canvas.
- Bundled third-party DLL license notes are listed in `THIRD_PARTY_NOTICES.md`.

## 日本語

このパッケージは、HOKUYO `UST-10LX` を Unity UI 入力および Input System のタッチ入力として利用できるようにします。SCIP の距離データを 2D スキャン平面へ変換し、`EventSystem` または Input System のタッチデバイスへ渡します。

### インストール

Unity Package Manager の `Add package from git URL...` に次を入力してください。

```text
https://github.com/MediaFrontJapan/URG-Unity.git?path=/Packages/com.mediafrontjapan.urg-unity
```

このパッケージは次の UPM 依存を必要とし、Unity が自動解決します。

- `com.unity.burst`
- `com.unity.collections`
- `com.unity.inputsystem`
- `com.unity.mathematics`
- `com.unity.ugui`

### 内容

- `Runtime/MediaFrontJapan/SCIP/Scripts`
  - センサー接続、スキャン処理、入力変換
- `Runtime/MediaFrontJapan/SCIP/SCIPInputModules.prefab`
  - スキャン平面、設定 UI、入力モジュールを含むプレハブ
- `Runtime/Plugins`
  - SCIP 通信用 DLL

### セットアップ

1. `SCIPInputModules.prefab` をシーンに追加します。
2. 接続するセンサーが HOKUYO `UST-10LX` であることを確認します。
3. `SCIPClient.playerPrefsKey` と `SCIPScanPlane.playerPrefsKey` を同じ値にします。
4. Play Mode 中に `C` キーを押して設定 UI を開きます。
5. IP、位置、角度、スケールを調整します。
6. 再度 `C` キーを押して設定 UI を閉じ、現在の設定を保存します。

### 保存される設定

設定 UI を閉じたときに、キャリブレーションデータが Unity `PlayerPrefs` に保存されます。パッケージディレクトリ内に専用の設定ファイルは作成しません。

保存される JSON には次が含まれます。

- `Offset`
- `Scale`
- `Angle`
- `Address`

`PlayerPrefs` のエントリ名は、共有された `playerPrefsKey` の値です。

保存先は導入先 Unity プロジェクトの `companyName` と `productName` に依存します。

代表的な `PlayerPrefs` 保存先:

- Windows Editor Play Mode:
  - `HKCU\Software\Unity\UnityEditor\<CompanyName>\<ProductName>`
- Windows standalone player:
  - `HKCU\Software\<CompanyName>\<ProductName>`
- macOS Editor Play Mode:
  - `~/Library/Preferences/com.<CompanyName>.<ProductName>.plist`
- macOS standalone player:
  - `~/Library/Preferences/<BundleIdentifier>.plist`
- Linux standalone player:
  - `~/.config/unity3d/<CompanyName>/<ProductName>`

### 注意事項

- このパッケージは `UST-10LX` のみ対応です。接続先が別機種で異なるスキャン長を返した場合は接続を拒否します。
- ドラッグによるキャリブレーションは `Screen Space Overlay` の Canvas を前提にしています。
