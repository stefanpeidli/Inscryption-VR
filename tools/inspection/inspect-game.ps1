param([string]$GamePath = 'C:\Program Files\Epic Games\InscryptionWkwPQ')
$managedPath = Join-Path $GamePath 'Inscryption_Data\Managed'
$resolver = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $name = ([Reflection.AssemblyName]$eventArgs.Name).Name
    $path = Join-Path $managedPath ($name + '.dll')
    if (Test-Path -LiteralPath $path) { return [Reflection.Assembly]::ReflectionOnlyLoadFrom($path) }
    return [Reflection.Assembly]::ReflectionOnlyLoad($eventArgs.Name)
}
[AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve($resolver)
$assembly = [Reflection.Assembly]::ReflectionOnlyLoadFrom((Join-Path $managedPath 'Assembly-CSharp.dll'))
try { $allTypes = $assembly.GetTypes() } catch { $allTypes = $_.Exception.InnerException.Types; $_.Exception.InnerException.LoaderExceptions | Select-Object -First 3 | ForEach-Object { Write-Warning $_.Message } }; $types = $allTypes | Where-Object { $_.FullName -match '^(DiskCardGame\.)?(InputButtons|PixelCamera|CameraEffects|KeyboardInputHandler|GamepadInputHandler|InputAxes|InputManager|InputManagerBase|MainInputInteractable|AlternateInputInteractable|InteractableBase|View|ViewLockState|AscensionSaveData|SaveFile|SaveManager|ViewManager|ViewController|InteractionCursor|MainInput|FirstPersonController|FirstPersonInteractable|TurnManager|CardBattleNPC|BoardManager|CardSlot|PlayableCard|PlayerHand|CardDrawPiles|CombatPhaseManager|PauseMenu|MapNode|SaveFile|SaveManager|Part1GameFlowManager|Part1BossOpponent|Part1PlayerHand|Part1CardDrawPiles|TableRuleBook|CombatBell)$' }
$flags = [Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly'
foreach ($type in $types) {
    Write-Output ('TYPE ' + $type.FullName + ' : ' + $type.BaseType.FullName)
    foreach ($field in $type.GetFields($flags)) { Write-Output ('  FIELD ' + $field.FieldType.FullName + ' ' + $field.Name) }
    foreach ($method in $type.GetMethods($flags)) { Write-Output ('  METHOD ' + $method.ToString()) }
}


Write-Output "RELATED TYPES"; $allTypes | Where-Object { $_.FullName -match "Input|SaveFile|SaveManager|PixelCamera|CameraEffects|CameraRig" } | ForEach-Object { Write-Output $_.FullName }
