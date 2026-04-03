# Third-Party Notices

This package redistributes the following third-party DLLs from `Runtime/Plugins`.

## Licenses

| DLL | Upstream package or project | Bundled version | License | Source |
| --- | --- | --- | --- | --- |
| `Microsoft.Experimental.Collections.dll` | `Microsoft.Experimental.Collections` | `1.0.6-e190117-3` | MIT | <https://www.nuget.org/packages/Microsoft.Experimental.Collections/1.0.6-e190117-3/License> |
| `Microsoft.Extensions.ObjectPool.dll` | `Microsoft.Extensions.ObjectPool` | `6.0.0` | MIT | <https://www.nuget.org/packages/Microsoft.Extensions.ObjectPool/> |
| `System.IO.Pipelines.dll` | `System.IO.Pipelines` | `6.0.0` | MIT | <https://www.nuget.org/packages/System.IO.Pipelines/> |
| `System.Runtime.CompilerServices.Unsafe.dll` | `System.Runtime.CompilerServices.Unsafe` | `6.0.0` | MIT | <https://www.nuget.org/packages/System.Runtime.CompilerServices.Unsafe> |
| `System.Threading.Channels.dll` | `System.Threading.Channels` | `6.0.0` | MIT | <https://www.nuget.org/packages/System.Threading.Channels> |
| `ValueTaskSupplement.dll` | `Cysharp/ValueTaskSupplement` | `1.1.0` | MIT | <https://github.com/Cysharp/ValueTaskSupplement> |

## Notes

- `Microsoft.Experimental.Collections` is deprecated upstream, but the bundled binary is still required by the current SCIP DLL build.
- The package also includes `MediaFrontJapan.SCIP.dll`, which is built from the source in `src/MediaFrontJapan.SCIP`.
