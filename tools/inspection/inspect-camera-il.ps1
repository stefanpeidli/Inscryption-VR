param([string]$GamePath = 'C:\Program Files\Epic Games\InscryptionWkwPQ')
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
[void][Reflection.Assembly]::LoadFrom((Join-Path $root '.dependencies\BepInEx\BepInEx\core\Mono.Cecil.dll'))
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GamePath 'Inscryption_Data\Managed\Assembly-CSharp.dll'))
try {
    foreach ($type in $assembly.MainModule.Types | Where-Object { $_.FullName -in @('PixelCamera','DiskCardGame.ViewManager','DiskCardGame.CameraEffects','DiskCardGame.Part1GameFlowManager','DiskCardGame.ViewController','LightCameraRenderer','ClearCameraRenderTextures','DiskCardGame.CardRenderCamera','DiskCardGame.PauseMenu3D','DiskCardGame.UIManager','ScreenEffectsLayer') }) {
        foreach ($method in $type.Methods | Where-Object { $_.Name -in @('Awake','LateUpdate','OnRenderImage','SwitchToView','SetCameraWorldPosition','OffsetPosition','OffsetRotation','OffsetFOV','SceneSpecificInitialization','SceneSpecificTransitionTo','GetViewInfo','ShowLayer','HideLayer','OnPreRender','OnDisable','StopLiveRenderCard','TryCreateCameraForLiveRender','SetOrthographicCameraVisible') }) {
            Write-Output $method.FullName
            foreach ($instruction in $method.Body.Instructions) { Write-Output ('  ' + $instruction.ToString()) }
        }
    }
} finally { $assembly.Dispose() }
